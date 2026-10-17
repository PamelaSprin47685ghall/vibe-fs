import test from 'node:test'

test('WHAT[semantic-trace-003] old physical tool provenance replays without adding a cursor or backfilling missing evidence', async () => {
  const assert = (await import('node:assert/strict')).default
  const { withReopenableJournal } = await import('./support/journal.mjs')
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'legacy-tool-provenance'
    const payload = await journal.JournalSurface_writePayload(handle, '{"path":"a"}')
    assert.equal(payload.ok, true)
    const captured = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, 'real-run', {
      family: 'Companion', case: 'XTracePartAppended', payload: {
        SessionId: session, CursorSequence: 1n, Role: 'assistant', Turn: 0, PartIndex: 0, Kind: 'tool_call', ToolName: 'read',
        TextRef: payload.blobRef, TextDigest: payload.blobDigest, Provenance: 'g:0/msg:real-message/host-part:real-part',
        ProviderRun: 'real-run', ToolCallId: 'real-call', HostToolPartId: null,
      },
    })
    assert.equal(captured.ok, true, JSON.stringify(captured.error))
    const observation = state => [{ hostMessageId: 'real-message', message: { info: { id: 'real-run', role: 'assistant' }, parts: [{ id: 'real-part', type: 'tool', tool: 'read', callID: 'real-call', state }] } }]
    const replay = await trace.captureObservedMessages(handle, session, observation({ status: 'running', input: { path: 'a' } }))
    assert.equal(replay.ok, true, JSON.stringify(replay.error))
    assert.equal(replay.receipt.capturedPartCount, 0)
    const result = await trace.captureObservedMessages(handle, session, observation({ status: 'completed', output: 'actual result' }))
    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.equal(result.receipt.capturedPartCount, 1)
    const parts = trace.orderedSemanticParts(trace.snapshot(handle, session))
    assert.equal(parts.length, 2)
    assert.equal(parts[0].cursor.sequence, 1)
    assert.equal(parts[0].provenance, 'g:0/msg:real-message/host-part:real-part')
    assert.equal(parts[0].hostToolPartId, null)
    const records = trace.traceRecords(trace.snapshot(handle, session))
    assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), session)), records)
  })
})

test('WHAT[semantic-trace-003] conflicting tool aliases in one capture batch are refused before its first append', async () => {
  const assert = (await import('node:assert/strict')).default
  const { withReopenableJournal } = await import('./support/journal.mjs')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async handle => {
    const observation = (message, output) => ({
      hostMessageId: message,
      message: { info: { id: 'actual-run', role: 'assistant' }, parts: [{ id: 'same-tool-part', type: 'tool', tool: 'read', callID: 'same-call', state: { status: 'completed', output } }] },
    })
    const refused = await trace.captureObservedMessages(handle, 'alias-conflict', [observation('first-wrapper', 'original'), observation('second-wrapper', 'conflict')])
    assert.equal(refused.ok, false)
    assert.match(JSON.stringify(refused.error), /conflict/i)
    assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, 'alias-conflict')).length, 0)
    assert.deepEqual(trace.traceRecords(trace.snapshot(handle, 'alias-conflict')), [])
  })
})

test('WHAT[semantic-trace-003] duplicate Host parts in one batch consume one semantic cursor', async () => {
  const assert = (await import('node:assert/strict')).default
  const { withReopenableJournal } = await import('./support/journal.mjs')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async (handle, reopen) => {
    const part = { id: 'actual-part', type: 'text', text: 'once' }
    const observed = [{ hostMessageId: 'actual-message', message: { info: { role: 'user' }, parts: [part, structuredClone(part)] } }]
    const captured = await trace.captureObservedMessages(handle, 'batch-replay', observed)
    assert.equal(captured.ok, true, JSON.stringify(captured.error))
    assert.equal(captured.receipt.capturedPartCount, 1)
    const records = trace.traceRecords(trace.snapshot(handle, 'batch-replay'))
    assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), 'batch-replay')), records)
  })
})

for (const change of ['bytes', 'role']) {
  test(`WHAT[semantic-trace-003] conflicting confirmed Host material is refused without advancing its prefix (${change})`, async () => {
    const assert = (await import('node:assert/strict')).default
    const { withReopenableJournal } = await import('./support/journal.mjs')
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    await withReopenableJournal(async handle => {
      const original = { hostMessageId: 'real-message', message: { info: { id: 'real-run', role: 'assistant' }, parts: [{ id: 'real-part', type: 'text', text: 'original' }] } }
      assert.equal((await trace.captureObservedMessages(handle, 'conflict-material', [original])).ok, true)
      const before = trace.traceRecords(trace.snapshot(handle, 'conflict-material'))
      const changed = structuredClone(original)
      if (change === 'bytes') changed.message.parts[0].text = 'changed'
      else changed.message.info.role = 'user'
      const refused = await trace.captureObservedMessages(handle, 'conflict-material', [changed])
      assert.equal(refused.ok, false)
      assert.match(JSON.stringify(refused.error), /conflict/i)
      assert.deepEqual(trace.traceRecords(trace.snapshot(handle, 'conflict-material')), before)
    })
  })
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, resolve } = await import("node:path");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

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

test('WHAT[semantic-trace-003] concurrent same-session captures serialize before allocating durable cursors', async () => {
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
    assert.deepEqual(parts.map((part) => part.cursor.sequence), [1, 2])
    assert.deepEqual(parts.map((part) => part.provenance).sort(), [
      'g:0/msg:concurrent-a/host-part:concurrent-a-part',
      'g:0/msg:concurrent-b/host-part:concurrent-b-part',
    ])
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

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
  body: `raw-${sequence}`,
  providerRun: `run-${sequence}`,
  ...overrides,
})

test('WHAT[semantic-trace-003] duplicate and retreating cursors are rejected', () => {
  const projection = unwrap(trace.appendPart(trace.emptyProjection(), part(5)))
  assert.equal(trace.appendPart(projection, part(5)).ok, false)
  assert.equal(trace.appendPart(projection, part(3)).ok, false)
})

test('WHAT[semantic-trace-003] new cursors cannot replay one physical material or corrupt the accepted owner prefix', async () => {
  const { Trace } = await import('wanxiangli/trace')
  const firstPart = part(1)
  const first = unwrap(trace.appendPart(trace.emptyProjection(), firstPart))
  const records = trace.traceRecords(first)
  assert.equal(trace.appendPart(first, { ...firstPart, sequence: 2 }).ok, false)
  assert.equal(trace.appendPart(first, part(2, { kind: 'invalid-material' })).ok, false)
  assert.deepEqual(trace.traceRecords(first), records)
  const head = trace.traceHeadCut(first)
  assert.deepEqual(new Trace(head.traceId, records).head(), head)
  const next = unwrap(trace.appendPart(first, part(2)))
  const branch = unwrap(trace.appendPart(first, part(2, { body: 'other branch' })))
  assert.deepEqual(trace.traceRecords(first), records)
  assert.notDeepEqual(trace.traceRecords(next), trace.traceRecords(branch))
  for (const projection of [first, next, branch]) {
    const cut = trace.traceHeadCut(projection)
    assert.deepEqual(new Trace(cut.traceId, trace.traceRecords(projection)).head(), cut)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");


test('WHAT[semantic-trace-003] owner cursor operations are monotonic', () => {
  const origin = trace.originCursor
  const second = trace.next(origin)
  const third = trace.next(second)
  assert.deepEqual([origin.sequence, second.sequence, third.sequence], [0, 1, 2])
  assert.equal(trace.isAfter(second, origin), true)
  assert.equal(trace.isAtOrAfter(second, second), true)
  assert.equal(trace.isBefore(origin, second), true)
})
}
