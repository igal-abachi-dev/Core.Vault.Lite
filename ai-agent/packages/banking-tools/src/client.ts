import type {
  AnalyzePortfolioRiskInput,
  AnalyzeSubscriptionBillsInput,
  AuthContext,
  ConfirmSimulationInput,
  FinancialHealthSnapshotInput,
  GetAccountBalanceInput,
  GetRecentTransactionsInput,
  SimulateCashflowForecastInput,
  SimulateCreditUtilizationInput,
  SimulateEmergencyFundGapInput,
  SimulateInvestmentPlanInput,
  SimulateLoanPayoffInput,
  SimulateMortgageRefinanceInput,
  SimulateRetirementRunwayInput,
  SimulateSavingsPlanInput,
  SimulateTaxScenarioInput,
  SimulateTransactionInput,
  ToolResult,
} from '@finance-agent/schemas';

export interface ToolGatewayClientOptions { baseUrl: string; token: string; timeoutMs?: number; }
export class ToolGatewayError extends Error { constructor(message: string, public readonly status: number, public readonly responseBody: unknown) { super(message); } }

export class ToolGatewayClient {
  private readonly baseUrl: string;
  private readonly token: string;
  private readonly timeoutMs: number;
  constructor(options: ToolGatewayClientOptions) { this.baseUrl = options.baseUrl.replace(/\/$/, ''); this.token = options.token; this.timeoutMs = options.timeoutMs ?? 15_000; }
  getFinancialHealthSnapshot(input: FinancialHealthSnapshotInput) { return this.post('/v1/tools/get_financial_health_snapshot', input); }
  getAccountBalance(input: GetAccountBalanceInput) { return this.post('/v1/tools/get_account_balance', input); }
  getRecentTransactions(input: GetRecentTransactionsInput) { return this.post('/v1/tools/get_recent_transactions', input); }
  simulateTransaction(input: SimulateTransactionInput) { return this.post('/v1/tools/simulate_transaction', input); }
  simulateSavingsPlan(input: SimulateSavingsPlanInput) { return this.post('/v1/tools/simulate_savings_plan', input); }
  simulateLoanPayoff(input: SimulateLoanPayoffInput) { return this.post('/v1/tools/simulate_loan_payoff', input); }
  simulateCashflowForecast(input: SimulateCashflowForecastInput) { return this.post('/v1/tools/simulate_cashflow_forecast', input); }
  simulateEmergencyFundGap(input: SimulateEmergencyFundGapInput) { return this.post('/v1/tools/simulate_emergency_fund_gap', input); }
  simulateInvestmentPlan(input: SimulateInvestmentPlanInput) { return this.post('/v1/tools/simulate_investment_plan', input); }
  analyzePortfolioRisk(input: AnalyzePortfolioRiskInput) { return this.post('/v1/tools/analyze_portfolio_risk', input); }
  simulateRetirementRunway(input: SimulateRetirementRunwayInput) { return this.post('/v1/tools/simulate_retirement_runway', input); }
  simulateTaxScenario(input: SimulateTaxScenarioInput) { return this.post('/v1/tools/simulate_tax_scenario', input); }
  analyzeSubscriptionBills(input: AnalyzeSubscriptionBillsInput) { return this.post('/v1/tools/analyze_subscription_bills', input); }
  simulateMortgageRefinance(input: SimulateMortgageRefinanceInput) { return this.post('/v1/tools/simulate_mortgage_refinance', input); }
  simulateCreditUtilization(input: SimulateCreditUtilizationInput) { return this.post('/v1/tools/simulate_credit_utilization_strategy', input); }
  confirmSimulation(input: ConfirmSimulationInput) { return this.post('/v1/tools/confirm_simulation', input); }

  private async post(path: string, body: unknown): Promise<ToolResult> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);
    try {
      const response = await fetch(`${this.baseUrl}${path}`, { method: 'POST', headers: { authorization: `Bearer ${this.token}`, 'content-type': 'application/json' }, body: JSON.stringify(body), signal: controller.signal });
      const json = await response.json().catch(() => undefined);
      if (!response.ok) throw new ToolGatewayError(`Tool gateway returned HTTP ${response.status}`, response.status, json);
      return json as ToolResult;
    } finally { clearTimeout(timer); }
  }
}

export function authForAgent(input: { userId: string; customerId: string; country?: 'IL'; language?: 'he' | 'en'; accountIds?: string[] }): AuthContext {
  return { userId: input.userId, customerId: input.customerId, country: input.country ?? 'IL', language: input.language ?? 'he', allowedAccountIds: input.accountIds ?? [] };
}
