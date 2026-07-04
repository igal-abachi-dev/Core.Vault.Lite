import { createGoogleGenerativeAI } from '@ai-sdk/google';
import type { LanguageModel } from 'ai';
import type { AgentEnv } from '../../config/env.js';

export interface AgentModelBundle {
  model: LanguageModel;
  supportsTemperature: boolean;
  providerOptions?: Record<string, unknown>;
}

export interface RoleModels {
  quality: AgentModelBundle;
  cheap: AgentModelBundle;
  fast: AgentModelBundle;
}

function stripGooglePrefix(spec: string) {
  return spec.startsWith('google/') ? spec.slice('google/'.length) : spec;
}

function isReasoningGemini(modelId: string) {
  const id = modelId.toLowerCase();
  return id.includes('gemini-2.5') || id.includes('gemini-3') || id.includes('-thinking');
}

function googleOptions(modelId: string, role: keyof RoleModels) {
  const id = modelId.toLowerCase();
  if (!isReasoningGemini(id)) return undefined;
  const effort = role === 'quality' ? 'high' : role === 'cheap' ? 'medium' : 'low';
  if (id.includes('gemini-3')) return { google: { thinkingConfig: { thinkingLevel: effort, includeThoughts: false } } };
  return { google: { thinkingConfig: { thinkingBudget: effort === 'high' ? -1 : effort === 'medium' ? 4096 : 0, includeThoughts: false } } };
}

function bundle(modelIdSpec: string, role: keyof RoleModels, apiKey: string): AgentModelBundle {
  const modelId = stripGooglePrefix(modelIdSpec);
  const google = createGoogleGenerativeAI({ apiKey });
  return { model: google(modelId), supportsTemperature: !isReasoningGemini(modelId), providerOptions: googleOptions(modelId, role) };
}

export function createModels(env: AgentEnv): RoleModels {
  return {
    quality: bundle(env.QUALITY_MODEL, 'quality', env.GOOGLE_GENERATIVE_AI_API_KEY),
    cheap: bundle(env.CHEAP_MODEL, 'cheap', env.GOOGLE_GENERATIVE_AI_API_KEY),
    fast: bundle(env.FAST_MODEL, 'fast', env.GOOGLE_GENERATIVE_AI_API_KEY),
  };
}

export function modelRoleForAgent(agentId: string): keyof RoleModels {
  if (agentId === 'triage' || agentId === 'confirmation_specialist') return 'cheap';
  if (agentId.includes('investment') || agentId.includes('portfolio') || agentId.includes('tax') || agentId.includes('retirement')) return 'quality';
  return 'cheap';
}
