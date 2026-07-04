import { z } from 'zod';

export const CurrencySchema = z.enum(['ILS', 'USD', 'EUR']);
export type Currency = z.infer<typeof CurrencySchema>;

export const CountrySchema = z.enum(['IL']);
export type SupportedCountry = z.infer<typeof CountrySchema>;

export const MoneyStringSchema = z
  .string()
  .trim()
  .regex(/^(0|[1-9]\d{0,11})(\.\d{1,2})?$/, 'Amount must be a positive decimal string with up to 2 decimals')
  .refine((value) => Number(value) > 0, 'Amount must be greater than zero');

export const NonNegativeMoneyStringSchema = z.string().trim().regex(/^(0|[1-9]\d{0,11})(\.\d{1,2})?$/, 'Amount must be a non-negative decimal string with up to 2 decimals');

export const AccountIdSchema = z.string().uuid();
export const ClientRequestIdSchema = z.string().min(8).max(120);
export const ToolRiskSchema = z.enum(['read', 'simulate', 'execute']);
export type ToolRisk = z.infer<typeof ToolRiskSchema>;

export const AuthContextSchema = z.object({
  userId: z.string().min(1),
  customerId: z.string().min(1),
  country: CountrySchema.default('IL'),
  language: z.enum(['he', 'en']).default('he'),
  allowedAccountIds: z.array(AccountIdSchema).default([]),
});
export type AuthContext = z.infer<typeof AuthContextSchema>;

export const GatewayEnvelopeSchema = z.object({
  auth: AuthContextSchema.optional(),
  requestId: z.string().optional(),
});

export const GetAccountBalanceInputSchema = GatewayEnvelopeSchema.extend({
  accountId: AccountIdSchema,
  currency: CurrencySchema.optional(),
});
export type GetAccountBalanceInput = z.infer<typeof GetAccountBalanceInputSchema>;

export const GetRecentTransactionsInputSchema = GatewayEnvelopeSchema.extend({
  accountId: AccountIdSchema,
  limit: z.number().int().min(1).max(100).default(20),
});
export type GetRecentTransactionsInput = z.infer<typeof GetRecentTransactionsInputSchema>;

export const FinancialHealthSnapshotInputSchema = GatewayEnvelopeSchema.extend({
  accountIds: z.array(AccountIdSchema).min(1).max(25),
  currency: CurrencySchema.default('ILS'),
});
export type FinancialHealthSnapshotInput = z.infer<typeof FinancialHealthSnapshotInputSchema>;

export const TransactionKindSchema = z.enum(['deposit', 'withdrawal', 'transfer']);
export type TransactionKind = z.infer<typeof TransactionKindSchema>;

export const SimulateTransactionInputSchema = GatewayEnvelopeSchema.extend({
  kind: TransactionKindSchema,
  accountId: AccountIdSchema.optional(),
  fromAccountId: AccountIdSchema.optional(),
  toAccountId: AccountIdSchema.optional(),
  amount: MoneyStringSchema,
  currency: CurrencySchema.default('ILS'),
  reference: z.string().min(1).max(200).optional(),
  userIntent: z.string().min(1).max(1000).optional(),
  clientRequestId: ClientRequestIdSchema.optional(),
}).superRefine((value, ctx) => {
  if (value.kind === 'transfer' && (!value.fromAccountId || !value.toAccountId)) {
    ctx.addIssue({ code: 'custom', message: 'transfer requires fromAccountId and toAccountId', path: ['fromAccountId'] });
  }
  if ((value.kind === 'deposit' || value.kind === 'withdrawal') && !value.accountId) {
    ctx.addIssue({ code: 'custom', message: `${value.kind} requires accountId`, path: ['accountId'] });
  }
});
export type SimulateTransactionInput = z.infer<typeof SimulateTransactionInputSchema>;

export const SimulateSavingsPlanInputSchema = GatewayEnvelopeSchema.extend({
  sourceAccountId: AccountIdSchema,
  targetAccountId: AccountIdSchema,
  monthlyAmount: MoneyStringSchema,
  currency: CurrencySchema.default('ILS'),
  months: z.number().int().min(1).max(360),
  expectedAnnualRatePercent: z.number().min(0).max(25).default(0),
});
export type SimulateSavingsPlanInput = z.infer<typeof SimulateSavingsPlanInputSchema>;

export const SimulateLoanPayoffInputSchema = GatewayEnvelopeSchema.extend({
  loanAccountId: AccountIdSchema,
  paymentAccountId: AccountIdSchema,
  extraMonthlyPayment: MoneyStringSchema.optional(),
  oneTimePayment: MoneyStringSchema.optional(),
  currency: CurrencySchema.default('ILS'),
});
export type SimulateLoanPayoffInput = z.infer<typeof SimulateLoanPayoffInputSchema>;

export const SimulateCashflowForecastInputSchema = GatewayEnvelopeSchema.extend({
  accountIds: z.array(AccountIdSchema).min(1).max(25),
  months: z.number().int().min(1).max(36).default(6),
  currency: CurrencySchema.default('ILS'),
});
export type SimulateCashflowForecastInput = z.infer<typeof SimulateCashflowForecastInputSchema>;

export const SimulateEmergencyFundGapInputSchema = GatewayEnvelopeSchema.extend({
  accountIds: z.array(AccountIdSchema).min(1).max(25),
  monthlyEssentialSpend: MoneyStringSchema,
  targetMonths: z.number().int().min(1).max(24).default(6),
  currency: CurrencySchema.default('ILS'),
});
export type SimulateEmergencyFundGapInput = z.infer<typeof SimulateEmergencyFundGapInputSchema>;

export const SimulateInvestmentPlanInputSchema = GatewayEnvelopeSchema.extend({
  sourceAccountId: AccountIdSchema,
  monthlyContribution: MoneyStringSchema.optional(),
  oneTimeContribution: MoneyStringSchema.optional(),
  currency: CurrencySchema.default('ILS'),
  years: z.number().int().min(1).max(60).default(10),
  expectedAnnualReturnPercent: z.number().min(-50).max(50).default(5),
  expectedAnnualVolatilityPercent: z.number().min(0).max(100).default(15),
  strategy: z.enum(['conservative', 'balanced', 'growth', 'custom']).default('balanced'),
  notes: z.string().max(1000).optional(),
});
export type SimulateInvestmentPlanInput = z.infer<typeof SimulateInvestmentPlanInputSchema>;

export const AnalyzePortfolioRiskInputSchema = GatewayEnvelopeSchema.extend({
  holdings: z.array(z.object({
    symbol: z.string().min(1).max(24),
    marketValue: MoneyStringSchema,
    currency: CurrencySchema.default('ILS'),
    assetClass: z.enum(['cash', 'bond', 'stock', 'fund', 'crypto', 'other']).default('stock'),
    geography: z.string().max(80).optional(),
  })).min(1).max(100),
  baseCurrency: CurrencySchema.default('ILS'),
});
export type AnalyzePortfolioRiskInput = z.infer<typeof AnalyzePortfolioRiskInputSchema>;

export const SimulateRetirementRunwayInputSchema = GatewayEnvelopeSchema.extend({
  currentPortfolioValue: MoneyStringSchema,
  monthlyContribution: NonNegativeMoneyStringSchema.default('0'),
  desiredMonthlyIncome: MoneyStringSchema,
  yearsUntilRetirement: z.number().int().min(0).max(80),
  retirementYears: z.number().int().min(1).max(80).default(30),
  expectedAnnualReturnPercent: z.number().min(-50).max(50).default(4),
  expectedInflationPercent: z.number().min(0).max(25).default(2),
  currency: CurrencySchema.default('ILS'),
});
export type SimulateRetirementRunwayInput = z.infer<typeof SimulateRetirementRunwayInputSchema>;

export const SimulateTaxScenarioInputSchema = GatewayEnvelopeSchema.extend({
  jurisdiction: z.literal('IL').default('IL'),
  currency: CurrencySchema.default('ILS'),
  annualIncome: MoneyStringSchema.optional(),
  realizedGains: MoneyStringSchema.optional(),
  deductibleExpenses: MoneyStringSchema.optional(),
  scenario: z.string().min(1).max(1000),
});
export type SimulateTaxScenarioInput = z.infer<typeof SimulateTaxScenarioInputSchema>;

export const AnalyzeSubscriptionBillsInputSchema = GatewayEnvelopeSchema.extend({
  accountIds: z.array(AccountIdSchema).min(1).max(25),
  lookbackMonths: z.number().int().min(1).max(24).default(6),
  currency: CurrencySchema.default('ILS'),
});
export type AnalyzeSubscriptionBillsInput = z.infer<typeof AnalyzeSubscriptionBillsInputSchema>;

export const SimulateMortgageRefinanceInputSchema = GatewayEnvelopeSchema.extend({
  mortgageAccountId: AccountIdSchema,
  paymentAccountId: AccountIdSchema.optional(),
  remainingPrincipal: MoneyStringSchema,
  currentAnnualRatePercent: z.number().min(0).max(30),
  newAnnualRatePercent: z.number().min(0).max(30),
  remainingMonths: z.number().int().min(1).max(600),
  fees: NonNegativeMoneyStringSchema.default('0'),
  currency: CurrencySchema.default('ILS'),
});
export type SimulateMortgageRefinanceInput = z.infer<typeof SimulateMortgageRefinanceInputSchema>;

export const SimulateCreditUtilizationInputSchema = GatewayEnvelopeSchema.extend({
  creditCardAccountId: AccountIdSchema,
  creditLimit: MoneyStringSchema,
  currentBalance: MoneyStringSchema,
  proposedPayment: MoneyStringSchema.optional(),
  currency: CurrencySchema.default('ILS'),
});
export type SimulateCreditUtilizationInput = z.infer<typeof SimulateCreditUtilizationInputSchema>;

export const ConfirmSimulationInputSchema = GatewayEnvelopeSchema.extend({
  simulationId: z.string().min(8).max(120),
  confirmationToken: z.string().min(16).max(256),
  confirmationText: z.string().min(1).max(500).optional(),
  idempotencyKey: ClientRequestIdSchema,
});
export type ConfirmSimulationInput = z.infer<typeof ConfirmSimulationInputSchema>;

export const ToolResultStatusSchema = z.enum(['OK', 'SIMULATED', 'REQUIRES_CONFIRMATION', 'EXECUTED', 'REJECTED', 'DENIED', 'ERROR']);
export const ToolResultSchema = z.object({
  status: ToolResultStatusSchema,
  risk: ToolRiskSchema,
  requiresConfirmation: z.boolean().default(false),
  summary: z.string(),
  data: z.unknown().optional(),
  warnings: z.array(z.string()).default([]),
  safeForUser: z.boolean().default(true),
});
export type ToolResult = z.infer<typeof ToolResultSchema>;

export const ChatMessageSchema = z.object({ role: z.enum(['system', 'user', 'assistant']), content: z.string().min(1).max(20000) });
export type ChatMessage = z.infer<typeof ChatMessageSchema>;

export const ChatRequestSchema = z.object({
  userId: z.string().default('demo-user'),
  customerId: z.string().default('demo-customer'),
  country: CountrySchema.default('IL'),
  language: z.enum(['he', 'en']).default('he'),
  message: z.string().min(1).max(10000),
  accountIds: z.array(AccountIdSchema).default([]),
});
export type ChatRequest = z.infer<typeof ChatRequestSchema>;

export const HandoffAgentIdSchema = z.enum([
  'triage',
  'daily_money_copilot',
  'savings_goal_optimizer',
  'debt_payoff_strategist',
  'family_finance_manager',
  'personal_wealth_advisor',
  'portfolio_risk_manager',
  'retirement_runway_modeler',
  'tax_optimization_strategist',
  'mortgage_loan_broker',
  'subscription_bill_negotiator',
  'credit_score_optimizer',
  'freelancer_cashflow_manager',
  'israel_fx_cash_manager',
  'kyc_aml_assistant',
  'investment_research_buffett',
  'investment_research_lynch',
  'investment_research_macro',
  'confirmation_specialist',
]);
export type HandoffAgentId = z.infer<typeof HandoffAgentIdSchema>;
