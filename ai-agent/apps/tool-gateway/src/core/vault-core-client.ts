import { randomUUID } from 'node:crypto';
import type {
  AnalyzePortfolioRiskInput,
  AnalyzeSubscriptionBillsInput,
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
} from '@finance-agent/schemas';

export interface VaultCoreClientOptions {
  baseUrl: string;
  token: string;
  mockMode: boolean;
}

export class VaultCoreClient {
  private readonly baseUrl: string;
  private readonly token: string;
  private readonly mockMode: boolean;

  constructor(options: VaultCoreClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, '');
    this.token = options.token;
    this.mockMode = options.mockMode;
  }

  async getAccountBalance(input: GetAccountBalanceInput) {
    if (this.mockMode) return { accountId: input.accountId, currency: input.currency ?? 'ILS', committed: '12500.00', pendingOut: '0.00', available: '12500.00', breakdown: [{ phase: 'COMMITTED', address: 'DEFAULT', amount: '12500.00' }] };
    return this.get(`/v1/accounts/${input.accountId}/balances`);
  }

  async getRecentTransactions(input: GetRecentTransactionsInput) {
    if (this.mockMode) return { accountId: input.accountId, postings: [{ id: randomUUID(), amount: '250.00', denomination: 'ILS', credit: false, reference: 'Groceries', valueTimestamp: new Date().toISOString() }, { id: randomUUID(), amount: '8500.00', denomination: 'ILS', credit: true, reference: 'Salary', valueTimestamp: new Date(Date.now() - 86_400_000).toISOString() }] };
    return this.get(`/v1/accounts/${input.accountId}/postings?limit=${input.limit}`);
  }

  async getFinancialHealthSnapshot(input: FinancialHealthSnapshotInput) {
    if (this.mockMode) return { currency: input.currency, accounts: input.accountIds.map((accountId, index) => ({ accountId, available: index === 0 ? '12500.00' : '3400.00' })), emergencyFundMonths: 3.2, cashflowStatus: 'STABLE', warnings: [] };
    const balances = await Promise.all(input.accountIds.map((accountId) => this.get(`/v1/accounts/${accountId}/balances`)));
    return { currency: input.currency, accounts: balances, warnings: [] };
  }

  async simulateTransaction(input: SimulateTransactionInput) {
    if (this.mockMode) return mockSimulation('transaction', input.amount, input.currency);
    return this.post('/v1/simulations/transaction', input);
  }

  async simulateSavingsPlan(input: SimulateSavingsPlanInput) {
    if (this.mockMode) {
      const monthly = Number(input.monthlyAmount);
      const totalContributions = monthly * input.months;
      const annualRate = input.expectedAnnualRatePercent / 100;
      const roughInterest = totalContributions * annualRate * (input.months / 24);
      return mockSimulation('savings_plan', (totalContributions + roughInterest).toFixed(2), input.currency, { totalContributions: totalContributions.toFixed(2), estimatedInterest: roughInterest.toFixed(2) });
    }
    return this.post('/v1/simulations/savings-plan', input);
  }

  async simulateLoanPayoff(input: SimulateLoanPayoffInput) {
    if (this.mockMode) return mockSimulation('loan_payoff', input.extraMonthlyPayment ?? input.oneTimePayment ?? '0.00', input.currency, { estimatedInterestSaved: '1250.00', estimatedMonthsReduced: 8 });
    return this.post('/v1/simulations/loan-payoff', input);
  }

  async simulateCashflowForecast(input: SimulateCashflowForecastInput) {
    if (this.mockMode) return { months: input.months, currency: input.currency, forecast: Array.from({ length: input.months }, (_, i) => ({ monthOffset: i + 1, projectedNetCashflow: '1800.00' })), warnings: [] };
    return this.post('/v1/simulations/cashflow-forecast', input);
  }

  async simulateEmergencyFundGap(input: SimulateEmergencyFundGapInput) {
    const needed = Number(input.monthlyEssentialSpend) * input.targetMonths;
    const mockLiquid = 15900;
    return mockSimulation('emergency_fund_gap', Math.max(0, needed - mockLiquid).toFixed(2), input.currency, { targetAmount: needed.toFixed(2), currentLiquidEstimate: mockLiquid.toFixed(2), targetMonths: input.targetMonths });
  }

  async simulateInvestmentPlan(input: SimulateInvestmentPlanInput) {
    const monthly = Number(input.monthlyContribution ?? '0');
    const oneTime = Number(input.oneTimeContribution ?? '0');
    const months = input.years * 12;
    const monthlyRate = input.expectedAnnualReturnPercent / 100 / 12;
    let value = oneTime;
    for (let i = 0; i < months; i++) value = value * (1 + monthlyRate) + monthly;
    const downside = value * (1 - input.expectedAnnualVolatilityPercent / 100);
    return mockSimulation('investment_plan', value.toFixed(2), input.currency, { strategy: input.strategy, projectedValue: value.toFixed(2), roughDownsideScenario: Math.max(0, downside).toFixed(2), assumptions: { expectedAnnualReturnPercent: input.expectedAnnualReturnPercent, expectedAnnualVolatilityPercent: input.expectedAnnualVolatilityPercent } });
  }

  async analyzePortfolioRisk(input: AnalyzePortfolioRiskInput) {
    const total = input.holdings.reduce((sum, h) => sum + Number(h.marketValue), 0);
    const byClass: Record<string, number> = {};
    for (const h of input.holdings) byClass[h.assetClass] = (byClass[h.assetClass] ?? 0) + Number(h.marketValue);
    return { baseCurrency: input.baseCurrency, totalMarketValue: total.toFixed(2), allocation: Object.fromEntries(Object.entries(byClass).map(([k, v]) => [k, { amount: v.toFixed(2), percent: total > 0 ? Number(((v / total) * 100).toFixed(2)) : 0 }])), concentrationWarnings: Object.entries(byClass).filter(([, v]) => total > 0 && v / total > 0.65).map(([k]) => `High concentration in ${k}`) };
  }

  async simulateRetirementRunway(input: SimulateRetirementRunwayInput) {
    const monthlyReturn = input.expectedAnnualReturnPercent / 100 / 12;
    const monthlyInflation = input.expectedInflationPercent / 100 / 12;
    let value = Number(input.currentPortfolioValue);
    for (let i = 0; i < input.yearsUntilRetirement * 12; i++) value = value * (1 + monthlyReturn) + Number(input.monthlyContribution);
    let runwayMonths = 0;
    let desiredIncome = Number(input.desiredMonthlyIncome);
    while (value > 0 && runwayMonths < input.retirementYears * 12) {
      value = value * (1 + monthlyReturn) - desiredIncome;
      desiredIncome *= 1 + monthlyInflation;
      runwayMonths++;
    }
    return { currency: input.currency, projectedAtRetirement: Math.max(0, value).toFixed(2), runwayMonths, coversTarget: runwayMonths >= input.retirementYears * 12 };
  }

  async simulateTaxScenario(input: SimulateTaxScenarioInput) {
    const income = Number(input.annualIncome ?? '0');
    const gains = Number(input.realizedGains ?? '0');
    const deductions = Number(input.deductibleExpenses ?? '0');
    return { jurisdiction: input.jurisdiction, currency: input.currency, taxableBaseEstimate: Math.max(0, income + gains - deductions).toFixed(2), scenario: input.scenario, notes: ['Tax rules should be connected to an official/local tax module before production filing.'] };
  }

  async analyzeSubscriptionBills(input: AnalyzeSubscriptionBillsInput) {
    return { currency: input.currency, lookbackMonths: input.lookbackMonths, recurringCandidates: [{ merchant: 'Streaming Service', monthlyAmount: '49.90' }, { merchant: 'Cloud Storage', monthlyAmount: '11.90' }], potentialMonthlySavings: '61.80' };
  }

  async simulateMortgageRefinance(input: SimulateMortgageRefinanceInput) {
    const principal = Number(input.remainingPrincipal);
    const currentMonthly = amortizedPayment(principal, input.currentAnnualRatePercent / 100 / 12, input.remainingMonths);
    const newMonthly = amortizedPayment(principal + Number(input.fees), input.newAnnualRatePercent / 100 / 12, input.remainingMonths);
    return mockSimulation('mortgage_refinance', Math.max(0, (currentMonthly - newMonthly) * input.remainingMonths).toFixed(2), input.currency, { currentMonthlyPayment: currentMonthly.toFixed(2), newMonthlyPayment: newMonthly.toFixed(2), estimatedTotalSavings: Math.max(0, (currentMonthly - newMonthly) * input.remainingMonths).toFixed(2) });
  }

  async simulateCreditUtilization(input: SimulateCreditUtilizationInput) {
    const limit = Number(input.creditLimit);
    const balance = Number(input.currentBalance);
    const payment = Number(input.proposedPayment ?? '0');
    const after = Math.max(0, balance - payment);
    return { currency: input.currency, utilizationBeforePercent: Number(((balance / limit) * 100).toFixed(2)), utilizationAfterPercent: Number(((after / limit) * 100).toFixed(2)), balanceAfter: after.toFixed(2) };
  }

  async confirmSimulation(input: ConfirmSimulationInput) {
    if (this.mockMode) return { status: 'EXECUTED', simulationId: input.simulationId, confirmationAuditId: randomUUID(), batchId: randomUUID(), summary: 'Mock execution completed from stored simulation request.' };
    return this.post(`/v1/simulations/${input.simulationId}:confirm`, { confirmationToken: input.confirmationToken, confirmationText: input.confirmationText, idempotencyKey: input.idempotencyKey });
  }

  private async get(path: string): Promise<unknown> {
    const response = await fetch(`${this.baseUrl}${path}`, { headers: this.headers() });
    return this.parse(response);
  }

  private async post(path: string, body: unknown): Promise<unknown> {
    const response = await fetch(`${this.baseUrl}${path}`, { method: 'POST', headers: { ...this.headers(), 'content-type': 'application/json' }, body: JSON.stringify(body) });
    return this.parse(response);
  }

  private headers() { return { authorization: `Bearer ${this.token}` }; }

  private async parse(response: Response): Promise<unknown> {
    const json = await response.json().catch(() => undefined);
    if (!response.ok) throw new Error(`VaultCoreLite returned HTTP ${response.status}: ${JSON.stringify(json)}`);
    return json;
  }
}

function mockSimulation(kind: string, amount: string, currency: string, extra: Record<string, unknown> = {}) {
  const simulationId = `sim_${randomUUID()}`;
  return { simulationId, confirmationToken: `confirm_${randomUUID()}_${randomUUID()}`, status: 'APPROVED_FOR_CONFIRMATION', kind, currency, amount, expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(), summary: `Simulation created for ${kind}. No money moved yet.`, projectedBalances: [], warnings: [], ...extra };
}

function amortizedPayment(principal: number, monthlyRate: number, months: number) {
  if (monthlyRate === 0) return principal / months;
  return principal * (monthlyRate / (1 - Math.pow(1 + monthlyRate, -months)));
}
