import test from 'node:test'
import { assertEffectIsInjected, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[PROC-012] process and PTY contracts exclude Node adapters mutable handles and delegation runtime', () => {
  assertPureContract('capability-type-only')
  assertEffectIsInjected('process-control')
})
