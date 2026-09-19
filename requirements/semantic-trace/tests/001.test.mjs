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

test('WHAT[semantic-trace-001] terminal capture returns explicit completion evidence', async () => {
  await withJournal(async (handle) => {
    const first = await trace.captureTerminalText(handle, SESSION, 'completed', 'terminal-run')
    const second = await trace.captureTerminalText(handle, SESSION, 'completed', 'terminal-run')
    assert.equal(first.terminalCaptured, true)
    assert.equal(second.terminalCaptured, false)
    assert.equal(trace.latestTerminalEvidence(trace.snapshot(handle, SESSION)).providerRun, 'terminal-run')
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

test('WHAT[semantic-trace-001] opening evidence is copied verbatim and idempotent', () => {
  const first = unwrap(trace.appendOpening(trace.emptyProjection(), 'first task', ['r1', 'r2']))
  const second = unwrap(trace.appendOpening(first, 'first task', ['r1', 'r2']))
  assert.deepEqual(trace.openingEvidence(second), {
    assignmentText: 'first task',
    authoritativeRequirements: ['r1', 'r2'],
    constitutiveBody: '',
  })
  assert.equal(trace.hasOpening(second), true)
})
test('WHAT[semantic-trace-001] semantic parts append in strict cursor order', () => {
  let projection = trace.emptyProjection()
  projection = unwrap(trace.appendPart(projection, part(1)))
  projection = unwrap(trace.appendPart(projection, part(2, { kind: 'reasoning' })))
  projection = unwrap(trace.appendPart(projection, part(3, { kind: 'tool_call', toolName: 'read' })))
  assert.deepEqual(trace.orderedSemanticParts(projection).map((value) => value.cursor.sequence), [1, 2, 3])
  assert.deepEqual(trace.partKinds(projection), ['text', 'reasoning', 'tool_call'])
  assert.equal(trace.headCursor(projection).sequence, 4)
})
test('WHAT[semantic-trace-001] terminal evidence is idempotent per provider run', () => {
  const terminal = { textRef: 'blob-terminal', textDigest: 'digest-terminal', providerRun: 'run-terminal' }
  const first = unwrap(trace.appendTerminal(trace.emptyProjection(), terminal))
  const second = unwrap(trace.appendTerminal(first, terminal))
  assert.deepEqual(trace.latestTerminalEvidence(second), {
    ...terminal,
    frontier: { sequence: 0 },
  })
  assert.deepEqual(trace.terminalEvidenceForProviderRun('run-terminal', second), trace.latestTerminalEvidence(second))
})
test('WHAT[semantic-trace-001] distinct provider runs retain distinct terminal evidence', () => {
  let projection = unwrap(trace.appendTerminal(trace.emptyProjection(), {
    textRef: 'blob-one', textDigest: 'digest-one', providerRun: 'run-one',
  }))
  projection = unwrap(trace.appendTerminal(projection, {
    textRef: 'blob-two', textDigest: 'digest-two', providerRun: 'run-two',
  }))
  assert.equal(trace.terminalEvidenceForProviderRun('run-one', projection).textRef, 'blob-one')
  assert.equal(trace.latestTerminalEvidence(projection).providerRun, 'run-two')
})
test('WHAT[semantic-trace-001] one provider run cannot publish conflicting terminal evidence', () => {
  const projection = unwrap(trace.appendTerminal(trace.emptyProjection(), {
    textRef: 'blob-one', textDigest: 'digest-one', providerRun: 'same-run',
  }))
  const rejected = trace.appendTerminal(projection, {
    textRef: 'blob-two', textDigest: 'digest-two', providerRun: 'same-run',
  })
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error, 'terminal-already-captured')
})
}
