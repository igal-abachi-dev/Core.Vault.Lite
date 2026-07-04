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
import type { AgentEnv } from './config/env.js';
import type { RoleModels } from './lib/ai/models.js';
import type { ToolGatewayClient } from '@finance-agent/banking-tools';
import { registerChatRoutes } from './routes/chat-routes.js';

export interface AppDeps { env: AgentEnv; models: RoleModels; gateway: ToolGatewayClient; }

export function buildApp({ env, models, gateway }: AppDeps) {
  const app = Fastify({
    logger: { level: env.LOG_LEVEL, ...(env.NODE_ENV === 'development' && { transport: { target: 'pino-pretty', options: { singleLine: true } } }) },
    requestIdHeader: 'x-request-id',
    trustProxy: env.NODE_ENV === 'production',
  }).withTypeProvider<ZodTypeProvider>();

  app.setValidatorCompiler(validatorCompiler);
  app.setSerializerCompiler(serializerCompiler);
  app.addHook('onRequest', async (request, reply) => reply.header('x-request-id', request.id));

  app.register(swagger, { openapi: { info: { title: 'Finance Agent Service API', version: '1.1.0' } }, transform: jsonSchemaTransform });
  app.register(swaggerUi, { routePrefix: '/documentation' });
  app.register(helmet);
  app.register(cors, { origin: env.NODE_ENV === 'production' ? env.CORS_ORIGIN : true, methods: ['GET', 'POST', 'OPTIONS'] });

  app.get('/healthz', async () => ({ status: 'ok', service: 'agent-service' }));
  app.register(async (scoped) => {
    await scoped.register(rateLimit, { max: env.RATE_LIMIT_MAX, timeWindow: '1m' });
    await scoped.register(registerChatRoutes, { models, gateway, maxHandoffs: env.MAX_HANDOFFS, maxModelSteps: env.MAX_MODEL_STEPS });
  }, { prefix: '/v1' });

  app.setErrorHandler((error: FastifyError, request, reply) => {
    if (hasZodFastifySchemaValidationErrors(error)) return reply.code(400).send({ error: 'Bad Request', issues: error.validation.map((v) => ({ path: v.instancePath, message: v.message })) });
    request.log.error({ err: error }, 'agent service failed');
    return reply.code(error.statusCode ?? 500).send({ error: 'Agent service failed safely' });
  });

  return app;
}
export type App = ReturnType<typeof buildApp>;
