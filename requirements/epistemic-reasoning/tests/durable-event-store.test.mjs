import test from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

// WHAT[EPI-019]: the canonical EventStore is the sole durable truth, and it
// lives in commonDir on disk — not in a process-local map. Sphinx events are
// translated to envelopes by the pure `encodeSphinxEnvelope` codec, judged by
// the pure `checkAppend` gate, and folded by the pure `sphinxCurrent` fold:
// Current advances only after a durable append; a NEW handle opened from the
// same commonDir (no events passed) recovers the same durable log, which is
// what a process restart observes. Dropping the test-side Current only clears
// memory: re-folding from a fresh store read yields the same result. Stale
// expectedRevision conflicts before any write; workId+attempt redelivery of
// the identical observation is idempotent while a conflicting payload fails.

const withRepo = async (t, fn) => {
  const root = mkdtempSync(join(tmpdir(), 'sphinx-spine-'))
  execFileSync('git', ['init', '-q', root])
  t.after(() => rmSync(root, { recursive: true, force: true }))
  await fn(join(root, '.git'))
}

const jsEvent = (inquiryId, revision, kind, payload, parents = []) => ({
  inquiryId,
  revision,
  kind,
  parents,
  payload,
})

const encode = (event) => {
  const result = gecSurface.encodeSphinxEnvelope(event)
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
  const { envelope } = result
  assert.match(envelope.id, /^[0-9a-f]{40}$/)
  assert.equal(envelope.stream, `sphinx/${event.inquiryId}`)
  assert.equal(envelope.type, `sphinx/${event.kind}`)
  assert.deepEqual(envelope.payloadRefs, [])
  return envelope
}

const appendDurable = async (handle, envelope) => {
  const result = await eventStore.append(handle, [envelope])
  assert.equal(result.ok, true, JSON.stringify(result.error ?? null))
}

const readSpine = (handle, stream) => {
  const head = eventStore.head(handle, stream)
  if (head == null) return []
  const ordered = []
  let cursor = head
  while (cursor != null) {
    const envelope = eventStore.read(handle, cursor)
    assert.ok(envelope != null, `durable spine is missing ${cursor}`)
    ordered.unshift(envelope)
    cursor = envelope.parents[0] ?? null
  }
  return ordered
}

const fold = (envelopes) => {
  const state = gecSurface.sphinxCurrent({ envelopes })
  assert.equal(state.ok, true, JSON.stringify(state.error ?? null))
  return state
}

test('WHAT[EPI-019] append_before_current_only_advances_after_durable_append_and_rejects_stale_expected_revision', async (t) => {
  await withRepo(t, async (commonDir) => {
    const inquiryId = `iq_${randomUUID()}`
    const stream = `sphinx/${inquiryId}`
    const handle = eventStore.create(commonDir, 'writer-spine-append')
    try {
      const genesis = fold([])
      assert.equal(genesis.current.revision, 0)
      assert.deepEqual(genesis.current.seen, {})
      assert.ok(genesis.eventHead == null)
      let current = genesis.current

      const unknown = gecSurface.encodeSphinxEnvelope(
        jsEvent(inquiryId, 0, 'BogusKind', { plugins: ['sphinx-legacy'] }),
      )
      assert.equal(unknown.ok, false)
      assert.match(unknown.error.code, /unknown-kind/)

      const envelope = encode(jsEvent(inquiryId, 0, 'plugin-set-bound', { plugins: ['sphinx-legacy'] }))
      const gate = gecSurface.checkAppend({ current, envelope, expectedRevision: 0 })
      assert.equal(gate.ok, true)
      assert.equal(gate.duplicate, false)
      assert.equal(gate.revision, 1)
      // The gate alone advances nothing: Current moves only after a durable append.
      assert.equal(current.revision, 0)

      await appendDurable(handle, envelope)
      const advanced = fold(readSpine(handle, stream))
      assert.equal(advanced.current.revision, 1)
      assert.equal(advanced.eventHead, envelope.id)
      assert.deepEqual(eventStore.heads(handle, stream), [envelope.id])
      current = advanced.current

      // Stale expectedRevision must conflict before any write: no silent advance.
      const stale = gecSurface.checkAppend({ current, envelope, expectedRevision: 0 })
      assert.equal(stale.ok, false)
      assert.match(stale.error.code, /REVISION_CONFLICT/)

      const afterConflict = fold(readSpine(handle, stream))
      assert.equal(afterConflict.current.revision, 1)
      assert.equal(afterConflict.eventHead, envelope.id)
      assert.equal(afterConflict.semanticHash, advanced.semanticHash)
    } finally {
      eventStore.dispose(handle)
    }
  })
})

test('WHAT[EPI-019] restart_recovery_and_cache_loss_replay_the_same_canonical_hash', async (t) => {
  await withRepo(t, async (commonDir) => {
    const inquiryId = `iq_${randomUUID()}`
    const stream = `sphinx/${inquiryId}`
    const first = encode(jsEvent(inquiryId, 0, 'plugin-set-bound', { plugins: ['sphinx-legacy'] }))
    const second = encode(
      jsEvent(
        inquiryId,
        1,
        'observation-accepted',
        { workId: 'work_alpha', attempt: 1, observation: 'first' },
        [first.id],
      ),
    )
    const handle = eventStore.create(commonDir, 'writer-spine-restart')
    let before
    let ids
    try {
      let current = fold([]).current
      for (const [index, envelope] of [first, second].entries()) {
        const gate = gecSurface.checkAppend({ current, envelope, expectedRevision: index })
        assert.equal(gate.ok, true, JSON.stringify(gate.error ?? null))
        await appendDurable(handle, envelope)
        current = fold(readSpine(handle, stream)).current
      }
      const log = readSpine(handle, stream)
      assert.equal(log.length, 2)
      assert.ok(log.every((entry) => /^[0-9a-f]{40}$/.test(entry.id)))
      ids = log.map((entry) => entry.id)
      before = fold(log)
      assert.equal(before.current.revision, 2)
    } finally {
      eventStore.dispose(handle)
    }

    // Process restart: a NEW handle from the same commonDir without passing
    // any events. The durable log and folded Current must come back identical.
    const reopened = eventStore.create(commonDir, 'writer-spine-restarted')
    try {
      const durableLog = readSpine(reopened, stream)
      assert.deepEqual(
        durableLog.map((entry) => entry.id),
        ids,
      )
      const recovered = fold(durableLog)
      assert.equal(recovered.current.revision, 2)
      assert.equal(recovered.eventHead, before.eventHead)
      assert.equal(recovered.semanticHash, before.semanticHash)

      // Dropping the hot Current only clears memory: the next fold reloads
      // from a fresh canonical store read with the same result.
      const dropped = fold(readSpine(reopened, stream))
      assert.deepEqual(dropped.current, recovered.current)
      assert.equal(dropped.eventHead, recovered.eventHead)
      assert.equal(dropped.semanticHash, recovered.semanticHash)
    } finally {
      eventStore.dispose(reopened)
    }
  })
})

test('WHAT[EPI-019] same_work_attempt_replay_is_idempotent_but_conflicting_payload_is_rejected', async (t) => {
  await withRepo(t, async (commonDir) => {
    const inquiryId = `iq_${randomUUID()}`
    const stream = `sphinx/${inquiryId}`
    const observation = { workId: 'work_alpha', attempt: 1, observation: 'first' }
    const handle = eventStore.create(commonDir, 'writer-spine-idempotent')
    try {
      let current = fold([]).current
      const envelope = encode(jsEvent(inquiryId, 0, 'observation-accepted', observation))
      const gate = gecSurface.checkAppend({ current, envelope, expectedRevision: 0 })
      assert.equal(gate.ok, true)
      await appendDurable(handle, envelope)
      current = fold(readSpine(handle, stream)).current
      const head = eventStore.head(handle, stream)

      // Exact redelivery under the same work attempt is the same canonical
      // fact: the gate reports a duplicate and nothing is appended twice.
      const redelivered = encode(jsEvent(inquiryId, 0, 'observation-accepted', { ...observation }))
      assert.equal(redelivered.id, envelope.id)
      const replay = gecSurface.checkAppend({ current, envelope: redelivered, expectedRevision: 1 })
      assert.equal(replay.ok, true)
      assert.equal(replay.duplicate, true)
      assert.equal(replay.revision, 1)
      assert.equal(readSpine(handle, stream).length, 1)
      assert.equal(eventStore.head(handle, stream), head)

      // Same workId+attempt with a conflicting payload must be rejected, not
      // merged and not appended.
      const conflicting = encode(
        jsEvent(inquiryId, 0, 'observation-accepted', {
          workId: 'work_alpha',
          attempt: 1,
          observation: 'contradictory rewrite',
        }),
      )
      const conflict = gecSurface.checkAppend({ current, envelope: conflicting, expectedRevision: 1 })
      assert.equal(conflict.ok, false)
      assert.match(conflict.error.code, /DUPLICATE_CONFLICT/)
      assert.equal(readSpine(handle, stream).length, 1)
      assert.equal(eventStore.head(handle, stream), head)
    } finally {
      eventStore.dispose(handle)
    }
  })
})
