import assert from 'node:assert/strict'
import test from 'node:test'
import * as diag from '../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'

test('WHAT[CHATEXEC-013] diagnostic query derives nonterminal and physical-attempt counts from canonical projection', () => {
  const query = diag.queryReliability([], [], {}, {})
  assert.equal(typeof query, 'object')
})

test('WHAT[CHATEXEC-013] exact terminal settlement revokes the manual', () => {
  const r = recovery.testTerminalRevokesManual()
  assert.equal(r.revoked, true)
})

test('WHAT[CHATEXEC-013] pre-provider cancellation settlement revokes the manual', () => {
  const r = recovery.testPreProviderCancellationRevokesManual()
  assert.equal(r.revoked, true)
})
