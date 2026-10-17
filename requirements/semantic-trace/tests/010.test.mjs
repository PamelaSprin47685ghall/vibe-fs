import test from 'node:test'
import { withReopenableJournal } from './support/journal.mjs'

test('WHAT[semantic-trace-010] legacy journal order restores parts and terminal before a complete late Opening', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const { Trace } = await import('wanxiangli/trace')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'late-opening-recovery'
    const messages = [{ role: 'assistant', parts: [trace.semanticText('prior material')] }]
    assert.equal((await trace.captureProjection(handle, session, { messages })).ok, true)
    assert.equal((await trace.captureTerminalText(handle, session, 'prior completion', 'run-before-opening')).ok, true)
    const before = trace.snapshot(handle, session)
    const oldRecords = trace.traceRecords(before)
    const oldHead = trace.traceHeadCut(before)
    const original = new Trace(oldHead.traceId, oldRecords)
    const frozen = original.materialize(original.cut(0), oldHead)

    const continued = await reopen()
    assert.deepEqual(trace.traceRecords(trace.snapshot(continued, session)), oldRecords)
    const assignment = '\ufeff\u0000\r\n' + '界'.repeat(210000) + 'exact tail'
    const requirements = ['keep exact material']
    const receipt = await trace.captureOpening(continued, session, assignment, requirements)
    assert.equal(receipt.ok, true, receipt.error)
    const expectedRecords = trace.traceRecords(trace.snapshot(continued, session))

    const recovered = await reopen()
    const snapshot = trace.snapshot(recovered, session)
    assert.deepEqual(trace.traceRecords(snapshot), expectedRecords)
    assert.equal(trace.openingEvidence(snapshot).assignmentText, assignment)
    assert.deepEqual(await trace.currentProjection(recovered, session), { messages })
    const restored = new Trace(oldHead.traceId, expectedRecords)
    assert.deepEqual(restored.materialize(restored.cut(0), oldHead), frozen)
    const payload = `assignment:${assignment.length}:${assignment}\n${requirements.join('\n')}`
    assert.deepEqual(restored.materialize(restored.cut(0), restored.head()).opening.bytes, new TextEncoder().encode(payload))
    assert.equal((await trace.captureOpening(recovered, session, assignment, requirements)).openingCaptured, false)
    assert.deepEqual(trace.traceRecords(trace.snapshot(recovered, session)), expectedRecords)
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

test('WHAT[semantic-trace-010] opening capture reports idempotent evidence', async () => {
  await withJournal(async (handle) => {
    const first = await trace.captureOpening(handle, SESSION, 'Review the tree.', ['Ship it.', 'Add tests.'])
    const second = await trace.captureOpening(handle, SESSION, 'Review the tree.', ['Ship it.', 'Add tests.'])
    assert.equal(first.openingCaptured, true)
    assert.equal(second.openingCaptured, false)
    assert.deepEqual(trace.openingEvidence(trace.snapshot(handle, SESSION)).authoritativeRequirements, ['Ship it.', 'Add tests.'])
  })
})
test('WHAT[semantic-trace-010] durable capture refuses conflicting text and requirements without changing opening', async () => {
  await withJournal(async (handle) => {
    assert.equal((await trace.captureOpening(handle, SESSION, 'Original task', ['r1'])).ok, true)
    for (const [text, requirements] of [['Other task', ['r1']], ['Original task', ['r2']]]) {
      const result = await trace.captureOpening(handle, SESSION, text, requirements)
      assert.equal(result.ok, false, 'a conflicting capture must return a refusal, not an idempotent success')
      assert.match(result.error, /opening-already-captured/)
      assert.deepEqual(trace.openingEvidence(trace.snapshot(handle, SESSION)), {
        assignmentText: 'Original task', authoritativeRequirements: ['r1'], constitutiveBody: '',
      })
    }
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

test('WHAT[semantic-trace-010] conflicting opening is rejected', () => {
  const projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'first task', []))
  const rejected = trace.appendOpening(projection, 'second task', [])
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error, 'opening-already-captured')
})

test('WHAT[semantic-trace-010] large Opening preserves the complete bytes and distinguishes equal-length tails', async () => {
  const { Trace } = await import('wanxiangli/trace')
  const assignment = '\ufeff\u0000\r\n' + '界'.repeat(210000)
  const requirements = ['first\r\nrequirement', 'second\u0000requirement']
  const first = unwrap(trace.appendOpening(trace.emptyProjection(), assignment + 'A', requirements))
  const second = unwrap(trace.appendOpening(trace.emptyProjection(), assignment + 'B', requirements))
  const head = trace.traceHeadCut(first)
  const restored = new Trace(head.traceId, trace.traceRecords(first))
  const payload = `assignment:${assignment.length + 1}:${assignment}A\n${requirements.join('\n')}`
  assert.deepEqual(restored.materialize(restored.cut(0), head).opening.bytes, new TextEncoder().encode(payload))
  assert.notDeepEqual(trace.traceHeadCut(second), head)
  assert.equal(trace.traceRecords(first).every(record => Buffer.byteLength(record) <= 1048576), true)
  assert.equal(JSON.parse(trace.traceRecords(first).at(-1)).observation.type, 'OpeningBound')
  assert.deepEqual(trace.traceRecords(unwrap(trace.appendOpening(first, assignment + 'A', requirements))), trace.traceRecords(first))
})

test('WHAT[semantic-trace-010] late Opening appends without changing old cuts or old semantic ranges', async () => {
  const { Trace } = await import('wanxiangli/trace')
  let before = unwrap(trace.appendPart(trace.emptyProjection(), part(1)))
  before = unwrap(trace.appendTerminal(before, { textRef: 'terminal', textDigest: 'terminal-digest', providerRun: 'run-1', body: 'terminal' }))
  const oldHead = trace.traceHeadCut(before)
  const oldRecords = trace.traceRecords(before)
  const original = new Trace(oldHead.traceId, oldRecords)
  const oldWindow = original.materialize(original.cut(0), oldHead)
  const range = trace.createRange(trace.cursor(1), trace.cursor(2))
  const interval = trace.traceIntervalOfRange(range, before)
  const after = unwrap(trace.appendOpening(before, 'confirmed later', ['requirement']))
  const restored = new Trace(oldHead.traceId, trace.traceRecords(after))
  assert.deepEqual(restored.records().slice(0, oldRecords.length), oldRecords)
  assert.deepEqual(restored.materialize(restored.cut(0), oldHead), oldWindow)
  assert.deepEqual(trace.traceIntervalOfRange(range, after), interval)
  assert.equal(restored.materialize(restored.cut(0), restored.head()).opening.text, 'assignment:15:confirmed later\nrequirement')
})

test('WHAT[semantic-trace-010] small first Opening retains the original record protocol', async () => {
  const { Trace } = await import('wanxiangli/trace')
  const projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'task', ['requirement']))
  const records = trace.traceRecords(projection)
  assert.equal(records.length, 1)
  assert.equal(JSON.parse(records[0]).observation.type, 'Opening')
  const legacy = new Trace(trace.traceHeadCut(projection).traceId)
  legacy.capture({ type: 'Opening', bytes: new TextEncoder().encode('assignment:4:task\nrequirement') })
  assert.deepEqual(records, legacy.records())
})
}
