export interface PendingConfirmation {
  simulationId: string;
  token: string;
  userId: string;
  customerId: string;
  expiresAt?: string;
  createdAt: number;
}

export interface ConfirmationTokenStore {
  put(entry: Omit<PendingConfirmation, 'createdAt'>): void;
  take(args: { simulationId: string; userId: string; customerId: string }): PendingConfirmation | undefined;
  peek(args: { simulationId: string; userId: string; customerId: string }): Omit<PendingConfirmation, 'token'> | undefined;
}

export class InMemoryConfirmationTokenStore implements ConfirmationTokenStore {
  private readonly entries = new Map<string, PendingConfirmation>();

  put(entry: Omit<PendingConfirmation, 'createdAt'>): void {
    this.entries.set(this.key(entry.simulationId, entry.userId, entry.customerId), { ...entry, createdAt: Date.now() });
    this.sweepExpired();
  }

  take(args: { simulationId: string; userId: string; customerId: string }): PendingConfirmation | undefined {
    const key = this.key(args.simulationId, args.userId, args.customerId);
    const value = this.entries.get(key);
    if (!value) return undefined;
    this.entries.delete(key);
    return value;
  }

  peek(args: { simulationId: string; userId: string; customerId: string }): Omit<PendingConfirmation, 'token'> | undefined {
    const value = this.entries.get(this.key(args.simulationId, args.userId, args.customerId));
    if (!value) return undefined;
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const { token, ...safe } = value;
    return safe;
  }

  private key(simulationId: string, userId: string, customerId: string) {
    return `${userId}:${customerId}:${simulationId}`;
  }

  private sweepExpired() {
    const now = Date.now();
    for (const [key, value] of this.entries) {
      const expires = value.expiresAt ? Date.parse(value.expiresAt) : value.createdAt + 15 * 60_000;
      if (Number.isFinite(expires) && expires <= now) this.entries.delete(key);
    }
  }
}
