// FROZEN — 2026-08-14. Dumb bare Git remote + independent hook-process convergence.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

test('WHAT[DURABLE-CONVERGENCE-008] reference_transaction_is_also_full_bidirectional_convergence', async () => {
  const source = readFileSync(new URL('../../../../src/Wanxiangshu/Git/Hook/Sync.fs', import.meta.url), 'utf8')
  assert.match(source, /runReferenceTransaction/)
  assert.match(source, /converge remote observed/)
  assert.doesNotMatch(source, /downloadOnly|importOnly|ConvergeObserved/)
})
