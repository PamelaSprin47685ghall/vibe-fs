import assert from 'node:assert/strict'
import test from 'node:test'
import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'

const ok = result => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}
const goal = text => Core.stateGoalOf(ok(Core.stateOfCreate('inquiry-goal-test', text)))

test('WHAT[sphinx-v2-001] a real created goal keeps the exact submitted text and initial revision', () => {
  const text = '\n给出一个更稳妥的发布流程\r\n  保留原文空白  '
  const created = goal(text)
  assert.equal(Core.goalTextOf(created), text)
  assert.equal(Core.goalRevisionValue(created), 0n)
})

test('WHAT[sphinx-v2-001] an explicit amendment advances revision while leaving the earlier goal value unchanged', () => {
  const before = goal('给出一个更稳妥的发布流程')
  const amended = ok(Core.goalTryAmend(
    'user', Core.listOfItems(['不影响线上用户']), '给出不影响线上用户的发布流程', before,
  ))
  assert.equal(Core.goalRevisionValue(amended), 1n)
  assert.equal(Core.goalTextOf(amended), '给出不影响线上用户的发布流程')
  assert.equal(Core.goalAmendmentsOf(amended).length, 1)
  assert.equal(Core.goalTextOf(before), '给出一个更稳妥的发布流程')
  assert.equal(Core.goalRevisionValue(before), 0n)
})

test('WHAT[sphinx-v2-001] a blank amendment authorizer is refused without advancing the original goal', () => {
  const before = goal('Original goal')
  assert.equal(Core.isError(Core.goalTryAmend('', Core.listOfItems([]), 'Replacement', before)), true)
  assert.equal(Core.goalTextOf(before), 'Original goal')
  assert.equal(Core.goalRevisionValue(before), 0n)
})

test.todo('WHAT[sphinx-v2-001] the actual command boundary accepts only user-authorized goal amendments and invalidates dependent estimates without deleting observations')
