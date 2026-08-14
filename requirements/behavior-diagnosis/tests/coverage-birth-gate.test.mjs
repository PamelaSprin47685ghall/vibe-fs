/**
 * ENFORCER-045 / PERSIST-010 — coverage birth gate.
 *
 * Known handleable failure: Next≤Prev or unmapped NextCursor.
 * → refuse at mainContextFromChunk (None), never Start a BloggerMain window.
 * Unknown escapes still hit commit-path Diagnostic.fatal (君子不立危墙).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  agentJournal,
  bloggerDelta,
  blogProjection,
  sessionId,
  xTraceCapture,
  listItems,
  caseOf,
} from '../../../tests/unit/support/domain.mjs'

const { mainContextFromChunk } = await import('../../../dist/Session/EnforcerHost.js')
const { XTraceProjection_empty, XTraceProjection_semanticCursorFor, XTraceProjection_parts: xTraceParts } = await import(
  '../../../dist/Journal/XTraceProjection.js'
)
const { PrefixEpochIdModule_initial } = await import('../../../dist/Kernel/Identity.js')

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'coverage-birth-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const MAIN = sessionId('ses-main')
const BLOG = sessionId('ses-blog')

const semantic = (messages) => xTraceCapture.semantic({ messages })

test('ENFORCER_045_mainContext_refuses_when_next_sequence_cannot_advance', async () => {
  await withJournal(async (journal) => {
    // Two turns captured → sequences 1,2. Coverage already at head (2).
    const messages = [
      { role: 'user', parts: [xTraceCapture.text('task')] },
      { role: 'assistant', parts: [xTraceCapture.text('work')] },
    ]
    const xTrace = await xTraceCapture.captureProjection(journal, MAIN, semantic(messages))
    assert.equal(listItems(xTraceParts(xTrace)).length, 2)
    const headSeq = Number(listItems(xTraceParts(xTrace)).at(-1).Cursor.Sequence)
    assert.equal(headSeq, 2)

    // Force RecordCoverage to the head: nothing left can strictly advance.
    let blog = blogProjection.empty
    const entry = blogProjection.applyEntry(
      {
        epoch: 0,
        previous: 0,
        next: headSeq,
        previousCutoff: 0,
        nextCutoff: 2,
        digest: 'covered-all',
        frame: blogProjection.frame({ kind: 'Entry', digest: 'sha-e1', ref: 'blob-e1' }),
      },
      blog,
    )
    assert.equal(entry.ok, true, entry.ok ? '' : entry.error)
    blog = entry.value
    assert.equal(blogProjection.coverage(blog).ingestedThroughSequence, headSeq)

    const projection = semantic(messages)

    // Force a NextCursor that maps onto already-covered head → Next≤Prev.
    const forced = {
      Items: [],
      Toml: 'stale-window',
      NextCursor: { TurnIndex: 2, PartIndex: 0 },
      NextCoverableTurnCutoffExclusive: 2,
    }

    const refused = mainContextFromChunk(
      MAIN,
      BLOG,
      PrefixEpochIdModule_initial,
      blog,
      xTrace,
      projection,
      forced,
    )
    assert.equal(refused, undefined, 'Next covering already-ingested head must refuse birth')
  })
})

test('ENFORCER_045_mainContext_refuses_unmapped_next_cursor', async () => {
  await withJournal(async (journal) => {
    const messages = [{ role: 'user', parts: [xTraceCapture.text('only')] }]
    const xTrace = await xTraceCapture.captureProjection(journal, MAIN, semantic(messages))
    assert.equal(listItems(xTraceParts(xTrace)).length, 1)

    const blog = blogProjection.empty
    const projection = semantic(messages)

    // NextCursor past any known part with empty tryFindBack → formerly defaulted to 0.
    // Prev=0 and Next=0 is still Next≤Prev → refuse.
    const chunk = {
      Items: [],
      Toml: 'ghost',
      NextCursor: { TurnIndex: 99, PartIndex: 0 },
      NextCoverableTurnCutoffExclusive: 0,
    }

    // empty Parts → lastCoveredSequence None → refuse.
    const emptyTrace = XTraceProjection_empty
    const refusedEmpty = mainContextFromChunk(
      MAIN,
      BLOG,
      PrefixEpochIdModule_initial,
      blog,
      emptyTrace,
      projection,
      chunk,
    )
    assert.equal(refusedEmpty, undefined, 'empty XTrace cannot stage coverage advance')

    // Prev>0 + mapping miss (cursor before every current-gen part) → None, never Next=0.
    const covered = blogProjection.applyEntry(
      {
        epoch: 0,
        previous: 0,
        next: 1,
        previousCutoff: 0,
        nextCutoff: 1,
        digest: 'd1',
        frame: blogProjection.frame({ kind: 'Entry', digest: 'sha-e1', ref: 'blob-e1' }),
      },
      blogProjection.empty,
    )
    assert.equal(covered.ok, true)
    const miss = {
      Items: [],
      Toml: 'miss',
      NextCursor: { TurnIndex: 0, PartIndex: 0 },
      NextCoverableTurnCutoffExclusive: 0,
    }
    const refusedMiss = mainContextFromChunk(
      MAIN,
      BLOG,
      PrefixEpochIdModule_initial,
      covered.value,
      xTrace,
      projection,
      miss,
    )
    assert.equal(refusedMiss, undefined, 'mapping miss with Prev>0 must not invent Next=0')
  })
})

test('ENFORCER_045_mainContext_accepts_strict_advance', async () => {
  await withJournal(async (journal) => {
    const messages = [
      { role: 'user', parts: [xTraceCapture.text('task')] },
      { role: 'assistant', parts: [xTraceCapture.text('work')] },
    ]
    const xTrace = await xTraceCapture.captureProjection(journal, MAIN, semantic(messages))
    const projection = semantic(messages)
    const blog = blogProjection.empty
    const ingestCursor = XTraceProjection_semanticCursorFor(0n, xTrace)
    const chunk = bloggerDelta.nextChunk({
      limit: bloggerDelta.limitBytes,
      cursor: { TurnIndex: ingestCursor.TurnIndex, PartIndex: ingestCursor.PartIndex },
      previousCutoff: 0,
      messages: projection.Messages,
    })
    assert.notEqual(chunk, undefined, 'fresh head must yield a chunk')

    const raw = {
      Items: [],
      Toml: chunk.toml,
      NextCursor: { TurnIndex: chunk.nextCursor.turn, PartIndex: chunk.nextCursor.part },
      NextCoverableTurnCutoffExclusive: chunk.nextCutoff,
    }
    const ctx = mainContextFromChunk(MAIN, BLOG, PrefixEpochIdModule_initial, blog, xTrace, projection, raw)
    assert.notEqual(ctx, undefined)
    assert.equal(caseOf(ctx), 'Main')
    const main = ctx.fields[0]
    assert.equal(Number(main.PreviousIngestedThroughSequence), 0)
    assert.ok(Number(main.NextIngestedThroughSequence) > 0, 'Next must strictly exceed Prev')
  })
})
