import { test } from 'node:test'
import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { loadGecSurface } from './gec-support.mjs'

// WHAT[EPI-019]: the canonical EventStore is the sole durable truth, and it
// lives in commonDir on disk — not in a process-local map. Current advances
// only after a durable append; a NEW adapter opened from the same commonDir
// (no events passed) recovers the same durable log and Integrator Current,
// which is what a process restart observes. Dropping the hot projection
// cache only clears memory: the next read reloads from the canonical store.
// Stale expectedRevision conflicts before any write; workId+attempt replays
// the identical observation idempotently while a conflicting payload fails.

const initialEnvelope = {
  schema: { id: 'sphinx.probe.open/input@1', hash: 'input-hash-001' },
  payload: { question: 'What evidence backs this claim?' },
}

const pluginLock = [{ id: 'sphinx-legacy', release: '0.8.4', abiHash: 'abi-hash-001' }]

async function tempCommonDir(t) {
  const dir = await mkdtemp(join(tmpdir(), 'sphinx-gec-'))
  t.after(() => rm(dir, { recursive: true, force: true }))
  return dir
}

const genIds = () => ({ inquiryId: `iq_${randomUUID()}`, store: null })

const event = (inquiryId, seq, rest) => ({
  eventId: `ev_${inquiryId.slice(3, 11)}_${seq}`,
  inquiryId,
  parent: seq === 0 ? null : `ev_${inquiryId.slice(3, 11)}_${seq - 1}`,
  ...rest,
})

test('WHAT[EPI-019] append_before_current_only_advances_after_durable_append_and_rejects_stale_expected_revision', async (t) => {
  const gecSurface = await loadGecSurface()
  const commonDir = await tempCommonDir(t)
  const { inquiryId } = genIds()
  const { storeId } = await gecSurface.createEventStore({ commonDir, inquiryId, initialEnvelope, pluginLock })

  const genesis = await gecSurface.currentState({ storeId })
  assert.equal(genesis.revision, 0)

  const appended = await gecSurface.appendEvent({
    storeId,
    expectedRevision: 0,
    event: event(inquiryId, 0, { kind: 'PluginSetBound', payload: { plugins: ['sphinx-legacy'] } }),
  })
  assert.equal(appended.error, undefined)
  assert.equal(appended.revision, 1)

  const current = await gecSurface.currentState({ storeId })
  assert.equal(current.revision, 1)
  assert.equal(current.eventHead, appended.eventHead)
  assert.equal(current.semanticHash, appended.semanticHash)

  // Stale expectedRevision must conflict before any write: no silent advance.
  const stale = await gecSurface.appendEvent({
    storeId,
    expectedRevision: 0,
    event: event(inquiryId, 0, { kind: 'PluginSetBound', payload: { plugins: ['sphinx-legacy'] } }),
  })
  assert.ok(stale.error, 'stale expectedRevision must return a typed conflict')
  assert.match(stale.error.code, /REVISION_CONFLICT/)

  const afterConflict = await gecSurface.currentState({ storeId })
  assert.equal(afterConflict.revision, 1)
  assert.equal(afterConflict.eventHead, appended.eventHead)
})

test('WHAT[EPI-019] restart_recovery_and_cache_loss_replay_the_same_canonical_hash', async (t) => {
  const gecSurface = await loadGecSurface()
  const commonDir = await tempCommonDir(t)
  const { inquiryId } = genIds()
  const { storeId } = await gecSurface.createEventStore({ commonDir, inquiryId, initialEnvelope, pluginLock })

  await gecSurface.appendEvent({
    storeId,
    expectedRevision: 0,
    event: event(inquiryId, 0, { kind: 'PluginSetBound', payload: { plugins: ['sphinx-legacy'] } }),
  })
  const second = await gecSurface.appendEvent({
    storeId,
    expectedRevision: 1,
    event: event(inquiryId, 1, {
      kind: 'ObservationAccepted',
      workId: 'work_alpha',
      attempt: 1,
      payload: { observation: 'first' },
    }),
  })
  assert.equal(second.error, undefined)

  const { events } = await gecSurface.readLog({ storeId })
  assert.equal(events.length, 2)
  assert.ok(events.every((entry) => entry.eventId.startsWith('ev_')))

  // Process restart: close the old adapter, then open a NEW one from the same
  // commonDir without passing any events. The durable log and Integrator
  // Current must come back identical from disk.
  const closed = await gecSurface.closeEventStore({ storeId })
  assert.equal(closed.closed, true)

  const reopened = await gecSurface.openEventStore({ commonDir, inquiryId })
  assert.notEqual(reopened.storeId, storeId)
  assert.equal(reopened.revision, 2)
  assert.equal(reopened.eventHead, second.eventHead)
  assert.equal(reopened.semanticHash, second.semanticHash)

  const { events: durableLog } = await gecSurface.readLog({ storeId: reopened.storeId })
  assert.deepEqual(
    durableLog.map((entry) => entry.eventId),
    events.map((entry) => entry.eventId),
  )
  const reopenedCurrent = await gecSurface.currentState({ storeId: reopened.storeId })
  assert.equal(reopenedCurrent.revision, 2)
  assert.equal(reopenedCurrent.eventHead, second.eventHead)
  assert.equal(reopenedCurrent.semanticHash, second.semanticHash)

  // Dropping the hot projection cache only clears memory: the next read
  // reloads from the canonical store with the same result.
  const dropped = await gecSurface.dropProjectionCache({ storeId: reopened.storeId })
  assert.equal(dropped.dropped, true)
  const afterDrop = await gecSurface.currentState({ storeId: reopened.storeId })
  assert.equal(afterDrop.semanticHash, second.semanticHash)
  assert.equal(afterDrop.eventHead, second.eventHead)
})

test('WHAT[EPI-019] same_work_attempt_replay_is_idempotent_but_conflicting_payload_is_rejected', async (t) => {
  const gecSurface = await loadGecSurface()
  const commonDir = await tempCommonDir(t)
  const { inquiryId } = genIds()
  const { storeId } = await gecSurface.createEventStore({ commonDir, inquiryId, initialEnvelope, pluginLock })

  const observation = {
    kind: 'ObservationAccepted',
    workId: 'work_alpha',
    attempt: 1,
    payload: { observation: 'first' },
  }
  const first = await gecSurface.appendEvent({
    storeId,
    expectedRevision: 0,
    event: event(inquiryId, 0, observation),
  })
  assert.equal(first.error, undefined)

  // Exact replay under a fresh envelope id is the same canonical fact: no
  // second append, same revision and head.
  const replay = await gecSurface.appendEvent({
    storeId,
    expectedRevision: 1,
    event: { ...event(inquiryId, 0, observation), eventId: `ev_${inquiryId.slice(3, 11)}_redelivered` },
  })
  assert.equal(replay.error, undefined)
  assert.equal(replay.revision, first.revision)
  assert.equal(replay.eventHead, first.eventHead)
  const { events } = await gecSurface.readLog({ storeId })
  assert.equal(events.length, 1)

  // Same workId+attempt with a conflicting payload must be rejected, not
  // merged and not appended.
  const conflict = await gecSurface.appendEvent({
    storeId,
    expectedRevision: 1,
    event: {
      ...event(inquiryId, 0, observation),
      eventId: `ev_${inquiryId.slice(3, 11)}_conflicting`,
      payload: { observation: 'contradictory rewrite' },
    },
  })
  assert.ok(conflict.error, 'conflicting payload on the same work attempt must fail')
  assert.match(conflict.error.code, /DUPLICATE_CONFLICT/)
  const { events: after } = await gecSurface.readLog({ storeId })
  assert.equal(after.length, 1)
})
