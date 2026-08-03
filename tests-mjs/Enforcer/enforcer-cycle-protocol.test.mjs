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

  const warns = []
  const origWarn = console.warn
  console.warn = (line) => {
    try {
      warns.push(JSON.parse(String(line)))
    } catch {
      warns.push({ raw: String(line) })
    }
  }

  try {
    await fn({
      journal,
      scope,
      blog,
      main,
      ctx,
      warns,
      lastWarn: () => warns.at(-1),
    })
  } finally {
    console.warn = origWarn
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const assistantStep = (id, parts) =>
  toList([
    {
      info: { id, role: 'assistant' },
      parts,
    },
  ])

const completedBlog = (id, callId, input) =>
  assistantStep(id, [
    {
      type: 'tool',
      tool: 'blog',
      callID: callId,
      state: { status: 'completed', input },
    },
  ])

const pendingBlog = (id, callId) =>
  assistantStep(id, [
    {
      type: 'tool',
      tool: 'blog',
      callID: callId,
      state: { status: 'pending', input: { text: 'later' } },
    },
  ])

const pureProse = (id, text) => assistantStep(id, [{ type: 'text', text }])

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

// ── empty text cycle → real one-shot InteractionRepair ──────────────────────

test('ENFORCER_061_empty_text_injects_repair_once_keeps_inflight', async () => {
  await withHarness(async ({ journal, scope, blog, warns, lastWarn }) => {
    const out = await handleContinuation(
      scope,
      journal,
      blog,
      completedBlog('asst-1', 'c1', { text: '' }),
    )

    assert.equal(hasRepairMessage(out), true, 'must inject RepairInstruction (not fake spent-only)')
    assert.equal(repairSpent(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight', 'repair must not clear InFlight')
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(lastWarn()?.operation, 'enforcer-cycle-repair')
    assert.match(lastWarn()?.result ?? '', /empty after canonicalisation/)
    assert.ok(warns.some((w) => w.operation === 'enforcer-cycle-repair'))
  })
})

test('ENFORCER_061_second_empty_text_exhausts_repair_and_idles', async () => {
  await withHarness(async ({ journal, scope, blog, lastWarn }) => {
    await handleContinuation(scope, journal, blog, completedBlog('asst-1', 'c1', { text: '' }))
    const out = await handleContinuation(
      scope,
      journal,
      blog,
      completedBlog('asst-2', 'c2', { text: '   ' }),
    )

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.equal(repairSpent(scope), false, 'onFail resets spent only after final fail')
    assert.equal(lastWarn()?.operation, 'enforcer-cycle-failed')
    assert.match(lastWarn()?.result ?? '', /protocol-repair-exhausted/)
  })
})

// ── no blog pure prose (ENFORCER-060) ───────────────────────────────────────

test('ENFORCER_060_pure_prose_injects_repair_once', async () => {
  await withHarness(async ({ journal, scope, blog, lastWarn }) => {
    const out = await handleContinuation(scope, journal, blog, pureProse('asst-p', 'I refuse tools'))

    assert.equal(hasRepairMessage(out), true)
    assert.equal(repairSpent(scope), true)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(lastWarn()?.operation, 'enforcer-cycle-repair')
    assert.match(lastWarn()?.result ?? '', /ENFORCER-060/)
  })
})

test('ENFORCER_060_pure_prose_second_failure_idles_not_busy_forever', async () => {
  await withHarness(async ({ journal, scope, blog, lastWarn }) => {
    await handleContinuation(scope, journal, blog, pureProse('asst-p1', 'no tools'))
    const out = await handleContinuation(scope, journal, blog, pureProse('asst-p2', 'still no'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
    assert.match(lastWarn()?.result ?? '', /protocol-repair-exhausted/)
  })
})

test('ENFORCER_060_pending_blog_is_not_pure_prose_repair', async () => {
  await withHarness(async ({ journal, scope, blog, warns }) => {
    const out = await handleContinuation(scope, journal, blog, pendingBlog('asst-pend', 'cp'))

    assert.equal(hasRepairMessage(out), false)
    assert.equal(repairSpent(scope), false)
    assert.equal(runtimeTag(scope), 'InFlight')
    assert.equal(
      warns.some((w) => w.operation === 'enforcer-cycle-repair'),
      false,
      'pending/running blog must wait for Host re-entry',
    )
  })
})

// ── stale cycle after abandon (out-of-sync closed) ──────────────────────────

test('ENFORCER_stale_cycle_after_abandon_emits_precise_reason', async () => {
  await withHarness(async ({ journal, scope, blog, lastWarn }) => {
    // Spend repair then exhaust → Idle, no open.
    await handleContinuation(scope, journal, blog, completedBlog('a1', 'c1', { text: '' }))
    await handleContinuation(scope, journal, blog, completedBlog('a2', 'c2', { text: '' }))
    assert.equal(runtimeTag(scope), 'Idle')

    // Old provider step still delivers a completed blog with valid text.
    const out = await handleContinuation(
      scope,
      journal,
      blog,
      completedBlog('a3', 'c3', { text: 'late work that arrived after abandon' }),
    )

    assert.equal(hasRepairMessage(out), false)
    assert.equal(runtimeTag(scope), 'Idle')
    assert.equal(lastWarn()?.operation, 'enforcer-cycle-failed')
    assert.equal(lastWarn()?.result, 'stale-cycle-after-abandon')
    assert.notEqual(lastWarn()?.result, 'missing CurrentRequest')
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
