import test from 'node:test'
import { assertEffectIsInjected, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[CAUSAL-009] causal wait contract excludes registry diagnostics mailbox and proof runtime', () => {
  assertPureContract('capability-type-only')
  assertEffectIsInjected('console')
})
