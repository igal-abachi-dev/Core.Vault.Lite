import { z } from 'zod';

const optionalApiKey = z.string().optional().transform((value) => (value?.trim() ? value.trim() : undefined));

const EnvSchema = z.object({
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
  AGENT_HOST: z.string().default('0.0.0.0'),
  AGENT_PORT: z.coerce.number().int().min(1).max(65535).default(4010),
  GOOGLE_GENERATIVE_AI_API_KEY: optionalApiKey,
  AGENT_MODEL: z.string().default('google/gemini-3.5-flash'),
  QUALITY_MODEL: z.string().default('google/gemini-3.5-flash'),
  CHEAP_MODEL: z.string().default('google/gemini-3.5-flash'),
  FAST_MODEL: z.string().default('google/gemini-3.5-flash'),
  TOOL_GATEWAY_BASE_URL: z.string().url().default('http://localhost:4020'),
  AGENT_GATEWAY_TOKEN: z.string().default('dev-gateway-token'),
  MAX_HANDOFFS: z.coerce.number().int().min(0).max(10).default(5),
  MAX_MODEL_STEPS: z.coerce.number().int().min(1).max(10).default(5),
  CORS_ORIGIN: z.string().default('http://localhost:5173'),
  RATE_LIMIT_MAX: z.coerce.number().int().positive().default(30),
  LOG_LEVEL: z.enum(['trace', 'debug', 'info', 'warn', 'error']).default('info'),
}).superRefine((env, ctx) => {
  if (env.NODE_ENV === 'production' && env.CORS_ORIGIN === '*') ctx.addIssue({ code: 'custom', path: ['CORS_ORIGIN'], message: 'CORS_ORIGIN must not be * in production' });
  if (!env.GOOGLE_GENERATIVE_AI_API_KEY) ctx.addIssue({ code: 'custom', path: ['GOOGLE_GENERATIVE_AI_API_KEY'], message: 'Google Generative AI API key is required for agent-service' });
});

export type AgentEnv = z.infer<typeof EnvSchema> & { GOOGLE_GENERATIVE_AI_API_KEY: string };
export function loadEnv(overrides: NodeJS.ProcessEnv = process.env): AgentEnv { return EnvSchema.parse(overrides) as AgentEnv; }
