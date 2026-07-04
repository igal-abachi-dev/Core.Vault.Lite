import type { FastifyPluginAsyncZod } from 'fastify-type-provider-zod';
import { z } from 'zod';
import { ChatRequestSchema } from '@finance-agent/schemas';
import type { ToolGatewayClient } from '@finance-agent/banking-tools';
import type { RoleModels } from '../lib/ai/models.js';
import { runFinanceSwarm } from '../swarm/runner.js';
import { agents } from '../swarm/agents.js';

export interface ChatRoutesOptions { models: RoleModels; gateway: ToolGatewayClient; maxHandoffs: number; maxModelSteps: number; }

export const registerChatRoutes: FastifyPluginAsyncZod<ChatRoutesOptions> = async (app, opts) => {
  app.post('/chat', { schema: { body: ChatRequestSchema, response: { 200: z.any() } } }, async (request) => runFinanceSwarm(
    { message: request.body.message, userId: request.body.userId, customerId: request.body.customerId, language: request.body.language, accountIds: request.body.accountIds },
    { models: opts.models, gateway: opts.gateway, maxHandoffs: opts.maxHandoffs, maxModelSteps: opts.maxModelSteps },
  ));

  app.get('/agents', async () => ({ agents: Object.values(agents).map((a) => ({ id: a.id, name: a.name, toolGroup: a.toolGroup, canHandoff: a.canHandoff })) }));
};
