import { ToolGatewayClient } from '@finance-agent/banking-tools';
import { buildApp } from './app.js';
import { loadEnv } from './config/env.js';
import { createModels } from './lib/ai/models.js';

const env = loadEnv();
const gateway = new ToolGatewayClient({ baseUrl: env.TOOL_GATEWAY_BASE_URL, token: env.AGENT_GATEWAY_TOKEN });
const app = buildApp({ env, models: createModels(env), gateway });

for (const signal of ['SIGINT', 'SIGTERM'] as const) {
  process.once(signal, async () => { app.log.info({ signal }, 'shutting down'); await app.close(); process.exit(0); });
}

await app.listen({ host: env.AGENT_HOST, port: env.AGENT_PORT });
