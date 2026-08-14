import assert from 'node:assert/strict'
import test from 'node:test'

import * as Intent from '../../../dist/Domain/ProjectionIntent.js'
import * as Renderer from '../../../dist/Domain/ProjectionRenderer.js'
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import * as Frame from '../../../dist/Domain/StrengthFrame.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { toList } from '../../verification-system/tests/support/domain.mjs'

const P = { ...Intent, ...Renderer }
const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
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
const bundle = resultOf(Frame.StrengthFrame_tryBuild(
  H,
  10000,
  toList([{ RequestOrdinal: 1, Exchanges: toList([
    { ToolName: 'read', CanonicalArguments: '{"filePath":"a"}', CanonicalResult: 'alpha' },
  ]) }]),
)).value

test('STRENGTH_012_candidate_and_promoted_semantic_bytes_have_no_mechanism_provenance', () => {
  const base = toList([
    message('user', [textPart('inspect the file')]),
    message('assistant', [textPart('primary output')]),
  ])
  const candidate = P.ProjectionIntentModule_strengthCandidate(
    session('owner'), decision('d1'), run('target-1'), run('target-1'), bundle,
  )
  const promoted = P.ProjectionIntentModule_strengthPromoted(
    session('owner'), decision('d1'), run('target-1'), 1, false, bundle,
  )

  for (const intent of [candidate, promoted]) {
    const rendered = P.ProjectionRenderer_renderMessagesWithHostIds(H, snapshot, base, toList([intent]))
    const wire = {
      ProviderId: undefined,
      ModelId: undefined,
      Variant: undefined,
      Tools: toList([]),
      System: toList([]),
      Messages: rendered.Messages,
    }
    const visible = `${Provider.renderWire(wire)}\n${Provider.renderSemantic(Provider.toSemantic(wire))}`
    assert.doesNotMatch(visible, /prefetch|weak model|confidence|prediction|source=sidecar/i)
    assert.doesNotMatch(visible, /\breplica\b/i)
    assert.doesNotMatch(visible, /strength-replica|strengthreplica/i)
  }
})
