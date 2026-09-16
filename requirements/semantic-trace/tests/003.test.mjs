import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const SESSION = 'ses_semantic_capture'

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'semantic-trace-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_semantic_trace', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
}

const projection = (messages) => ({ messages })

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}

const part = (sequence, overrides = {}) => ({
  sequence,
  role: 'assistant',
  provenance: `g:0/msg:run-${sequence}/host-part:part-${sequence}`,
  turn: 0,
  partIndex: sequence - 1,
  kind: 'text',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  providerRun: `run-${sequence}`,
  ...overrides,
})

test('WHAT[SEMANTIC-TRACE-003] concurrent same-session captures serialize before allocating durable cursors', async () => {
  await withJournal(async (handle) => {
    const capture = (hostMessageId, text) => trace.captureObservedMessages(handle, SESSION, [
      {
        hostMessageId,
        message: {
          info: { id: `${hostMessageId}-run`, role: 'assistant' },
          parts: [{ id: `${hostMessageId}-part`, type: 'text', text }],
        },
      },
    ])

    const [first, second] = await Promise.all([
      capture('concurrent-a', 'first concurrent semantic part'),
      capture('concurrent-b', 'second concurrent semantic part'),
    ])

    assert.equal(first.ok, true, first.ok ? '' : first.error)
    assert.equal(second.ok, true, second.ok ? '' : second.error)
    const parts = trace.orderedSemanticParts(trace.snapshot(handle, SESSION))
    assert.equal(parts.length, 2)
    assert.deepEqual(parts.map((part) => part.provenance).sort(), [
      'g:0/msg:concurrent-a/host-part:concurrent-a-part',
      'g:0/msg:concurrent-b/host-part:concurrent-b-part',
    ])
  })
})

test('WHAT[SEMANTIC-TRACE-003] duplicate and retreating cursors are rejected', () => {
  const projection = unwrap(trace.appendPart(trace.emptyProjection(), part(5)))
  assert.equal(trace.appendPart(projection, part(5)).ok, false)
  assert.equal(trace.appendPart(projection, part(3)).ok, false)
})

test('WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque', () => {
  const origin = trace.originCursor
  const second = trace.next(origin)
  const third = trace.next(second)
  assert.deepEqual([origin.sequence, second.sequence, third.sequence], [0, 1, 2])
  assert.equal(trace.isAfter(second, origin), true)
  assert.equal(trace.isAtOrAfter(second, second), true)
  assert.equal(trace.isBefore(origin, second), true)
})
