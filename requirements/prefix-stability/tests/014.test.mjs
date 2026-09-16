import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as providerCodec from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'

// H13: HOST-013 anchored replay — PREFIX LAW, placement idempotence, restart
// byte-equality, fail-closed anchors, prior-tip isolation, N-round property.
//
// The one decisive assertion everywhere is
// `ProviderProjection.isAppendOnlyPrefix(previousWire, nextWire)` — pair count,
// callID pairing and marker bytes alone can all pass on an implementation that
// has already broken the prefix cache (historyBlock relocation).

const {
  tryInjectWithJournal,
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  source,
  text,
  stableCallId,
} = pair

// ── helpers ─────────────────────────────────────────────────────────────────

const userMsg = (id, body = 'hello') => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: body }],
})

const assistantText = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'text', text: 'ok' }],
})

const toolCall = (id, tool, callID) => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'pending', input: {}, time: { start: 0 } },
  }],
})

const toolResult = (id, tool, callID, output = 'ok') => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'completed', input: {}, output, time: { start: 0, end: 0 } },
  }],
})

const pairMessages = (messages) => messages.filter((m) => isPairProgrammingThought(m))

const inject = async (journalOrSession, sessionOrRaw, rawOrMarker = text, markerText = text) => {
  if (Array.isArray(sessionOrRaw)) {
    const result = await tryInject(journalOrSession, rawOrMarker ?? text, sessionOrRaw)
    assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
    return result.value
  }
  const result = await tryInjectWithJournal(journalOrSession, sessionOrRaw, markerText ?? text, rawOrMarker)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}

/** The authority: ARCH-004 append-only prefix law (ProviderProjection.isAppendOnlyPrefix). */
const wire = (raw) => providerCodec.decodeMessageView(raw)
const assertPrefixLaw = (previous, next, label) => {
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(previous), wire(next)),
    true,
    `${label}: previous wire must be an exact prefix of next wire`,
  )
}
const assertWireEqual = (a, b, label) => {
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(a), wire(b)),
    true,
    `${label}: wire(a) must be a prefix of wire(b)`,
  )
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(b), wire(a)),
    true,
    `${label}: wire(b) must be a prefix of wire(a)`,
  )
}

const toolNames = (messages) => messages.map((m) => m.parts[0]?.tool)
// Universal cursor mode: guidance is a NUL+BOM suffix on a real tool result,
// so history rows carry no synthetic tool names and pairMessages stays empty.
const guidanceSuffixCount = (messages) =>
  messages
    .map((m) => m.parts[0]?.state?.output ?? m.parts[0]?.state?.error ?? '')
    .filter((value) => typeof value === 'string')
    .map((value) => (value.match(/\0/g) ?? []).length)

/** Fresh durable journal in a temp dir. */
const openJournal = async (dir) => {
  const opened = await pair.createJournal(dir)
  assert.equal(opened.ok, true, JSON.stringify(opened))
  return opened
}

const durablePairCount = (journal, session) => pair.pairCount(journal, session)

// Universal cursor mode: the only guidance carrier is a NUL+BOM suffix on the
// terminal real tool result. There are no synthetic skill messages, so every
// shape assertion below counts pairMessages === 0 and checks suffix bytes.
const guidanceSuffix = (markerText) => `\0\uFEFF${markerText}`
const terminalOutputOf = (messages, id) => messages.find((m) => m.info.id === id).parts[0].state.output

test('WHAT[PREFIX-STABILITY-014] PREFIX_STABILITY_pair_body_stays_out_of_the_trace_projections', () => {
  const session = 'h13-014'
  const markerBody = 'SECRET synthetic marker body'
  const result = pair.foldAnchoredPair({
    session,
    ordinal: 1,
    callId: stableCallId(session, 1n),
    markerText: markerBody,
    callGapAfter: 'msg_7',
    resultGapAfter: 'msg_7',
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result))

  const flags = result.flags
  // The only projection the pair fact may grow is Guidelines (HOST-013 recovery).
  assert.equal(flags.guidelines, true, 'the anchored fact must land in Guidelines')

  // HOST-013 行为约束 4: no trace-family projection may be created or touched.
  // (LifecycleWorkRecord is materialized FROM the XTrace, so an absent XTrace
  // also means the work record can never carry pair bytes.)
  assert.equal(flags.xTrace, false, 'XTrace must not be created by the pair fact')
  assert.equal(flags.blog, false, 'Companion frame sequence must not be created by the pair fact')
})

test('WHAT[PREFIX-STABILITY-014] PPT_source_is_the_frozen_side_channel_identity', () => {
  assert.equal(source, 'pair-programming-auto-injected')
  assert.ok(text.length > 0, 'frozen thought text must be non-empty')
  assert.equal(isPairProgrammingThought(null), false)
  assert.equal(isPairProgrammingThought({}), false)
  assert.equal(isPairProgrammingThought({ info: { source: 'other' } }), false)
  assert.equal(isPairProgrammingThought({ info: { source } }), true)
  assert.equal(isPairProgrammingThought({ parts: [] }), false, 'no info.source means not a marker')
})

test('WHAT[PREFIX-STABILITY-014] PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker', async () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('msg_1', text)]
  const out = await inject('ses_quote', raw)
  assert.equal(isPairProgrammingThought(out[2]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 3)
  assert.equal(out[2].info.role, 'user')
})
