import assert from 'node:assert/strict'
import test from 'node:test'
import * as rwu from '../../../dist/Execution/Delegation/ReusableWorkUnitSurface.js'

test('WHAT[DELEG-025] reusable fork terminal failure is guarded by the accepted authority root', () => {
  const terminal = rwu.guardTerminalOutcome('auth-root-1', { authorityRootId: 'auth-root-2' })
  assert.equal(terminal.accepted, false)
})
