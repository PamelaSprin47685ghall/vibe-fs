import assert from 'node:assert/strict'
import test from 'node:test'
import * as Turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'

const reasoning = (text) => ({ type: 'reasoning', text })
const toolCall = (callID, tool, args) => ({ type: 'tool-call', callID, tool, args })
const activity = (kind) => ({ type: kind })

// manager 会话 2026-09-28 00:13:01 的静默截断回合：step-start + reasoning，无正文、无工具调用，finish=length
const truncatedTurn = [activity('step-start'), reasoning('schema-contract 已接下。四个 in-flight…')]

test('WHAT[interaction-authority-023] an unsettled retry attempt suppresses idle repair', () => {
  assert.equal(Turns.repairSuppressionHolds(true, false, false, undefined, []), true)
  assert.equal(Turns.repairSuppressionHolds(true, false, false, undefined, [reasoning('on the wire')]), true)
  assert.equal(Turns.repairSuppressionHolds(true, false, true, 'tool-calls', [toolCall('c1', 'read', '{}')]), true)
})

test('WHAT[interaction-authority-023] a settled attempt releases the suppression and regains nudge qualification', () => {
  assert.equal(Turns.repairSuppressionHolds(true, false, true, 'length', truncatedTurn), false)
  assert.equal(Turns.repairSuppressionHolds(true, false, true, 'stop', [activity('step-finish')]), false)
  assert.equal(Turns.repairSuppressionHolds(true, false, true, 'stop', [reasoning('no formal text')]), false)
  assert.equal(Turns.repairDefectDecision(false, true, 'length', truncatedTurn), 'RequestRepair')
})

test('WHAT[interaction-authority-023] the durable execution terminal releases the suppression on its own', () => {
  assert.equal(Turns.repairSuppressionHolds(true, true, false, undefined, [reasoning('still streaming')]), false)
  assert.equal(Turns.repairSuppressionHolds(true, true, true, 'tool-calls', [toolCall('c1', 'read', '{}')]), false)
})

test('WHAT[interaction-authority-023] suppression is scoped to the retry continuation only', () => {
  assert.equal(Turns.repairSuppressionHolds(false, false, false, undefined, []), false)
  assert.equal(Turns.repairSuppressionHolds(false, false, true, 'length', truncatedTurn), false)
})
