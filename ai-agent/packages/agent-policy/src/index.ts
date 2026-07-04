import { randomUUID } from 'node:crypto';
import type { AuthContext, ToolRisk } from '@finance-agent/schemas';

export interface PolicyDecision {
  allowed: boolean;
  risk: ToolRisk;
  reason?: string;
  requiresConfirmation: boolean;
  warnings: string[];
}

export interface PolicyConfig {
  supportedCountry: 'IL';
  supportedCurrencies: readonly string[];
  defaultDailyExecutionLimit: number;
  requireConfirmationForExecution: boolean;
  /** Deployment switch: enables advanced investment/tax/portfolio simulations for eligible IL users. */
  enableAdvancedFinanceTools: boolean;
}

const EXECUTION_TOOL_PREFIXES = ['execute_', 'create_', 'change_', 'schedule_', 'apply_', 'open_'] as const;
const ADVANCED_TOOL_MARKERS = ['investment', 'portfolio', 'retirement', 'tax', 'mortgage', 'credit_utilization', 'fx'];

export function classifyToolRisk(toolName: string): ToolRisk {
  if (toolName.startsWith('get_') || toolName.startsWith('analyze_')) return 'read';
  if (toolName.startsWith('simulate_')) return 'simulate';
  if (toolName === 'confirm_simulation') return 'execute';
  if (EXECUTION_TOOL_PREFIXES.some((prefix) => toolName.startsWith(prefix))) return 'execute';
  return 'simulate';
}

export function evaluatePolicy(input: {
  toolName: string;
  auth: AuthContext;
  amount?: string;
  currency?: string;
  accountIds?: string[];
  config: PolicyConfig;
}): PolicyDecision {
  const risk = classifyToolRisk(input.toolName);
  const warnings: string[] = [];

  if (input.auth.country !== input.config.supportedCountry) {
    return { allowed: false, risk, reason: `Country ${input.auth.country} is not supported by this deployment`, requiresConfirmation: risk === 'execute', warnings };
  }

  if (input.currency && !input.config.supportedCurrencies.includes(input.currency)) {
    return { allowed: false, risk, reason: `Currency ${input.currency} is not supported`, requiresConfirmation: risk === 'execute', warnings };
  }

  const unauthorizedAccount = (input.accountIds ?? []).find((accountId) => input.auth.allowedAccountIds.length > 0 && !input.auth.allowedAccountIds.includes(accountId));
  if (unauthorizedAccount) {
    return { allowed: false, risk, reason: `Account ${unauthorizedAccount} is not available to this user`, requiresConfirmation: risk === 'execute', warnings };
  }

  if (ADVANCED_TOOL_MARKERS.some((marker) => input.toolName.includes(marker)) && !input.config.enableAdvancedFinanceTools) {
    return {
      allowed: false,
      risk,
      reason: 'Advanced finance tools are disabled by deployment policy.',
      requiresConfirmation: risk === 'execute',
      warnings,
    };
  }

  if (risk === 'execute') {
    const amount = input.amount ? Number(input.amount) : undefined;
    if (amount !== undefined && amount > input.config.defaultDailyExecutionLimit) {
      return { allowed: false, risk, reason: `Amount exceeds configured execution limit ${input.config.defaultDailyExecutionLimit}`, requiresConfirmation: true, warnings };
    }
    warnings.push('Execution requires a prior core simulation, out-of-band human confirmation, idempotency key, and audit metadata.');
  }

  return { allowed: true, risk, requiresConfirmation: risk === 'execute' && input.config.requireConfirmationForExecution, warnings };
}

export function redactForAgent(error: unknown): string {
  if (error instanceof Error) return error.message.replace(/Bearer\s+[A-Za-z0-9._-]+/g, 'Bearer [redacted]').slice(0, 500);
  return 'Tool failed';
}

export function makeIdempotencyKey(prefix: string, userId: string): string {
  return `${prefix}:${userId}:${Date.now()}:${randomUUID()}`;
}
