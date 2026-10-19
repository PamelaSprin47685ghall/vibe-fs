import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as events from '../../../../dist/Persistence/EventStore/Surface.js'
import * as facts from '../../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as record from '../../../../dist/Mission/WorkRecord/Surface.js'
import * as trace from '../../../../dist/Context/Trace/SemanticTraceSurface.js'

export const causalSession = 'causal-completed-record'
export const causalRun = 'completed-a'
export const causalRange = { StartInclusive: { Sequence: 0 }, EndExclusive: { Sequence: 4 }, ProviderRun: causalRun }

async function boot(directory, runtime) {
  const opened = await journal.JournalSurface_boot(directory, runtime, process.pid, new Date().toISOString())
  assert.equal(opened.ok, true, JSON.stringify(opened.error))
  return opened.journal
}

async function summary(handle, name, from, through, frameEpoch = 0) {
  const blob = await journal.JournalSurface_writePayload(handle, name.toUpperCase().replaceAll('-', '_'))
  assert.equal(blob.ok, true)
  const appended = await record.appendBlogObservation(handle, causalSession, name, {
    bloggerSessionId: 'causal-blog', requestId: name, frameEpoch,
    previousIngestedThroughSequence: from, nextIngestedThroughSequence: through,
    previousCoverableTurnCutoffExclusive: 0, nextCoverableTurnCutoffExclusive: 0,
    nextCoveredPrefixDigest: '', textRef: blob.blobRef, textDigest: blob.blobDigest,
    toolCallIds: [], tipName: name, observedPrefixEpoch: 0,
  })
  assert.equal(appended.ok, true, JSON.stringify(appended.error))
}

async function seedTemplates(directory, gapFrames) {
  const handle = await boot(directory, 'causal-seed-runtime')
  const messages = [
    { role: 'user', parts: [{ kind: 'text', text: 'CALLER_CHARGE' }] },
    { role: 'assistant', parts: [{ kind: 'reasoning', text: 'ORIGINAL_REASONING' }, { kind: 'text', text: 'FORMAL_RESULT' }] },
  ]
  try {
    await record.captureOpening(handle, causalSession, 'CALLER_CHARGE', [])
    const captured = await record.captureProjection(handle, causalSession, { messages })
    assert.equal(captured.currentHeadSequence, 4)
    if (gapFrames) {
      await summary(handle, 'disconnected-first', 0, 2)
      await summary(handle, 'disconnected-second', 2, 3)
      await record.captureTerminalText(handle, causalSession, 'FORMAL_RESULT', causalRun)
    } else {
      await summary(handle, 'caller-summary', 0, 1)
      await summary(handle, 'original-summary', 1, 2)
      await record.captureTerminalText(handle, causalSession, 'FORMAL_RESULT', causalRun)
      await summary(handle, 'late-entry', 2, 3)
      await summary(handle, 'late-tip', 3, 4)
      await record.captureProjection(handle, causalSession, { messages: [
        ...messages, { role: 'assistant', parts: [{ kind: 'text', text: 'LATE_ORIGINAL' }] },
      ] })
      await record.captureTerminalText(handle, causalSession, 'SECOND_FINAL', 'completed-b')
    }
  } finally {
    journal.JournalSurface_dispose(handle)
  }
  const store = events.create(directory, 'causal-seed-reader')
  try {
    const found = new Map()
    const pending = events.streams(store).flatMap(stream => events.heads(store, stream))
    while (pending.length) {
      const id = pending.pop()
      if (found.has(id)) continue
      const event = events.read(store, id)
      found.set(id, event)
      pending.push(...event.parents)
    }
    const ordered = [...found.values()].sort((left, right) => Number(left.payload.LocalSeq[1]) - Number(right.payload.LocalSeq[1]))
    const terminalIndex = ordered.findIndex(event => event.payload.ProviderRun?.[1] === causalRun)
    assert.ok(terminalIndex > 0)
    const named = new Map(ordered.map(event => [
      event.payload.ProviderRun?.[1] ?? 'late-original', event,
    ]))
    for (const event of ordered) {
      const decoded = facts.decode(JSON.stringify(event.payload.Fact))
      assert.equal(decoded.ok, true, JSON.stringify(decoded.error))
      event.payload.Fact = JSON.parse(decoded.line)
      event.payloads = Object.fromEntries(await Promise.all(event.payloadRefs.map(async reference => {
        const body = await events.readPayload(store, reference)
        assert.ok(body)
        return [reference, Buffer.from(body).toString('base64')]
      })))
    }
    return { base: ordered.slice(0, terminalIndex), named }
  } finally {
    events.dispose(store)
  }
}

const eventId = order => `${order.toString(16).padStart(8, '0')}-0000-0000-0000-000000000000`

export async function withCausalHistory(run, { parentCursors = {}, openingOnly = false, rawOnly = false, gapFrames = false, emptyBase = false } = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'completed-causal-history-'))
  const stores = new Map()
  try {
    const templates = await seedTemplates(join(directory, 'seed'), gapFrames)
    const opening = templates.base.find(event => event.payload.Fact?.[1]?.[1]?.[0] === 'OpeningPromptCaptured')
    assert.ok(opening)
    templates.named.set('opening', opening)
    const replay = join(directory, 'replay')
    const sequences = new Map()
    const append = async (template, writer, order, parents) => {
      let store = stores.get(writer)
      if (!store) {
        store = events.create(replay, writer)
        stores.set(writer, store)
      }
      const id = eventId(order)
      const sequence = (sequences.get(writer) ?? 0) + 1
      sequences.set(writer, sequence)
      const payload = { ...template.payload, EventId: ['EventId', id], RuntimeId: ['RuntimeId', writer],
        LocalSeq: ['LocalSeq', String(sequence)], ObservedAt: new Date().toISOString() }
      const appended = await events.append(store, [{ ...template, id, parents, payload }])
      assert.equal(appended.ok, true, JSON.stringify(appended.error))
      assert.deepEqual(appended.cuts, [])
      return id
    }
    let base
    let openingEvent
    const cursorEvents = new Map()
    for (let index = 0; index < templates.base.length && !emptyBase; index += 1) {
      const template = templates.base[index]
      if (rawOnly && template.payload.Fact?.[1]?.[1]?.[0] === 'BlogObservationCommitted') break
      const cursor = template.payload.Fact?.[1]?.[1]?.[1]?.CursorSequence
      const parents = Object.hasOwn(parentCursors, cursor)
        ? parentCursors[cursor].map(value => {
          const id = cursorEvents.get(value)
          assert.ok(id, `missing parent cursor ${value}`)
          return id
        })
        : base ? [base] : []
      base = await append(template, 'base-writer', index + 1, parents)
      if (template === opening) openingEvent = base
      if (cursor !== undefined) cursorEvents.set(Number(cursor), base)
      if (openingOnly && template === opening) break
    }
    const headState = () => {
      const store = events.create(replay, 'causal-head-reader')
      try {
        const heads = events.heads(store, `journal/session/${causalSession}`)
        return { heads, event: heads.length === 1 ? events.read(store, heads[0]) : null }
      } finally {
        events.dispose(store)
      }
    }
    await run({ directory: replay, base, opening: openingEvent,
      append: (name, writer, order, parents) => {
        const template = templates.named.get(name)
        assert.ok(template, `missing event template ${name}`)
        return append(template, writer, order, parents)
      },
      headState,
      withJournal: async observe => {
        const handle = await boot(replay, 'causal-live-writer')
        try {
          return await observe(handle)
        } finally {
          journal.JournalSurface_dispose(handle)
        }
      },
      read: (range = causalRange) => JSON.parse(execFileSync(process.execPath, [
        fileURLToPath(import.meta.url), '--read', replay, JSON.stringify(range),
      ], { encoding: 'utf8', env: { ...process.env, WXS_E2E_QUIET: '1' } })),
    })
  } finally {
    for (const store of stores.values()) events.dispose(store)
    rmSync(directory, { recursive: true, force: true })
  }
}

if (process.argv[2] === '--read') {
  const handle = await boot(process.argv[3], 'causal-cold-reader')
  try {
    const range = JSON.parse(process.argv[4])
    const projection = trace.snapshot(handle, causalSession)
    process.stdout.write(JSON.stringify({
      terminal: trace.terminalEvidenceForProviderRun(range.ProviderRun ?? causalRun, projection),
      head: trace.headCursor(projection),
      record: await record.lifecycleWorkRecordBounded(handle, causalSession, range),
      live: await record.lifecycleWorkRecord(handle, causalSession, false),
    }))
  } finally {
    journal.JournalSurface_dispose(handle)
  }
}
