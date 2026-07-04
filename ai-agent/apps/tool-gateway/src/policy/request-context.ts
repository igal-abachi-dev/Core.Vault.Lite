import type { FastifyRequest } from 'fastify';
import { AuthContextSchema, type AuthContext } from '@finance-agent/schemas';

export function authFromRequest(request: FastifyRequest): AuthContext {
  const userId = header(request, 'x-user-id') ?? 'demo-user';
  const customerId = header(request, 'x-customer-id') ?? 'demo-customer';
  const language = (header(request, 'x-language') ?? 'he') as 'he' | 'en';
  const accountHeader = header(request, 'x-allowed-account-ids') ?? '';
  const allowedAccountIds = accountHeader.split(',').map((x) => x.trim()).filter(Boolean);
  return AuthContextSchema.parse({ userId, customerId, country: 'IL', language, allowedAccountIds });
}

function header(request: FastifyRequest, name: string): string | undefined {
  const value = request.headers[name];
  return Array.isArray(value) ? value[0] : value;
}
