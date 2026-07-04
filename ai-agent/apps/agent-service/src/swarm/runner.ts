import { generateText, type CoreMessage } from 'ai';
import { createBankingTools, authForAgent, type ToolGatewayClient } from '@finance-agent/banking-tools';
import type { HandoffAgentId } from '@finance-agent/schemas';
import { agents } from './agents.js';
import { createHandoffTools } from '../tools/handoff-tools.js';
import { modelRoleForAgent, type RoleModels } from '../lib/ai/models.js';
import type { FinanceAgentNode, PendingHumanConfirmation, SwarmRunInput, SwarmRunResult, SwarmState } from './types.js';

export interface SwarmRunnerOptions { models: RoleModels; gateway: ToolGatewayClient; maxHandoffs: number; maxModelSteps: number; }

export async function runFinanceSwarm(input: SwarmRunInput, options: SwarmRunnerOptions): Promise<SwarmRunResult> {
  const auth = authForAgent({ userId: input.userId, customerId: input.customerId, language: input.language, accountIds: input.accountIds });
  const state: SwarmState = { currentAgent: 'triage', handoffCount: 0, contextData: { accountIds: input.accountIds, language: input.language }, trace: [] };
  const messages: CoreMessage[] = input.messages?.length ? [...input.messages] : [{ role: 'user', content: input.message }];
  const allToolResults: unknown[] = [];
  let lastText = '';

  while (true) {
    const active = agents[state.currentAgent];
    const tools = makeToolsForAgent(active, options.gateway, auth);
    const role = modelRoleForAgent(active.id);
    const model = options.models[role];

    const result = await generateText({
      model: model.model,
      system: active.system,
      messages,
      tools,
      maxSteps: options.maxModelSteps,
      ...(model.supportsTemperature ? { temperature: 0.2 } : {}),
      ...(model.providerOptions ? { providerOptions: model.providerOptions } : {}),
    } as any);

    lastText = result.text || lastText;
    const anyResult = result as any;
    if (Array.isArray(anyResult.response?.messages)) messages.push(...anyResult.response.messages);
    else if (result.text) messages.push({ role: 'assistant', content: result.text });

    const toolResults = extractToolResults(anyResult);
    allToolResults.push(...toolResults);
    const handoff = toolResults.find((x) => x && typeof x === 'object' && '_handoffTo' in x) as ({ _handoffTo: HandoffAgentId; reason?: string; context?: Record<string, unknown> }) | undefined;
    if (!handoff) break;
    if (state.handoffCount >= options.maxHandoffs) {
      lastText = 'I need to stop routing between agents and give a direct answer. Please choose one next action: read, simulate, or confirm.';
      break;
    }

    const nextAgent = handoff._handoffTo;
    state.trace.push({ from: state.currentAgent, to: nextAgent, reason: handoff.reason ?? 'handoff' });
    state.currentAgent = nextAgent;
    state.handoffCount += 1;
    state.contextData = { ...state.contextData, ...(handoff.context ?? {}) };
    messages.push({ role: 'user', content: `[System routing note] Switched to ${nextAgent}. Typed context only: ${JSON.stringify(state.contextData)}. Continue without repeating prior routing.` });
  }

  return { text: lastText, activeAgent: state.currentAgent, trace: state.trace, toolResults: allToolResults, pendingConfirmations: extractPendingConfirmations(allToolResults) };
}

function makeToolsForAgent(active: FinanceAgentNode, gateway: ToolGatewayClient, auth: ReturnType<typeof authForAgent>) {
  const banking = createBankingTools({ gateway, auth });
  const handoffs = active.canHandoff ? createHandoffTools() : {};
  switch (active.toolGroup) {
    case 'read': return { get_financial_health_snapshot: banking.get_financial_health_snapshot, get_account_balance: banking.get_account_balance, get_recent_transactions: banking.get_recent_transactions, ...handoffs };
    case 'confirmation': return {}; // Structural HITL: no model-visible execution tool. UI calls /v1/human-confirmations/{simulationId}/confirm directly.
    case 'wealth': return { get_financial_health_snapshot: banking.get_financial_health_snapshot, simulate_savings_plan: banking.simulate_savings_plan, simulate_loan_payoff: banking.simulate_loan_payoff, simulate_investment_plan: banking.simulate_investment_plan, simulate_retirement_runway: banking.simulate_retirement_runway, ...handoffs };
    case 'investment': return { simulate_investment_plan: banking.simulate_investment_plan, analyze_portfolio_risk: banking.analyze_portfolio_risk, simulate_retirement_runway: banking.simulate_retirement_runway, ...handoffs };
    case 'risk': return { analyze_portfolio_risk: banking.analyze_portfolio_risk, simulate_cashflow_forecast: banking.simulate_cashflow_forecast, simulate_investment_plan: banking.simulate_investment_plan, ...handoffs };
    case 'tax': return { simulate_tax_scenario: banking.simulate_tax_scenario, simulate_cashflow_forecast: banking.simulate_cashflow_forecast, ...handoffs };
    case 'credit': return { simulate_credit_utilization_strategy: banking.simulate_credit_utilization_strategy, simulate_loan_payoff: banking.simulate_loan_payoff, ...handoffs };
    case 'operations': return { get_recent_transactions: banking.get_recent_transactions, get_financial_health_snapshot: banking.get_financial_health_snapshot, ...handoffs };
    case 'planning':
    default:
      return { get_financial_health_snapshot: banking.get_financial_health_snapshot, get_account_balance: banking.get_account_balance, get_recent_transactions: banking.get_recent_transactions, simulate_transaction: banking.simulate_transaction, simulate_savings_plan: banking.simulate_savings_plan, simulate_loan_payoff: banking.simulate_loan_payoff, simulate_cashflow_forecast: banking.simulate_cashflow_forecast, simulate_emergency_fund_gap: banking.simulate_emergency_fund_gap, analyze_subscription_bills: banking.analyze_subscription_bills, simulate_mortgage_refinance: banking.simulate_mortgage_refinance, ...handoffs };
  }
}

function extractToolResults(result: any): unknown[] {
  if (Array.isArray(result.toolResults)) return result.toolResults.map((x: any) => x.result ?? x.output ?? x);
  if (Array.isArray(result.steps)) return result.steps.flatMap((step: any) => (step.toolResults ?? []).map((x: any) => x.result ?? x.output ?? x));
  return [];
}

function extractPendingConfirmations(toolResults: unknown[]): PendingHumanConfirmation[] {
  const out: PendingHumanConfirmation[] = [];
  for (const item of toolResults) {
    const data = item && typeof item === 'object' ? (item as any).data : undefined;
    const hc = data && typeof data === 'object' ? data.humanConfirmation : undefined;
    if (hc && typeof hc.simulationId === 'string' && typeof hc.path === 'string') {
      out.push({ simulationId: hc.simulationId, path: hc.path, summary: typeof (item as any).summary === 'string' ? (item as any).summary : undefined, expiresAt: typeof data.expiresAt === 'string' ? data.expiresAt : undefined });
    }
  }
  return out;
}
