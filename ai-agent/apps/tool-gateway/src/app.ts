import cors from '@fastify/cors';
import helmet from '@fastify/helmet';
import rateLimit from '@fastify/rate-limit';
import swagger from '@fastify/swagger';
import swaggerUi from '@fastify/swagger-ui';
import Fastify, { type FastifyError } from 'fastify';
import {
  hasZodFastifySchemaValidationErrors,
  jsonSchemaTransform,
  serializerCompiler,
  validatorCompiler,
  type ZodTypeProvider,
} from 'fastify-type-provider-zod';
import type { GatewayEnv } from './config/env.js';
import { VaultCoreClient } from './core/vault-core-client.js';
import { registerToolRoutes } from './routes/tool-routes.js';

export function buildApp(env: GatewayEnv) {
  const app = Fastify({
    logger: { level: env.LOG_LEVEL },
    requestIdHeader: 'x-request-id',
    trustProxy: true,
  }).withTypeProvider<ZodTypeProvider>();

  app.setValidatorCompiler(validatorCompiler);
  app.setSerializerCompiler(serializerCompiler);
  app.addHook('onRequest', async (request, reply) => reply.header('x-request-id', request.id));
  app.addHook('preHandler', async (request, reply) => {
    if (request.url === '/healthz' || request.url.startsWith('/documentation')) return;
    const header = request.headers.authorization ?? '';
    const token = Array.isArray(header) ? header[0] : header;
    const value = token.startsWith('Bearer ') ? token.slice('Bearer '.length).trim() : '';
    if (!env.apiKeys.has(value)) return reply.code(401).send({ error: { code: 'UNAUTHORIZED', message: 'Invalid gateway token' } });
  });

  app.register(swagger, { openapi: { info: { title: 'Finance Agent Tool Gateway API', version: '1.1.0' } }, transform: jsonSchemaTransform });
  app.register(swaggerUi, { routePrefix: '/documentation' });
  app.register(helmet);
  app.register(cors, { origin: process.env.NODE_ENV === 'production' ? env.CORS_ORIGIN : true });
  app.register(rateLimit, { max: env.RATE_LIMIT_MAX, timeWindow: '1m' });
  app.get('/healthz', async () => ({ status: 'ok', service: 'tool-gateway' }));

  const core = new VaultCoreClient({ baseUrl: env.BANK_CORE_BASE_URL, token: env.BANK_CORE_TOKEN, mockMode: env.BANK_CORE_MOCK_MODE });
  app.register(registerToolRoutes, {
    prefix: '/v1/tools',
    core,
    policy: {
      supportedCountry: env.SUPPORTED_COUNTRY,
      supportedCurrencies: env.supportedCurrencies,
      defaultDailyExecutionLimit: env.DEFAULT_DAILY_EXECUTION_LIMIT,
      requireConfirmationForExecution: env.REQUIRE_CONFIRMATION_FOR_EXECUTION,
      enableAdvancedFinanceTools: env.ENABLE_ADVANCED_FINANCE_TOOLS,
    },
  });

  app.setErrorHandler((error: FastifyError, request, reply) => {
    if (hasZodFastifySchemaValidationErrors(error)) return reply.code(400).send({ error: 'Bad Request', issues: error.validation.map((v) => ({ path: v.instancePath, message: v.message })) });
    request.log.error({ err: error }, 'unhandled error');
    return reply.code(error.statusCode ?? 500).send({ error: 'Tool gateway failed safely' });
  });

  return app;
}
