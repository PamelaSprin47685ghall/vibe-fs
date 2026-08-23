import assert from 'node:assert/strict'
import test from 'node:test'
import * as turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'

const text = (value) => [{ type: 'text', text: value }]

test('WHAT[INTERACTION-AUTHORITY-019] repair claim does not turn an in-flight repair into exhaustion', () => {
  assert.equal(turns.repairDefectDecision(false, false, null, []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, false, null, []), 'AwaitRepairTerminal')
  assert.equal(turns.repairDefectDecision(true, false, 'tool-calls', []), 'AwaitRepairTerminal')
})

test('WHAT[INTERACTION-AUTHORITY-019] fresh invalid repair terminals re-open the gate reminder', () => {
  assert.equal(turns.repairDefectDecision(true, true, 'stop', []), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'length', text('partial')), 'RequestRepair')
  assert.equal(turns.repairDefectDecision(true, true, 'stop', text('done')), 'NoRepair')
})
