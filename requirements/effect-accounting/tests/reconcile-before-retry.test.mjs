// Requested-only effect reconciliation stays pending until physical proof.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'

test('WHAT[EFFECT-ACCOUNTING-005] requested_only_without_physical_evidence_stays_pending_not_blind_retry', () => {
  assert.equal(child.resolve('active', 'missing', [], '').result, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-005] outcome_unknown_without_physical_evidence_never_becomes_terminal', () => {
  assert.equal(child.resolve('active', 'missing', [], '').result, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-005] terminal_issued_only_after_proven_physical_evidence', () => {
  assert.equal(child.provenTerminal('{"status":"ok"}').ok, true)
  assert.equal(child.resolve('active', 'terminal', [], 'body').result, 'RecoveredTerminal')
})
