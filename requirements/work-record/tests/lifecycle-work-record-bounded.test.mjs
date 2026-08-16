// COMPANION-015 / EXEC-031 — bounded inspect/LWR Chronicle must not leak
// prior-invocation Y frames. TRACE was already sliced; Chronicle is sliced by
// coverage-interval overlap with the invocation range.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/Surface.js'

const SEM = 'ses_bounded_lwr'

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'lwr-bounded-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_bounded_lwr', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
}

const lastSequence = (trace) => trace.lastSequence

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

test('WHAT[WORK-RECORD-002] COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames', async () => {
  await withJournal(async (journal) => {
    const { s1, s2, inv1Through, inv2Through } = await seedTwoInvocations(journal)
    // CURRENT Y through == inv2 StartInclusive. Inclusive-through keeps it;
    // treating Next as exclusive (through > Start) would drop CURRENT_Y_INV2.
    assert.equal(s1, inv1Through + 1, 'CURRENT Y through must equal StartInclusive')
    await commitY(journal, { from: inv1Through, to: s1, body: 'CURRENT_Y_INV2', n: 2 })

    const full = await workRecord.lifecycleWorkRecord(journal, SEM, false)
    assert.equal(typeof full, 'string')
    assert.match(full, /PRIOR_Y_INV1/, 'unbounded Chronicle still holds the prior Y frame')
    assert.match(full, /CURRENT_Y_INV2/)

    const bounded = await workRecord.lifecycleWorkRecordBounded(journal, SEM, {
      StartInclusive: { Sequence: s1 },
      EndExclusive: { Sequence: s2 },
    })
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /Chronicle\nCURRENT_Y_INV2/)
    assert.doesNotMatch(bounded, /PRIOR_Y_INV1/)
    assert.doesNotMatch(bounded, /inv1 work/)
    assert.match(bounded, /inv2 work/)
    assert.doesNotMatch(bounded, /^Opening\n/m)
  })
})

test('WHAT[WORK-RECORD-016] COMPANION_015_bounded_review_consumes_request_range_not_session_head', async () => {
  await withJournal(async (journal) => {
    const { s1, s2, inv1Through } = await seedTwoInvocations(journal)
    await commitY(journal, { from: inv1Through, to: s1, body: 'CURRENT_Y_INV2', n: 2 })

    // REVIEW-016 / GLORY-004：process review / Finality / SyncDelegate 消费的 LWR
    // 一律 request-range bounded。同一 bounded 渲染不得混入 session head（prior invocation）。
    const bounded = await workRecord.lifecycleWorkRecordBounded(journal, SEM, {
      StartInclusive: { Sequence: s1 },
      EndExclusive: { Sequence: s2 },
    })
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /Chronicle\nCURRENT_Y_INV2/)
    assert.doesNotMatch(bounded, /PRIOR_Y_INV1/)
    assert.doesNotMatch(bounded, /inv1 work/)
    assert.match(bounded, /inv2 work/)
  })
})

test('WHAT[WORK-RECORD-004] COMPANION_015_bounded_chronicle_heading_omitted_when_invocation_has_no_y', async () => {
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
