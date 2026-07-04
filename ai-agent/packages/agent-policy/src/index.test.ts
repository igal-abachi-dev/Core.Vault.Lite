import { describe, expect, it } from 'vitest';
import { classifyToolRisk } from './index.js';

describe('classifyToolRisk', () => {
  it('classifies read, simulate and execute tools', () => {
    expect(classifyToolRisk('get_account_balance')).toBe('read');
    expect(classifyToolRisk('simulate_transaction')).toBe('simulate');
    expect(classifyToolRisk('confirm_simulation')).toBe('execute');
    expect(classifyToolRisk('execute_transfer')).toBe('execute');
  });
});
