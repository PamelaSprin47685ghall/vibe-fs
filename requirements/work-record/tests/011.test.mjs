import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/Surface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as openingSemantic from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'

// COMPANION-015 / EXEC-031 — bounded inspect/LWR Chronicle must not leak
// prior-invocation Y frames. TRACE was already sliced; Chronicle is sliced by
// coverage-interval overlap with the invocation range.

const SEM = 'ses_bounded_lwr'

const openJournal = async () => {
  const dir = mkdtempSync(join(tmpdir(), 'lwr-bounded-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_bounded_lwr', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  return {
    handle: created.journal,
    close: () => {
      journal.JournalSurface_dispose(created.journal)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

const withJournal = async (fn) => {
  const opened = await openJournal()
  try {
    return await fn(opened.handle)
  } finally {
    opened.close()
  }
}

const lastSequence = (trace) => trace.currentHeadSequence - 1

const commitY = async (handle, { from, to, body, n }) => {
  const written = await journal.JournalSurface_writePayload(handle, body)
  assert.equal(written.ok, true, written.ok ? '' : JSON.stringify(written.error))
  const run = `msg_y${n}`
  const result = await workRecord.appendBlogObservation(handle, SEM, run, {
    bloggerSessionId: 'ses_blogger',
    requestId: `req-y${n}`,
    frameEpoch: 0,
    previousIngestedThroughSequence: from,
    nextIngestedThroughSequence: to,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 0,
    nextCoveredPrefixDigest: '',
    textRef: written.blobRef,
    textDigest: written.blobDigest,
    toolCallIds: [],
    tipRuleId: `tip-y${n}`,
    fieldNameAtCommit: `field-y${n}`,
    observedPrefixEpoch: 0,
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}

const seedTwoInvocations = async (handle) => {
  await workRecord.captureOpening(handle, SEM, 'first charge', [])

  const inv1 = await workRecord.captureProjection(handle, SEM, {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'first charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'inv1 work' }] },
    ],
  })
  const inv1Through = lastSequence(inv1)
  // S1 = XTrace.head after inv1 (one-past last part). S2 is likewise one-past inv2.
  const s1 = inv1Through + 1
  assert.equal(inv1Through, 2, `inv1 last part must be 2, got ${inv1Through}`)
  assert.equal(s1, 3, `inv2 StartInclusive must be one-past inv1 (3), got ${s1}`)
  await commitY(handle, { from: 0, to: inv1Through, body: 'PRIOR_Y_INV1', n: 1 })

  const inv2 = await workRecord.captureProjection(handle, SEM, {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'first charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'inv1 work' }] },
      { role: 'user', parts: [{ kind: 'text', text: 'second charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'inv2 work' }] },
    ],
  })
  const inv2Through = lastSequence(inv2)
  const s2 = inv2Through + 1
  assert.equal(inv2Through, 4, `inv2 last part must be 4, got ${inv2Through}`)
  assert.equal(s2, 5, `EndExclusive must be one-past last part (5), got ${s2}`)
  assert.equal(inv1Through, s1 - 1, 'inv1 Y CoveredThroughSequence must equal inv2 StartInclusive - 1')
  return { s1, s2, inv1Through, inv2Through }
}

// COMPANION-003 / EXEC-006 / EXEC-008 — LifecycleWorkRecord 物化。
//
// LWR = Opening? + Chronicle + Recent work。Closing report 已删除。
// 覆盖：byte-exact opening、最后一条助手文本在 Recent work、gap 从
// max(ingestedThrough, openingEnd) 起、无 Y 时同一算法、空段省略、determinism、
// child opening 排除 parent envelope。

const xTrace = {
  item: traceOwner.item,
  text: traceOwner.textPart,
  reasoning: traceOwner.reasoningPart,
  toolCall: (name, args) => traceOwner.toolCallPart('fixture-call', name, args),
  toolResult: (result) => traceOwner.toolResultPart('fixture-call', result),
}
const opening = (assignment, requirements = []) => openingSemantic.opening(assignment, requirements, '')
const materialize = (
  openingValue,
  frames,
  trace,
  coverage,
  openingEnd = { Sequence: 0 },
  includeOpening = true,
) => {
  const gapStart = Math.max(Number(coverage.Sequence), Number(openingEnd.Sequence))
  const gap = traceOwner.render(traceOwner.forWorkRecord(traceOwner.sliceFrom({ sequence: gapStart }, trace)))
  return openingSemantic.materialize(openingValue, frames, gap, includeOpening)
}

// 公共 fixture：trace 中 cursor 0 是 opening（Y 起点在 opening 之后，方案 4.1）
const OPENING_END = { Sequence: 1 }

// WORK-RECORD-011 / WORK-RECORD-012 — the statement is prose, not a fixed DTO.
//
// The formal statement of a WorkRecord is the LAST assistant text in Recent work.
// There is no Closing report section and no universal report schema: rendering
// recent work must not manufacture `### Summary` / Files / Tests / Risks headings,
// and the last assistant text must appear exactly once as the final statement.

const trace = [
  xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('Rewrite the fallback controller.') }),
  xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('investigating') }),
  xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('I found the root cause.') }),
  xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('Implemented and verified the fix.') }),
]

// COMPANION-003 / §18 — WorkRecord exposes exactly three canonical section headings.

test('WHAT[WORK-RECORD-011] bounded terminal-only completion still yields Recent work after Chronicle covered every durable part', async () => {
  await withJournal(async (handle) => {
    await workRecord.captureOpening(handle, SEM, 'terminal race charge', [])
    const captured = await workRecord.captureProjection(handle, SEM, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'terminal race charge' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'work before final statement' }] },
      ],
    })
    assert.equal(captured.currentHeadSequence, 3)
    await commitY(handle, { from: 0, to: 2, body: 'CURRENT_CHRONICLE', n: 10 })
    await workRecord.captureTerminalText(handle, SEM, 'FINAL_STATEMENT_FROM_TERMINAL', 'run-terminal-race')

    const bounded = await workRecord.lifecycleWorkRecordBounded(handle, SEM, {
      StartInclusive: { Sequence: 0 },
      EndExclusive: { Sequence: 3 },
      ProviderRun: 'run-terminal-race',
    })
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /Chronicle\nCURRENT_CHRONICLE/)
    assert.match(bounded, /Recent work/)
    assert.match(bounded, /FINAL_STATEMENT_FROM_TERMINAL/)
  })
})

test('WHAT[WORK-RECORD-011] LWR_last_assistant_text_is_in_recent_work_not_a_closing_report', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary with detail') }),
  ]

  const rendered = materialize(opening('task'), [], trace, { Sequence: 1 }, OPENING_END)

  assert.match(rendered, /Recent work\nassistant: Final summary with detail/)
  assert.equal(rendered.includes('Closing report'), false)
  assert.equal(rendered.includes('Final output'), false)
  assert.equal(rendered.includes('final_text'), false)
})

test('WHAT[WORK-RECORD-011] LWR_empty_sections_are_omitted', () => {
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') })]

  const rendered = materialize(opening('task'), [], trace, { Sequence: 1 }, OPENING_END)
  // gap 空、无 frames → 只有 Opening
  assert.equal(rendered.includes('Chronicle'), false)
  assert.equal(rendered.includes('Recent work'), false)
  assert.equal(rendered.includes('Closing report'), false)
  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Opening task'), false)
})

test('WHAT[WORK-RECORD-011] LWR_statement_is_the_last_assistant_text_in_recent_work', () => {
  const rendered = materialize(
    opening('Rewrite the fallback controller.'),
    [],
    trace,
    { Sequence: 0 },
    { Sequence: 1 },
    true,
  )

  // The last assistant text appears in Recent work — not in any Closing section.
  assert.ok(rendered.includes('Implemented and verified the fix.'))
  assert.ok(rendered.includes('Recent work'))
  // No fourth section named Closing / Final output / Answer exists.
  for (const forbidden of ['Closing report', 'Final output', '## Answer', 'Closing:']) {
    assert.ok(!rendered.includes(forbidden), `no ${forbidden} section may exist`)
  }
  // The last assistant text is the final non-empty line of the record
  // (rendered with its role prefix, per XTrace.renderItem).
  const lines = rendered.split('\n').map((l) => l.trim()).filter(Boolean)
  assert.equal(lines[lines.length - 1], 'assistant: Implemented and verified the fix.')
})

test('WHAT[WORK-RECORD-011] WORK_RECORD_SECTIONS_lifecycle_source_declares_three_canonical_headings', () => {
  const opening = openingSemantic.opening('Fix the bug', ['Must be tested', 'Must be performant'], 'Plan for the mission')
  const frames = ['Frame 1 chronicle entry', 'Frame 2 chronicle entry']
  const gap = 'Recent edits performed in this round'

  const rendered = openingSemantic.materialize(opening, frames, gap, true)

  // Verify three canonical section headings exist in exact structure
  assert.match(rendered, /^Opening\n/)
  assert.match(rendered, /\n\nChronicle\n/)
  assert.match(rendered, /\n\nRecent work\n/)

  // Legacy headings must not appear
  for (const legacy of ['Opening task', 'Work log', 'Uncompressed tail', 'Final output', 'Closing report']) {
    assert.equal(rendered.includes(legacy), false, `legacy heading must not appear: ${legacy}`)
  }

  const renderedEmpty = openingSemantic.materialize(opening, [], '', true)
  assert.match(renderedEmpty, /^Opening\n/)
  assert.equal(renderedEmpty.includes('Chronicle'), false, 'empty Chronicle section must be omitted')
  assert.equal(renderedEmpty.includes('Recent work'), false, 'empty Recent work section must be omitted')
})
