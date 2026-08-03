/**
 * ENFORCER-060/061 — InteractionRepair + empty-text / no-blog / stale cycle.
 *
 * Fake Host trajectory (VERIFY-003): drive EnforcerHost.handleContinuation with
 * crafted provider steps. Each path must have been red under the pre-fix
 * "mark RepairSpent but never inject" implementation.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
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
} from '../domain.mjs'

const { AgentJournalModule_appendAgent } = await import('../../build/next/Journal/AgentJournal.js')
const { StreamId } = await import('../../build/next/Journal/Envelope.js')
const {
  handleContinuation,
  RepairInstruction,
  resolveCycleContext,
} = await import('../../build/next/Session/EnforcerHost.js')
const BlogTool = await import('../../build/next/Infrastructure/OpenCode/Tools/BlogTool.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) => new StreamId(1, sid)

const withHarness = async (fn) => {
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

  const scope = parkedTransform.scope()
  const ctx = bloggerRequestContext.main({
    requestId: 'req-1',
    mainSession: MAIN,
    bloggerSession: BLOG,
    toml: 'work',
    previousIngested: 0,
    nextIngested: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextDigest: 'd1',
    deltaDigest: 'sha-work',
  })
  parkedTransform.setCurrentRequest(scope, BLOG, ctx)

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

  try {
    await fn({
      journal,
      scope,
      blog,
      main,
      ctx,
      fatals,
      lastFatal: () => fatals.at(-1),
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
        state: { status: 'completed', input },
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
        state: { status: 'completed', input },
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

const hasRepairMessage = (messages) =>
  listItems(messages).some((msg) => {
    const text = msg?.parts?.[0]?.text ?? ''
    return text.includes('Protocol repair') || text === RepairInstruction
  })

const runtimeTag = (scope) => caseOf(scope.GetBloggerRuntime(BLOG).State)
const repairSpent = (scope) => scope.GetBloggerRuntime(BLOG).RepairSpent

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

// ── empty text cycle → real one-shot InteractionRepair (live tool-loop) ─────

test('ENFORCER_061_empty_text_injects_repair_once_keeps_inflight', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    const out = await handleContinuation(scope, journal, blog, liveBlog('asst-1', 'c1', { text: '' }))

    assert.equal(hasRepairMessage(out), true, 'must inject RepairInstruction (not fake spent-only)')
    assert.equal(repairSpent(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight', 'repair must not clear InFlight')
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 0, 'expected repair is silent')
  })
})

test('ENFORCER_061_second_empty_text_exhausts_repair_and_fatals', async () => {
  await withHarness(async ({ journal, scope, blog, fatals, lastFatal }) => {
    await handleContinuation(scope, journal, blog, liveBlog('asst-1', 'c1', { text: '' }))
    const out = await handleContinuation(scope, journal, blog, liveBlog('asst-2', 'c2', { text: '   ' }))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 1, 'second empty text is coverage/protocol stall → fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /protocol-repair-exhausted|aabb-refresh-empty|empty/)
  })
})

test('ENFORCER_061_historical_empty_blog_tail_is_not_live_repair', async () => {
  // Finished assistant (time.completed set) with empty blog is historical noise,
  // not a live tool-loop. Must not mark RepairSpent / inject repair.
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    const out = await handleContinuation(
      scope,
      journal,
      blog,
      historicalBlog('asst-hist', 'c-hist', { text: '' }),
    )

    assert.equal(hasRepairMessage(out), false)
    assert.equal(repairSpent(scope), false)
    assert.equal(runtimeTag(scope), 'InFlight', 'live request kept; historical tail ignored')
    assert.equal(fatals.length, 0)
  })
})

// ── no blog pure prose (ENFORCER-060) ───────────────────────────────────────

test('ENFORCER_060_pure_prose_injects_repair_once', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    const out = await handleContinuation(scope, journal, blog, pureProse('asst-p', 'I refuse tools'))

    assert.equal(hasRepairMessage(out), true)
    assert.equal(repairSpent(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_pure_prose_second_failure_fatals', async () => {
  await withHarness(async ({ journal, scope, blog, fatals, lastFatal }) => {
    await handleContinuation(scope, journal, blog, pureProse('asst-p1', 'no tools'))
    const out = await handleContinuation(scope, journal, blog, pureProse('asst-p2', 'still no'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 1, 'AABB exhausted pure prose is fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
  })
})

test('ENFORCER_060_pending_blog_is_not_pure_prose_repair', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    const out = await handleContinuation(scope, journal, blog, pendingBlog('asst-pend', 'cp'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(repairSpent(scope), false)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_outbound_assistant_shell_is_not_pure_prose_repair', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    const out = await handleContinuation(scope, journal, blog, outboundShell('asst-outbound'))

    assert.equal(hasRepairMessage(out), false, 'must not inject Protocol repair on outbound shell')
    assert.equal(repairSpent(scope), false)
    assert.equal(runtimeTag(scope), 'InFlight', 'keep live request; session content remains')
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(fatals.length, 0)
    assert.equal(
      listItems(out).some((m) => m?.info?.source === 'interaction-repair'),
      false,
    )
  })
})

test('ENFORCER_060_host_interrupted_blog_is_aabb_once_not_pure_prose', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    const out = await handleContinuation(scope, journal, blog, interruptedBlog('asst-killed', 'blog-hang'))

    // First interrupted attempt is AABB: refresh + repair injection, keep InFlight.
    assert.equal(hasRepairMessage(out), true, 'first interrupt uses AABB repair')
    assert.equal(repairSpent(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_completed_interrupted_tail_with_inflight_uses_aabb_once', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    // Harness starts InFlight. A completed interrupted blog part is Host abort cleanup:
    // AABB once (refresh + repair), not pure-prose silent idle.
    const out = await handleContinuation(
      scope,
      journal,
      blog,
      historicalTail(pureProse('asst-old-prose', 'earlier'), interruptedBlog('asst-last', 'c-int')),
    )

    assert.equal(hasRepairMessage(out), true)
    assert.equal(repairSpent(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_060_completed_prose_without_inflight_is_best_effort_no_repair', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(runtimeTag(scope), 'Idle')

    const out = await handleContinuation(scope, journal, blog, pureProse('asst-orphan', 'old prose'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(repairSpent(scope), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(fatals.length, 0)
  })
})

// ── historical completed blog tail must never re-enter cycle logic ──────────

test('ENFORCER_historical_completed_blog_after_idle_is_noop', async () => {
  await withHarness(async ({ journal, scope, blog, fatals }) => {
    await handleContinuation(scope, journal, blog, liveBlog('a1', 'c1', { text: '' }))
    await handleContinuation(scope, journal, blog, liveBlog('a2', 'c2', { text: '' }))
    assert.equal(runtimeTag(scope), 'Idle')
    // Second empty is fatal under the new policy; clear for historical-tail check.
    const fatalCount = fatals.length
    assert.ok(fatalCount >= 1)

    const out = await handleContinuation(
      scope,
      journal,
      blog,
      historicalBlog('a3', 'c3', { text: 'late work after abandon' }),
    )

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
  await withHarness(async ({ journal, scope, blog, fatals, lastFatal }) => {
    parkedTransform.clearCurrentRequest(scope, BLOG)
    assert.equal(runtimeTag(scope), 'Idle')

    await handleContinuation(scope, journal, blog, liveBlog('asst-orphan-live', 'c-orphan', { text: 'work' }))

    assert.equal(fatals.length, 1, 'unexpected live blog without authority must fatal')
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.match(lastFatal()?.result ?? '', /no live cycle authority|missing CurrentRequest|without/)
  })
})

test('ENFORCER_delta_digest_mismatch_is_fatal', async () => {
  await withHarness(async ({ journal, scope, blog, fatals, lastFatal, ctx }) => {
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

    await handleContinuation(scope, journal, blog, liveBlog('asst-bad', 'c-bad', { text: 'entry' }))

    assert.equal(fatals.length, 1)
    assert.equal(lastFatal()?.operation, 'enforcer-cycle-failed')
    assert.equal(lastFatal()?.result, 'delta digest mismatch')
    assert.equal(runtimeTag(scope), 'Idle')
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

// ── RepairSpent lifecycle on pure runtime cell ──────────────────────────────

test('ENFORCER_RepairSpent_mark_keeps_inflight_onFail_resets', () => {
  const ctx = bloggerRequestContext.main({ toml: 'cell' })
  const started = bloggerRuntime.onMaterial(bloggerRuntime.idle, ctx)
  assert.equal(started.ok, true)
  assert.equal(bloggerRuntime.repairSpentOf(started.state), false)

  const spent = bloggerRuntime.markRepairSpent(started.state)
  assert.equal(bloggerRuntime.repairSpentOf(spent), true)
  assert.equal(bloggerRuntime.stateOf(spent), 'InFlight')

  const failed = bloggerRuntime.onFail(spent)
  assert.equal(failed.ok, true)
  assert.equal(bloggerRuntime.stateOf(failed.state), 'Idle')
  assert.equal(bloggerRuntime.repairSpentOf(failed.state), false)
})

test('ENFORCER_RepairInstruction_is_stable_minimal_protocol_text', () => {
  assert.match(RepairInstruction, /Protocol repair/)
  assert.match(RepairInstruction, /blog tool exactly once/)
  assert.equal(RepairInstruction.includes('{{'), false, 'no dynamic template')
  assert.equal(RepairInstruction.includes('toml'), false)
})
