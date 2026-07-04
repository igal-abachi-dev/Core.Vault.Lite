import { tool } from 'ai';
import { z } from 'zod';
import type { HandoffAgentId } from '@finance-agent/schemas';

function handoffTool(target: HandoffAgentId, description: string) {
  return tool({
    description,
    parameters: z.object({ reason: z.string().min(1).max(500), context: z.record(z.string(), z.unknown()).default({}) }),
    execute: async ({ reason, context }) => ({ _handoffTo: target, reason, context }),
  });
}

export function createHandoffTools() {
  return {
    transfer_to_daily_money_copilot: handoffTool('daily_money_copilot', 'Use for budgeting, spending, balances, overdraft, cashflow, and everyday money management.'),
    transfer_to_savings_goal_optimizer: handoffTool('savings_goal_optimizer', 'Use for extra cash, emergency fund, savings goals, term deposits, and allocation planning.'),
    transfer_to_debt_payoff_strategist: handoffTool('debt_payoff_strategist', 'Use for loans, credit cards, mortgage payoff, refinancing comparison, and repayment strategy.'),
    transfer_to_family_finance_manager: handoffTool('family_finance_manager', 'Use for family, spouse, joint accounts, children savings, allowances, and shared goals.'),
    transfer_to_personal_wealth_advisor: handoffTool('personal_wealth_advisor', 'Use for broad wealth allocation across cash, debt, savings, retirement and investment simulations.'),
    transfer_to_portfolio_risk_manager: handoffTool('portfolio_risk_manager', 'Use for portfolio concentration, risk, asset allocation, currency exposure and rebalancing simulation.'),
    transfer_to_retirement_runway_modeler: handoffTool('retirement_runway_modeler', 'Use for long-horizon retirement runway, withdrawals, inflation and contribution analysis.'),
    transfer_to_tax_optimization_strategist: handoffTool('tax_optimization_strategist', 'Use for tax scenario modeling, deductions, timing of gains/losses, and set-aside planning.'),
    transfer_to_mortgage_loan_broker: handoffTool('mortgage_loan_broker', 'Use for mortgage refinancing, extra payments, balance transfers and loan comparison.'),
    transfer_to_subscription_bill_negotiator: handoffTool('subscription_bill_negotiator', 'Use for recurring subscriptions, bills, duplicate expenses and cancellation/negotiation opportunities.'),
    transfer_to_credit_score_optimizer: handoffTool('credit_score_optimizer', 'Use for credit utilization, card payoff strategy and credit-building simulations.'),
    transfer_to_freelancer_cashflow_manager: handoffTool('freelancer_cashflow_manager', 'Use for irregular income, tax set-aside, invoice/cashflow and self-employed planning.'),
    transfer_to_israel_fx_cash_manager: handoffTool('israel_fx_cash_manager', 'Use for ILS/USD/EUR cash exposure, travel/tuition/mortgage FX planning and currency risk.'),
    transfer_to_kyc_aml_assistant: handoffTool('kyc_aml_assistant', 'Use for operational KYC/AML summaries, missing documents and unusual-activity review assistance.'),
    transfer_to_buffett_gatekeeper: handoffTool('investment_research_buffett', 'Use for intrinsic value, moat, owner earnings and margin-of-safety research.'),
    transfer_to_lynch_scout: handoffTool('investment_research_lynch', 'Use for consumer trend, GARP and everyday product adoption investment research.'),
    transfer_to_macro_modeler: handoffTool('investment_research_macro', 'Use for macro regime, inflation/rates/currency and all-weather allocation scenario analysis.'),
    transfer_to_confirmation_specialist: handoffTool('confirmation_specialist', 'Use only when the user wants to approve a pending simulation. The model has no execution tool; the UI must call the human confirmation endpoint.'),
  };
}
