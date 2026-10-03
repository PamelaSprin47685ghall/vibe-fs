import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'
import * as persistence from '../../../dist/Sphinx/V2/Persistence/Surface.js'

export { store, persistence }
export const digest = text => createHash('sha256').update(text).digest('hex')
export const mustOk = result => {
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.value
}
export const body = (kind, payload) => ({ case: kind, payload })
export const schema = { id: 'response@2', hash: digest('{"type":"object"}') }
export const envelope = text => ({ schema, canonicalPayload: text })
export const goal = text => ({
  goalId: 'goal-1', revision: '0', originalText: text, constraints: [],
  materialRefs: ['artifact-not-a-payload-hash'], authorizationRef: 'user-authorized',
  createdBy: 'user', amendments: [],
})
export const createdBody = text => body('InquiryCreated', {
  goal: goal(text), resourceSpecs: [{ name: 'calls', kind: { case: 'Consumed', unitName: 'call' }, authorizedLimit: 10 }],
  profileRef: 'proof-profile', configHash: digest('{"profile":"proof"}'),
  renderReserve: [{ key: 'calls', value: 1 }],
})
export const work = id => ({
  id, attempt: 1, fence: id + ':1:logical', roundId: 'round-1', planId: 'plan-1',
  producer: 'proof-producer', capability: 'render', input: envelope('{}'), outputSchema: schema,
  dependencies: [], conflictKeys: [], physicalRef: null, reserved: [{ key: 'calls', value: 1 }],
})
export const transition = (id, fromState, nextState) => body('WorkAttemptTransitioned', {
  workId: id, attempt: 1, fence: id + ':1:logical', fromState, nextState,
  physicalRef: nextState.physicalRef ?? null,
})
export const accepted = (id, observationId) => body('ResultAccepted', {
  workId: id, attempt: 1, fence: id + ':1:logical', observationId,
  canonicalResult: '{"answer":"kept"}', resultSchema: schema, clusterId: 'cluster-1',
})
export const graphNode = (id, kind = 'Material') => ({
  id, role: 'epistemic', kind, payload: envelope('{}'), revision: '0', contentHash: digest('{}'),
})
export const seedBodies = text => [
  createdBody(text),
  body('RoundOpened', { roundId: 'round-1', scopeId: 'scope-1', expectedWork: ['work-1', 'work-2'] }),
  body('GraphPatched', { pluginRef: 'proof-plugin', patch: { upsertNodes: [graphNode('node-1')], removeNodes: [], upsertEdges: [], removeEdges: [] } }),
  body('WorkPlanned', { work: [work('work-1'), work('work-2')] }),
  transition('work-1', 'Planned', { case: 'Running', fence: 'work-1:1:logical', physicalRef: 'adapter-receipt-1' }),
  accepted('work-1', 'observation-1'),
  transition('work-2', 'Planned', { case: 'Running', fence: 'work-2:1:logical', physicalRef: 'adapter-receipt-2' }),
]
export const batch = (inquiry, commandId, events, parent = null) => ({
  schemaVersion: '2', inquiry,
  previousRevision: parent?.payload.revision ?? '0', previousHead: parent?.id ?? null,
  revision: parent ? String(BigInt(parent.payload.revision) + 1n) : '0',
  commandId, commandFingerprint: digest('command:' + commandId), postStateFingerprint: null, events,
})
export const prepare = (handle, raw) => mustOk(persistence.prepareTransition(handle, digest, raw))
export const append = async (handle, raw) => {
  const encoded = prepare(handle, raw)
  const receipt = await store.append(handle, [encoded])
  assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
  assert.deepEqual(receipt.cuts, [])
  return encoded
}
export const current = (handle, inquiry) => persistence.canonicalCurrent(handle, digest, inquiry)
export const withStore = async action => {
  const root = mkdtempSync(join(tmpdir(), 'sphinx-v2-roundtrip-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir)
  const handles = new Set()
  let serial = 0
  const open = () => {
    const handle = store.create(commonDir, 'roundtrip-writer-' + (++serial))
    handles.add(handle)
    return handle
  }
  const close = handle => {
    store.dispose(handle)
    handles.delete(handle)
  }
  try { return await action({ open, close }) }
  finally {
    for (const handle of handles) store.dispose(handle)
    rmSync(root, { recursive: true, force: true })
  }
}
