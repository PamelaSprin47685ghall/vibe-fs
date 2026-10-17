import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/Surface.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import { commitFrame, withReopenableJournal, readBoundedInNewProcess } from './support/record.mjs'

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

test('WHAT[work-record-004] COMPANION_015_bounded_chronicle_heading_omitted_when_invocation_has_no_y', async () => {
  await withJournal(async (journal) => {
    const { s1, s2 } = await seedTwoInvocations(journal)

    const bounded = await workRecord.lifecycleWorkRecordBounded(journal, SEM, {
      StartInclusive: { Sequence: s1 },
      EndExclusive: { Sequence: s2 },
    })
    assert.equal(typeof bounded, 'string')
    assert.doesNotMatch(bounded, /PRIOR_Y_INV1/)
    assert.doesNotMatch(bounded, /^Chronicle\n/m)
    assert.match(bounded, /inv2 work/)
    assert.doesNotMatch(bounded, /^Opening\n/m)
  })
})

test('WHAT[work-record-004] same terminal text in a reused child is a fresh occurrence when ProviderRun changes', async () => {
  await withJournal(async (handle) => {
    await workRecord.captureOpening(handle, SEM, 'reuse first', [])
    const first = await workRecord.captureProjection(handle, SEM, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'reuse first' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'first work' }] },
      ],
    })
    assert.equal(first.currentHeadSequence, 3)
    await commitY(handle, { from: 0, to: 2, body: 'FIRST_CHRONICLE', n: 11 })
    await workRecord.captureTerminalText(handle, SEM, 'SAME_FINAL_TEXT', 'run-reuse-1')

    const second = await workRecord.captureProjection(handle, SEM, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'reuse first' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'first work' }] },
        { role: 'user', parts: [{ kind: 'text', text: 'reuse second' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'second work' }] },
      ],
    })
    assert.equal(second.currentHeadSequence, 5)
    await commitY(handle, { from: 2, to: 3, body: 'SECOND_CALLER_CHARGE', n: 12 })
    await commitY(handle, { from: 3, to: 4, body: 'SECOND_CHRONICLE', n: 15 })
    await workRecord.captureTerminalText(handle, SEM, 'SAME_FINAL_TEXT', 'run-reuse-2')

    const bounded = await workRecord.lifecycleWorkRecordBounded(handle, SEM, {
      StartInclusive: { Sequence: 3 },
      EndExclusive: { Sequence: 5 },
      ProviderRun: 'run-reuse-2',
    })
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /Chronicle\nSECOND_CHRONICLE/)
    assert.doesNotMatch(bounded, /FIRST_CHRONICLE/)
    assert.match(bounded, /Recent work/)
    assert.match(bounded, /SAME_FINAL_TEXT/)
  })
})

test('WHAT[work-record-004] rematerializing an older bounded range never substitutes a later terminal', async () => {
  await withJournal(async (handle) => {
    await workRecord.captureOpening(handle, SEM, 'history first', [])
    await workRecord.captureProjection(handle, SEM, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'history first' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'first work' }] },
      ],
    })
    await commitY(handle, { from: 0, to: 2, body: 'HISTORY_CHRONICLE_1', n: 13 })
    await workRecord.captureTerminalText(handle, SEM, 'FIRST_FINAL', 'run-history-1')

    await workRecord.captureProjection(handle, SEM, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'history first' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'first work' }] },
        { role: 'user', parts: [{ kind: 'text', text: 'history second' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'second work' }] },
      ],
    })
    await commitY(handle, { from: 2, to: 4, body: 'HISTORY_CHRONICLE_2', n: 14 })
    await workRecord.captureTerminalText(handle, SEM, 'SECOND_FINAL', 'run-history-2')

    const firstBounded = await workRecord.lifecycleWorkRecordBounded(handle, SEM, {
      StartInclusive: { Sequence: 0 },
      EndExclusive: { Sequence: 3 },
      ProviderRun: 'run-history-1',
    })
    assert.equal(typeof firstBounded, 'string')
    assert.match(firstBounded, /FIRST_FINAL/)
    assert.doesNotMatch(firstBounded, /SECOND_FINAL/)
    assert.doesNotMatch(firstBounded, /HISTORY_CHRONICLE_2/)
  })
})

test('WHAT[work-record-004] a frame spanning two invocations cannot leak the earlier work into the later bounded record', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-crossing-frame'
    await workRecord.captureOpening(handle, session, 'charge', [])
    await workRecord.captureProjection(handle, session, { messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'EARLIER_WORK' }] },
      { role: 'user', parts: [{ kind: 'text', text: 'later charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'LATER_WORK' }] },
    ] })
    await commitFrame(handle, session, { from: 0, through: 4, body: 'EARLIER_WORK and LATER_WORK', id: 'crossing' })
    const range = {
      StartInclusive: { Sequence: 3 }, EndExclusive: { Sequence: 5 },
    }
    const bounded = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
    assert.equal(typeof bounded, 'string')
    assert.ok(bounded.includes('LATER_WORK'))
    assert.equal(bounded.includes('EARLIER_WORK'), false)
    assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), bounded)
  })
})

const rangeMessages = [
  { role: 'user', parts: [{ kind: 'text', text: 'ROOT_CHARGE' }] },
  { role: 'assistant', parts: [{ kind: 'text', text: 'PRIOR_WORK' }] },
  { role: 'user', parts: [{ kind: 'text', text: 'CURRENT_CHARGE' }] },
  { role: 'assistant', parts: [
    { kind: 'reasoning', text: 'CURRENT_REASONING' },
    { kind: 'tool-call', name: 'read', args: 'CURRENT_TOOL_CALL' },
    { kind: 'tool-result', result: 'CURRENT_TOOL_RESULT' },
    { kind: 'text', text: 'CURRENT_BODY' },
  ] },
  { role: 'user', parts: [{ kind: 'text', text: 'NEXT_CHARGE' }] },
  { role: 'assistant', parts: [{ kind: 'text', text: 'NEXT_WORK' }] },
]

for (const scenario of [
  { name: 'no frames', frames: [], summary: false },
  { name: 'contained prefix', frames: [[3, 'PRIOR_SUMMARY'], [5, 'CURRENT_REASONING_SUMMARY']], summary: true },
  { name: 'contained through final text', frames: [[3, 'PRIOR_SUMMARY'], [7, 'CURRENT_REASONING_SUMMARY']], summary: true },
  { name: 'crosses start', frames: [[6, 'PRIOR_WORK CURRENT_BODY CROSSING_SUMMARY']], summary: false },
  { name: 'crosses end', frames: [[3, 'PRIOR_SUMMARY'], [9, 'CURRENT_BODY NEXT_WORK CROSSING_SUMMARY']], summary: false },
  { name: 'crosses both', frames: [[9, 'PRIOR_WORK CURRENT_BODY NEXT_WORK CROSSING_SUMMARY']], summary: false },
  { name: 'contained then crossing', frames: [[3, 'PRIOR_SUMMARY'], [5, 'CURRENT_REASONING_SUMMARY'], [9, 'CURRENT_BODY NEXT_WORK CROSSING_SUMMARY']], summary: true },
  { name: 'crossing then contained', frames: [[4, 'PRIOR_WORK CROSSING_SUMMARY'], [6, 'UNUSABLE_LATER_SUMMARY']], summary: false },
  { name: 'inclusive end at range start', frames: [[4, 'PRIOR_WORK CROSSING_SUMMARY']], summary: false },
]) {
  test(`WHAT[work-record-004] bounded originals and coverage stay complete when a frame ${scenario.name}`, async () => {
    await withReopenableJournal(async (handle, reopen) => {
      const session = `record-range-${scenario.name}`
      await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
      const captured = await workRecord.captureProjection(handle, session, { messages: rangeMessages })
      assert.equal(captured.currentHeadSequence, 10)
      let previous = 0
      for (const [index, [through, body]] of scenario.frames.entries()) {
        await commitFrame(handle, session, { from: previous, through, body, id: `frame-${index}` })
        previous = through
      }
      const full = await workRecord.lifecycleWorkRecord(handle, session, false)
      const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 } }
      const bounded = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
      assert.equal(typeof bounded, 'string')
      assert.match(bounded, /CURRENT_BODY/)
      assert.doesNotMatch(bounded, /ROOT_CHARGE|PRIOR_|CURRENT_CHARGE|NEXT_|CROSSING_SUMMARY|UNUSABLE_LATER_SUMMARY|CURRENT_TOOL_/)
      if (scenario.summary) {
        assert.match(bounded, /^Chronicle\nCURRENT_REASONING_SUMMARY\n\nRecent work\nassistant: CURRENT_BODY$/)
      } else {
        assert.equal(bounded, 'Recent work\nCURRENT_REASONING\nassistant: CURRENT_BODY')
      }
      assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, range), bounded)
      assert.equal(await workRecord.lifecycleWorkRecord(handle, session, false), full)
      const recovered = await reopen()
      assert.equal(await workRecord.lifecycleWorkRecordBounded(recovered, session, range), bounded)
      assert.equal(await workRecord.lifecycleWorkRecord(recovered, session, false), full)
    })
  })
}

test('WHAT[work-record-004] a later crossing frame cannot expand an earlier bounded range or hide its original suffix', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-range-later-frame'
    await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
    await workRecord.captureProjection(handle, session, { messages: rangeMessages.slice(0, 4) })
    await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
    await commitFrame(handle, session, { from: 3, through: 5, body: 'CURRENT_REASONING_SUMMARY', id: 'current' })
    const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 } }
    const before = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
    await workRecord.captureProjection(handle, session, { messages: rangeMessages })
    await commitFrame(handle, session, { from: 5, through: 9, body: 'NEXT_WORK CROSSING_SUMMARY', id: 'later' })
    assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, range), before)
    assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), before)
  })
})

test('WHAT[work-record-004] empty and terminal-only bounded records do not borrow a crossing summary', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-range-terminal'
    await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
    await workRecord.captureProjection(handle, session, { messages: rangeMessages.slice(0, 4) })
    await workRecord.captureTerminalText(handle, session, 'CURRENT_FINAL', 'current-run')
    await commitFrame(handle, session, { from: 0, through: 7, body: 'PRIOR_WORK CURRENT_BODY', id: 'all' })
    const empty = { StartInclusive: { Sequence: 8 }, EndExclusive: { Sequence: 8 } }
    const terminal = { ...empty, ProviderRun: 'current-run' }
    assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, empty), '')
    assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, terminal), 'Recent work\nassistant: CURRENT_FINAL')
    const recovered = await reopen()
    assert.equal(await workRecord.lifecycleWorkRecordBounded(recovered, session, empty), '')
    assert.equal(await workRecord.lifecycleWorkRecordBounded(recovered, session, terminal), 'Recent work\nassistant: CURRENT_FINAL')
  })
})

for (const terminal of [false, true]) {
  test(`WHAT[work-record-004] bounded formal statement survives a fully covered trailing tool with terminal=${terminal}`, async () => {
    await withReopenableJournal(async (handle, reopen) => {
      const session = `bounded-final-before-tool-${terminal}`
      await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
      await workRecord.captureProjection(handle, session, { messages: [
        ...rangeMessages.slice(0, 3),
        { role: 'assistant', parts: [
          { kind: 'text', text: 'CURRENT_STATEMENT' },
          { kind: 'tool-call', name: 'read', args: 'CURRENT_TOOL_CALL' },
          { kind: 'tool-result', result: 'CURRENT_TOOL_RESULT' },
        ] },
      ] })
      await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
      await commitFrame(handle, session, { from: 3, through: 6, body: 'current summary', id: 'current' })
      if (terminal) await workRecord.captureTerminalText(handle, session, 'CURRENT_STATEMENT', 'current-run')
      const range = {
        StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 7 },
        ...(terminal ? { ProviderRun: 'current-run' } : {}),
      }
      const bounded = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
      assert.equal(bounded, 'Chronicle\ncurrent summary\n\nRecent work\nassistant: CURRENT_STATEMENT')
      assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), bounded)
    })
  })
}

for (const prefixKind of ['reasoning', 'text']) {
  for (const trailingTool of [false, true]) {
    for (const terminalKind of ['turn', 'suffix', 'distinct']) {
      test(`WHAT[work-record-004] partial ${prefixKind} coverage keeps one formal statement with ${terminalKind} terminal and trailingTool=${trailingTool}`, async () => {
        await withReopenableJournal(async (handle, reopen) => {
          const session = `record-terminal-${prefixKind}-${terminalKind}-${trailingTool}`
          await workRecord.captureOpening(handle, session, 'PRIOR_CHARGE', [])
          const captured = await workRecord.captureProjection(handle, session, { messages: [
            { role: 'user', parts: [{ kind: 'text', text: 'PRIOR_CHARGE' }] },
            { role: 'assistant', parts: [{ kind: 'text', text: 'PRIOR_WORK' }] },
            { role: 'user', parts: [{ kind: 'text', text: 'CURRENT_CHARGE' }] },
            { role: 'assistant', parts: [
              { kind: prefixKind, text: 'CURRENT_COVERED_PREFIX' },
              { kind: 'tool-call', name: 'read', args: 'PRIVATE_TOOL' },
              { kind: 'text', text: 'CURRENT_FORMAL_STATEMENT' },
              ...(trailingTool ? [{ kind: 'tool-result', result: 'PRIVATE_RESULT' }] : []),
            ] },
          ] })
          await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
          await commitFrame(handle, session, {
            from: 3, through: trailingTool ? 7 : 5, body: 'CURRENT_SUMMARY', id: 'current',
          })
          const range = {
            StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: captured.currentHeadSequence },
            ProviderRun: 'current-run',
          }
          const before = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
          assert.equal(before, 'Chronicle\nCURRENT_SUMMARY\n\nRecent work\nassistant: CURRENT_FORMAL_STATEMENT')
          const terminal = terminalKind === 'turn'
            ? 'CURRENT_COVERED_PREFIX\n\nCURRENT_FORMAL_STATEMENT'
            : terminalKind === 'suffix' ? 'CURRENT_FORMAL_STATEMENT' : 'DISTINCT_FORMAL_STATEMENT'
          await workRecord.captureTerminalText(handle, session, terminal, 'current-run')
          const expected = terminalKind === 'distinct' ? `${before}\nassistant: ${terminal}` : before
          const completed = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
          assert.equal(completed, expected)
          assert.doesNotMatch(completed, /PRIOR_|CURRENT_CHARGE|CURRENT_COVERED_PREFIX|PRIVATE_/)
          assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), expected)
        })
      })
    }
  }
}

for (const emptySuffix of ['', ' ', '\n']) {
  for (const formalVisible of [false, true]) {
    test(`WHAT[work-record-004] empty reasoning suffix ${JSON.stringify(emptySuffix)} preserves terminal evidence with formalVisible=${formalVisible}`, async () => {
      await withReopenableJournal(async (handle, reopen) => {
        const session = `record-empty-suffix-${emptySuffix.length}-${formalVisible}`
        await workRecord.captureOpening(handle, session, 'CHARGE', [])
        const captured = await workRecord.captureProjection(handle, session, { messages: [
          { role: 'user', parts: [{ kind: 'text', text: 'CHARGE' }] },
          { role: 'assistant', parts: [
            { kind: 'reasoning', text: 'COVERED_REASONING' },
            ...(formalVisible ? [{ kind: 'text', text: 'VISIBLE_FORMAL' }] : []),
            { kind: 'reasoning', text: emptySuffix },
            { kind: 'tool-result', result: 'PRIVATE_RESULT' },
          ] },
        ] })
        await commitFrame(handle, session, { from: 0, through: 1, body: 'CALLER_SUMMARY', id: 'caller' })
        await commitFrame(handle, session, {
          from: 1, through: 3, body: 'CURRENT_SUMMARY', id: 'current',
        })
        const range = {
          StartInclusive: { Sequence: 2 }, EndExclusive: { Sequence: captured.currentHeadSequence },
          ProviderRun: 'current-run',
        }
        const before = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
        const terminal = ['COVERED_REASONING', ...(formalVisible ? ['VISIBLE_FORMAL'] : []), emptySuffix].join('\n\n')
        await workRecord.captureTerminalText(handle, session, terminal, 'current-run')
        const completed = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
        if (formalVisible) assert.equal(completed, before)
        else assert.equal(completed, `Chronicle\nCURRENT_SUMMARY\n\nRecent work\n${emptySuffix ? `${emptySuffix}\n` : ''}assistant: ${terminal}`)
        assert.doesNotMatch(completed, /CALLER_SUMMARY|CHARGE|PRIVATE_RESULT/)
        assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), completed)
      })
    })
  }
}

for (const prefixKind of ['reasoning', 'text']) {
  for (const crossingFrame of [false, true]) {
    for (const terminalKind of ['formal', 'earlier']) {
      test(`WHAT[work-record-004] complete raw ${prefixKind} work with crossingFrame=${crossingFrame} uses latest formal evidence for ${terminalKind} terminal`, async () => {
        await withReopenableJournal(async (handle, reopen) => {
          const session = `record-raw-${prefixKind}-${crossingFrame}-${terminalKind}`
          await workRecord.captureOpening(handle, session, 'PRIOR_CHARGE', [])
          const captured = await workRecord.captureProjection(handle, session, { messages: [
            { role: 'user', parts: [{ kind: 'text', text: 'PRIOR_CHARGE' }] },
            { role: 'assistant', parts: [{ kind: 'text', text: 'PRIOR_WORK' }] },
            { role: 'user', parts: [{ kind: 'text', text: 'CURRENT_CHARGE' }] },
            { role: 'assistant', parts: [
              { kind: prefixKind, text: 'CURRENT_PREFIX' },
              { kind: 'text', text: 'CURRENT_FORMAL' },
            ] },
          ] })
          if (crossingFrame) await commitFrame(handle, session, {
            from: 0, through: 5, body: 'PRIOR_WORK CURRENT_FORMAL CROSSING_SUMMARY', id: 'crossing',
          })
          const range = {
            StartInclusive: { Sequence: 3 }, EndExclusive: { Sequence: captured.currentHeadSequence },
            ProviderRun: 'current-run',
          }
          const before = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
          const prefix = prefixKind === 'text' ? 'assistant: CURRENT_PREFIX' : 'CURRENT_PREFIX'
          assert.equal(before, `Recent work\n${prefix}\nassistant: CURRENT_FORMAL`)
          const terminal = terminalKind === 'formal' ? 'CURRENT_FORMAL' : 'CURRENT_PREFIX'
          await workRecord.captureTerminalText(handle, session, terminal, 'current-run')
          const expected = terminalKind === 'formal' ? before : `${before}\nassistant: ${terminal}`
          const completed = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
          assert.equal(completed, expected)
          assert.doesNotMatch(completed, /PRIOR_|CURRENT_CHARGE|CROSSING_SUMMARY/)
          assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), expected)
        })
      })
    }
  }
}

test('WHAT[work-record-004] an actual durable squash spanning invocations restores only bounded originals after cold replay', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-range-squashed'
    await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
    await workRecord.captureProjection(handle, session, { messages: rangeMessages })
    await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
    await commitFrame(handle, session, { from: 3, through: 5, body: 'CURRENT_REASONING_SUMMARY', id: 'current' })
    await commitFrame(handle, session, { from: 5, through: 9, body: 'CURRENT_BODY NEXT_WORK', id: 'later' })
    const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 } }
    const written = await journal.JournalSurface_writePayload(handle, 'PRIOR_WORK CURRENT_BODY NEXT_WORK SQUASHED_SUMMARY')
    assert.equal(written.ok, true)
    const committed = await workRecord.appendBlogSquash(handle, session, 'squash-run', {
      bloggerSessionId: `blog-${session}`, requestId: 'squash-request',
      previousFrameEpoch: 0, nextFrameEpoch: 1, coveredFrameCount: 3,
      textRef: written.blobRef, textDigest: written.blobDigest,
    })
    assert.equal(committed.ok, true, committed.error)
    const bounded = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
    assert.equal(bounded, 'Recent work\nCURRENT_REASONING\nassistant: CURRENT_BODY')
    assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), bounded)
  })
})

for (const providerRun of [undefined, 'current-run']) {
  for (const readBefore of [false, true]) {
    for (const mutation of ['entry', 'squash']) {
      test(`WHAT[work-record-004] completed representation survives later ${mutation}, readBefore=${readBefore}, providerRun=${providerRun}`, async () => {
        await withReopenableJournal(async (handle, reopen) => {
          const session = `completed-record-${providerRun}-${readBefore}-${mutation}`
          await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
          await workRecord.captureProjection(handle, session, { messages: rangeMessages.slice(0, 4) })
          await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
          await commitFrame(handle, session, { from: 3, through: 5, body: 'CURRENT_REASONING_SUMMARY', id: 'current' })
          await workRecord.captureTerminalText(handle, session, 'CURRENT_BODY', 'current-run')
          const range = {
            StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 },
            ...(providerRun ? { ProviderRun: providerRun } : {}),
          }
          const expected = 'Chronicle\nCURRENT_REASONING_SUMMARY\n\nRecent work\nassistant: CURRENT_BODY'
          if (readBefore) assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, range), expected)
          if (mutation === 'entry') {
            await commitFrame(handle, session, { from: 5, through: 7, body: 'LATE_SUMMARY', id: 'late' })
          } else {
            await workRecord.captureProjection(handle, session, { messages: rangeMessages })
            await commitFrame(handle, session, { from: 5, through: 9, body: 'CURRENT_BODY NEXT_WORK', id: 'future' })
            const blob = await journal.JournalSurface_writePayload(handle, 'PRIOR_WORK CURRENT_BODY NEXT_WORK SQUASHED_SUMMARY')
            assert.equal(blob.ok, true)
            const squashed = await workRecord.appendBlogSquash(handle, session, 'squash-run', {
              bloggerSessionId: `blog-${session}`, requestId: 'squash-request',
              previousFrameEpoch: 0, nextFrameEpoch: 1, coveredFrameCount: 3,
              textRef: blob.blobRef, textDigest: blob.blobDigest,
            })
            assert.equal(squashed.ok, true, squashed.error)
            await workRecord.captureTerminalText(handle, session, 'NEXT_FINAL', 'next-run')
          }
          const live = await workRecord.lifecycleWorkRecord(handle, session, false)
          assert.match(live, mutation === 'entry' ? /LATE_SUMMARY/ : /SQUASHED_SUMMARY/)
          assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, range), expected)
          const recovered = await reopen()
          assert.equal(await workRecord.lifecycleWorkRecordBounded(recovered, session, range), expected)
          assert.equal(await workRecord.lifecycleWorkRecord(recovered, session, false), live)
        })
      })
    }
  }
}

test('WHAT[work-record-004] empty completion frames, duplicate terminals and same-frontier runs retain distinct historical choices', async () => {
  await withReopenableJournal(async (handle, reopen, directory) => {
    const session = 'completed-empty-frames'
    await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
    await workRecord.captureProjection(handle, session, { messages: rangeMessages.slice(0, 4) })
    await workRecord.captureTerminalText(handle, session, 'CURRENT_BODY', 'first-run')
    await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
    await commitFrame(handle, session, { from: 3, through: 5, body: 'LATE_SUMMARY', id: 'late' })
    const first = trace.terminalEvidenceForProviderRun('first-run', trace.snapshot(handle, session))
    const duplicate = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, null, {
      family: 'Companion', case: 'TerminalOutputCaptured', payload: {
        SessionId: session, ProviderRun: 'first-run', TextRef: first.textRef, TextDigest: first.textDigest,
      },
    })
    assert.equal(duplicate.ok, true, duplicate.error)
    await workRecord.captureTerminalText(handle, session, 'SECOND_FINAL', 'second-run')
    const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 } }
    const raw = 'Recent work\nCURRENT_REASONING\nassistant: CURRENT_BODY'
    const second = 'Chronicle\nLATE_SUMMARY\n\nRecent work\nassistant: CURRENT_BODY\nassistant: SECOND_FINAL'
    for (const [providerRun, expected] of [[undefined, raw], ['first-run', raw], ['second-run', second]]) {
      const query = { ...range, ...(providerRun ? { ProviderRun: providerRun } : {}) }
      assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, query), expected)
      assert.equal(readBoundedInNewProcess(directory, session, query), expected)
      handle = await reopen()
      assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, query), expected)
    }
    const wrongRun = await workRecord.lifecycleWorkRecordBounded(handle, session, { ...range, ProviderRun: 'missing-run' })
    assert.match(wrongRun, /LATE_SUMMARY/)
    assert.doesNotMatch(wrongRun, /SECOND_FINAL/)
    assert.match(await workRecord.lifecycleWorkRecordBounded(handle, session, {
      ...range, EndExclusive: { Sequence: 9 }, ProviderRun: 'first-run',
    }), /LATE_SUMMARY/)
  })
})

test('WHAT[work-record-004] a completed representation survives reanchor and a fresh invocation in another process', async () => {
  await withReopenableJournal(async (handle, reopen, directory) => {
    const session = 'completed-reanchor'
    await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
    await workRecord.captureProjection(handle, session, { messages: rangeMessages.slice(0, 4) })
    await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
    await commitFrame(handle, session, { from: 3, through: 5, body: 'CURRENT_REASONING_SUMMARY', id: 'current' })
    await workRecord.captureTerminalText(handle, session, 'CURRENT_BODY', 'current-run')
    const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 }, ProviderRun: 'current-run' }
    const expected = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
    assert.match(expected, /CURRENT_REASONING_SUMMARY/)
    const reanchored = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, null, {
      family: 'Context', case: 'ContextReanchored', payload: {
        SessionId: session, PreviousEpochId: 0, NextEpochId: 1, ObservedCompactionRun: 'compaction-run',
      },
    })
    assert.equal(reanchored.ok, true, JSON.stringify(reanchored.error))
    assert.equal(Number(journal.JournalSurface_snapshot(handle).sessionProjections[session].prefixEpoch.epochId), 1)
    const observed = await trace.captureObservedMessages(handle, session, [
      { hostMessageId: 'next-charge', message: { info: { id: 'next-charge', role: 'user' }, parts: [{ id: 'next-charge-text', type: 'text', text: 'NEXT_CHARGE' }] } },
      { hostMessageId: 'next-work', message: { info: { id: 'next-work', role: 'assistant' }, parts: [{ id: 'next-work-text', type: 'text', text: 'NEXT_WORK' }] } },
    ])
    assert.equal(observed.ok, true, JSON.stringify(observed.error))
    await workRecord.captureTerminalText(handle, session, 'NEXT_FINAL', 'next-run')
    const nextEnd = trace.terminalEvidenceForProviderRun('next-run', trace.snapshot(handle, session)).frontier.sequence
    const nextRange = { StartInclusive: { Sequence: 8 }, EndExclusive: { Sequence: nextEnd }, ProviderRun: 'next-run' }
    const next = await workRecord.lifecycleWorkRecordBounded(handle, session, nextRange)
    assert.match(next, /NEXT_WORK/)
    assert.match(next, /NEXT_FINAL/)
    assert.doesNotMatch(next, /PRIOR_|CURRENT_|NEXT_CHARGE/)
    assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, range), expected)
    assert.equal(readBoundedInNewProcess(directory, session, range), expected)
    assert.equal(readBoundedInNewProcess(directory, session, nextRange), next)
    assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), expected)
  })
})

test('WHAT[work-record-004] a corrupt frozen frame fails closed even after a valid squash replaces the live frames', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'completed-corrupt-frame'
    await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
    await workRecord.captureProjection(handle, session, { messages: rangeMessages.slice(0, 4) })
    await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
    const other = await journal.JournalSurface_writePayload(handle, 'different digest')
    assert.equal(other.ok, true)
    await commitFrame(handle, session, { from: 3, through: 5, body: 'CORRUPT_CURRENT_SUMMARY', textDigest: other.blobDigest, id: 'current' })
    await workRecord.captureTerminalText(handle, session, 'CURRENT_BODY', 'current-run')
    await workRecord.captureProjection(handle, session, { messages: rangeMessages })
    await commitFrame(handle, session, { from: 5, through: 9, body: 'FUTURE_SUMMARY', id: 'future' })
    const blob = await journal.JournalSurface_writePayload(handle, 'VALID_SQUASHED_SUMMARY')
    assert.equal(blob.ok, true)
    const squashed = await workRecord.appendBlogSquash(handle, session, 'squash-run', {
      bloggerSessionId: `blog-${session}`, requestId: 'squash-request',
      previousFrameEpoch: 0, nextFrameEpoch: 1, coveredFrameCount: 3,
      textRef: blob.blobRef, textDigest: blob.blobDigest,
    })
    assert.equal(squashed.ok, true, squashed.error)
    assert.match(await workRecord.lifecycleWorkRecord(handle, session, false), /VALID_SQUASHED_SUMMARY/)
    const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 }, ProviderRun: 'current-run' }
    assert.equal(await workRecord.lifecycleWorkRecordBounded(handle, session, range), null)
    assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), null)
  })
})

for (const corrupt of ['selected', 'crossing']) {
  test(`WHAT[work-record-004] ${corrupt} frame corruption obeys the bounded evidence boundary`, async () => {
    await withReopenableJournal(async (handle, reopen) => {
      const session = `record-range-corrupt-${corrupt}`
      await workRecord.captureOpening(handle, session, 'ROOT_CHARGE', [])
      await workRecord.captureProjection(handle, session, { messages: rangeMessages })
      if (corrupt === 'selected') {
        await commitFrame(handle, session, { from: 0, through: 3, body: 'PRIOR_SUMMARY', id: 'prior' })
      }
      const other = await journal.JournalSurface_writePayload(handle, 'a different verified payload')
      assert.equal(other.ok, true)
      await commitFrame(handle, session, {
        from: corrupt === 'selected' ? 3 : 0, through: 5,
        body: 'untrusted frame', id: 'corrupt', textDigest: other.blobDigest,
      })
      const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 8 } }
      const bounded = await workRecord.lifecycleWorkRecordBounded(handle, session, range)
      if (corrupt === 'selected') assert.equal(bounded, null)
      else assert.equal(bounded, 'Recent work\nCURRENT_REASONING\nassistant: CURRENT_BODY')
      assert.equal(await workRecord.lifecycleWorkRecordBounded(await reopen(), session, range), bounded)
    })
  })
}

const { withCausalHistory, causalRange, causalSession } = await import('./support/causal-history.mjs')

test('WHAT[work-record-004] a completed Chronicle excludes a later concurrent writer entry after cold replay', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const completed = history.read()
    assert.equal(completed.terminal.frontier.sequence, 4)
    assert.equal(completed.record, 'Chronicle\nORIGINAL_SUMMARY\n\nRecent work\nORIGINAL_REASONING\nassistant: FORMAL_RESULT')
    await history.append('late-entry', 'late-writer', 0x10000000, [history.base])
    const recovered = history.read()
    assert.match(recovered.live, /LATE_ENTRY/)
    assert.equal(recovered.terminal.frontier.sequence, 4)
    assert.equal(recovered.record, completed.record)
    assert.equal(history.read().record, completed.record)
  })
})

test('WHAT[work-record-004] a completed frontier excludes later concurrent writer originals after cold replay', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const completed = history.read()
    await history.append('late-original', 'late-writer', 0x10000000, [history.base])
    const recovered = history.read()
    assert.equal(recovered.head.sequence, 5)
    assert.match(recovered.live, /LATE_ORIGINAL/)
    assert.equal(recovered.terminal.frontier.sequence, completed.terminal.frontier.sequence)
    assert.equal(recovered.record, completed.record)
    assert.equal(history.read(causalRange).record, completed.record)
  })
})

for (const [mutation, steps] of [
  ['partial-squash', ['late-entry', 'partial-squash']],
  ['full-squash', ['late-entry', 'partial-squash', 'full-squash']],
  ['nested-squash', ['late-entry', 'partial-squash', 'full-squash', 'late-tip', 'nested-squash']],
]) {
  test(`WHAT[work-record-004] completed bytes exclude concurrent writer ${mutation} ancestry after cold replay`, async () => {
    await withCausalHistory(async history => {
      await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
      const completed = history.read()
      const genericRange = { StartInclusive: causalRange.StartInclusive, EndExclusive: causalRange.EndExclusive }
      const generic = history.read(genericRange).record
      let parent = history.base
      for (let index = 0; index < steps.length; index += 1) {
        parent = await history.append(steps[index], 'late-writer', 0x10000000 + index, [parent])
      }
      const recovered = history.read()
      assert.match(recovered.live, new RegExp(mutation.toUpperCase().replaceAll('-', '_')))
      assert.equal(recovered.terminal.frontier.sequence, 4)
      assert.equal(recovered.record, completed.record)
      assert.equal(history.read(genericRange).record, generic)
      assert.equal(history.read().record, completed.record)
    })
  })
}

test('WHAT[work-record-004] a new Terminal sees all merged heads and retains them across later concurrent originals', async () => {
  await withCausalHistory(async history => {
    const terminal = await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const first = history.read()
    const late = await history.append('late-entry', 'late-writer', 0x10000000, [history.base])
    await history.append('completed-b', 'merged-writer', 0xff000000, [terminal, late])
    const secondRange = { ...causalRange, ProviderRun: 'completed-b' }
    const second = history.read(secondRange)
    assert.equal(second.terminal.frontier.sequence, 4)
    assert.match(second.record, /LATE_ENTRY/)
    assert.match(second.record, /SECOND_FINAL/)
    await history.append('late-original', 'original-writer', 0x11000000, [late])
    const recovered = history.read(secondRange)
    assert.equal(recovered.head.sequence, 5)
    assert.equal(recovered.terminal.frontier.sequence, 4)
    assert.equal(recovered.record, second.record)
    assert.equal(history.read().record, first.record)
    assert.equal(history.read(secondRange).record, second.record)
  })
})

test('WHAT[work-record-004] an ordered duplicate terminal keeps its first representation after a later Entry', async () => {
  await withCausalHistory(async history => {
    const terminal = await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const first = history.read()
    const late = await history.append('late-entry', 'late-writer', 0x10000000, [terminal])
    await history.append('completed-a', 'duplicate-writer', 0x11000000, [late])
    const recovered = history.read()
    assert.match(recovered.live, /LATE_ENTRY/)
    assert.equal(recovered.terminal.frontier.sequence, 4)
    assert.equal(recovered.record, first.record)
  })
})

for (const [conflict, event] of [['frontier', 'late-original'], ['Chronicle', 'late-entry']]) {
  test(`WHAT[work-record-004] concurrent same-run terminals fail closed when their ${conflict} evidence differs`, async () => {
    await withCausalHistory(async history => {
      await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
      const first = history.read()
      assert.equal(typeof first.record, 'string')
      const late = await history.append(event, 'late-writer', 0x10000000, [history.base])
      await history.append('completed-a', 'duplicate-writer', 0x11000000, [late])
      assert.equal(history.read().record, null)
      assert.equal(history.read().record, null)
    })
  })
}

test('WHAT[work-record-004] concurrent same-frontier runs require a run identity when their Chronicle choices differ', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const first = history.read()
    const late = await history.append('late-entry', 'late-writer', 0x10000000, [history.base])
    await history.append('completed-b', 'other-terminal-writer', 0x11000000, [late])
    const secondRange = { ...causalRange, ProviderRun: 'completed-b' }
    const second = history.read(secondRange)
    assert.equal(second.terminal.frontier.sequence, 4)
    assert.match(second.record, /LATE_ENTRY/)
    assert.match(second.record, /SECOND_FINAL/)
    assert.equal(history.read({ StartInclusive: causalRange.StartInclusive, EndExclusive: causalRange.EndExclusive }).record, null)
    assert.equal(history.read().record, first.record)
    assert.equal(history.read(secondRange).record, second.record)
  })
})

test('WHAT[work-record-004] concurrent identical same-run terminals retain their shared completion evidence', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const first = history.read()
    await history.append('completed-a', 'duplicate-writer', 0x10000000, [history.base])
    const recovered = history.read()
    assert.equal(recovered.terminal.frontier.sequence, 4)
    assert.equal(recovered.record, first.record)
  })
})

test('WHAT[work-record-004] concurrent same-frontier runs share a generic view when their Chronicle evidence agrees', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const first = history.read()
    const genericRange = { StartInclusive: causalRange.StartInclusive, EndExclusive: causalRange.EndExclusive }
    const generic = history.read(genericRange).record
    await history.append('completed-b', 'other-terminal-writer', 0x10000000, [history.base])
    assert.equal(history.read(genericRange).record, generic)
    assert.equal(history.read().record, first.record)
    assert.match(history.read({ ...causalRange, ProviderRun: 'completed-b' }).record, /SECOND_FINAL/)
  })
})

test('WHAT[work-record-004] a refreshed Journal writer includes every session head in its new Terminal parents', async () => {
  await withCausalHistory(async history => {
    const terminal = await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const late = await history.append('late-entry', 'late-writer', 0x10000000, [history.base])
    assert.deepEqual(new Set(history.headState().heads), new Set([terminal, late]))
    await history.withJournal(async handle => {
      assert.match(await workRecord.lifecycleWorkRecord(handle, causalSession, false), /LATE_ENTRY/)
      await workRecord.captureTerminalText(handle, causalSession, 'MERGED_FINAL', 'merged-live-run')
      const captured = history.headState()
      assert.equal(captured.heads.length, 1)
      assert.deepEqual(new Set(captured.event.parents), new Set([terminal, late]))
      const query = { ...causalRange, ProviderRun: 'merged-live-run' }
      const warm = await workRecord.lifecycleWorkRecordBounded(handle, causalSession, query)
      assert.match(warm, /LATE_ENTRY/)
      assert.match(warm, /MERGED_FINAL/)
      assert.equal(history.read(query).record, warm)
    })
  })
})

for (const [missing, parentCursors] of [
  ['Opening ancestry', { 1: [] }],
  ['an original below the frontier', { 3: [1] }],
]) {
  test(`WHAT[work-record-004] a Terminal with ${missing} missing fails closed without borrowing the current view`, async () => {
    await withCausalHistory(async history => {
      await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
      const recovered = history.read()
      assert.match(recovered.live, /ORIGINAL_SUMMARY/)
      assert.equal(recovered.record, null)
      assert.equal(history.read({ StartInclusive: causalRange.StartInclusive, EndExclusive: causalRange.EndExclusive }).record, null)
      assert.equal(history.read().record, null)
    }, { parentCursors })
  })
}

test('WHAT[work-record-004] a Terminal that knows only a second identical Opening has valid zero-frontier evidence', async () => {
  await withCausalHistory(async history => {
    const opening = await history.append('opening', 'opening-writer', 0x10000000, [])
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [opening])
    const query = { StartInclusive: { Sequence: 0 }, EndExclusive: { Sequence: 0 }, ProviderRun: 'completed-a' }
    const recovered = history.read(query)
    assert.equal(recovered.head.sequence, 4)
    assert.equal(recovered.terminal.frontier.sequence, 0)
    assert.equal(recovered.record, 'Recent work\nassistant: FORMAL_RESULT')
    assert.equal(history.read(query).record, recovered.record)
  })
})

test('WHAT[work-record-004] a formal-only completion without semantic parts retains its zero frontier after cold replay', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const query = { StartInclusive: { Sequence: 0 }, EndExclusive: { Sequence: 0 }, ProviderRun: 'completed-a' }
    const recovered = history.read(query)
    assert.equal(recovered.head.sequence, 0)
    assert.equal(recovered.terminal.frontier.sequence, 0)
    assert.equal(recovered.record, 'Recent work\nassistant: FORMAL_RESULT')
    assert.equal(history.read(query).record, recovered.record)
  }, { openingOnly: true })
})

test('WHAT[work-record-004] a causally selected later frame cannot skip originals represented only by an unseen frame', async () => {
  await withCausalHistory(async history => {
    await history.append('disconnected-first', 'first-frame-writer', 0x10000000, [history.base])
    const second = await history.append('disconnected-second', 'second-frame-writer', 0x11000000, [history.base])
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [second])
    const recovered = history.read()
    assert.match(recovered.live, /DISCONNECTED_FIRST/)
    assert.match(recovered.live, /DISCONNECTED_SECOND/)
    assert.equal(recovered.terminal.frontier.sequence, 4)
    assert.equal(recovered.record, 'Recent work\nORIGINAL_REASONING\nassistant: FORMAL_RESULT')
    assert.equal(history.read().record, recovered.record)
  }, { rawOnly: true, gapFrames: true })
})

test('WHAT[work-record-004] an old unproven Terminal does not block a later unfinished generic range', async () => {
  await withCausalHistory(async history => {
    const terminal = await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    assert.equal(history.read().record, null)
    const late = await history.append('late-original', 'new-work-writer', 0xff000000, [terminal, history.opening])
    const range = { StartInclusive: { Sequence: 4 }, EndExclusive: { Sequence: 5 } }
    const recovered = history.read(range)
    assert.equal(recovered.head.sequence, 5)
    assert.equal(recovered.record, 'Recent work\nassistant: LATE_ORIGINAL')
    assert.equal(history.read(range).record, recovered.record)
    await history.append('completed-a', 'ordered-replay-writer', 0xff100000, [late])
    assert.equal(history.read(range).record, recovered.record)
  }, { parentCursors: { 1: [] } })
})

test('WHAT[work-record-004] a concurrent completion can prove its material without knowing another Terminal shortcut', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    const first = history.read()
    const part = await history.append('late-original', 'new-work-writer', 0x10000000, [history.base])
    await history.append('completed-b', 'other-terminal-writer', 0x11000000, [part])
    const second = history.read({ ...causalRange, EndExclusive: { Sequence: 5 }, ProviderRun: 'completed-b' })
    assert.equal(second.terminal.frontier.sequence, 5)
    assert.match(second.record, /LATE_ORIGINAL/)
    assert.match(second.record, /SECOND_FINAL/)
    assert.equal(history.read().record, first.record)
  })
})

test('WHAT[work-record-004] a standalone Trace Terminal keeps its zero frontier when a concurrent Opening arrives later', async () => {
  await withCausalHistory(async history => {
    await history.append('completed-a', 'terminal-writer', 0xf0000000, [])
    const range = { StartInclusive: { Sequence: 0 }, EndExclusive: { Sequence: 0 }, ProviderRun: 'completed-a' }
    const before = history.read(range)
    assert.equal(before.terminal?.frontier.sequence, 0)
    assert.equal(before.record, null)
    await history.append('opening', 'opening-writer', 0x10000000, [])
    const recovered = history.read(range)
    assert.equal(recovered.terminal?.frontier.sequence, 0)
    assert.equal(recovered.record, null)
    assert.equal(history.read(range).terminal?.frontier.sequence, 0)
  }, { emptyBase: true })
})

test('WHAT[work-record-004] concurrent identical completions keep current views for a later mismatched endpoint', async () => {
  await withCausalHistory(async history => {
    const terminal = await history.append('completed-a', 'terminal-writer', 0xf0000000, [history.base])
    await history.append('late-original', 'new-work-writer', 0xff000000, [terminal])
    const range = { ...causalRange, EndExclusive: { Sequence: 5 } }
    const before = history.read(range)
    assert.equal(before.terminal.frontier.sequence, 4)
    assert.match(before.record, /LATE_ORIGINAL/)
    await history.append('completed-a', 'concurrent-replay-writer', 0x10000000, [history.base])
    const recovered = history.read(range)
    assert.equal(recovered.terminal.frontier.sequence, 4)
    assert.equal(recovered.record, before.record)
    assert.equal(history.read(range).record, before.record)
  })
})
