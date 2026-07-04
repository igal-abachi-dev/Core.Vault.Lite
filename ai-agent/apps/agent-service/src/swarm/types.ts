import type { CoreMessage, ToolSet } from 'ai';
import type { HandoffAgentId } from '@finance-agent/schemas';

export interface FinanceAgentNode {
  id: HandoffAgentId;
  name: string;
  system: string;
  toolGroup: 'read' | 'planning' | 'confirmation' | 'wealth' | 'investment' | 'risk' | 'tax' | 'credit' | 'operations';
  canHandoff: boolean;
}

export interface SwarmState {
  currentAgent: HandoffAgentId;
  handoffCount: number;
  contextData: Record<string, unknown>;
  trace: Array<{ from: HandoffAgentId; to: HandoffAgentId; reason: string }>;
}

export interface SwarmRunInput { message: string; messages?: CoreMessage[]; userId: string; customerId: string; language: 'he' | 'en'; accountIds: string[]; }
export interface PendingHumanConfirmation { simulationId: string; path: string; summary?: string; expiresAt?: string; }
export interface SwarmRunResult { text: string; activeAgent: HandoffAgentId; trace: SwarmState['trace']; toolResults: unknown[]; pendingConfirmations: PendingHumanConfirmation[]; }
export type AgentTools = ToolSet;
