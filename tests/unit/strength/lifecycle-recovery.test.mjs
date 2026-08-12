import assert from 'node:assert/strict'
import test from 'node:test'

import * as Lifecycle from '../../../dist/Application/Strength/StrengthLifecycle.js'
import * as TraceRecovery from '../../../dist/Application/Strength/StrengthTraceRecovery.js'
import * as EventStore from '../../../dist/Domain/EventStore.js'
import * as Events from '../../../dist/Domain/StrengthEvents.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Projection from '../../../dist/Domain/StrengthProjection.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import { MessagePart } from '../../../dist/Infrastructure/OpenCode/Codec/HostMessageCodec.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { FSharpResult$2_Ok as ok } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

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

test('STRENGTH_007_lifecycle_promotes_only_exact_target_with_real_provider_output', () => {
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

test('STRENGTH_006_008_replay_excludes_Prepared_and_rebuilds_only_Promoted_at_exact_target_anchor', () => {
  const frame = bundle()
  let projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))
  const messages = toList([{ id: 'user-1' }, { id: 'run-1' }, { id: 'user-2' }])
  const messageIdOf = (message) => message.id
  const load = () => ok(frame)

  const beforePromotion = resultOf(Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, messages, load, projection))
  assert.equal(beforePromotion.ok, true)
  assert.equal(listItems(beforePromotion.value).length, 0)

  projection = apply(projection, promoted(frame))
  const afterPromotion = resultOf(Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, messages, load, projection))
  assert.equal(afterPromotion.ok, true)
  const [plan] = listItems(afterPromotion.value)
  assert.equal(plan.BeforeMessageIndex, 1)
  assert.equal(plan.Bundle.Digest, frame.Digest)
  assert.equal(plan.ExistingTraceRange, undefined)

  projection = apply(projection, Events.StrengthEvents_traced(decision('d1'), 10n, 14n))
  const tracedPlans = resultOf(
    Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, messages, load, projection),
  )
  assert.equal(tracedPlans.ok, true)
  const [tracedPlan] = listItems(tracedPlans.value)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(12n, tracedPlan), true)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(13n, tracedPlan), false)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(20n, tracedPlan), false)

  const missingAnchor = resultOf(
    Lifecycle.StrengthLifecycle_replayPlans(session('owner'), messageIdOf, toList([{ id: 'user-1' }]), load, projection),
  )
  assert.equal(missingAnchor.ok, false)
  assert.match(missingAnchor.error, /target anchor is absent/i)
})

test('STRENGTH_006_008_prepared_candidate_cannot_be_traced_or_raw_replayed', () => {
  const frame = bundle()
  const projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))
  assert.equal(Projection.StrengthProjectionModule_isPromoted(decision('d1'), projection), false)

  const traced = resultOf(Projection.StrengthProjectionModule_apply(
    projection,
    Events.StrengthEvents_traced(decision('d1'), 10n, 14n),
  ))
  assert.equal(traced.ok, false)

  const replay = resultOf(Lifecycle.StrengthLifecycle_replayPlans(
    session('owner'),
    (message) => message.id,
    toList([{ id: 'user-1' }, { id: 'run-1' }]),
    () => ok(frame),
    projection,
  ))
  assert.equal(replay.ok, true)
  assert.equal(listItems(replay.value).length, 0)
})

test('STRENGTH_008_compaction_does_not_retire_raw_replay_without_xtrace_coverage', () => {
  const frame = bundle()
  let projection = apply(Projection.StrengthProjectionModule_empty, prepared(frame))
  projection = apply(projection, promoted(frame))
  projection = apply(projection, Events.StrengthEvents_traced(decision('d1'), 40n, 44n))

  const [plan] = listItems(resultOf(Lifecycle.StrengthLifecycle_replayPlans(
    session('owner'),
    (message) => message.id,
    toList([{ id: 'user-1' }, { id: 'run-1' }]),
    () => ok(frame),
    projection,
  )).value)

  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(undefined, plan), true, 'physical cutoff / missing coverage cannot retire replay')
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(42n, plan), true)
  assert.equal(Lifecycle.StrengthLifecycle_needsRawReplay(43n, plan), false)
})

test('STRENGTH_008_trace_recovery_requires_one_exact_contiguous_canonical_match', () => {
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
