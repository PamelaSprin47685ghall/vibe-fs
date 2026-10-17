import test from 'node:test'
import { withReopenableJournal } from './support/journal.mjs'

test('WHAT[semantic-trace-007] a payload cannot manufacture terminal evidence without an actual ProviderRun', async () => {
  const assert = (await import('node:assert/strict')).default
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async handle => {
    const payload = await journal.JournalSurface_writePayload(handle, 'formal material without execution evidence')
    assert.equal(payload.ok, true)
    await assert.rejects(journal.JournalSurface_appendAgent(handle, { kind: 'Session', session: 'missing-run' }, null, {
      family: 'Companion', case: 'TerminalOutputCaptured',
      payload: { SessionId: 'missing-run', TextRef: payload.blobRef, TextDigest: payload.blobDigest },
    }), /Terminal evidence requires an explicit ProviderRun/)
    assert.deepEqual(trace.traceRecords(trace.snapshot(handle, 'missing-run')), [])
    assert.equal(journal.JournalSurface_hasSession(handle, 'missing-run'), false)
  })
})

for (const error of ['digest mismatch', 'missing payload']) {
  test(`WHAT[semantic-trace-007] an invalid durable terminal cannot advance the original owner (${error})`, async () => {
    const assert = (await import('node:assert/strict')).default
    const journal = await import('../../../dist/Persistence/Journal/Surface.js')
    const { spawnSync } = await import('node:child_process')
    const { fileURLToPath } = await import('node:url')
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    await withReopenableJournal(async (handle, reopen, directory) => {
      const session = 'refused-material'
      assert.equal((await trace.captureProjection(handle, session, { messages: [{ role: 'user', parts: [trace.semanticText('accepted original')] }] })).ok, true)
      const before = trace.traceRecords(trace.snapshot(handle, session))
      journal.JournalSurface_dispose(handle)
      const { NODE_TEST_CONTEXT, WANXIANGSHU_NO_FATAL_EXIT, ...env } = process.env
      const refused = spawnSync(process.execPath, [fileURLToPath(new URL('./support/invalid-material-child.mjs', import.meta.url)), directory, error], {
        encoding: 'utf8', env, timeout: 10000,
      })
      assert.ifError(refused.error)
      if (error === 'digest mismatch') {
        assert.equal(refused.signal, 'SIGKILL', refused.stderr)
        assert.match(refused.stderr, /XTrace material digest mismatch/)
        assert.match(refused.stderr, /journal-semantic-cut/)
      } else {
        assert.equal(refused.status, 0, refused.stderr)
        assert.equal(JSON.parse(refused.stdout).ok, false)
      }
      assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), session)), before)
    })
  })
}

test('WHAT[semantic-trace-007] real terminal evidence keeps its exact run and frozen original frontier after later capture', async () => {
  const assert = (await import('node:assert/strict')).default
  const { Trace } = await import('wanxiangli/trace')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'terminal-original-owner'
    const observation = (id, value) => [{ hostMessageId: id, message: { info: { id, role: 'assistant' }, parts: [{ id: `${id}-part`, type: 'reasoning', text: value }] } }]
    assert.equal((await trace.captureObservedMessages(handle, session, observation('real-run', 'reasoning'))).ok, true)
    const frontier = trace.traceHeadCut(trace.snapshot(handle, session))
    await trace.captureTerminalText(handle, session, 'formal\r\n\0statement', 'real-run')
    assert.equal((await trace.captureObservedMessages(handle, session, observation('later-run', 'later'))).ok, true)
    const current = trace.snapshot(handle, session)
    const head = trace.traceHeadCut(current)
    const owner = new Trace(head.traceId, trace.traceRecords(current))
    const terminal = owner.materialize(owner.cut(0), owner.head()).terminals[0]
    assert.deepEqual(terminal.frontier, frontier)
    assert.equal(terminal.providerRun, 'real-run')
    assert.equal(terminal.content.text, 'formal\r\n\0statement')
    assert.equal(owner.inspect().runs, 0)
    assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), session)), trace.traceRecords(current))
  })
})

test('WHAT[semantic-trace-007] real Host bytes and missing run evidence survive in the single original owner', async () => {
  const assert = (await import('node:assert/strict')).default
  const { Trace } = await import('wanxiangli/trace')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'actual-original-owner'
    const raw = '\uFEFF原话\r\n\0' + '🧭'.repeat(160000)
    const observations = [
      { hostMessageId: 'actual-user', message: { info: { role: 'user' }, parts: [{ id: 'actual-text', type: 'text', text: raw }] } },
      { hostMessageId: 'actual-assistant', message: { info: { role: 'assistant' }, parts: [{ id: 'actual-thought', type: 'reasoning', text: '确切推理' }] } },
    ]
    const receipt = await trace.captureObservedMessages(handle, session, observations)
    assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
    const projection = trace.snapshot(handle, session)
    const records = trace.traceRecords(projection)
    const head = trace.traceHeadCut(projection)
    const owner = new Trace(head.traceId, records)
    const materials = owner.materialize(owner.cut(0), owner.head()).materials
    assert.equal(materials[0].content.text, raw)
    assert.equal(materials[1].content.text, '确切推理')
    assert.equal(materials[1].providerRun, null)
    assert.equal(owner.inspect().runs, 0)
    assert.equal(records.some(row => JSON.parse(row).observation.type === 'RunFinished'), false)
    const semantic = await trace.currentProjection(handle, session)
    const cold = await reopen()
    assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
    assert.deepEqual(await trace.currentProjection(cold, session), semantic)
  })
})

test('WHAT[semantic-trace-007] durable materialization survives reopening with exact user and tool text', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async (handle, reopen) => {
    const messages = [
      { role: 'user', parts: [trace.semanticText('原话\r\n"task" <tag> /path')] },
      { role: 'assistant', parts: [trace.semanticToolCall('read', '{"path":"a"}'), trace.semanticToolResult('line 1\nline 2\n')] },
    ]
    assert.equal((await trace.captureProjection(handle, 'raw-history', { messages })).ok, true)
    const before = await trace.currentProjection(handle, 'raw-history')
    assert.deepEqual(before.messages, messages)
    const next = await reopen()
    assert.deepEqual(await trace.currentProjection(next, 'raw-history'), before)
    assert.equal((await trace.captureProjection(next, 'raw-history', { messages })).capturedPartCount, 0)
  })
})

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

test('WHAT[semantic-trace-007] projection capture is idempotent and reports owner receipts', async () => {
  await withJournal(async (handle) => {
    const value = projection([
      { role: 'user', parts: [trace.semanticText('task')] },
      { role: 'assistant', parts: [trace.semanticText('work'), trace.semanticReasoning('considered')] },
    ])
    const first = await trace.captureProjection(handle, SESSION, value)
    const second = await trace.captureProjection(handle, SESSION, value)
    assert.equal(first.ok, true, first.ok ? '' : first.error)
    assert.equal(first.capturedPartCount, 3)
    assert.equal(second.capturedPartCount, 0)
    assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, SESSION)).length, 3)
  })
})
test('WHAT[semantic-trace-007] materialization reads canonical durable semantics', async () => {
  await withJournal(async (handle) => {
    await trace.captureProjection(handle, SESSION, projection([
      { role: 'user', parts: [trace.semanticText('raw opening')] },
      { role: 'assistant', parts: [trace.semanticText('raw answer')] },
    ]))
    const first = await trace.currentProjection(handle, SESSION)
    assert.deepEqual(first.messages, [
      { role: 'user', parts: [{ kind: 'text', text: 'raw opening' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'raw answer' }] },
    ])
    assert.deepEqual(await trace.currentProjectionBetween(
      handle,
      SESSION,
      trace.createRange(trace.cursor(2), trace.cursor(3)),
    ), { messages: [{ role: 'assistant', parts: [{ kind: 'text', text: 'raw answer' }] }] })
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");


test('WHAT[semantic-trace-007] canonical flatten preserves semantic part and role order', () => {
  const flat = trace.flatten([
    { role: 'user', parts: [trace.semanticText('task'), trace.semanticToolCall('read', '{}')] },
    { role: 'assistant', parts: [trace.semanticReasoning('considered'), trace.semanticText('done')] },
  ])
  assert.deepEqual(flat.map((entry) => entry.role), ['user', 'user', 'assistant', 'assistant'])
  assert.deepEqual(flat.map((entry) => entry.part.kind), ['text', 'tool-call', 'reasoning', 'text'])
})
}

test.todo('WHAT[semantic-trace-007] actual Y, prefix, WorkRecord and Casebook consumers share the same bounded durable history and exclude request presentation; GAP-101')
