// Split from tests/unit/strength/projection-algebra.test.mjs (cutover Wave 2a); owner: speculative-investigation
//
// Strength product semantics over the projection algebra: candidate must match
// its target (wrong target fails closed), promoted replica reflection is
// rejected, the candidate renders concurrent calls then results with stable
// ids, promoted frames splice at BeforeMessageIndex without dropping later
// pair-anchor messages, and the replica mirror replaces the base while local
// batches append. Pure algebra oracles (constructor surface, mirror conflict
// rule, registration-order permutation) went to provider-projection.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as Intent from '../../../dist/Domain/ProjectionIntent.js'
import * as Planner from '../../../dist/Domain/ProjectionPlanner.js'
import * as Renderer from '../../../dist/Domain/ProjectionRenderer.js'
// Fable emits a bare `plan` for the single-module Planner file; keep the
// historical prefixed name so call sites stay stable.
const P = { ...Intent, ...Planner, ...Renderer, ProjectionPlanner_plan: Planner.plan }
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { toList, listItems } from '../../verification-system/tests/support/domain.mjs'

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

test('STRENGTH_009_012_policy_promoted_frames_leave_later_pair_anchor_messages_in_place', () => {
  // Not the live Host PairProgrammingThought canary. Unit-level: Strength
  // splice is BeforeMessageIndex and does not drop messages after the target.
  const base = toList([
    message('user', [textPart('u1')]),
    message('assistant', [textPart('target-assistant')]),
    message('user', [textPart('pair-anchor-stand-in')]),
  ])
  const promoted = P.ProjectionIntentModule_strengthPromoted(
    session('owner'), decision('d1'), run('target-1'), 1, false, bundle,
  )
  const rendered = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([promoted]))
  const roles = listItems(rendered.Messages).map((item) => item.Role)
  const last = listItems(rendered.Messages).at(-1)
  assert.deepEqual(roles, ['user', 'assistant', 'tool', 'assistant', 'user'])
  assert.equal(last.Parts.head.fields[0], 'pair-anchor-stand-in')
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
