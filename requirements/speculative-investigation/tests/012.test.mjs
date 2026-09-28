import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = text => `H(${text})`
const base = [
  { role: 'user', parts: [{ kind: 'text', text: 'inspect how this repository implements replica prefetch' }] },
  { role: 'assistant', parts: [{ kind: 'text', text: 'primary output' }] },
]
const snapshot = () => Projection.projectionSnapshot(Projection.semanticProjection(base))

test('WHAT[speculative-investigation-012] Candidate and Promoted add only the exact tool exchange, preserving legitimate user text about replicas', () => {
  const result = 'The file discusses confidence, prediction and weak model prefetch.'
  const bundle = Strength.frameTryBuild(H, 10000, [{ requestOrdinal: 1, exchanges: [{ toolName: 'read', canonicalArguments: '{"filePath":"a"}', canonicalResult: result }] }]).value
  const candidate = Strength.candidate(H, { ownerSessionId: 'private-owner', decisionId: 'private-decision', targetProviderRun: 'private-target', currentProviderRun: 'private-target', bundle }).value
  const promoted = Strength.promoted(H, { ownerSessionId: 'private-owner', decisionId: 'private-decision', targetProviderRun: 'private-target', beforeIndex: 1, isReplicaRequest: false, bundle }).value
  for (const intent of [candidate, promoted]) {
    const rendered = Projection.renderMessagesWithHostIds(snapshot(), base, [intent])
    const ordinary = rendered.messages.filter(message => message.parts.some(part => part.kind === 'text'))
    assert.deepEqual(ordinary, base)
    const added = rendered.messages.filter(message => message.parts.every(part => part.kind !== 'text'))
    assert.deepEqual(added.map(message => message.role), ['assistant', 'tool'])
    assert.equal(added[0].parts.length, 1)
    assert.equal(added[1].parts.length, 1)
    const callId = added[0].parts[0].callId
    assert.deepEqual(added[0].parts[0], { kind: 'tool-call', callId, name: 'read', args: '{"filePath":"a"}' })
    assert.deepEqual(added[1].parts[0], { kind: 'tool-result', callId, result })
  }
})

test.todo('WHAT[speculative-investigation-012] GAP-183: actual Owner and Replica visible interaction has no injected mechanism identity while Host/EventStore retain the required audit fields')
