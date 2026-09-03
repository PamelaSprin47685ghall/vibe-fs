import test from 'node:test'
import { assertEffectIsInjected, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[TIME-008] temporal contracts exclude Node adapters mutable timers and SessionStartedAt projection', () => {
  assertPureContract('capability-type-only')
  assertEffectIsInjected('timer')
})
