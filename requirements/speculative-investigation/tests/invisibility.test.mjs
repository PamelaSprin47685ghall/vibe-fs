import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = (text) => `H(${text})`
const bundle = Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: 'alpha' }] }]).value
const base = [
  { role: 'user', parts: [{ kind: 'text', text: 'inspect the file' }] },
  { role: 'assistant', parts: [{ kind: 'text', text: 'primary output' }] },
]
const snapshot = () => Projection.projectionSnapshot(Projection.semanticProjection(base), { committedPrefix: null, blogFrames: [], transportMessages: [], hostReanchor: null })

test('WHAT[SPEC-INV-012] STRENGTH_012_candidate_and_promoted_semantic_bytes_have_no_mechanism_provenance', () => {
  const candidate = Projection.strengthCandidate({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-1', currentProviderRun: 'target-1', bundle })
  const promoted = Projection.strengthPromoted({ ownerSessionId: 'owner', decisionId: 'd1', targetProviderRun: 'target-1', beforeIndex: 1, isReplicaRequest: false, bundle })
  for (const intent of [candidate, promoted]) {
    const rendered = Projection.renderMessagesWithHostIds(H, snapshot(), base, [intent])
    const visible = `${Projection.renderWire(rendered.messages)}\n${Projection.renderSemantic(Projection.semanticProjection(rendered.messages))}`
    assert.doesNotMatch(visible, /prefetch|weak model|confidence|prediction|source=sidecar/i)
    assert.doesNotMatch(visible, /\breplica\b/i)
    assert.doesNotMatch(visible, /strength-replica|strengthreplica/i)
  }
})
