import type { FastifyPluginAsync } from 'fastify';
import { z } from 'zod';
import { ConfirmSimulationInputSchema, type ToolResult } from '@finance-agent/schemas';
import { evaluatePolicy, type PolicyConfig } from '@finance-agent/agent-policy';
import { authFromRequest } from '../policy/request-context.js';
import type { VaultCoreClient } from '../core/vault-core-client.js';
import type { ConfirmationTokenStore } from '../confirmation/confirmation-token-store.js';

const HumanConfirmBodySchema = z.object({
  confirmationText: z.string().min(1).max(500),
  idempotencyKey: z.string().min(8).max(120).optional(),
});

export interface HumanConfirmationRoutesDeps {
  core: VaultCoreClient;
  policy: PolicyConfig;
  confirmationTokens: ConfirmationTokenStore;
}

export const registerHumanConfirmationRoutes: FastifyPluginAsync<HumanConfirmationRoutesDeps> = async (app, deps) => {
  app.post('/:simulationId/confirm', async (request, reply) => {
    const auth = authFromRequest(request);
    const simulationId = z.string().min(8).max(120).parse((request.params as { simulationId: string }).simulationId);
    const body = HumanConfirmBodySchema.parse(request.body ?? {});
    const decision = evaluatePolicy({ toolName: 'confirm_simulation', auth, config: deps.policy });
    if (!decision.allowed) return reply.code(403).send(denied(decision.reason ?? 'Denied'));

    const pending = deps.confirmationTokens.take({ simulationId, userId: auth.userId, customerId: auth.customerId });
    if (!pending) {
      return reply.code(409).send({
        status: 'DENIED',
        risk: 'execute',
        requiresConfirmation: true,
        summary: 'No live confirmation token is available for this user and simulation. Re-run the simulation to get a fresh human confirmation challenge.',
        warnings: ['The confirmation token is never exposed to the model; it must be present in the gateway human-confirmation store.'],
        safeForUser: true,
      } satisfies ToolResult);
    }

    const input = ConfirmSimulationInputSchema.parse({
      auth,
      simulationId,
      confirmationToken: pending.token,
      confirmationText: body.confirmationText,
      idempotencyKey: body.idempotencyKey ?? `human-confirm:${simulationId}`,
    });

    return {
      status: 'EXECUTED',
      risk: 'execute',
      requiresConfirmation: false,
      summary: 'Human-confirmed simulation executed through VaultCoreLite. The core executed the stored request, not a new LLM-generated request.',
      data: await deps.core.confirmSimulation(input),
      warnings: decision.warnings,
      safeForUser: true,
    } satisfies ToolResult;
  });
};

function denied(summary: string): ToolResult {
  return { status: 'DENIED', risk: 'execute', requiresConfirmation: true, summary, warnings: [], safeForUser: true };
}
