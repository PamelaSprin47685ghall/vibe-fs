import test from 'node:test'
import { assertEffectIsInjected, assertFatalBoundary, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[DELEG-029] delegation ports reject Host runtime PTY process and AgentFact reverse ownership', () => {
  assertPureContract('capability-type-only')
  assertEffectIsInjected('process-control')
})

test('WHAT[DELEG-030] delegation invariant fatal preserves settlement and one injected fuse', () => {
  assertFatalBoundary('delegation')
})
