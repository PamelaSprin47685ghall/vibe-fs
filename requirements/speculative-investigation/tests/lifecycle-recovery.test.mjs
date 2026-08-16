import assert from 'node:assert/strict'
import test from 'node:test'

import * as Lifecycle from '../../../dist/Strength/Lifecycle.js'
import * as TraceRecovery from '../../../dist/Strength/Replica/TraceRecovery.js'
import * as EventStore from '../../../dist/Persistence/EventStore/Model.js'
import * as Events from '../../../dist/Strength/Events.js'
import * as Frame from '../../../dist/Strength/Frame.js'
import * as Projection from '../../../dist/Strength/Projection/Model.js'
import { TurnOutcome } from '../../../dist/Composition/Turn/Program.js'
import { StrengthBudget } from '../../../dist/Strength/Budget.js'
import { MessagePart } from '../../../dist/OpenCode/Codec/HostMessageCodec.js'
import * as Id from '../../../dist/Foundation/Identity.js'
import { okResult, toList, listItems } from '../../verification-system/tests/support/domain.mjs'

const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
const H = (text) => `H(${text})`
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const payload = (value) => EventStore.PayloadRefModule_create(value)
const exchange = (tool, args, result) => ({ ToolName: tool, CanonicalArguments: args, CanonicalResult: result })
const batch = (ordinal, exchanges) => ({ RequestOrdinal: ordinal, Exchanges: toList(exchanges) })
const call = (id, name, args) => new MessagePart(2, [id, name, args])

const bundle = () => resultOf(Frame.StrengthFrame_tryBuild(
  H,
  10000,
  toList([batch(1, [exchange('read', '{"filePath":"a"}', 'alpha'), exchange('grep', '{"pattern":"x"}', 'a:1:x')])]),
)).value

const prepared = (frame, decisionId = 'd1', target = 'run-1') => Events.StrengthEvents_prepared(
  session('owner'),
  decision(decisionId),
  run(target),
  session(`replica-${decisionId}`),
  StrengthBudget.K1,
  'anchor-a',
  frame.Digest,
  frame.ByteLength,
  toList([payload(`p-${decisionId}`)]),
)

const promoted = (frame, decisionId = 'd1', target = 'run-1') => Events.StrengthEvents_promoted(
  session('owner'),
  decision(decisionId),
  run(target),
  frame.Digest,
  toList([payload(`p-${decisionId}`)]),
)

const apply = (state, event) => resultOf(Projection.StrengthProjectionModule_apply(state, event)).value

test('WHAT[SPEC-INV-007] STRENGTH_007_lifecycle_promotes_only_exact_target_with_real_provider_output', () => {
  const frame = bundle()
  let projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))

  const realTurn = {
    ProviderRun: run('run-1'),
    Parts: [call('c1', 'read', '{}')],
    Outcome: TurnOutcome.TurnCompleted,
  }
  const event = Lifecycle.StrengthLifecycle_reconcileEvent(projection, realTurn)
  assert.equal(caseOf(event), 'Promoted')
  assert.equal(Id.StrengthDecisionIdModule_value(event.fields[0].DecisionId), 'd1')
  assert.equal(event.fields[0].FrameDigest, frame.Digest)

  assert.equal(Lifecycle.StrengthLifecycle_reconcileEvent(projection, { ...realTurn, ProviderRun: run('other') }), undefined)

  const abandoned = Lifecycle.StrengthLifecycle_reconcileEvent(projection, {
    ProviderRun: run('run-1'),
    Parts: [call('partial', 'read', '{}')],
    Outcome: new TurnOutcome(4, ['failed']),
  })
  assert.equal(caseOf(abandoned), 'Abandoned')
  const abandonedProjection = apply(projection, abandoned)
  assert.equal(Projection.StrengthProjectionModule_tryDecisionForTarget(run('run-1'), abandonedProjection), undefined)

  projection = apply(projection, event)
  assert.equal(Lifecycle.StrengthLifecycle_reconcileEvent(projection, realTurn), undefined)
})

test('WHAT[SPEC-INV-008] STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor', async () => {
  const frame = bundle()
  let projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))
  const messages = toList([{ id: 'user-1' }, { id: 'run-1' }, { id: 'user-2' }])
  const messageIdOf = (message) => message.id
  const load = async () => okResult(frame)

  const beforePromotion = resultOf(await Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, messages, load, projection))
  assert.equal(beforePromotion.ok, true)
  assert.equal(listItems(beforePromotion.value).length, 0)

  projection = apply(projection, promoted(frame))
  const afterPromotion = resultOf(await Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, messages, load, projection))
  assert.equal(afterPromotion.ok, true)
  const [plan] = listItems(afterPromotion.value)
  assert.equal(plan.BeforeMessageIndex, 1)
  assert.equal(plan.Bundle.Digest, frame.Digest)
  assert.equal(plan.ExistingTraceRange, undefined)

  projection = apply(projection, Events.StrengthEvents_traced(decision('d1'), 10n, 14n))
  const tracedPlans = resultOf(
    await Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, messages, load, projection),
  )
  assert.equal(tracedPlans.ok, true)
  const [tracedPlan] = listItems(tracedPlans.value)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(12n, tracedPlan), true)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(13n, tracedPlan), false)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(20n, tracedPlan), false)

  const missingAnchor = resultOf(
    await Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, toList([{ id: 'user-1' }]), load, projection),
  )
  assert.equal(missingAnchor.ok, false)
  assert.match(missingAnchor.error, /target anchor is absent/i)
})

test('WHAT[SPEC-INV-006] STRENGTH_006_008_prepared_candidate_cannot_be_traced_or_raw_replayed', async () => {
  const frame = bundle()
  const projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))
  assert.equal(Projection.StrengthProjectionModule_isPromoted(decision('d1'), projection), false)

  const traced = resultOf(Projection.StrengthProjectionModule_apply(
    projection,
    Events.StrengthEvents_traced(decision('d1'), 10n, 14n),
  ))
  assert.equal(traced.ok, false)

  const replay = resultOf(await Lifecycle.StrengthLifecycle_replayPlans(
    session('owner'),
    (message) => message.id,
    toList([{ id: 'user-1' }, { id: 'run-1' }]),
    async () => okResult(frame),
    projection,
  ))
  assert.equal(replay.ok, true)
  assert.equal(listItems(replay.value).length, 0)
})

test('WHAT[SPEC-INV-008] STRENGTH_008_compaction_does_not_retire_raw_replay_without_xtrace_coverage', async () => {
  const frame = bundle()
  let projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))
  projection = apply(projection, promoted(frame))
  projection = apply(projection, Events.StrengthEvents_traced(decision('d1'), 40n, 44n))

  const [plan] = listItems(resultOf(await Lifecycle.StrengthLifecycle_replayPlans(
    session('owner'),
    (message) => message.id,
    toList([{ id: 'user-1' }, { id: 'run-1' }]),
    async () => okResult(frame),
    projection,
  )).value)

  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(undefined, plan), true, 'physical cutoff / missing coverage cannot retire replay')
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(42n, plan), true)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(43n, plan), false)
})

test('WHAT[SPEC-INV-008] STRENGTH_008_trace_recovery_requires_one_exact_contiguous_canonical_match', () => {
  const frame = bundle()
  const expected = listItems(TraceRecovery.StrengthTraceRecovery_expectedParts(frame))
  assert.equal(expected.length, 4)

  const observed = expected.map(([kind, toolName, body], index) => ({
    CursorSequence: 20n + BigInt(index),
    Kind: kind,
    ToolName: toolName,
    Body: body,
  }))

  const recovered = resultOf(TraceRecovery.StrengthTraceRecovery_recoverRange(frame, toList(observed)))
  assert.equal(recovered.ok, true)
  assert.equal(recovered.value.StartInclusive, 20n)
  assert.equal(recovered.value.EndExclusive, 24n)

  const ambiguous = resultOf(TraceRecovery.StrengthTraceRecovery_recoverRange(frame, toList([...observed, ...observed.map((p, i) => ({ ...p, CursorSequence: 30n + BigInt(i) }))])))
  assert.equal(ambiguous.ok, false)
  assert.match(ambiguous.error, /ambiguous/i)

  const gapped = observed.map((part, index) => ({ ...part, CursorSequence: index < 2 ? part.CursorSequence : part.CursorSequence + 1n }))
  const gapResult = resultOf(TraceRecovery.StrengthTraceRecovery_recoverRange(frame, toList(gapped)))
  assert.equal(gapResult.ok, false)
  assert.match(gapResult.error, /contiguous/i)
})
