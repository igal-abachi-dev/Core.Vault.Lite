import { z } from 'zod';

const BoolStringSchema = z.union([z.boolean(), z.string()]).transform((value) => {
  if (typeof value === 'boolean') return value;
  return ['true', '1', 'yes', 'on'].includes(value.toLowerCase());
});

const EnvSchema = z.object({
  GATEWAY_HOST: z.string().default('0.0.0.0'),
  GATEWAY_PORT: z.coerce.number().int().min(1).max(65535).default(4020),
  GATEWAY_API_KEYS: z.string().default('dev-gateway-token'),
  BANK_CORE_BASE_URL: z.string().url().default('http://localhost:5080'),
  BANK_CORE_TOKEN: z.string().default('dev-core-token'),
  BANK_CORE_MOCK_MODE: BoolStringSchema.default(false),
  SUPPORTED_COUNTRY: z.literal('IL').default('IL'),
  SUPPORTED_CURRENCIES: z.string().default('ILS,USD,EUR'),
  DEFAULT_DAILY_EXECUTION_LIMIT: z.coerce.number().positive().default(5000),
  REQUIRE_CONFIRMATION_FOR_EXECUTION: BoolStringSchema.default(true),
  ENABLE_ADVANCED_FINANCE_TOOLS: BoolStringSchema.default(true),
  CORS_ORIGIN: z.string().default('http://localhost:5173'),
  RATE_LIMIT_MAX: z.coerce.number().int().positive().default(120),
  LOG_LEVEL: z.enum(['trace', 'debug', 'info', 'warn', 'error']).default('info'),
}).superRefine((env, ctx) => {
  if (process.env.NODE_ENV === 'production' && env.CORS_ORIGIN === '*') {
    ctx.addIssue({ code: 'custom', path: ['CORS_ORIGIN'], message: 'CORS_ORIGIN must not be * in production' });
  }
});

export type GatewayEnv = z.infer<typeof EnvSchema> & { apiKeys: Set<string>; supportedCurrencies: readonly string[] };

export function loadEnv(overrides: NodeJS.ProcessEnv = process.env): GatewayEnv {
  const parsed = EnvSchema.parse(overrides);
  return {
    ...parsed,
    apiKeys: new Set(parsed.GATEWAY_API_KEYS.split(',').map((x) => x.trim()).filter(Boolean)),
    supportedCurrencies: parsed.SUPPORTED_CURRENCIES.split(',').map((x) => x.trim().toUpperCase()).filter(Boolean),
  };
}
