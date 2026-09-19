import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/Surface.js'

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
    await commitY(handle, { from: 2, to: 4, body: 'SECOND_CHRONICLE', n: 12 })
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
