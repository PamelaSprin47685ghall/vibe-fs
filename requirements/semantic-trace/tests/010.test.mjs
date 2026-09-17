import test from 'node:test'

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

test('WHAT[SEMANTIC-TRACE-010] opening capture reports idempotent evidence', async () => {
  await withJournal(async (handle) => {
    const first = await trace.captureOpening(handle, SESSION, 'Review the tree.', ['Ship it.', 'Add tests.'])
    const second = await trace.captureOpening(handle, SESSION, 'Review the tree.', ['Ship it.', 'Add tests.'])
    assert.equal(first.openingCaptured, true)
    assert.equal(second.openingCaptured, false)
    assert.deepEqual(trace.openingEvidence(trace.snapshot(handle, SESSION)).authoritativeRequirements, ['Ship it.', 'Add tests.'])
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
  providerRun: `run-${sequence}`,
  ...overrides,
})

test('WHAT[SEMANTIC-TRACE-010] conflicting opening is rejected', () => {
  const projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'first task', []))
  const rejected = trace.appendOpening(projection, 'second task', [])
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error, 'opening-already-captured')
})
}
