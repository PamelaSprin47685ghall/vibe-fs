import assert from 'node:assert/strict'
import test from 'node:test'

import * as Core from '../../../dist/Sphinx/V2/Core/Surface.js'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'
import {
  store, persistence, digest, mustOk, body, createdBody, work, transition, accepted,
  batch, prepare, append, current, withStore,
} from './persistence-support.mjs'

const ok = (result) => {
  assert.equal(Core.isOk(result), true)
  return Core.okValue(result)
}

const goalSpec = (text) =>
  Core.goalCreate({
    GoalId: Core.goalIdCreate('goal_loop'),
    Revision: ok(Core.revisionTryCreate(0n)),
    OriginalText: text,
    Constraints: Core.setOf([]),
    MaterialRefs: Core.setOf([]),
    AuthorizationRef: 'user-auth-1',
    CreatedBy: 'user',
    Amendments: Core.listOfItems([]),
  })

// A decision estimate. `Rank` is the only thing the selector reads, so a plan that was
// never compared has no rank at all rather than a rank of zero.
const modelEstimate = (planId, scopeId, location, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: location,
  Rank: rank,
  Kind: 'model-estimate',
})

const ordinalOnly = (planId, scopeId, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: rank,
  Kind: 'ordinal-only',
})

const provisional = (planId, scopeId, rank) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: rank,
  Kind: 'single-response-provisional',
})

const unestimated = (planId, scopeId) => ({
  PlanId: planId,
  ScopeId: scopeId,
  Location: null,
  Rank: null,
  Kind: 'unestimated',
})

const resourceSpec = (name, unit, limit) => ({
  Name: name,
  Kind: Core.resourceKindCreate('consumed', unit),
  AuthorizedLimit: limit,
})

test('WHAT[sphinx-v2-017] stop reasons stay distinguishable', () => {
  assert.equal(Loop.stopReasonName(Loop.stopRanked()), 'model-ranked-stop')
  assert.equal(Loop.stopReasonName(Loop.stopOrdinal()), 'ordinal-stop')
  assert.equal(Loop.stopReasonName(Loop.stopResourceLimited()), 'resource-limited')
  assert.equal(Loop.stopReasonName(Loop.stopNoPlan()), 'no-executable-plan')
  assert.equal(Loop.stopReasonName(Loop.stopCancelled()), 'user-cancelled')
})

test('WHAT[sphinx-v2-017] a newly created inquiry classifies as having no runnable work', () => {
  const state = ok(Core.stateOfCreate('iq_loop', '给出更稳妥的发布流程'))
  assert.deepEqual(Loop.classifyOutcome(state), {Outcome: 'no-runnable-work', Detail: 'no dispatchable work'})
})

test.todo('WHAT[sphinx-v2-017] runtime completion requires an accepted renderer result and durable AnswerCommitted')

const answer = (resultObservationId = 'answer-result') => body('AnswerCommitted', {
  renderWorkId: 'answer-work', resultObservationId,
  answerRef: 'answer-artifact', stopReason: 'model-ranked-stop',
})

const running = id => transition(id, 'Planned', {
  case: 'Running', fence: id + ':1:logical', physicalRef: id + ':host-receipt',
})

const successfulWork = () => [
  body('WorkPlanned', { work: [work('answer-work')] }),
  running('answer-work'),
  accepted('answer-work', 'answer-result'),
]

const rejectedAnswers = [
  {
    name: 'unexecuted work',
    events: () => [body('WorkPlanned', { work: [work('answer-work')] })],
    answer: () => answer(),
    reason: /answer work must have succeeded in its current attempt/,
  },
  {
    name: 'running work',
    events: () => [body('WorkPlanned', { work: [work('answer-work')] }), running('answer-work')],
    answer: () => answer(),
    reason: /answer work must have succeeded in its current attempt/,
  },
  ...['Failed', 'Cancelled'].map(state => ({
    name: state.toLowerCase() + ' work',
    events: () => [
      body('WorkPlanned', { work: [work('answer-work')] }), running('answer-work'),
      transition('answer-work', 'Running', { case: state, attempt: 1 }),
    ],
    answer: () => answer(),
    reason: /answer work must have succeeded in its current attempt/,
  })),
  {
    name: 'success tagged with a different attempt',
    events: () => [
      body('WorkPlanned', { work: [work('answer-work')] }), running('answer-work'),
      transition('answer-work', 'Running', { case: 'Succeeded', attempt: 2 }),
    ],
    answer: () => answer(),
    reason: /answer work must have succeeded in its current attempt/,
  },
  {
    name: 'missing accepted observation',
    events: successfulWork,
    answer: () => answer('missing-result'),
    reason: /answer must reference an accepted result/,
  },
  {
    name: 'another successful work observation',
    events: () => [
      ...successfulWork(),
      body('WorkPlanned', { work: [work('other-work')] }),
      running('other-work'),
      accepted('other-work', 'other-result'),
    ],
    answer: () => answer('other-result'),
    reason: /answer result does not match the successful work/,
  },
  {
    name: 'accepted result with a different output schema',
    events: () => {
      const result = accepted('answer-work', 'answer-result')
      result.payload.resultSchema = { id: 'other-response@2', hash: digest('{"type":"string"}') }
      return [body('WorkPlanned', { work: [work('answer-work')] }), running('answer-work'), result]
    },
    answer: () => answer(),
    reason: /answer result does not match the successful work/,
  },
]

for (const scenario of rejectedAnswers) {
  test(`WHAT[sphinx-v2-017] ${scenario.name} cannot publish an answer or its valid batch prefix`, async () => {
    await withStore(async ({ open, close }) => {
      const handle = open()
      const creation = await append(handle, batch('answer-provenance', 'create', [createdBody('answer provenance')]))
      const before = current(handle, 'answer-provenance')
      const events = scenario.events()
      const candidate = batch('answer-provenance', 'answer', [...events, scenario.answer()], creation)
      const result = persistence.prepareTransition(handle, digest, candidate)
      assert.equal(result.ok, false)
      assert.match(result.error.message, scenario.reason, 'rejection must come from the answer provenance guard')
      assert.deepEqual(current(handle, 'answer-provenance'), before)
      assert.deepEqual(store.heads(handle, creation.stream), [creation.id])

      const invalidFact = prepare(handle, batch('answer-provenance', 'invalid-fact', events, creation))
      invalidFact.payload.events.push(scenario.answer())
      const receipt = await store.append(handle, [invalidFact])
      assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
      assert.equal(receipt.cuts.length, 1)
      assert.equal(receipt.cuts[0].rule, 'SphinxV2')
      assert.match(receipt.cuts[0].reason, scenario.reason)
      assert.equal(receipt.cuts[0].failedEventId, invalidFact.id)
      assert.deepEqual(store.read(handle, invalidFact.id), invalidFact, 'invalid evidence remains durable')
      const rejected = current(handle, 'answer-provenance')
      assert.equal(rejected.ok, false)
      assert.equal(rejected.error.code, 'SemanticCut')
      assert.equal(Object.hasOwn(rejected, 'value'), false)
      close(handle)
      assert.deepEqual(current(open(), 'answer-provenance'), rejected)
    })
  })
}

test('WHAT[sphinx-v2-017] an answer preserves its exact successful work and accepted result after a cold reopen', async () => {
  await withStore(async ({ open, close }) => {
    const handle = open()
    const creation = await append(handle, batch('durable-answer', 'create', [createdBody('answer provenance')]))
    const committed = await append(handle, batch('durable-answer', 'answer', [...successfulWork(), answer()], creation))
    const state = current(handle, 'durable-answer')
    const value = mustOk(state)
    assert.deepEqual(value.answer, answer().payload)
    assert.deepEqual(value.status, { case: 'StopReached', stopReason: 'model-ranked-stop' })
    assert.equal(value.observations.find(entry => entry.key === 'answer-result').value.workId, 'answer-work')
    assert.deepEqual(store.read(handle, committed.id).payload.events.at(-1), answer())
    close(handle)
    assert.deepEqual(current(open(), 'durable-answer'), state)
  })
})
