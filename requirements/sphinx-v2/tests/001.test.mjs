import assert from 'node:assert/strict'
import test from 'node:test'
import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import { persistence, mustOk, body, envelope, digest, graphNode, seedBodies, batch, append, current, withStore } from './persistence-support.mjs'

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

test('WHAT[sphinx-v2-001] durable goal snapshots preserve exact user text and proposals do not become amendments during cold replay', async () => {
  await withStore(async ({ open, close }) => {
    const first = open()
    const text = '\n用户原文\r\n  保留空白  '
    const creation = await append(first, batch('goal-durable', 'create', seedBodies(text)))
    const before = mustOk(current(first, 'goal-durable'))
    const proposedText = '{"replacementText":"model proposal"}'
    const proposal = graphNode('proposal-1', 'ReframingProposal')
    proposal.payload = envelope(proposedText)
    proposal.contentHash = digest(proposedText)
    const proposed = await append(first, batch('goal-durable', 'proposal', [body('GraphPatched', {
      pluginRef: 'proposal-plugin', patch: { upsertNodes: [proposal], removeNodes: [], upsertEdges: [], removeEdges: [] },
    })], creation))
    assert.deepEqual(mustOk(current(first, 'goal-durable')).goal, before.goal)
    const userText = '  用户明确修订\n'
    const amendedGoal = {
      ...before.goal, revision: '1', originalText: userText, constraints: ['user constraint'],
      amendments: [{ authorizedBy: 'user-authorized', revision: '1', addedConstraints: ['user constraint'], replacedText: userText }],
    }
    const malformed = { ...amendedGoal, amendments: [{ ...amendedGoal.amendments[0], authorizedBy: '' }] }
    const rejected = persistence.prepareTransition(first, digest, batch('goal-durable', 'bad-amendment', [body('GoalAmended', malformed)], proposed))
    assert.equal(rejected.ok, false)
    assert.deepEqual(mustOk(current(first, 'goal-durable')).goal, before.goal)
    await append(first, batch('goal-durable', 'user-amendment', [body('GoalAmended', amendedGoal)], proposed))
    const amended = current(first, 'goal-durable')
    assert.deepEqual(mustOk(amended).goal, amendedGoal)
    assert.deepEqual(mustOk(amended).observations, before.observations)
    assert.equal(before.goal.originalText, text)
    close(first)
    assert.deepEqual(current(open(), 'goal-durable'), amended)
  })
})

test.todo('WHAT[sphinx-v2-001] the actual command boundary accepts only user-authorized goal amendments and invalidates dependent estimates without deleting observations')
