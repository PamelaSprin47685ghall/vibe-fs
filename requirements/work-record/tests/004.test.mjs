import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/Surface.js'
import { commitFrame, withReopenableJournal } from './support/record.mjs'

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

test('WHAT[work-record-004] a later crossing frame cannot expand a completed range or hide its original suffix', async () => {
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

test.todo('WHAT[work-record-004] later squash preserves frozen historical representation bytes; GAP-111: bounded originals are complete, but current frames alone do not freeze the earlier rendered summary')

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
