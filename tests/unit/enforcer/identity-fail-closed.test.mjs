// tests/unit/enforcer/identity-fail-closed.test.mjs — docs/what/enforcer.md §13.2 /
// ENFORCER-043 identity fail-closed + ENFORCER-042 multi-call protocol-violation.
//
// Two fail-closed identity gates on the commit/merge path (EnforcerHost.validateCycle):
//   - empty/missing messageId → Error "no provable provider run" (ENFORCER-043)
//   - duplicate ToolCallIds    → Error "duplicate ToolCallIds"          (ENFORCER-043)
//
// And one diagnostic side-effect gate:
//   - multi-call (distinct callIDs, valid) is a protocol violation that still
//     COMMITS a single cycle (NOT fail-closed); the "enforcer-protocol-violation"
//     Diagnostic.emit is silent by design (HOST-007: emit never prints), so this
//     suite asserts the observable contract — no enforcer-cycle-failed fatal AND the
//     coverage advances to a committed BlogObservationCommitted. The emit's own validity is
//     locked separately in tests/unit/context/ctx014.test.mjs
//     (CTX_014_enforcer_protocol_violation_fields_are_whitelisted), which guarantees
//     the multi-call emit does not throw.
//
// VERIFY-003: fake Host trajectory against real dist (same pattern as bounds.test.mjs).

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
  fold,
  authorityRoot,
  logicalRunId,
} from '../support/domain.mjs'

// EnforcerHost.extractCalls reads RuntimeResources.current().EnforcerRules.
runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent, AgentJournalModule_snapshot } = await import(
  '../../../dist/Journal/AgentJournal.js'
)
const { handleContinuation } = await import('../../../dist/Session/EnforcerHost.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')
const digestForToml = (toml) => sha256Hex(toml)

/** Seed AgentOwnerRoot so the commit/abandon path stays on contract. */
const seedBloggerAuthority = (journal) => {
  const root = AgentJournalModule_appendAgent(
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
 * Drive the real handleContinuation with a crafted provider step, then hand the
 * harness the captured fatals + a commit-mode runner. `messageId` is overridable so
 * the empty-id fail-closed path is reachable. Commit mode swaps ParkTransform for an
 * immediate settle so a valid multi-call actually finalises its BlogObservationCommitted.
 */
const withHarness = async (fn, { messageId = 'asst-identity' } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-identity-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  const main = sessionId(MAIN)
  const blog = sessionId(BLOG)

  const link = AgentJournalModule_appendAgent(
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
  seedBloggerAuthority(journal)

  const scope = parkedTransform.scope()
  const toml = 'work'
  const ctx = bloggerRequestContext.main({
    requestId: 'req-identity',
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

  const fatals = []
  const origError = console.error
  console.error = (line) => {
    try {
      fatals.push(JSON.parse(String(line)))
    } catch {
      fatals.push({ raw: String(line) })
    }
  }

  const rawMessagesFor = (parts) =>
    toList([
      {
        info: { id: messageId, role: 'assistant', time: { completed: Date.now() } },
        parts,
      },
    ])

  const run = async (parts) => {
    await handleContinuation(scope, journal, undefined, undefined, () => undefined, blog, rawMessagesFor(parts))
  }

  // Owned commit with no further XTrace material reaches ParkTransform; settle it
  // immediately so coverage commit finalises (same technique as enforcer-cycle-protocol).
  const runOwnedCommit = async (parts) => {
    const original = scope.ParkTransform.bind(scope)
    scope.ParkTransform = (_sessionId, _lifetime) => Promise.resolve(false)
    try {
      await run(parts)
    } finally {
      scope.ParkTransform = original
    }
  }

  try {
    await fn({
      journal,
      scope,
      blog,
      main,
      fatals,
      lastFatal: () => fatals.at(-1),
      run,
      runOwnedCommit,
      mainSessionCoverage: () => {
        const session = fold.session(AgentJournalModule_snapshot(journal), MAIN)
        return session?.Blog?.Coverage
      },
      enforcementReceiptCount: () => {
        const session = fold.session(AgentJournalModule_snapshot(journal), MAIN)
        return session?.Enforcement?.ByProviderRun?.size ?? 0
      },
    })
  } finally {
    console.error = origError
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

/** A single completed blog tool part (tip = a real packaged catalog field). */
const blogCall = (callId, input) => ({
  type: 'tool',
  tool: 'blog',
  callID: callId,
  state: { status: 'completed', input: { tip: 'primitive-obsession', ...input } },
})

// ── duplicate ToolCallIds → fail closed (ENFORCER-043) ───────────────────────

test('ENFORCER_043_duplicate_tool_call_ids_fails_closed', async () => {
  // Two completed blog calls sharing ONE ToolCallId: identity is not provable, so
  // the cycle must fail closed — never merge under an ambiguous provider run.
  const parts = [blogCall('same-call', { text: 'first' }), blogCall('same-call', { text: 'second' })]
  await withHarness(async ({ run, fatals, lastFatal }) => {
    await run(parts)
    assert.ok(fatals.length >= 1, 'duplicate ToolCallIds must fail closed with a fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /duplicate ToolCallIds/)
  })
})

// ── empty / missing messageId → fail closed (ENFORCER-043) ───────────────────

test('ENFORCER_043_no_provable_provider_run_fails_closed', async () => {
  // Empty provider messageId: there is no provable provider run, so the cycle must
  // fail closed even though the blog call itself is well-formed.
  const parts = [blogCall('c1', { text: 'work' })]
  await withHarness(
    async ({ run, fatals, lastFatal }) => {
      await run(parts)
      assert.ok(fatals.length >= 1, 'no provable provider run must fail closed with a fatal')
      assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
      assert.match(lastFatal()?.result ?? '', /no provable provider run/)
    },
    { messageId: '' },
  )
})

// ── multi-call = protocol violation, still commits (NOT fail-closed) ─────────

test('ENFORCER_042_multi_call_commits_single_cycle_with_protocol_violation', async () => {
  // Distinct callIDs, valid tips/text: multi-call is a protocol violation but must
  // NOT fail closed. It still merges defensively (single canonical BlogObservationCommitted)
  // and emits the silent "enforcer-protocol-violation" diagnostic (HOST-007).
  // The observable contract asserted here: no enforcer-cycle-failed fatal, and the
  // coverage advances (commit happened). The diagnostic's own field validity is locked
  // by ctx014 CTX_014_enforcer_protocol_violation_fields_are_whitelisted.
  const parts = [blogCall('c-a', { text: 'first' }), blogCall('c-b', { text: 'second' })]
  await withHarness(async ({ runOwnedCommit, fatals, mainSessionCoverage, enforcementReceiptCount }) => {
    await runOwnedCommit(parts)

    // Multi-call alone must NOT fail closed.
    assert.equal(fatals.length, 0, JSON.stringify(fatals))
    // Single cycle still commits: coverage advances 0 → 1 and one enforcement receipt.
    assert.equal(Number(mainSessionCoverage().IngestedThroughSequence), 1)
    assert.equal(Number(mainSessionCoverage().CoverableTurnCutoffExclusive), 1)
    assert.equal(enforcementReceiptCount(), 1, 'one BlogObservationCommitted receipt by ProviderRun')
  })
})
