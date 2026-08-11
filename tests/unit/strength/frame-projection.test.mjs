import assert from 'node:assert/strict'
import test from 'node:test'

import * as EventStore from '../../../dist/Domain/EventStore.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Events from '../../../dist/Domain/StrengthEvents.js'
import * as Projection from '../../../dist/Domain/StrengthProjection.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const caseOf = (value) => value.cases()[value.tag]
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }

const H = (text) => `H(${text})`
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const payload = (value) => EventStore.PayloadRefModule_create(value)

const exchange = (tool, args, result) => ({
  ToolName: tool,
  CanonicalArguments: args,
  CanonicalResult: result,
})

const batch = (ordinal, exchanges) => ({ RequestOrdinal: ordinal, Exchanges: toList(exchanges) })

test('STRENGTH_005_frame_bundle_accepts_only_complete_read_glob_grep_batches', () => {
  const good = resultOf(
    Frame.StrengthFrame_tryBuild(
      H,
      10000,
      toList([
        batch(1, [exchange('read', '{"filePath":"a"}', 'alpha'), exchange('grep', '{"pattern":"x"}', 'a:1:x')]),
        batch(2, [exchange('glob', '{"pattern":"**/*.fs"}', 'a.fs')]),
      ]),
    ),
  )

  assert.equal(good.ok, true)
  assert.equal(listItems(good.value.Batches).length, 2)
  assert.match(good.value.Digest, /^H\(/)
  assert.ok(good.value.ByteLength > 0)

  const write = resultOf(Frame.StrengthFrame_tryBuild(H, 10000, toList([batch(1, [exchange('write', '{}', 'ok')])])))
  assert.equal(write.ok, false)
  assert.equal(caseOf(write.error), 'UnsupportedTool')

  const empty = resultOf(Frame.StrengthFrame_tryBuild(H, 10000, toList([batch(1, [])])))
  assert.equal(empty.ok, false)
  assert.equal(caseOf(empty.error), 'EmptyBatch')
})

test('STRENGTH_005_frame_digest_and_owner_wire_ids_are_restart_stable', () => {
  const batches = toList([batch(1, [exchange('read', '{"filePath":"a"}', 'alpha')])])
  const first = resultOf(Frame.StrengthFrame_tryBuild(H, 10000, batches)).value
  const second = resultOf(Frame.StrengthFrame_tryBuild(H, 10000, batches)).value
  assert.equal(first.Digest, second.Digest)

  const id1 = Frame.StrengthFrame_wireToolCallId(H, session('owner'), decision('d1'), 1, 1, first.Digest)
  const id2 = Frame.StrengthFrame_wireToolCallId(H, session('owner'), decision('d1'), 1, 1, first.Digest)
  const changed = Frame.StrengthFrame_wireToolCallId(H, session('owner'), decision('d1'), 1, 2, first.Digest)
  assert.equal(id1, id2)
  assert.notEqual(id1, changed)
  assert.doesNotMatch(id1, /time|guid|random/i)
})

const prepared = ({ decisionId = 'd1', target = 'run-1', digest = 'frame-a', refs = ['p1'] } = {}) =>
  Events.StrengthEvents_prepared(
    session('owner'),
    decision(decisionId),
    run(target),
    session('replica'),
    StrengthBudget.K1,
    'anchor-a',
    digest,
    123,
    toList(refs.map(payload)),
  )

const promoted = ({ decisionId = 'd1', target = 'run-1', digest = 'frame-a', refs = ['p1'] } = {}) =>
  Events.StrengthEvents_promoted(
    session('owner'),
    decision(decisionId),
    run(target),
    digest,
    toList(refs.map(payload)),
  )

const apply = (state, event) => resultOf(Projection.StrengthProjectionModule_apply(state, event))

test('STRENGTH_006_007_projection_enforces_Prepared_then_same_target_Promoted', () => {
  let state = Projection.StrengthProjectionModule_empty

  const noPrepared = apply(state, promoted())
  assert.equal(noPrepared.ok, false)
  assert.equal(caseOf(noPrepared.error), 'PromotionWithoutPrepared')

  state = apply(state, prepared()).value
  assert.equal(Projection.StrengthProjectionModule_hasPrepared(decision('d1'), state), true)
  assert.equal(
    Id.StrengthDecisionIdModule_value(Projection.StrengthProjectionModule_tryDecisionForTarget(run('run-1'), state)),
    'd1',
  )

  const duplicate = apply(state, prepared())
  assert.equal(duplicate.ok, true)

  const conflict = apply(state, prepared({ digest: 'frame-b' }))
  assert.equal(conflict.ok, false)
  assert.equal(caseOf(conflict.error), 'PreparedConflict')

  const wrongRun = apply(state, promoted({ target: 'run-2' }))
  assert.equal(wrongRun.ok, false)
  assert.equal(caseOf(wrongRun.error), 'PromotionMismatch')

  state = apply(state, promoted()).value
  assert.equal(Projection.StrengthProjectionModule_isPromoted(decision('d1'), state), true)
  assert.equal(apply(state, promoted()).ok, true)
})

test('STRENGTH_008_traced_requires_promotion_and_monotonic_nonempty_range', () => {
  let state = apply(Projection.StrengthProjectionModule_empty, prepared()).value

  const early = apply(state, Events.StrengthEvents_traced(decision('d1'), 10n, 12n))
  assert.equal(early.ok, false)
  assert.equal(caseOf(early.error), 'TraceWithoutPromotion')

  state = apply(state, promoted()).value
  const emptyRange = apply(state, Events.StrengthEvents_traced(decision('d1'), 12n, 12n))
  assert.equal(emptyRange.ok, false)
  assert.equal(caseOf(emptyRange.error), 'InvalidTraceRange')

  state = apply(state, Events.StrengthEvents_traced(decision('d1'), 10n, 12n)).value
  const range = Projection.StrengthProjectionModule_tryTraceRange(decision('d1'), state)
  assert.equal(range.StartInclusive, 10n)
  assert.equal(range.EndExclusive, 12n)

  assert.equal(apply(state, Events.StrengthEvents_traced(decision('d1'), 10n, 12n)).ok, true)
  const conflict = apply(state, Events.StrengthEvents_traced(decision('d1'), 11n, 13n))
  assert.equal(conflict.ok, false)
  assert.equal(caseOf(conflict.error), 'TraceConflict')
})
