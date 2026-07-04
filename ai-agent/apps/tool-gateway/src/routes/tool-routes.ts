import type { FastifyPluginAsync } from 'fastify';
import {
  AnalyzePortfolioRiskInputSchema,
  AnalyzeSubscriptionBillsInputSchema,
  ConfirmSimulationInputSchema,
  FinancialHealthSnapshotInputSchema,
  GetAccountBalanceInputSchema,
  GetRecentTransactionsInputSchema,
  SimulateCashflowForecastInputSchema,
  SimulateCreditUtilizationInputSchema,
  SimulateEmergencyFundGapInputSchema,
  SimulateInvestmentPlanInputSchema,
  SimulateLoanPayoffInputSchema,
  SimulateMortgageRefinanceInputSchema,
  SimulateRetirementRunwayInputSchema,
  SimulateSavingsPlanInputSchema,
  SimulateTaxScenarioInputSchema,
  SimulateTransactionInputSchema,
  type ToolResult,
} from '@finance-agent/schemas';
import { evaluatePolicy, type PolicyConfig } from '@finance-agent/agent-policy';
import { authFromRequest } from '../policy/request-context.js';
import type { VaultCoreClient } from '../core/vault-core-client.js';

export interface ToolRoutesDeps {
  core: VaultCoreClient;
  policy: PolicyConfig;
}

export const registerToolRoutes: FastifyPluginAsync<ToolRoutesDeps> = async (app, deps) => {
  app.post('/get_account_balance', async (request, reply) => {
    const input = GetAccountBalanceInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'get_account_balance', auth: input.auth!, currency: input.currency, accountIds: [input.accountId], config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return ok('read', 'Account balance loaded.', await deps.core.getAccountBalance(input), decision.warnings);
  });

  app.post('/get_recent_transactions', async (request, reply) => {
    const input = GetRecentTransactionsInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'get_recent_transactions', auth: input.auth!, accountIds: [input.accountId], config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return ok('read', 'Recent transactions loaded.', await deps.core.getRecentTransactions(input), decision.warnings);
  });

  app.post('/get_financial_health_snapshot', async (request, reply) => {
    const input = FinancialHealthSnapshotInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'get_financial_health_snapshot', auth: input.auth!, currency: input.currency, accountIds: input.accountIds, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return ok('read', 'Financial health snapshot loaded.', await deps.core.getFinancialHealthSnapshot(input), decision.warnings);
  });

  app.post('/simulate_transaction', async (request, reply) => {
    const input = SimulateTransactionInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const accountIds = [input.accountId, input.fromAccountId, input.toAccountId].filter(Boolean) as string[];
    const decision = evaluatePolicy({ toolName: 'simulate_transaction', auth: input.auth!, amount: input.amount, currency: input.currency, accountIds, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Transaction simulation created. No money moved.', await deps.core.simulateTransaction(input), decision.warnings);
  });

  app.post('/simulate_savings_plan', async (request, reply) => {
    const input = SimulateSavingsPlanInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'simulate_savings_plan', auth: input.auth!, amount: input.monthlyAmount, currency: input.currency, accountIds: [input.sourceAccountId, input.targetAccountId], config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Savings plan simulation created. No schedule or transfer created.', await deps.core.simulateSavingsPlan(input), decision.warnings);
  });

  app.post('/simulate_loan_payoff', async (request, reply) => {
    const input = SimulateLoanPayoffInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const amount = input.extraMonthlyPayment ?? input.oneTimePayment;
    const decision = evaluatePolicy({ toolName: 'simulate_loan_payoff', auth: input.auth!, amount, currency: input.currency, accountIds: [input.loanAccountId, input.paymentAccountId], config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Loan payoff simulation created. No payment applied.', await deps.core.simulateLoanPayoff(input), decision.warnings);
  });

  app.post('/simulate_cashflow_forecast', async (request, reply) => {
    const input = SimulateCashflowForecastInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'simulate_cashflow_forecast', auth: input.auth!, currency: input.currency, accountIds: input.accountIds, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Cashflow forecast created. No money moved.', await deps.core.simulateCashflowForecast(input), decision.warnings);
  });

  app.post('/simulate_emergency_fund_gap', async (request, reply) => {
    const input = SimulateEmergencyFundGapInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'simulate_emergency_fund_gap', auth: input.auth!, amount: input.monthlyEssentialSpend, currency: input.currency, accountIds: input.accountIds, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Emergency fund gap simulation created.', await deps.core.simulateEmergencyFundGap(input), decision.warnings);
  });

  app.post('/simulate_investment_plan', async (request, reply) => {
    const input = SimulateInvestmentPlanInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const amount = input.monthlyContribution ?? input.oneTimeContribution;
    const decision = evaluatePolicy({ toolName: 'simulate_investment_plan', auth: input.auth!, amount, currency: input.currency, accountIds: [input.sourceAccountId], config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Investment plan simulation created. No order or money movement happened.', await deps.core.simulateInvestmentPlan(input), decision.warnings);
  });

  app.post('/analyze_portfolio_risk', async (request, reply) => {
    const input = AnalyzePortfolioRiskInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'analyze_portfolio_risk', auth: input.auth!, currency: input.baseCurrency, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return ok('read', 'Portfolio risk analysis completed.', await deps.core.analyzePortfolioRisk(input), decision.warnings);
  });

  app.post('/simulate_retirement_runway', async (request, reply) => {
    const input = SimulateRetirementRunwayInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'simulate_retirement_runway', auth: input.auth!, currency: input.currency, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Retirement runway simulation created.', await deps.core.simulateRetirementRunway(input), decision.warnings);
  });

  app.post('/simulate_tax_scenario', async (request, reply) => {
    const input = SimulateTaxScenarioInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'simulate_tax_scenario', auth: input.auth!, currency: input.currency, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Tax scenario simulation created.', await deps.core.simulateTaxScenario(input), decision.warnings);
  });

  app.post('/analyze_subscription_bills', async (request, reply) => {
    const input = AnalyzeSubscriptionBillsInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'analyze_subscription_bills', auth: input.auth!, currency: input.currency, accountIds: input.accountIds, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return ok('read', 'Recurring bills analysis completed.', await deps.core.analyzeSubscriptionBills(input), decision.warnings);
  });

  app.post('/simulate_mortgage_refinance', async (request, reply) => {
    const input = SimulateMortgageRefinanceInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const ids = [input.mortgageAccountId, input.paymentAccountId].filter(Boolean) as string[];
    const decision = evaluatePolicy({ toolName: 'simulate_mortgage_refinance', auth: input.auth!, amount: input.remainingPrincipal, currency: input.currency, accountIds: ids, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Mortgage refinance simulation created.', await deps.core.simulateMortgageRefinance(input), decision.warnings);
  });

  app.post('/simulate_credit_utilization_strategy', async (request, reply) => {
    const input = SimulateCreditUtilizationInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'simulate_credit_utilization_strategy', auth: input.auth!, amount: input.proposedPayment, currency: input.currency, accountIds: [input.creditCardAccountId], config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return simulated('Credit utilization strategy simulation created.', await deps.core.simulateCreditUtilization(input), decision.warnings);
  });

  app.post('/confirm_simulation', async (request, reply) => {
    const input = ConfirmSimulationInputSchema.parse({ ...(request.body as object), auth: authFromRequest(request) });
    const decision = evaluatePolicy({ toolName: 'confirm_simulation', auth: input.auth!, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied', decision.risk));
    return { status: 'EXECUTED', risk: 'execute', requiresConfirmation: false, summary: 'Confirmed simulation executed through VaultCoreLite. The core executed the stored request, not a new LLM-generated request.', data: await deps.core.confirmSimulation(input), warnings: decision.warnings, safeForUser: true } satisfies ToolResult;
  });

  for (const blocked of ['execute_transfer', 'execute_deposit', 'execute_withdrawal', 'create_bank_account', 'schedule_recurring_payment', 'open_term_deposit', 'buy_security', 'sell_security', 'rebalance_portfolio']) {
    app.post(`/${blocked}`, async () => ({ status: 'REQUIRES_CONFIRMATION', risk: 'execute', requiresConfirmation: true, summary: `${blocked} cannot be called directly by the agent. First call a simulation tool, show the preview to the user, then call confirm_simulation with the user's token.`, warnings: ['Direct execution tools are intentionally blocked at the gateway.'], safeForUser: true } satisfies ToolResult));
  }
};

function ok(risk: 'read', summary: string, data: unknown, warnings: string[] = []): ToolResult { return { status: 'OK', risk, requiresConfirmation: false, summary, data, warnings, safeForUser: true }; }
function simulated(summary: string, data: unknown, warnings: string[] = []): ToolResult { return { status: 'SIMULATED', risk: 'simulate', requiresConfirmation: true, summary, data, warnings, safeForUser: true }; }
function denied(summary: string, risk: 'read' | 'simulate' | 'execute'): ToolResult { return { status: 'DENIED', risk, requiresConfirmation: risk === 'execute', summary, warnings: [], safeForUser: true }; }
