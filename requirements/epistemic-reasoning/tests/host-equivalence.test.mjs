import { test } from 'node:test'
import assert from 'node:assert/strict'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

// WHAT[EPI-018]: the two hosts must fold the same canonical accepted-event
// list through the same reducer to the same semantic state and hash.
// Host-private session IDs, transport receipts and arrival timing ride in the
// outer fold arguments and must never enter the semantic hash.

const initialEnvelope = {
  schema: { id: 'sphinx.probe.open/input@1', hash: 'input-hash-001' },
  payload: { question: 'Why do small changes ship faster?' },
}

const pluginLock = [
  {
    id: 'sphinx-legacy',
    release: '0.8.4',
    abiHash: 'abi-hash-001',
    capabilities: ['observe', 'refine'],
    schemas: [{ id: 'sphinx.legacy/observation@1', hash: 'obs-hash-001' }],
  },
]

const canonicalEvents = [
  {
    eventId: 'evt-018-001',
    kind: 'InquiryCreated',
    inquiryId: 'iq_host_equiv_001',
    revision: 0,
    parent: null,
    payload: { question: 'Why do small changes ship faster?' },
  },
  {
    eventId: 'evt-018-002',
    kind: 'PluginSetBound',
    inquiryId: 'iq_host_equiv_001',
    revision: 1,
    parent: 'evt-018-001',
    payload: { plugins: ['sphinx-legacy'] },
  },
  {
    eventId: 'evt-018-003',
    kind: 'ObservationAccepted',
    inquiryId: 'iq_host_equiv_001',
    revision: 2,
    parent: 'evt-018-002',
    workId: 'work_blind_001',
    attempt: 1,
    payload: { observation: 'first accepted observation' },
  },
]

const resourceFacts = [{ workId: 'work_blind_001', debited: 1 }]

const foldVia = (surface, host, hostSessionId, extra = {}) =>
  surface.foldHostEvents({
    host,
    hostSessionId,
    transportReceipt: { receiptId: `${host}-receipt-1`, ...(extra.transportReceipt ?? {}) },
    arrivedAtMs: extra.arrivedAtMs ?? 1000,
    initialEnvelope,
    pluginLock,
    events: extra.events ?? canonicalEvents,
    resourceFacts,
  })

test('WHAT[EPI-018] same_ordered_canonical_events_fold_to_same_semantic_hash_across_hosts', async () => {
  const mcp = await foldVia(gecSurface, 'mcp', 'mcp-session-aaa', { arrivedAtMs: 1000 })
  const opencode = await foldVia(gecSurface, 'opencode', 'oc-session-bbb', { arrivedAtMs: 9281 })

  assert.equal(mcp.error, undefined)
  assert.equal(opencode.error, undefined)
  assert.notEqual('mcp-session-aaa', 'oc-session-bbb')

  // Same reducer result on every semantic slot despite differing host IDs.
  assert.deepEqual(opencode.graph, mcp.graph)
  assert.deepEqual(opencode.certificates, mcp.certificates)
  assert.deepEqual(opencode.work, mcp.work)
  assert.deepEqual(opencode.budget, mcp.budget)
  assert.deepEqual(opencode.status, mcp.status)
  assert.deepEqual(opencode.answer, mcp.answer)
  assert.equal(opencode.semanticHash, mcp.semanticHash)
  assert.equal(opencode.eventHead, mcp.eventHead)
  assert.deepEqual(opencode.appliedOrder, mcp.appliedOrder)

  // Arrival timing alone is host-private: same host, later clock, same hash.
  const mcpLater = await foldVia(gecSurface, 'mcp', 'mcp-session-aaa', { arrivedAtMs: 777000 })
  assert.equal(mcpLater.error, undefined)
  assert.equal(mcpLater.semanticHash, mcp.semanticHash)
})

test('WHAT[EPI-018] reordered_arrivals_do_not_fold_to_the_same_semantic_hash', async () => {
  const ordered = await foldVia(gecSurface, 'mcp', 'mcp-session-aaa')
  assert.equal(ordered.error, undefined)

  // Concurrent providers racing in a different arrival order is a different
  // event sequence by definition: it must never silently alias the hash.
  const reordered = await foldVia(gecSurface, 'opencode', 'oc-session-bbb', {
    events: [canonicalEvents[0], canonicalEvents[2], canonicalEvents[1]],
  })
  assert.ok(
    reordered.error !== undefined || reordered.semanticHash !== ordered.semanticHash,
    'reordered arrivals must be rejected or diverge, never silently equal',
  )
})
