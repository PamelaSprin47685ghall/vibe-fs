/**
 * ENFORCER-060/061/064..068 — InteractionNudge → AABB + empty-text / no-blog.
 *
 * Fake Host trajectory (VERIFY-003): drive EnforcerHost.handleContinuation with
 * crafted provider steps. Pure prose uses durable InteractionRepair first;
 * AABB only on nudge hard-fail or second pure prose.
 */
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
  toList,
  listItems,
  caseOf,
  bloggerRequestContext,
  parkedTransform,
  bloggerRuntime,
  fold,
  runtimeResources,
  promptDispatcher,
  transportReceipt,
  authorityRoot,
  logicalRunId,
} from '../support/domain.mjs'

// EnforcerHost.extractCalls reads RuntimeResources.current().EnforcerRules.
// Production installs at SpikePlugin.init; this suite drives Host without init.
runtimeResources.installFromPackage()

const { AgentJournalModule_appendAgent, AgentJournalModule_snapshot } = await import(
  '../../../dist/Journal/AgentJournal.js'
)
const { StreamId } = await import('../../../dist/Journal/Envelope.js')
const {
  handleContinuation,
  RepairInstruction,
  resolveCycleContext,
} = await import('../../../dist/Session/EnforcerHost.js')
const HostSessionNudge = await import('../../../dist/Infrastructure/OpenCode/Host/HostSessionNudge.js')
const BlogTool = await import('../../../dist/Infrastructure/OpenCode/Tools/BlogTool.js')

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

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  new StreamId(1, typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')
/** HostDigest.sha256Hex(toml) — required by commit path DeltaDigest check. */
const digestForToml = (toml) => sha256Hex(toml)

/** Seed AgentOwnerRoot so HostSessionNudge.tryActiveProfile succeeds. */
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
  // InFlight + CurrentRequest: owned cycle. Without InFlight cell, commit still
  // peeks CurrentRequest, but runtime transitions need a real cell.
  const started = bloggerRuntime.onMaterial(bloggerRuntime.idle, ctx)
  assert.equal(started.ok, true)
  parkedTransform.setRuntime(scope, BLOG, started.state)
  parkedTransform.setCurrentRequest(scope, BLOG, ctx)

  const capturedSends = []
  const sessionPort =
    portMode === 'none'
      ? undefined
      : capturingPort(capturedSends, {
          fail: portMode === 'fail',
        })
  const repairNudge = repairNudgeOf(sessionPort)

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

  // Third arg is InteractionRepairNudge option (not raw ISessionHostPort).
  const run = (messages) => handleContinuation(scope, journal, repairNudge, blog, messages)

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
        tool: 'blog',
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
        tool: 'blog',
        callID: callId,
        state: { status: 'completed', input: withTip(input) },
      },
    ],
    { completed: true },
  )

const pendingBlog = (id, callId) =>
  assistantStep(
    id,
    [
      {
        type: 'tool',
        tool: 'blog',
        callID: callId,
        state: { status: 'pending', input: { text: 'later' } },
      },
    ],
    { completed: false },
  )

/** Host SessionProcessor.cleanup after abort/kill. */
const interruptedBlog = (id, callId) =>
  assistantStep(
    id,
    [
      {
        type: 'tool',
        tool: 'blog',
        callID: callId,
        state: {
          status: 'error',
          error: 'Tool execution aborted',
          input: { text: 'was writing' },
          metadata: { interrupted: true },
          time: { start: 1, end: 2 },
        },
      },
    ],
    { completed: true },
  )

const pureProse = (id, text) => assistantStep(id, [{ type: 'text', text }], { completed: true })

/** Outbound shell: Host created assistant before provider; not a terminal. */
const outboundShell = (id) => assistantStep(id, [], { completed: false })

/**
 * Host transform msgs do not include the newly created outbound assistant
 * (prompt.ts). Continuation after restart therefore sees the historical tail.
 */
const historicalTail = (...steps) => {
  const msgs = steps.flatMap((step) => listItems(step))
  return toList(msgs)
}

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

const hasRepairMessage = (outcome) =>
  messagesOf(outcome).some((msg) => {
    const text = msg?.parts?.[0]?.text ?? ''
    return text.includes('Protocol repair') || text === RepairInstruction
  })

const assertNonEmptyMessages = (outcome, label = 'messages') => {
  const msgs = messagesOf(outcome)
  assert.ok(msgs.length > 0, `${label} must be non-empty (empty blanks Host transcript)`)
  return msgs
}

const runtimeTag = (scope) => caseOf(scope.GetBloggerRuntime(BLOG).State)
const recoveryTag = (scope) => caseOf(scope.GetBloggerRuntime(BLOG).Recovery)
const isAabb = (scope) => recoveryTag(scope) === 'AabbRepairConsumed'
const isNudge = (scope) => recoveryTag(scope) === 'InteractionNudgeIssued'
const isNoRecovery = (scope) => recoveryTag(scope) === 'NoRecovery'

// ── BlogTool execute gate (ENFORCER-061) ────────────────────────────────────

test('ENFORCER_061_blog_tool_rejects_empty_canonical_text', () => {
  assert.equal(typeof BlogTool.EmptyTextError, 'string')
  assert.match(BlogTool.EmptyTextError, /ENFORCER-061/)

  const blank = BlogTool.tryCanonicalText('   ')
  assert.equal(caseOf(blank), 'Error')
  assert.equal(blank.fields[0], BlogTool.EmptyTextError)

  const missing = BlogTool.tryCanonicalText('')
  assert.equal(caseOf(missing), 'Error')

  const ok = BlogTool.tryCanonicalText('  work log  ')
  assert.equal(caseOf(ok), 'Ok')
  assert.equal(ok.fields[0], 'work log')
})

test('ENFORCER_blog_tool_without_CurrentRequest_rejects_not_ok', async () => {
  // Request-scoped capability: Role=Blogger alone is insufficient.
  assert.equal(typeof BlogTool.NoLiveCycleError, 'string')
  assert.match(BlogTool.NoLiveCycleError, /no live CurrentRequest|not InFlight/i)

  // Fable option: None ≈ null/undefined; Some host ≈ host reference.
  assert.equal(BlogTool.hasLiveCycle(null, BLOG), false)
  assert.equal(BlogTool.hasLiveCycle(undefined, BLOG), false)

  await withHarness(async ({ scope }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    parkedTransform.setRuntime(scope, BLOG, bloggerRuntime.idle)
    assert.equal(BlogTool.hasLiveCycle(scope, BLOG), false, 'Idle without CurrentRequest')

    // withHarness starts InFlight + CurrentRequest before fn; re-arm.
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
    const started = bloggerRuntime.onMaterial(bloggerRuntime.idle, ctx)
    assert.equal(started.ok, true)
    parkedTransform.setRuntime(scope, BLOG, started.state)
    parkedTransform.setCurrentRequest(scope, BLOG, ctx)
    assert.equal(BlogTool.hasLiveCycle(scope, BLOG), true, 'InFlight CurrentRequest authorises blog')
  })
})

// ── empty text cycle → real one-shot InteractionRepair (live tool-loop) ─────

test('ENFORCER_061_empty_text_injects_repair_once_keeps_inflight', async () => {
  await withHarness(async ({ scope, fatals, run }) => {
    const out = await run(liveBlog('asst-1', 'c1', { text: '' }))

    assert.equal(hasRepairMessage(out), true, 'must inject RepairInstruction (not fake spent-only)')
    assert.equal(isAabb(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight', 'repair must not clear InFlight')
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 0, 'expected repair is silent')
  })
})

test('ENFORCER_061_second_empty_text_exhausts_repair_and_fatals', async () => {
  await withHarness(async ({ scope, fatals, lastFatal, run }) => {
    await run(liveBlog('asst-1', 'c1', { text: '' }))
    const out = await run(liveBlog('asst-2', 'c2', { text: '   ' }))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 1, 'second empty text is coverage/protocol stall → fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /protocol-repair-exhausted|aabb-refresh-empty|empty/)
  })
})

test('ENFORCER_061_completed_empty_blog_with_live_request_is_aabb_not_silent_ignore', async () => {
  // Real Host sets time.completed before the next transform. Ownership is
  // live CurrentRequest, not the completed flag. Empty text still AABB once.
  await withHarness(async ({ scope, fatals, run }) => {
    const out = await run(historicalBlog('asst-hist', 'c-hist', { text: '' }))

    assert.equal(hasRepairMessage(out), true, 'owned empty cycle must repair once')
    assert.equal(isAabb(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_061_unowned_completed_empty_blog_is_not_repair', async () => {
  await withHarness(async ({ scope, fatals, run }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    parkedTransform.setRuntime(scope, BLOG, bloggerRuntime.idle)

    const out = await run(historicalBlog('asst-orphan-hist', 'c-hist', { text: '' }))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(isNoRecovery(scope), true)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(fatals.length, 0)
  })
})

// ── no blog pure prose (ENFORCER-060 / 064..068) ────────────────────────────

test('ENFORCER_060_pure_prose_first_issues_interaction_nudge_not_aabb', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    const out = await run(pureProse('asst-p', 'I refuse tools'))

    assert.equal(hasRepairMessage(out), false, 'first pure prose must not inject AABB transcript repair')
    assert.equal(isNudge(scope), true, 'stage = InteractionNudgeIssued')
    assert.equal(isAabb(scope), false)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(capturedSends.length, 1, 'exactly one durable InteractionRepair send')
    assert.match(String(capturedSends[0].text), /blog tool exactly once|Protocol repair/)
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_pure_prose_same_terminal_reentry_does_not_aabb', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    await run(pureProse('asst-same', 'no tools'))
    assert.equal(capturedSends.length, 1)
    assert.equal(isNudge(scope), true)

    // Same assistant id = same provider run: transform re-fire, not semantic failure.
    const out = await run(pureProse('asst-same', 'no tools again'))

    assert.equal(hasRepairMessage(out), false, 'same terminal must not AABB')
    assert.equal(isNudge(scope), true)
    assert.equal(capturedSends.length, 1, 'must not send a second nudge')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_already_claimed_pure_prose_is_nudge_not_aabb_no_second_send', async () => {
  // ENFORCER-067 / defect 3: durable claim already exists for this terminal.
  // Pure prose re-entry must NOT AABB, must set InteractionNudgeIssued, must not
  // re-dispatch InteractionRepair.
  await withHarness(async ({ journal, scope, fatals, run, capturedSends }) => {
    const {
      agentFact,
      authority,
      authorityRoot,
      logicalRunId,
      promptKey,
      providerRun,
      sessionId,
    } = await import('../support/domain.mjs')
    const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')

    // Pre-claim InteractionRepair for asst-preclaim (same shape as SendInteractionRepair).
    const terminal = providerRun('asst-preclaim')
    const digest = authority.repairPayloadDigest(terminal, 'blogger-missing-tool')
    const claimed = AgentJournalModule_appendAgent(
      streamSession(BLOG),
      undefined,
      agentFact('PluginPromptClaimed', {
        PromptKey: promptKey('pk-preclaim-blogger-missing'),
        SessionId: sessionId(BLOG),
        ContinuationKind: 'InteractionRepair',
        LogicalRunId: logicalRunId('blog-run-1'),
        AuthorityRootUserMessageId: authorityRoot('msg-blog-root'),
        EffectiveAgent: 'fast-blogger',
        PayloadDigest: digest,
      }),
      journal,
    )
    assert.equal(caseOf(claimed), 'Ok', 'pre-claim must fold')

    const out = await run(pureProse('asst-preclaim', 'prose after durable claim'))

    assert.equal(hasRepairMessage(out), false, 'already claimed must not AABB')
    assert.equal(isNudge(scope), true, 'stage = InteractionNudgeIssued')
    assert.equal(isAabb(scope), false)
    assert.equal(capturedSends.length, 0, 'must not re-send nudge when claim exists')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_pure_prose_second_terminal_triggers_aabb', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    await run(pureProse('asst-p1', 'no tools'))
    assert.equal(capturedSends.length, 1)
    assert.equal(isNudge(scope), true)

    // New assistant id = new pure-prose terminal after nudge → semantic failure → AABB.
    const out = await run(pureProse('asst-p2', 'still no'))

    assert.equal(hasRepairMessage(out), true, 'second pure prose is AABB')
    assert.equal(isAabb(scope), true)
    assert.equal(capturedSends.length, 1, 'must not send a second nudge')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_pure_prose_after_aabb_fatals', async () => {
  await withHarness(async ({ scope, fatals, lastFatal, run }) => {
    await run(pureProse('asst-p1', 'no tools'))
    await run(pureProse('asst-p2', 'still no')) // AABB
    const out = await run(pureProse('asst-p3', 'third'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 1, 'AABB exhausted pure prose is fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
  })
})

test('ENFORCER_060_nudge_dispatch_hard_fail_triggers_aabb', async () => {
  await withHarness(
    async ({ scope, fatals, run }) => {
      const out = await run(pureProse('asst-fail-nudge', 'prose'))
      assert.equal(hasRepairMessage(out), true, 'dispatch failure → immediate AABB')
      assert.equal(isAabb(scope), true)
      assert.equal(runtimeTag(scope), 'InFlight')
      assert.equal(fatals.length, 0)
    },
    { portMode: 'fail' },
  )
})

test('ENFORCER_060_nudge_without_session_port_triggers_aabb', async () => {
  await withHarness(
    async ({ scope, fatals, run }) => {
      const out = await run(pureProse('asst-no-port', 'prose'))
      assert.equal(hasRepairMessage(out), true, 'no port → AABB')
      assert.equal(isAabb(scope), true)
      assert.equal(fatals.length, 0)
    },
    { portMode: 'none' },
  )
})

test('ENFORCER_060_pending_blog_is_not_pure_prose_repair', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    const out = await run(pendingBlog('asst-pend', 'cp'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(isNoRecovery(scope), true)
    assert.equal(capturedSends.length, 0)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_outbound_assistant_shell_is_not_pure_prose_repair', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    const out = await run(outboundShell('asst-outbound'))

    assert.equal(hasRepairMessage(out), false, 'must not inject Protocol repair on outbound shell')
    assert.equal(isNoRecovery(scope), true)
    assert.equal(capturedSends.length, 0)
    assert.equal(runtimeTag(scope), 'InFlight', 'keep live request; session content remains')
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 0)
    assert.equal(outcomeTag(out), 'ProjectMessages')
    assert.equal(
      messagesOf(out).some((m) => m?.info?.source === 'interaction-repair'),
      false,
    )
  })
})

test('ENFORCER_060_host_interrupted_blog_is_aabb_once_not_pure_prose', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    const out = await run(interruptedBlog('asst-killed', 'blog-hang'))

    // Interrupted tool is not pure-prose nudge (ENFORCER-065); original AABB path.
    assert.equal(hasRepairMessage(out), true, 'first interrupt uses AABB repair')
    assert.equal(isAabb(scope), true)
    assert.equal(capturedSends.length, 0, 'interrupt must not send InteractionNudge')
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_completed_interrupted_tail_with_inflight_uses_aabb_once', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    // Harness starts InFlight. A completed interrupted blog part is Host abort cleanup:
    // AABB once (refresh + repair), not pure-prose nudge.
    const out = await run(
      historicalTail(pureProse('asst-old-prose', 'earlier'), interruptedBlog('asst-last', 'c-int')),
    )

    assert.equal(hasRepairMessage(out), true)
    assert.equal(isAabb(scope), true)
    assert.equal(capturedSends.length, 0)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_completed_prose_without_inflight_stops_no_repair', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(runtimeTag(scope), 'Idle')

    const out = await run(pureProse('asst-orphan', 'old prose'))

    // No live cycle → stop physical run; never inject Protocol repair (tool-loop bug closed).
    assert.equal(hasRepairMessage(out), false)
    assert.equal(outcomeTag(out), 'StopPhysicalRun')
    assertNonEmptyMessages(out, 'unowned completed prose stop payload')
    assert.match(String(stopReasonOf(out)), /unowned/)
    assert.equal(isNoRecovery(scope), true)
    assert.equal(capturedSends.length, 0)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_interrupted_blog_without_live_request_stops_no_repair', async () => {
  await withHarness(async ({ scope, fatals, run, capturedSends }) => {
    // P0 AbortSession residue: interrupted blog parts remain after stop, but CurrentRequest
    // is gone. Must not re-derive durable open and inject # Protocol repair.
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(runtimeTag(scope), 'Idle')

    const out = await run(interruptedBlog('asst-orphan-killed', 'hang'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(outcomeTag(out), 'StopPhysicalRun')
    assertNonEmptyMessages(out, 'unowned interrupted blog stop payload')
    assert.match(String(stopReasonOf(out)), /unowned/)
    assert.equal(capturedSends.length, 0)
    assert.equal(fatals.length, 0)
  })
})

// ── historical completed blog tail must never re-enter cycle logic ──────────

test('ENFORCER_historical_completed_blog_after_idle_is_noop', async () => {
  await withHarness(async ({ scope, fatals, run }) => {
    await run(liveBlog('a1', 'c1', { text: '' }))
    await run(liveBlog('a2', 'c2', { text: '' }))
    assert.equal(runtimeTag(scope), 'Idle')
    // Second empty is fatal under the new policy; clear for historical-tail check.
    const fatalCount = fatals.length
    assert.ok(fatalCount >= 1)

    const out = await run(historicalBlog('a3', 'c3', { text: 'late work after abandon' }))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
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
    assert.equal(runtimeTag(scope), 'Idle')

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
    const started = bloggerRuntime.onMaterial(bloggerRuntime.idle, bad)
    assert.equal(started.ok, true)
    parkedTransform.setRuntime(scope, BLOG, started.state)
    parkedTransform.setCurrentRequest(scope, BLOG, bad)

    await run(liveBlog('asst-bad', 'c-bad', { text: 'entry' }))

    assert.equal(fatals.length, 1)
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.equal(lastFatal()?.result, 'delta digest mismatch')
    assert.equal(runtimeTag(scope), 'Idle')
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
  const original = scope.ParkTransform.bind(scope)
  scope.ParkTransform = (_sessionId, _lifetime) => Promise.resolve(false)
  try {
    return await run(messages)
  } finally {
    scope.ParkTransform = original
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
    // After cancel of empty park, cell stays Parked (caught-up, waiting material).
    assert.equal(runtimeTag(scope), 'Parked')
  })
})

test('ENFORCER_host_completed_blog_without_live_request_is_noop_not_commit', async () => {
  await withHarness(async ({ journal, scope, fatals, run }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    parkedTransform.setRuntime(scope, BLOG, bloggerRuntime.idle)

    const out = await run(historicalBlog('asst-unowned', 'call-u1', { text: 'should not commit' }))

    assert.equal(fatals.length, 0)
    // Unowned completed blog must StopPhysicalRun — not silent ProjectMessages that
    // feed the Host tool-call loop forever.
    assert.equal(outcomeTag(out), 'StopPhysicalRun')
    assertNonEmptyMessages(out, 'unowned completed blog stop payload')
    assert.match(String(stopReasonOf(out)), /unowned-completed-blog/)
    const session = fold.session(AgentJournalModule_snapshot(journal), MAIN)
    assert.equal(session?.Blog, undefined, 'unowned completed blog must not invent BlogEntryCommitted')
    assert.equal(runtimeTag(scope), 'Idle')
  })
})

test('ENFORCER_host_completed_blog_second_pass_same_run_is_idempotent', async () => {
  await withHarness(async ({ journal, scope, fatals, run }) => {
    await runOwnedCommit(scope, run, historicalBlog('asst-idem', 'call-idem', { text: 'once' }))
    assert.equal(fatals.length, 0)

    // Replay same ProviderRun (Host re-transform / restart). Must not double-commit.
    // alreadyEntry short-circuits before park when no further material.
    await run(historicalBlog('asst-idem', 'call-idem', { text: 'once' }))

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
    const started2 = bloggerRuntime.onMaterial(bloggerRuntime.idle, ctx2)
    assert.equal(started2.ok, true)
    parkedTransform.setRuntime(scope, BLOG, started2.state)
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
    const live = resolveCycleContext(scope, journal, main, blog)
    assert.ok(live)
    assert.equal(caseOf(live), 'Main')
    assert.equal(bloggerRequestContext.toml(live), bloggerRequestContext.toml(ctx))

    // Clear InFlight without open materialization → None (not a silent invent).
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(runtimeTag(scope), 'Idle')
    const missing = resolveCycleContext(scope, journal, main, blog)
    assert.equal(missing, undefined)
  })
})

// ── Recovery stage lifecycle on pure runtime cell ───────────────────────────

test('ENFORCER_064_recovery_stage_nudge_then_aabb_onFail_resets', () => {
  const ctx = bloggerRequestContext.main({ toml: 'cell' })
  const started = bloggerRuntime.onMaterial(bloggerRuntime.idle, ctx)
  assert.equal(started.ok, true)
  assert.equal(bloggerRuntime.recoveryOf(started.state).tag, 'NoRecovery')

  const nudged = bloggerRuntime.markInteractionNudgeIssued(started.state, 'run-1')
  assert.equal(bloggerRuntime.recoveryOf(nudged).tag, 'InteractionNudgeIssued')
  assert.equal(bloggerRuntime.stateOf(nudged), 'InFlight')

  const aabb = bloggerRuntime.markAabbRepairConsumed(nudged)
  assert.equal(bloggerRuntime.recoveryOf(aabb).tag, 'AabbRepairConsumed')

  const failed = bloggerRuntime.onFail(aabb)
  assert.equal(failed.ok, true)
  assert.equal(bloggerRuntime.stateOf(failed.state), 'Idle')
  assert.equal(bloggerRuntime.recoveryOf(failed.state).tag, 'NoRecovery')
})

test('ENFORCER_RepairInstruction_is_stable_minimal_protocol_text', () => {
  assert.match(RepairInstruction, /Protocol repair/)
  assert.match(RepairInstruction, /blog tool exactly once/)
  assert.equal(RepairInstruction.includes('{{'), false, 'no dynamic template')
  assert.equal(RepairInstruction.includes('toml'), false)
})
