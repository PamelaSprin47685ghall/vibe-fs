import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'

test('WHAT[CHATEXEC-008] recovery begins from durable activation and re-enters only on causal events', () => {
  const r = recovery.testRecoveryReentry()
  assert.equal(r.ok, true)
})
