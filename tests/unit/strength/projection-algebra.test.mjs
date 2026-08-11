import assert from 'node:assert/strict'
import test from 'node:test'

import * as P from '../../../dist/Domain/ProjectionAlgebra.js'
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
const session = (value) => Id.SessionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const textPart = (text) => new Provider.WirePart(0, [text])
const message = (role, parts) => ({ Role: role, Parts: toList(parts) })

const snapshot = new P.ProjectionSnapshot(
  { ProviderId: undefined, ModelId: undefined, Variant: undefined, Tools: toList([]), System: toList([]), Messages: toList([]) },
  undefined,
  toList([]),
  undefined,
  undefined,
)

const bundle = resultOf(
  Frame.StrengthFrame_tryBuild(
    H,
    10000,
    toList([
      {
        RequestOrdinal: 1,
        Exchanges: toList([
          { ToolName: 'read', CanonicalArguments: '{"filePath":"a"}', CanonicalResult: 'alpha' },
          { ToolName: 'grep', CanonicalArguments: '{"pattern":"x"}', CanonicalResult: 'a:1:x' },
        ]),
      },
    ]),
  ),
).value

test('STRENGTH_009_016_projection_exposes_strength_intent_constructors', () => {
  assert.equal(typeof P.ProjectionIntentModule_useStrengthMirror, 'function')
  assert.equal(typeof P.ProjectionIntentModule_strengthCandidate, 'function')
  assert.equal(typeof P.ProjectionIntentModule_strengthPromoted, 'function')
  assert.equal(typeof P.ProjectionIntentModule_strengthReplicaLocal, 'function')
})

test('STRENGTH_009_mirror_conflicts_with_normal_work_base_selection', () => {
  const mirror = P.ProjectionIntentModule_useStrengthMirror(
    decision('d1'),
    run('target'),
    'sem-a',
    toList([message('user', [textPart('mirror')])]),
  )
  const planned = resultOf(P.ProjectionPlanner_plan(toList([P.ProjectionIntent.KeepPhysicalPrefix, mirror])))
  assert.equal(planned.ok, false)
  assert.equal(caseOf(planned.error), 'ConflictingPrefixSelection')
})

test('STRENGTH_006_009_candidate_wrong_target_and_promoted_replica_reflection_conflict', () => {
  const wrongTarget = P.ProjectionIntentModule_strengthCandidate(
    session('owner'), decision('d1'), run('target-a'), run('target-b'), bundle,
  )
  const plannedWrong = resultOf(P.ProjectionPlanner_plan(toList([wrongTarget])))
  assert.equal(plannedWrong.ok, false)
  assert.equal(caseOf(plannedWrong.error), 'StrengthCandidateWrongTarget')

  const reflected = P.ProjectionIntentModule_strengthPromoted(
    session('owner'), decision('d1'), run('target-a'), 0, true, bundle,
  )
  const plannedReflected = resultOf(P.ProjectionPlanner_plan(toList([reflected])))
  assert.equal(plannedReflected.ok, false)
  assert.equal(caseOf(plannedReflected.error), 'StrengthPromotedReplicaReflection')
})

test('STRENGTH_005_009_candidate_renders_concurrent_calls_then_results_with_stable_ids', () => {
  const base = toList([message('user', [textPart('base')])])
  const intent = P.ProjectionIntentModule_strengthCandidate(
    session('owner'), decision('d1'), run('target'), run('target'), bundle,
  )

  const first = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([intent]))
  const second = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([intent]))
  const messages = listItems(first.Messages)
  assert.equal(messages.length, 3)
  assert.equal(messages[0].Role, 'user')
  assert.equal(messages[1].Role, 'assistant')
  assert.equal(messages[2].Role, 'tool')

  const calls = listItems(messages[1].Parts)
  const results = listItems(messages[2].Parts)
  assert.deepEqual(calls.map(caseOf), ['WireToolCall', 'WireToolCall'])
  assert.deepEqual(results.map(caseOf), ['WireToolResult', 'WireToolResult'])
  assert.deepEqual(calls.map((part) => Id.ToolCallIdModule_value(part.fields[0])), results.map((part) => Id.ToolCallIdModule_value(part.fields[0])))
  assert.equal(Provider.renderWire(
    { ProviderId: undefined, ModelId: undefined, Variant: undefined, Tools: toList([]), System: toList([]), Messages: first.Messages },
  ), Provider.renderWire(
    { ProviderId: undefined, ModelId: undefined, Variant: undefined, Tools: toList([]), System: toList([]), Messages: second.Messages },
  ))
})

test('STRENGTH_009_replica_mirror_replaces_base_then_local_batches_append', () => {
  const mirrorMessages = toList([message('user', [textPart('mirror-base')])])
  const mirror = P.ProjectionIntentModule_useStrengthMirror(decision('d1'), run('target'), 'sem-a', mirrorMessages)
  const local = P.ProjectionIntentModule_strengthReplicaLocal(session('owner'), decision('d1'), bundle)
  const base = toList([message('user', [textPart('child-physical')])])

  const rendered = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([mirror, local]))
  const messages = listItems(rendered.Messages)
  assert.equal(messages.length, 3)
  assert.equal(messages[0].Parts.head.fields[0], 'mirror-base')
  assert.equal(messages[1].Role, 'assistant')
  assert.equal(messages[2].Role, 'tool')
})
