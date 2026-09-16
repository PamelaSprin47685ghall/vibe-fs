import assert from 'node:assert/strict'
import test from 'node:test'
import * as rwu from '../../../dist/Execution/Delegation/ReusableWorkUnitSurface.js'

test('WHAT[DELEG-027] active fork assignment never becomes BusyAgentNudge', () => {
  assert.equal(rwu.isBusyAgentNudgeAllowedForActive(), false)
})
