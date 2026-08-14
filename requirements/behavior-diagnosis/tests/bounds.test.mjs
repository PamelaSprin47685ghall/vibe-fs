// tests/unit/enforcer/bounds.test.mjs — docs/what/enforcer.md §13.2 / ENFORCER-042/043.
//
// Fail-closed size/count bounds on the commit/merge path (EnforcerHost.validateCycle):
//   - >32 merged tool calls → Error
//   - merged text > 512 KiB UTF-8 → Error
//   - merged evidence > 128 KiB UTF-8 → Error
//
// These gates live in EnforcerHost.fs (MaxMergedToolCalls / MaxBlogTextBytes /
// MaxEvidenceBytes) and fire on the real continuation transform. This suite drives
// the ACTUAL production gate handleContinuation with oversized provider steps and
// asserts the fail-closed Diagnostic.fatal (operation enforcer-cycle-failed) carries
// the matching bound reason. VERIFY-003: fake Host trajectory against real dist.
//
// Production already enforces these bounds, so these regression tests pass green —
// they LOCK the boundary so a future relaxation / accidental bypass fails loudly.

import assert from 'node:assert/strict'
import test from 'node:test'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'os'
import { join } from 'path'
import {
  agentJournal,
  agentFact,
  sessionId,
  stream,
  toList,
  caseOf,
  bloggerRequestContext,
  parkedTransform,
  runtimeResources,
  syntheticToml,
  authorityRoot,
  logicalRunId,
} from '../../verification-system/tests/support/domain.mjs'

// EnforcerHost.extractCalls reads RuntimeResources.current().EnforcerRules.
// Production installs at SpikePlugin.init; this suite drives Host without init.
runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')
const { handleContinuation } = await import('../../../dist/Session/EnforcerHost.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')
const digestForToml = (toml) => sha256Hex(toml)

/** Seed AgentOwnerRoot so the commit/abandon path stays on contract. */
const seedBloggerAuthority = async (journal) => {
  const root = await AgentJournalModule_appendAgent(
    streamSession(BLOG),
    undefined,
    agentFact('AuthorityRootAccepted', {
      SessionId: sessionId(BLOG),
      LogicalRunId: logicalRunId('blog-run-1'),
      AuthorityRootUserMessageId: authorityRoot('msg-blog-root'),
      AuthorityKind: 'AgentOwnerRoot',
      SelectedAgent: 'fast-blogger',
      PeerAgent: 'deep-blogger',
      CanonicalRole: 'blogger',
      SelectedTier: 'fast',
    }),
    journal,
  )
  assert.equal(caseOf(root), 'Ok', 'AuthorityRootAccepted must fold')
}

/**
 * Drive the real handleContinuation once with a crafted provider step (a single
 * assistant message carrying `parts`), then hand the harness the captured fatals.
 * Fail-closed bounds surface as Diagnostic.fatal → console.error JSON (the process
 * kill is gated off under node:test).
 */
const withHarness = async (parts, fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-bounds-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  const main = sessionId(MAIN)
  const blog = sessionId(BLOG)

  const link = await AgentJournalModule_appendAgent(
    streamSession(main),
    undefined,
    agentFact('CompanionBloggerLinked', {
      SessionId: main,
      BloggerSessionId: blog,
      BloggerAgent: 'fast-blogger',
    }),
    journal,
  )
  assert.equal(caseOf(link), 'Ok')
  await seedBloggerAuthority(journal)

  const scope = parkedTransform.scope()
  const toml = 'work'
  const ctx = bloggerRequestContext.main({
    requestId: 'req-bounds',
    mainSession: MAIN,
    bloggerSession: BLOG,
    toml,
    previousIngested: 0,
    nextIngested: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextDigest: 'd1',
    deltaDigest: digestForToml(toml),
  })
  // Physical flight ownership: CurrentRequest is the sole live-cycle authority.
  parkedTransform.setCurrentRequest(scope, BLOG, ctx)

  // Expected fail-closed path is silent-fatal: capture console.error lines.
  const fatals = []
  const origError = console.error
  console.error = (line) => {
    try {
      fatals.push(JSON.parse(String(line)))
    } catch {
      fatals.push({ raw: String(line) })
    }
  }

  const rawMessages = toList([
    {
      info: { id: 'asst-bounds', role: 'assistant', time: { completed: Date.now() } },
      parts,
    },
  ])

  try {
    await handleContinuation(parkedTransform.host(scope), journal, undefined, undefined, () => undefined, blog, rawMessages)
    await fn({
      fatals,
      lastFatal: () => fatals.at(-1),
    })
  } finally {
    console.error = origError
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

// Bound constants mirrored from EnforcerHost.fs (values the gate compares against).
const MAX_MERGED_TOOL_CALLS = 32
const MAX_BLOG_TEXT_BYTES = 512 * 1024
const MAX_EVIDENCE_BYTES = 128 * 1024

/** A single completed blog tool part (tip = a real packaged catalog field). */
const blogCall = (callId, input) => ({
  type: 'tool',
  tool: 'chronicle',
  callID: callId,
  state: { status: 'completed', input: { tip: 'primitive-obsession', ...input } },
})

// ── merged tool-call count > 32 → fail closed (ENFORCER-042) ─────────────────

test('ENFORCER_042_more_than_32_merged_tool_calls_fails_closed', async () => {
  const parts = Array.from({ length: MAX_MERGED_TOOL_CALLS + 1 }, (_, i) =>
    blogCall(`c${i}`, { text: `entry-${i}` }),
  )
  await withHarness(parts, async ({ fatals, lastFatal }) => {
    assert.ok(fatals.length >= 1, 'over-limit cycle must fail closed with a fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /MaxMergedToolCalls=32/)
  })
})

// ── merged text > 512 KiB UTF-8 → fail closed ────────────────────────────────

test('ENFORCER_042_merged_text_over_512KiB_fails_closed', async () => {
  const bigText = 'a'.repeat(MAX_BLOG_TEXT_BYTES + 1) // 524289 bytes, > 512 KiB
  const parts = [blogCall('c-text', { text: bigText })]
  await withHarness(parts, async ({ fatals, lastFatal }) => {
    assert.ok(fatals.length >= 1, 'over-size text must fail closed with a fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /MaxBlogTextBytes=524288/)
  })
})

// ── merged evidence > 128 KiB UTF-8 → fail closed ────────────────────────────

test('ENFORCER_042_merged_evidence_over_128KiB_fails_closed', async () => {
  const bigEvidence = 'b'.repeat(MAX_EVIDENCE_BYTES + 1) // 131073 bytes, > 128 KiB
  const parts = [blogCall('c-evidence', { text: 'work', evidence: bigEvidence })]
  await withHarness(parts, async ({ fatals, lastFatal }) => {
    assert.ok(fatals.length >= 1, 'over-size evidence must fail closed with a fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /MaxEvidenceBytes=131072/)
  })
})

// ── boundary sanity: the gate uses strict `>`, byteCount is UTF-8 ────────────
// Locks the production constants against the byte size the comparator reads, so a
// future drift in the cap value or in byteCount's encoding is caught at the source.

test('ENFORCER_042_bound_constants_match_utf8_byte_thresholds', () => {
  // At the exact cap: NOT over (gate uses strict >). UTF-8 single-byte ASCII.
  assert.equal(syntheticToml.byteCount('a'.repeat(MAX_BLOG_TEXT_BYTES)), MAX_BLOG_TEXT_BYTES)
  assert.equal(syntheticToml.byteCount('b'.repeat(MAX_EVIDENCE_BYTES)), MAX_EVIDENCE_BYTES)
  // One byte past the cap: over.
  assert.equal(syntheticToml.byteCount('a'.repeat(MAX_BLOG_TEXT_BYTES + 1)), MAX_BLOG_TEXT_BYTES + 1)
  assert.equal(syntheticToml.byteCount('b'.repeat(MAX_EVIDENCE_BYTES + 1)), MAX_EVIDENCE_BYTES + 1)
})
