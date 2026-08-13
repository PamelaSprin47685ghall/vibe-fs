// COMPANION-015 / EXEC-031 — bounded inspect/LWR Chronicle must not leak
// prior-invocation Y frames. TRACE was already sliced; Chronicle is sliced by
// coverage-interval overlap with the invocation range.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  agentFact,
  agentJournal,
  bloggerRequestId,
  frameEpochId,
  lifecycleWorkRecordProjection,
  listItems,
  prefixEpochId,
  providerRun,
  sessionId,
  stream,
  xTraceCapture,
} from '../support/domain.mjs'

const SEM = sessionId('ses_bounded_lwr')

const withJournal = (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'lwr-bounded-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return fn(created.journal)
  } finally {
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const lastSequence = (trace) => Number(listItems(trace.Parts).at(-1).Cursor.Sequence)

const commitY = (journal, { from, to, body, n }) => {
  const written = agentJournal.writeBlob(body, journal)
  assert.equal(written.ok, true, written.ok ? '' : JSON.stringify(written.error))
  const run = `msg_y${n}`
  const result = agentJournal.appendAgent(
    stream.session(SEM),
    providerRun(run),
    agentFact('BlogObservationCommitted', {
      SessionId: SEM,
      BloggerSessionId: sessionId('ses_blogger'),
      RequestId: bloggerRequestId(`req-y${n}`),
      FrameEpochId: frameEpochId(0),
      PreviousIngestedThroughSequence: BigInt(from),
      NextIngestedThroughSequence: BigInt(to),
      PreviousCoverableTurnCutoffExclusive: 0,
      NextCoverableTurnCutoffExclusive: 0,
      NextCoveredPrefixDigest: '',
      TextRef: written.value.BlobRef,
      TextDigest: written.value.BlobDigest,
      ProviderRun: providerRun(run),
      ToolCallIds: [],
      TipRuleId: `tip-y${n}`,
      FieldNameAtCommit: `field-y${n}`,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }),
    journal,
  )
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}

test('COMPANION_015_bounded_chronicle_excludes_prior_invocation_y_frames', () => {
  withJournal((journal) => {
    xTraceCapture.captureOpening(journal, SEM, 'first charge', [])

    const inv1 = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('first charge')] },
          { role: 'assistant', parts: [xTraceCapture.text('inv1 work')] },
        ],
      }),
    )
    const inv1Through = lastSequence(inv1)
    const inv2Start = inv1Through + 1
    commitY(journal, { from: 0, to: inv1Through, body: 'PRIOR_Y_INV1', n: 1 })

    const inv2 = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('first charge')] },
          { role: 'assistant', parts: [xTraceCapture.text('inv1 work')] },
          { role: 'user', parts: [xTraceCapture.text('second charge')] },
          { role: 'assistant', parts: [xTraceCapture.text('inv2 work')] },
        ],
      }),
    )
    const inv2Through = lastSequence(inv2)
    const inv2End = inv2Through + 1
    commitY(journal, { from: inv1Through, to: inv2Through, body: 'CURRENT_Y_INV2', n: 2 })

    const full = lifecycleWorkRecordProjection.lifecycleWorkRecord(journal, SEM, false)
    assert.equal(typeof full, 'string')
    assert.match(full, /PRIOR_Y_INV1/, 'unbounded Chronicle still holds the prior Y frame')
    assert.match(full, /CURRENT_Y_INV2/)

    const bounded = lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(journal, SEM, {
      StartInclusive: { Sequence: inv2Start },
      EndExclusive: { Sequence: inv2End },
    })
    assert.equal(typeof bounded, 'string')
    assert.match(bounded, /Chronicle\nCURRENT_Y_INV2/)
    assert.doesNotMatch(bounded, /PRIOR_Y_INV1/)
    assert.doesNotMatch(bounded, /inv1 work/)
    assert.match(bounded, /inv2 work/)
    assert.doesNotMatch(bounded, /^Opening\n/m)
  })
})
