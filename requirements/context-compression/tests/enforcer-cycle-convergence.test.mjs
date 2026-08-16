// Split from tests/unit/enforcer/enforcer-cycle-protocol.test.mjs (cutover Wave 2a); owner: context-compression.
//
// Blogger request-cycle convergence chain: flight-ownership gate, owned commit
// and coverage advance, idempotent replay, second-window advance, historical
// tail noop, live-vs-open resolution, fatal commit guards. The nudge/AABB
// repair-protocol half (ENFORCER-060/061/064..068, LOOP_006, ENFORCER-065)
// moved to behavior-diagnosis (enforcer-cycle-protocol.test.mjs).
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
  listItems,
  caseOf,
  payloadOf,
  idValue,
  bloggerRequestContext,
  parkedTransform,
  fold,
  runtimeResources,
  promptDispatcher,
  transportReceipt,
  authorityRoot,
  logicalRunId,
  providerRun,
} from '../../verification-system/tests/support/domain.mjs'

// EnforcerHost.extractCalls reads RuntimeResources.current().EnforcerRules.
// Production installs at SpikePlugin.init; this suite drives Host without init.
runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent, AgentJournalModule_snapshot } = await import(
  '../../../dist/Persistence/Journal/AgentJournal.js'
)
const { handleContinuation } = await import('../../../dist/Enforcer/Host.js')
const { RepairInstruction } = await import('../../../dist/Enforcer/Repair.js')
const { resolveCycleContext } = await import('../../../dist/Enforcer/Cycle/Recovery.js')
const HostSessionNudge = await import('../../../dist/Interaction/Dispatch/OpenCode/SessionNudge.js')
const FallbackLedger = await import('../../../dist/Participant/Provider/Attempt/Fallback/Ledger.js')
const AgentPairCursor = await import('../../../dist/Participant/Provider/Attempt/Cursor.js')
const BloggerRecoveryProbe = await import('../../../dist/Enforcer/Cycle/BloggerProbe.js')
const { lastAssistantStep } = await import('../../../dist/Enforcer/Cycle/Decode.js')
const BlogTool = await import('../../../dist/OpenCode/Tools/ChronicleTool.js')

/**
 * Production wiring: close sessionPort into InteractionRepairNudge (ENFORCER-066).
 * Fable emits HostSessionNudge.trySendInteractionRepair as multi-arg JS; SpikePlugin
 * does the same close in F# (`trySendInteractionRepair sessionPort`).
 */
const repairNudgeOf = (sessionPort) => {
  if (sessionPort === undefined) return undefined
  const send = HostSessionNudge.trySendInteractionRepair
  if (typeof send !== 'function') {
    throw new Error('HostSessionNudge.trySendInteractionRepair missing from dist')
  }
  return (sessionId, prompt, directory, journal, terminalRun, repairKind) =>
    send(sessionPort, sessionId, prompt, directory, journal, terminalRun, repairKind)
}

/**
 * Production wiring: close journal + budget into ConfirmedFailurePort (rabbit §13.1 / S9.1).
 * Injected 3-arg port EnforcerHost invokes.
 */
const confirmedFailureOf = (journal) => {
  if (!journal) return undefined
  const admit =
    FallbackLedger.FallbackLedger_admitConfirmedFailure ?? FallbackLedger.admitConfirmedFailure
  const budget = AgentPairCursor.DefaultAutoRecoveryBudget ?? 12
  return (sessionId, providerRun, reason) =>
    admit(journal, budget, sessionId, providerRun, reason)
}

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')
/** HostDigest.sha256Hex(toml) — required by commit path DeltaDigest check. */
const digestForToml = (toml) => sha256Hex(toml)

/** Seed AgentOwnerRoot so HostSessionNudge.tryActiveProfile succeeds. */
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

const capturingPort = (captured, { fail = false } = {}) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    if (fail) {
      return promptDispatcher.retryable('simulated dispatch failure')
    }
    return promptDispatcher.admittedWithReceipt(transportReceipt(`rcpt-${captured.length}`))
  },
  AbortSession: async () => ({ tag: 0, fields: [] }),
  AbortChildren: async () => {},
  CreateChildSession: async () => ({ tag: 1, fields: ['unused'] }),
})

const withHarness = async (fn, { portMode = 'ok' } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-cycle-'))
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
    requestId: 'req-1',
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
  assert.equal(parkedTransform.hasFlight(scope, BLOG), true)

  const capturedSends = []
  const sessionPort =
    portMode === 'none'
      ? undefined
      : capturingPort(capturedSends, {
          fail: portMode === 'fail',
        })
  const repairNudge = repairNudgeOf(sessionPort)
  const confirmedFailure = confirmedFailureOf(journal)

  // Expected paths are silent (no console). Unexpected paths print via console.error
  // then would kill the process — under node:test the kill is gated off.
  const fatals = []
  const origError = console.error
  console.error = (line) => {
    try {
      fatals.push(JSON.parse(String(line)))
    } catch {
      fatals.push({ raw: String(line) })
    }
  }

  // Production probe (SpikePlugin): the recovery stage derives from durable
  // claim + transcript on every read; the runtime cell carries no mirror.
  // NOTE: Fable compiles the curried RecoveryStageProbe as a FOUR-argument
  // direct call (journal, sid, messages, ctx) — a JS closure returning a
  // function would be matched as a function value and silently take the wrong
  // branch.
  const recoveryProbe = (durable, sid, rawMessages, ctx) => {
    const step = lastAssistantStep(rawMessages)
    const terminalRun =
      step !== undefined && step[0] !== undefined && String(step[0]).trim() !== ''
        ? providerRun(step[0])
        : providerRun('unknown-prose-run')
    // ctx is the BloggerRequestContext union; the request id lives on the payload
    // as a BloggerRequestId identity — the probe compares it against the string
    // requestKey embedded in injected repair messages.
    const requestKey = idValue.bloggerRequest(payloadOf(ctx).RequestId)
    return BloggerRecoveryProbe.BloggerRecoveryProbe_repairState
      ? BloggerRecoveryProbe.BloggerRecoveryProbe_repairState(durable, sid, requestKey, terminalRun, rawMessages)
      : BloggerRecoveryProbe.repairState(durable, sid, requestKey, terminalRun, rawMessages)
  }

  // Third arg is InteractionRepairNudge option (not raw ISessionHostPort).
  // The Host transform input is a FULL session snapshot: every earlier run's
  // output — including an injected AABB repair message — is visible on later
  // runs. The harness accumulates exactly that (ENFORCER-153: AABB spent is
  // derived from the transcript, so the repair message IS the budget marker).
  const outcomeMessages = (outcome) => {
    const tag = caseOf(outcome)
    if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') return listItems(outcome.fields[0])
    return []
  }
  let transcript = []
  const run = async (messages) => {
    const input = toList([...transcript, ...listItems(messages)])
    const out = await handleContinuation(parkedTransform.host(scope), journal, repairNudge, confirmedFailure, recoveryProbe, blog, input)
    transcript = [...transcript, ...outcomeMessages(out)]
    return out
  }

  try {
    await fn({
      journal,
      scope,
      blog,
      main,
      ctx,
      fatals,
      lastFatal: () => fatals.at(-1),
      capturedSends,
      sessionPort,
      run,
    })
  } finally {
    console.error = origError
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

/** Host only sets time.completed when the run ends. Outbound transform leaves it unset. */
const assistantStep = (id, parts, { completed = true } = {}) =>
  toList([
    {
      info: {
        id,
        role: 'assistant',
        ...(completed ? { time: { completed: Date.now() } } : { time: { created: Date.now() } }),
      },
      parts,
    },
  ])

// ENFORCER-020: tip is required at transform re-validate. Fixtures without tip
// are skipped by extractCalls and silently fall into the empty-calls arm.
const DEFAULT_TIP = 'primitive-obsession'
const withTip = (input = {}) => ({ tip: DEFAULT_TIP, ...input })

/**
 * Live tool-loop blog: tool status=completed but assistant.time.completed unset.
 * Host only completes the assistant after the turn ends / cleanup.
 */
const liveBlog = (id, callId, input) =>
  assistantStep(
    id,
    [
      {
        type: 'tool',
        tool: 'chronicle',
        callID: callId,
        state: { status: 'completed', input: withTip(input) },
      },
    ],
    { completed: false },
  )

/** Historical tail after a finished cycle (assistant already completed). */
const historicalBlog = (id, callId, input) =>
  assistantStep(
    id,
    [
      {
        type: 'tool',
        tool: 'chronicle',
        callID: callId,
        state: { status: 'completed', input: withTip(input) },
      },
    ],
    { completed: true },
  )

/** handleContinuation returns ContinuationOutcome, never a bare message list. */
const outcomeTag = (outcome) => caseOf(outcome)

const messagesOf = (outcome) => {
  const tag = outcomeTag(outcome)
  if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') {
    return listItems(outcome.fields[0])
  }
  throw new Error(`unexpected ContinuationOutcome '${tag}'`)
}

const stopReasonOf = (outcome) => {
  assert.equal(outcomeTag(outcome), 'StopPhysicalRun', 'expected StopPhysicalRun')
  return outcome.fields[1]
}

/**
 * Whether THIS outcome appended a fresh AABB repair message. Repair injection
 * always appends to the transcript, and the Host snapshot accumulates earlier
 * messages — so the check is the LAST message, never a scan (a fatal/project
 * outcome echoes the transcript including any historical repair).
 */
const hasRepairMessage = (outcome) => {
  const msgs = messagesOf(outcome)
  if (msgs.length === 0) return false
  const text = msgs.at(-1)?.parts?.[0]?.text ?? ''
  return text.includes('Protocol repair') || text === RepairInstruction
}

const assertNonEmptyMessages = (outcome, label = 'messages') => {
  const msgs = messagesOf(outcome)
  assert.ok(msgs.length > 0, `${label} must be non-empty (empty blanks Host transcript)`)
  return msgs
}

/** Physical flight ownership (PR7 D6): hasFlight replaces State.InFlight/Idle. */
const hasFlight = (scope) => parkedTransform.hasFlight(scope, BLOG)

// ── BlogTool execute gate (ENFORCER-061): flight ownership authorises blog ──

test('ENFORCER_blog_tool_without_CurrentRequest_rejects_not_ok', async () => {
  // Request-scoped capability: Role=Blogger alone is insufficient.
  assert.equal(typeof BlogTool.NoLiveCycleError, 'string')
  assert.match(BlogTool.NoLiveCycleError, /CHRONICLE_NO_LIVE_CYCLE/)

  // Fable option: None ≈ null/undefined; Some host ≈ host reference.
  assert.equal(BlogTool.hasLiveCycle(null, BLOG), false)
  assert.equal(BlogTool.hasLiveCycle(undefined, BLOG), false)

  await withHarness(async ({ scope }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(BlogTool.hasLiveCycle(parkedTransform.host(scope), BLOG), false, 'no flight without CurrentRequest')

    // withHarness starts with CurrentRequest before fn; re-arm physical ownership.
    const toml = 'work'
    const ctx = bloggerRequestContext.main({
      requestId: 'req-live-gate',
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
    parkedTransform.setCurrentRequest(scope, BLOG, ctx)
    assert.equal(BlogTool.hasLiveCycle(parkedTransform.host(scope), BLOG), true, 'CurrentRequest flight authorises blog')
  })
})

// ── historical completed blog tail must never re-enter cycle logic ──────────

test('ENFORCER_historical_completed_blog_after_idle_is_noop', async () => {
  await withHarness(async ({ scope, fatals, run }) => {
    await run(liveBlog('a1', 'c1', { text: '' }))
    await run(liveBlog('a2', 'c2', { text: '' }))
    assert.equal(hasFlight(scope), false)
    // Second empty is fatal under the new policy; clear for historical-tail check.
    const fatalCount = fatals.length
    assert.ok(fatalCount >= 1)

    const out = await run(historicalBlog('a3', 'c3', { text: 'late work after abandon' }))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(hasFlight(scope), false)
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, fatalCount, 'historical tail must not emit extra fatals')
  })
})

// ── unexpected = fatal (console.error; kill gated under node:test) ──────────

test('ENFORCER_live_blog_without_CurrentRequest_and_without_open_is_fatal', async () => {
  // Live tool-loop blog (assistant not completed) with no InFlight and no open
  // materialization means the plugin never owned this step — programmer gap.
  await withHarness(async ({ scope, fatals, lastFatal, run }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(hasFlight(scope), false)

    await run(liveBlog('asst-orphan-live', 'c-orphan', { text: 'work' }))

    assert.equal(fatals.length, 1, 'unexpected live blog without authority must fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /no live cycle authority|missing CurrentRequest|without/)
  })
})

test('ENFORCER_delta_digest_mismatch_is_fatal', async () => {
  await withHarness(async ({ scope, fatals, lastFatal, run }) => {
    // Corrupt InFlight delta digest so commit path hits unexpectedEnd.
    const bad = bloggerRequestContext.main({
      requestId: 'req-bad-digest',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'work',
      previousIngested: 0,
      nextIngested: 1,
      previousCutoff: 0,
      nextCutoff: 1,
      nextDigest: 'd1',
      deltaDigest: 'sha-NOT-matching-toml',
    })
    parkedTransform.setCurrentRequest(scope, BLOG, bad)

    await run(liveBlog('asst-bad', 'c-bad', { text: 'entry' }))

    assert.equal(fatals.length, 1)
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.equal(lastFatal()?.result, 'delta digest mismatch')
    assert.equal(hasFlight(scope), false)
  })
})

// ── P0: Host completed-assistant must still commit when we own the cycle ───

/**
 * Owned commit with no further XTrace material reaches ParkTransform.
 * Production parks until main offers (ENFORCER-050); that wait is Host-event
 * driven. In unit tests there is no main transform, so swap ParkTransform for
 * an immediate settle (same outcome as cancel / quiet catch-up: resumed=false).
 * Time-independent: no sleep, no poll, no 10m timer.
 */
const runOwnedCommit = async (scope, run, messages) => {
  const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
  parkedTransform.host(scope).ParkTransform = (_sessionId, _lifetime) => Promise.resolve(false)
  try {
    return await run(messages)
  } finally {
    parkedTransform.host(scope).ParkTransform = original
  }
}

test('ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage', async () => {
  // Manager real Host trajectory (processor.cleanup):
  //   tool status=completed AND assistant.time.completed set
  // before the next loop transform reloads msgs. Old code treated that as a
  // historical tail and resumeWithContext'd the same 200 KiB forever.
  await withHarness(async ({ journal, scope, fatals, run }) => {
    const out = await runOwnedCommit(
      scope,
      run,
      historicalBlog('asst-mgr-1', 'call-mgr-1', { text: 'recorded first window' }),
    )

    assert.equal(fatals.length, 0, JSON.stringify(fatals))
    assert.equal(hasRepairMessage(out), false)
    // ParkTransform=false → catch-up complete → StopPhysicalRun, never [].
    assert.equal(outcomeTag(out), 'StopPhysicalRun')
    assertNonEmptyMessages(out, 'owned commit after park end')
    assert.match(String(stopReasonOf(out)), /park-ended|catch-up|sealed/)

    const snap = AgentJournalModule_snapshot(journal)
    const session = fold.session(snap, MAIN)
    assert.ok(session?.Blog, 'Blog projection must exist after commit')
    assert.equal(
      Number(session.Blog.Coverage.IngestedThroughSequence),
      1,
      'RecordCoverage must advance from staged next_ingest',
    )
    assert.equal(session.Blog.Coverage.CoverableTurnCutoffExclusive, 1)
    assert.equal(session.Enforcement?.ByProviderRun?.size ?? 0, 1, 'enforcement receipt by ProviderRun')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined, 'CurrentRequest cleared on commit')
    // After cancel of empty park, cell stays Idle (caught-up, waiting material).
    assert.equal(hasFlight(scope), false)
  })
})

test('ENFORCER_host_completed_blog_without_live_request_is_noop_not_commit', async () => {
  await withHarness(async ({ journal, scope, fatals, run }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)

    const out = await run(historicalBlog('asst-unowned', 'call-u1', { text: 'should not commit' }))

    assert.equal(fatals.length, 0)
    // Unowned completed blog must StopPhysicalRun — not silent ProjectMessages that
    // feed the Host tool-call loop forever.
    assert.equal(outcomeTag(out), 'StopPhysicalRun')
    assertNonEmptyMessages(out, 'unowned completed blog stop payload')
    assert.match(String(stopReasonOf(out)), /unowned-completed-blog/)
    const session = fold.session(AgentJournalModule_snapshot(journal), MAIN)
    assert.equal(session?.Blog, undefined, 'unowned completed blog must not invent BlogObservationCommitted')
    assert.equal(hasFlight(scope), false)
  })
})

test('ENFORCER_host_completed_blog_second_pass_same_run_is_idempotent', async () => {
  await withHarness(async ({ journal, scope, fatals, run }) => {
    await runOwnedCommit(scope, run, historicalBlog('asst-idem', 'call-idem', { text: 'once' }))
    assert.equal(fatals.length, 0)

    // Replay same ProviderRun (Host re-transform / restart). Must not double-commit.
    // CTX-018: idempotent receipt + quiet still crosses ParkTransform; this test
    // simulates the physical park expiry immediately rather than waiting 10 minutes.
    await runOwnedCommit(scope, run, historicalBlog('asst-idem', 'call-idem', { text: 'once' }))

    assert.equal(fatals.length, 0)
    const session = fold.session(AgentJournalModule_snapshot(journal), MAIN)
    assert.equal(Number(session.Blog.Coverage.IngestedThroughSequence), 1)
    assert.equal(session.Enforcement.ByProviderRun.size, 1)
  })
})

test('ENFORCER_host_completed_blog_second_window_advances_coverage_not_resend', async () => {
  // Two sequential Host-completed cycles with staged next_ingest windows.
  // Fingerprint of the bug: every request restarts at prev=0.
  await withHarness(async ({ journal, scope, fatals, run }) => {
    await runOwnedCommit(scope, run, historicalBlog('asst-w1', 'call-w1', { text: 'window-one' }))
    assert.equal(fatals.length, 0)

    const afterFirst = fold.session(AgentJournalModule_snapshot(journal), MAIN)
    assert.equal(Number(afterFirst.Blog.Coverage.IngestedThroughSequence), 1)

    const toml2 = 'window-two'
    const ctx2 = bloggerRequestContext.main({
      requestId: 'req-2',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: toml2,
      previousIngested: 1,
      nextIngested: 2,
      previousCutoff: 1,
      nextCutoff: 2,
      nextDigest: 'd2',
      deltaDigest: digestForToml(toml2),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, ctx2)

    await runOwnedCommit(scope, run, historicalBlog('asst-w2', 'call-w2', { text: 'window-two-body' }))

    assert.equal(fatals.length, 0, JSON.stringify(fatals))
    const afterSecond = fold.session(AgentJournalModule_snapshot(journal), MAIN)
    assert.equal(Number(afterSecond.Blog.Coverage.IngestedThroughSequence), 2)
    assert.equal(afterSecond.Blog.Coverage.CoverableTurnCutoffExclusive, 2)
    assert.equal(afterSecond.Enforcement.ByProviderRun.size, 2, 'two distinct ProviderRuns')
    // Previous of second must not be origin.
    assert.notEqual(Number(afterSecond.Blog.Coverage.IngestedThroughSequence), 0)
  })
})

// ── resolveCycleContext does not clobber live InFlight ──────────────────────

test('ENFORCER_resolveCycleContext_prefers_live_inflight_request', async () => {
  await withHarness(async ({ journal, scope, blog, main, ctx }) => {
    const live = await resolveCycleContext(parkedTransform.host(scope), journal, main, blog)
    assert.ok(live)
    assert.equal(caseOf(live), 'Main')
    assert.equal(bloggerRequestContext.toml(live), bloggerRequestContext.toml(ctx))

    // Clear InFlight without open materialization → None (not a silent invent).
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(hasFlight(scope), false)
    const missing = await resolveCycleContext(parkedTransform.host(scope), journal, main, blog)
    assert.equal(missing, undefined)
  })
})
