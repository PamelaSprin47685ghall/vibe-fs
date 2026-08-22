// COMPANION-003 / COMPANION-007 / PERSIST-010 — XTrace capture hardening.
//
// These tests lock two blocking regressions:
//  1. captureProjection idempotence: re-observing one projection never appends
//     duplicate parts.
//  2. opening capture is the original assignment, not a transport envelope.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { resolve } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as xTrace from '../../../dist/Context/Trace/XTraceSurface.js'

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_xtrace_capture', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
}

const SEM = 'ses_cap'
const parts = (projection) => xTrace.parts(projection)

test('WHAT[SEMANTIC-TRACE-002] ProviderRetryAttempt_is_transport_control_not_durable_X_semantics', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Trace/XTracePipeline.fs'),
    'utf8',
  )

  assert.match(source, /PromptAuthority\.ProviderRetryAttempt/)
  assert.match(source, /ProviderWireDecode\.promptOriginOfMessage/)
  assert.match(source, /if isProviderRetryAttempt rawMessage then[\s\S]*?\{ message with Parts = \[\] \}[\s\S]*?else/)
  assert.match(
    source,
    /tryStableHostMessageIds[\s\S]{0,500}ProviderWireCapture\.decodeCapturedMessage/,
    'stable identity and semantic capture must enumerate the same decodable Host message universe',
  )
})

test('WHAT[SEMANTIC-TRACE-007] COMPANION_007_capture_projection_is_idempotent_across_transforms', async () => {
  await withJournal(async (handle) => {
    const projection = xTrace.semantic({
      messages: [
        { role: 'user', parts: [xTrace.textPart('task one')] },
        { role: 'assistant', parts: [xTrace.textPart('work a'), xTrace.reasoningPart('considered')] },
      ],
    })

    const first = await xTrace.captureProjection(handle, SEM, projection)
    assert.equal(parts(first).length, 3)

    const second = await xTrace.captureProjection(handle, SEM, projection)
    assert.equal(parts(second).length, 3, 're-observing the same projection must not duplicate the trace')
  })
})

test('WHAT[SEMANTIC-TRACE-007] XTrace_materialization_is_the_canonical_X_view_not_the_latest_request_presentation', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('raw opening')] },
          { role: 'assistant', parts: [xTrace.textPart('raw answer')] },
        ],
      }),
    )

    const first = await xTrace.materializeCurrentProjection(handle, SEM)
    assert.deepEqual(first.messages, [
      { role: 'user', parts: [{ kind: 'text', text: 'raw opening' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'raw answer' }] },
    ])

    // The same Host coordinates may be presented differently on a later request
    // (manager narrative, request-local replay, grounding, etc.). XTrace is the
    // already-accepted semantic history, so a presentation rewrite must not mutate
    // the canonical X projection used by Blogger coverage / prefix proof.
    await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('request-local rewritten opening')] },
          { role: 'assistant', parts: [xTrace.textPart('raw answer')] },
        ],
      }),
    )

    const second = await xTrace.materializeCurrentProjection(handle, SEM)
    assert.deepEqual(second, first)
  })
})

test('WHAT[SEMANTIC-TRACE-001] COMPANION_007_capture_projection_appends_only_new_turns', async () => {
  await withJournal(async (handle) => {
    const first = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({ messages: [{ role: 'user', parts: [xTrace.textPart('task')] }] }),
    )
    assert.equal(parts(first).length, 1)

    const second = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('task')] },
          { role: 'assistant', parts: [xTrace.textPart('work')] },
        ],
      }),
    )
    const traceParts = parts(second)
    assert.equal(traceParts.length, 2)
    assert.deepEqual(
      traceParts.map((part) => part.provenance),
      ['g:0/turn:0/part:0', 'g:0/turn:1/part:0'],
      'provenance is generation-scoped turn/part (HOST-006 reanchor isolation)',
    )
  })
})

test('WHAT[SEMANTIC-TRACE-004] COMPANION_007_capture_projection_provenance_is_stored_verbatim', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('task')] },
          { role: 'assistant', parts: [xTrace.toolCallPart('call-1', 'read', '{}')] },
        ],
      }),
    )

    const updated = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({ messages: [{ role: 'user', parts: [xTrace.textPart('task')] }] }),
    )

    const traceParts = parts(updated)
    assert.deepEqual(
      traceParts.map((part) => part.provenance),
      ['g:0/turn:0/part:0', 'g:0/turn:1/part:0'],
    )
  })
})

test('WHAT[SEMANTIC-TRACE-004] HOST_006_capture_projection_after_reanchor_uses_next_generation', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_xtrace_reanchor', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    const handle = created.journal
    const first = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('pre-compact task')] },
          { role: 'assistant', parts: [xTrace.textPart('pre-compact work')] },
        ],
      }),
    )
    assert.equal(parts(first).length, 2)
    assert.deepEqual(
      parts(first).map((part) => part.provenance),
      ['g:0/turn:0/part:0', 'g:0/turn:1/part:0'],
    )

    const reanchor = await xTrace.appendReanchor(handle, SEM, 0, 1, 'msg_compaction_1')
    assert.equal(reanchor.ok, true, 'ContextReanchored must fold')

    const second = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('summary-of-prior')] },
          { role: 'assistant', parts: [xTrace.textPart('post-compact work')] },
        ],
      }),
    )
    const traceParts = parts(second)
    assert.equal(traceParts.length, 4, 'reanchor generation must append, not collide')
    assert.deepEqual(
      traceParts.map((part) => part.provenance),
      [
        'g:0/turn:0/part:0',
        'g:0/turn:1/part:0',
        'g:1/turn:0/part:0',
        'g:1/turn:1/part:0',
      ],
    )
    assert.deepEqual(
      traceParts.map((part) => part.cursor.sequence),
      [1, 2, 3, 4],
    )
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[SEMANTIC-TRACE-010] COMPANION_003_capture_opening_takes_authoritative_requirements', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureOpening(handle, SEM, 'Review the tree.', ['Ship it.', 'Add tests.'])

    const again = await xTrace.captureOpening(handle, SEM, 'Review the tree.', ['Ship it.', 'Add tests.'])
    assert.equal(again, undefined)
  })
})

test('WHAT[SEMANTIC-TRACE-010] COMPANION_003_opening_capture_is_idempotent_for_the_same_text', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureOpening(handle, SEM, 'first task', [])
    await xTrace.captureOpening(handle, SEM, 'first task', [])
  })
})

test('WHAT[SEMANTIC-TRACE-010] COMPANION_003_parent_work_record_renders_the_opening_exactly_once', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureOpening(handle, SEM, 'first task', [])
    await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('first task')] },
          { role: 'assistant', parts: [xTrace.textPart('work a')] },
        ],
      }),
    )

    const parentBound = await xTrace.lifecycleWorkRecord(handle, SEM, true)
    assert.equal(typeof parentBound, 'string')
    assert.equal(parentBound.split('first task').length - 1, 1, 'opening appears exactly once for parent→child')
    assert.ok(parentBound.includes('Opening\n'), 'parent→child keeps Opening')
    assert.ok(!parentBound.includes('Opening task'), 'old Opening task heading is gone')
    assert.ok(parentBound.includes('assistant: work a'), 'the tail must carry the work after the opening')

    const joinBound = await xTrace.lifecycleWorkRecord(handle, SEM, false)
    assert.equal(typeof joinBound, 'string')
    assert.ok(!joinBound.includes('Opening\nfirst task'), 'child→parent join omits Opening')
    assert.ok(!joinBound.includes('Opening task'), 'old Opening task heading is gone')
    assert.ok(!joinBound.includes('first task'), 'assignment text is not echoed to the parent')
    assert.ok(joinBound.includes('assistant: work a'), 'work tail still returns')
  })
})

test('WHAT[SEMANTIC-TRACE-001] COMPANION_003_terminal_only_completion_projects_into_recent_work_without_appending_a_trace_part', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureOpening(handle, SEM, 'consult independently', [])
    const before = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({ messages: [{ role: 'user', parts: [xTrace.textPart('consult independently')] }] }),
    )
    const beforeCount = parts(before).length

    const terminal = 'Independent consultation perspective: preserve the original charge.'
    await xTrace.captureTerminalText(handle, SEM, terminal, 'msg_terminal_only')

    const after = await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({ messages: [{ role: 'user', parts: [xTrace.textPart('consult independently')] }] }),
    )
    assert.equal(
      parts(after).length,
      beforeCount,
      'terminal fallback is a read-time LWR projection, not a durable XTracePartAppended',
    )

    const record = await xTrace.lifecycleWorkRecord(handle, SEM, false)
    assert.equal(typeof record, 'string')
    assert.match(record, /Recent work/)
    assert.match(record, /Independent consultation perspective/)
    assert.equal(record.includes('Opening\nconsult independently'), false)
  })
})

test('WHAT[SEMANTIC-TRACE-001] COMPANION_003_last_words_land_in_recent_work_not_closing_report', async () => {
  await withJournal(async (handle) => {
    await xTrace.captureOpening(handle, SEM, 'finish the life', [])
    await xTrace.captureProjection(
      handle,
      SEM,
      xTrace.semantic({
        messages: [
          { role: 'user', parts: [xTrace.textPart('finish the life')] },
          { role: 'assistant', parts: [xTrace.textPart('did the work')] },
        ],
      }),
    )
    const words = 'the last words to the user'
    const written = await journal.JournalSurface_writePayload(handle, words)
    assert.equal(written.ok, true, written.ok ? '' : written.error)
    await xTrace.captureLastWords(handle, SEM, written.blobRef, written.blobDigest, 'run_last_words')

    const record = await xTrace.lifecycleWorkRecord(handle, SEM, true)
    assert.equal(typeof record, 'string')
    assert.match(record, /Recent work/)
    assert.match(record, /did the work/)
    assert.match(record, /the last words to the user/)
    assert.equal(record.includes('Closing report'), false)
  })
})
