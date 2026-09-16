import assert from 'node:assert/strict';
import test from 'node:test';

import { StrictMockSignals } from './e2e/support/strict-mock-signals.js';

test('WHAT[VERIFICATION-SYSTEM-006] waitAny selects either exact branch and removes every sibling waiter', async () => {
  for (const winner of ['original.1', 'guarded.0']) {
    const signals = new StrictMockSignals();
    const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0']);

    signals.consume({ id: winner, permanent: true });

    assert.equal(await waiting, winner);
    assert.equal(signals._expectationWaiters.size, 0);
  }
});

test('WHAT[VERIFICATION-SYSTEM-005] waitAny fatal cancellation removes every registered waiter', async () => {
  const signals = new StrictMockSignals();
  const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0']);

  signals.fail(new Error('provider mismatch'));

  await assert.rejects(waiting, /provider mismatch/);
  assert.equal(signals._expectationWaiters.size, 0);
});

test('WHAT[VERIFICATION-SYSTEM-005] waitAny rejects an open or malformed alternative set', async () => {
  const signals = new StrictMockSignals();

  await assert.rejects(signals.waitForAnyExpectation('original.1'), /array of at least two/);
  await assert.rejects(signals.waitForAnyExpectation(['original.1', 'original.1']), /unique non-blank/);
});
