import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}

const partDescriptor = {
  sequence: 1,
  role: 'assistant',
  provenance: 'g:0/msg:message-1/host-part:part-1',
  turn: 0,
  partIndex: 0,
  kind: 'tool_call',
  toolName: 'read',
  textRef: 'blob-1',
  textDigest: 'digest-1',
  providerRun: 'provider-run-1',
  toolCallId: 'call-1',
  hostToolPartId: 'part-1',
}

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

const append = (projection, sequence, options = {}) => unwrap(trace.appendPart(projection, {
  sequence,
  role: 'assistant',
  provenance: `g:0/msg:${options.messageId ?? 'message-a'}/host-part:${options.hostToolPartId ?? `part-${sequence}`}`,
  turn: options.turn ?? 0,
  partIndex: options.partIndex ?? sequence - 1,
  kind: options.kind ?? 'tool_call',
  toolName: options.toolName ?? 'todowrite',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  providerRun: options.providerRun ?? 'provider-run-a',
  toolCallId: options.toolCallId ?? 'call-a',
  hostToolPartId: options.hostToolPartId ?? `part-${sequence}`,
}))

const fixture = () => {
  let projection = trace.emptyProjection()
  projection = append(projection, 1, { messageId: 'message-a', hostToolPartId: 'part-a', partIndex: 0 })
  projection = append(projection, 2, { messageId: 'message-a', hostToolPartId: 'part-b', partIndex: 1, kind: 'tool_result' })
  projection = append(projection, 3, { messageId: 'message-b', providerRun: 'provider-run-b', toolCallId: 'call-b', hostToolPartId: 'part-c', turn: 1, partIndex: 0 })
  return projection
}

test('WHAT[SEMANTIC-TRACE-002] copied semantic evidence excludes transport metadata', () => {
  const projection = unwrap(trace.appendPart(trace.emptyProjection(), partDescriptor))
  const evidence = trace.orderedSemanticParts(projection)[0]
  assert.equal(evidence.providerRun, 'provider-run-1')
  assert.equal(evidence.toolName, 'read')
  for (const forbidden of ['usage', 'cost', 'timestamp', 'elapsed', 'directory', 'finishReason', 'runtimeId', 'tokens', 'uiDelta']) {
    assert.equal(Object.hasOwn(evidence, forbidden), false, `semantic evidence must not carry ${forbidden}`)
  }
})

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

test('WHAT[SEMANTIC-TRACE-002] capture mapper copies text and reasoning semantics', () => {
  assert.deepEqual(trace.mapPart(trace.textPart('hello')), { kind: 'text', text: 'hello' })
  assert.deepEqual(trace.mapPart(trace.reasoningPart('considering')), { kind: 'reasoning', text: 'considering' })
})

test('WHAT[SEMANTIC-TRACE-002] capture mapper drops transport call identities', () => {
  assert.deepEqual(trace.mapPart(trace.toolCallPart('call-1', 'read', '{}')), {
    kind: 'tool-call',
    name: 'read',
    args: '{}',
  })
  assert.deepEqual(trace.mapPart(trace.toolResultPart('call-1', 'output')), {
    kind: 'tool-result',
    result: 'output',
  })
})

test('WHAT[SEMANTIC-TRACE-002] activity bookkeeping has no semantic part', () => {
  assert.equal(trace.mapPart(trace.activityPart('step-start')), undefined)
})

test('WHAT[SEMANTIC-TRACE-002] provider-run query returns copied semantic evidence', () => {
  const parts = trace.providerRunParts('provider-run-a', fixture())
  assert.deepEqual(parts.map((part) => part.cursor.sequence), [1, 2])
  assert.deepEqual(parts.map((part) => part.providerRun), ['provider-run-a', 'provider-run-a'])
})

test('WHAT[SEMANTIC-TRACE-002] exact provider tool and Host identities are queryable', () => {
  const projection = fixture()
  assert.deepEqual(trace.toolResultParts('provider-run-a', 'call-a', projection).map((part) => part.cursor.sequence), [2])
  assert.deepEqual(
    trace.toolPartsForHostIdentity('provider-run-a', 'call-a', 'part-a', projection).map((part) => part.cursor.sequence),
    [1],
  )
})
