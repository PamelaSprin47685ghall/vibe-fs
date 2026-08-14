// Split from tests/unit/strength/projection-algebra.test.mjs (cutover Wave 2a); owner: provider-projection
//
// PROVIDER-PROJECTION algebra oracle over the Strength intents: the projection
// module exposes the intent constructors, the planner's conflict rule rejects a
// Strength mirror alongside a normal-work base selection, and multiple promoted
// absolute anchors render registration-order independent (permutation) with
// deterministic wire output. Strength product-semantics assertions
// (candidate/promotion behaviour) went to speculative-investigation.

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

test('STRENGTH_008_009_multiple_promoted_absolute_anchors_are_registration_order_independent', () => {
  const base = toList([
    message('user', [textPart('u1')]),
    message('assistant', [textPart('target-1')]),
    message('user', [textPart('u2')]),
    message('assistant', [textPart('target-2')]),
  ])
  const first = P.ProjectionIntentModule_strengthPromoted(
    session('owner'), decision('d1'), run('target-1'), 1, false, bundle,
  )
  const second = P.ProjectionIntentModule_strengthPromoted(
    session('owner'), decision('d2'), run('target-2'), 3, false, bundle,
  )

  const forward = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([first, second]))
  const reverse = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([second, first]))
  const forwardMessages = listItems(forward.Messages)
  const forwardIds = listItems(forward.HostMessageIds)

  assert.deepEqual(forwardMessages.map((item) => item.Role), [
    'user', 'assistant', 'tool', 'assistant', 'user', 'assistant', 'tool', 'assistant',
  ])
  assert.equal(
    forwardIds[1],
    Frame.StrengthFrame_hostMessageId(H, session('owner'), decision('d1'), 1, 'call', bundle.Digest),
  )
  assert.equal(
    forwardIds[5],
    Frame.StrengthFrame_hostMessageId(H, session('owner'), decision('d2'), 1, 'call', bundle.Digest),
  )
  assert.deepEqual(listItems(reverse.HostMessageIds), forwardIds)
  assert.equal(Provider.renderWire(
    { ProviderId: undefined, ModelId: undefined, Variant: undefined, Tools: toList([]), System: toList([]), Messages: forward.Messages },
  ), Provider.renderWire(
    { ProviderId: undefined, ModelId: undefined, Variant: undefined, Tools: toList([]), System: toList([]), Messages: reverse.Messages },
  ))
})
