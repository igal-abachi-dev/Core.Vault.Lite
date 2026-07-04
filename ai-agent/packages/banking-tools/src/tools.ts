import { tool } from 'ai';
import { z } from 'zod';
import { redactForAgent } from '@finance-agent/agent-policy';
import {
  AnalyzePortfolioRiskInputSchema,
  AnalyzeSubscriptionBillsInputSchema,
  CurrencySchema,
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
  type AuthContext,
} from '@finance-agent/schemas';
import type { ToolGatewayClient } from './client.js';

export interface BankingToolContext { gateway: ToolGatewayClient; auth: AuthContext; }
function withAuth<T extends z.ZodTypeAny>(schema: T, auth: AuthContext, input: z.input<T>): z.infer<T> { return schema.parse({ ...input, auth }); }
async function safe<T>(fn: () => Promise<T>) { try { return await fn(); } catch (error) { return { status: 'ERROR', risk: 'read', requiresConfirmation: false, summary: redactForAgent(error), warnings: [], safeForUser: true }; } }

export function createBankingTools(ctx: BankingToolContext) {
  return {
    get_financial_health_snapshot: tool({ description: 'Read-only: get a financial health snapshot across user accounts.', parameters: z.object({ accountIds: z.array(z.string().uuid()).min(1).max(25), currency: CurrencySchema.default('ILS') }), execute: async (args) => safe(() => ctx.gateway.getFinancialHealthSnapshot(withAuth(FinancialHealthSnapshotInputSchema, ctx.auth, args))) }),
    get_account_balance: tool({ description: 'Read-only: get real-time balance with committed/pending/accrued breakdown.', parameters: z.object({ accountId: z.string().uuid(), currency: CurrencySchema.optional() }), execute: async (args) => safe(() => ctx.gateway.getAccountBalance(withAuth(GetAccountBalanceInputSchema, ctx.auth, args))) }),
    get_recent_transactions: tool({ description: 'Read-only: get recent immutable ledger postings/transactions for an account.', parameters: z.object({ accountId: z.string().uuid(), limit: z.number().int().min(1).max(100).default(20) }), execute: async (args) => safe(() => ctx.gateway.getRecentTransactions(withAuth(GetRecentTransactionsInputSchema, ctx.auth, args))) }),
    simulate_transaction: tool({ description: 'Simulation-only: preview a deposit, withdrawal, or transfer. Does not move money.', parameters: SimulateTransactionInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateTransaction(withAuth(SimulateTransactionInputSchema, ctx.auth, args))) }),
    simulate_savings_plan: tool({ description: 'Simulation-only: recurring savings plan projection. Does not schedule or transfer.', parameters: SimulateSavingsPlanInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateSavingsPlan(withAuth(SimulateSavingsPlanInputSchema, ctx.auth, args))) }),
    simulate_loan_payoff: tool({ description: 'Simulation-only: compare debt payoff scenarios, extra payments, and estimated interest saved.', parameters: SimulateLoanPayoffInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateLoanPayoff(withAuth(SimulateLoanPayoffInputSchema, ctx.auth, args))) }),
    simulate_cashflow_forecast: tool({ description: 'Simulation-only: forecast cashflow for upcoming months.', parameters: SimulateCashflowForecastInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateCashflowForecast(withAuth(SimulateCashflowForecastInputSchema, ctx.auth, args))) }),
    simulate_emergency_fund_gap: tool({ description: 'Simulation-only: calculate emergency fund target and gap.', parameters: SimulateEmergencyFundGapInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateEmergencyFundGap(withAuth(SimulateEmergencyFundGapInputSchema, ctx.auth, args))) }),
    simulate_investment_plan: tool({ description: 'Simulation-only: model contribution plan, expected return, volatility, and long-run scenarios. Does not place trades.', parameters: SimulateInvestmentPlanInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateInvestmentPlan(withAuth(SimulateInvestmentPlanInputSchema, ctx.auth, args))) }),
    analyze_portfolio_risk: tool({ description: 'Read-only analysis: summarize portfolio allocation, concentration, and risk flags.', parameters: AnalyzePortfolioRiskInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.analyzePortfolioRisk(withAuth(AnalyzePortfolioRiskInputSchema, ctx.auth, args))) }),
    simulate_retirement_runway: tool({ description: 'Simulation-only: estimate retirement accumulation and runway under assumptions.', parameters: SimulateRetirementRunwayInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateRetirementRunway(withAuth(SimulateRetirementRunwayInputSchema, ctx.auth, args))) }),
    simulate_tax_scenario: tool({ description: 'Simulation-only: deterministic tax scenario estimate for the configured jurisdiction.', parameters: SimulateTaxScenarioInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateTaxScenario(withAuth(SimulateTaxScenarioInputSchema, ctx.auth, args))) }),
    analyze_subscription_bills: tool({ description: 'Read-only: detect recurring bills and possible subscription savings from account history.', parameters: AnalyzeSubscriptionBillsInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.analyzeSubscriptionBills(withAuth(AnalyzeSubscriptionBillsInputSchema, ctx.auth, args))) }),
    simulate_mortgage_refinance: tool({ description: 'Simulation-only: compare current mortgage rate to refinance scenario.', parameters: SimulateMortgageRefinanceInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateMortgageRefinance(withAuth(SimulateMortgageRefinanceInputSchema, ctx.auth, args))) }),
    simulate_credit_utilization_strategy: tool({ description: 'Simulation-only: show credit utilization before/after a payment strategy.', parameters: SimulateCreditUtilizationInputSchema.omit({ auth: true, requestId: true }), execute: async (args) => safe(() => ctx.gateway.simulateCreditUtilization(withAuth(SimulateCreditUtilizationInputSchema, ctx.auth, args))) }),
  };
}
export type BankingTools = ReturnType<typeof createBankingTools>;
