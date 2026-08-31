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

test('WHAT[SEMANTIC-TRACE-002] ProviderRetryAttempt_is_transport_control_not_durable_X_semantics', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Trace/Capture.fs'),
    'utf8',
  )

  assert.match(
    source,
    /let private capturedObservationMessage[\s\S]*?PromptAuthority\.PromptOrigin\.Continuation PromptAuthority\.ContinuationKind\.ProviderRetryAttempt[\s\S]*?\{ observation\.Message with Parts = \[\] \}/,
    'retry transport control must be classified by the exact continuation origin',
  )
  assert.match(
    source,
    /let captured = observations \|> List\.map capturedObservationMessage/,
    'semantic capture must strip retry transport material before durable append',
  )
})

test('WHAT[SEMANTIC-TRACE-007] projection capture is idempotent and reports owner receipts', async () => {
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

test('WHAT[SEMANTIC-TRACE-007] materialization reads canonical durable semantics', async () => {
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

test('WHAT[SEMANTIC-TRACE-002] typed retry observation retains stable identity but appends no semantics', async () => {
  await withJournal(async (handle) => {
    const captured = await trace.captureObservedMessages(handle, SESSION, [
      {
        hostMessageId: 'retry-message',
        origin: 'ProviderRetryAttempt',
        message: { info: { id: 'retry-run', role: 'user' }, parts: [{ id: 'retry-part', type: 'text', text: 'transport retry' }] },
      },
      {
        hostMessageId: 'answer-message',
        message: { info: { id: 'answer-run', role: 'assistant' }, parts: [{ id: 'answer-part', type: 'text', text: 'semantic answer' }] },
      },
    ])
    assert.equal(captured.ok, true, captured.ok ? '' : captured.error)
    assert.equal(captured.receipt.identity, 'stable-host')
    assert.equal(captured.receipt.capturedPartCount, 1)
    assert.deepEqual(trace.orderedSemanticParts(captured.projection).map((part) => part.provenance), [
      'g:0/msg:answer-message/host-part:answer-part',
    ])
    assert.deepEqual(
      trace.orderedSemanticParts(captured.projection),
      trace.orderedSemanticParts(trace.snapshot(handle, SESSION)),
      'the returned opaque current projection is the resulting owner state',
    )
  })
})

test('WHAT[SEMANTIC-TRACE-010] opening capture reports idempotent evidence', async () => {
  await withJournal(async (handle) => {
    const first = await trace.captureOpening(handle, SESSION, 'Review the tree.', ['Ship it.', 'Add tests.'])
    const second = await trace.captureOpening(handle, SESSION, 'Review the tree.', ['Ship it.', 'Add tests.'])
    assert.equal(first.openingCaptured, true)
    assert.equal(second.openingCaptured, false)
    assert.deepEqual(trace.openingEvidence(trace.snapshot(handle, SESSION)).authoritativeRequirements, ['Ship it.', 'Add tests.'])
  })
})

test('WHAT[SEMANTIC-TRACE-001] terminal capture returns explicit completion evidence', async () => {
  await withJournal(async (handle) => {
    const first = await trace.captureTerminalText(handle, SESSION, 'completed', 'terminal-run')
    const second = await trace.captureTerminalText(handle, SESSION, 'completed', 'terminal-run')
    assert.equal(first.terminalCaptured, true)
    assert.equal(second.terminalCaptured, false)
    assert.equal(trace.latestTerminalEvidence(trace.snapshot(handle, SESSION)).providerRun, 'terminal-run')
  })
})
