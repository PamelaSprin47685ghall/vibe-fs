import test from 'node:test'
import { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[HOST-BOUNDARY-027] Host message loop and envelope slices reject the old wide signal closure', () => {
  assertPureContract()
  assertEffectIsInjected('host')
})

test('WHAT[HOST-BOUNDARY-028] typed subscription and diagnostic injection preserve one failure owner', async () => {
  await assertOptionalObservationNoninterference()
  assertEffectIsInjected('console')
})

test('WHAT[HOST-BOUNDARY-029] fatal vocabulary stays pure and physical execution is composition-only', () => {
  assertPureContract('capability-type-only')
  assertFatalBoundary('host-boundary')
})
