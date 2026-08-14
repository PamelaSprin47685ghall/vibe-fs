// Split from tests/unit/enforcer/enforcer-predicate-branches.test.mjs (cutover Wave 2a); owner: behavior-diagnosis.
//
// ENFORCER small-branch coverage, cycle-protocol half: extractCalls protocol
// skips, lastAssistantStep shape variants, blog-part status predicates
// (hasIncompleteBlogTool / hasFailedBlogAttempt / blogPartInterrupted),
// validateCycle whitespace id, and blogCallFromPart name/lowercase-id shape.
// The fail-closed BlogFrame loader half (loadEffectiveFrames) moved to
// context-compression (enforcer-frame-loader.test.mjs).
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
  fold,
  bloggerRequestContext,
  parkedTransform,
  xTraceCapture,
  runtimeResources,
  authorityRoot,
  logicalRunId,
} from '../../verification-system/tests/support/domain.mjs'

runtimeResources.installFromPackage()

const {
  AgentJournalModule_appendAgent,
  AgentJournalModule_snapshot,
} = await import('../../../dist/Journal/AgentJournal.js')
const { handleContinuation } = await import('../../../dist/Session/EnforcerHost.js')
const { lastAssistantStep } = await import('../../../dist/Session/EnforcerCycleDecode.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')

const withHarness = async (fn, { link = true, material = 0 } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-predicates-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  if (link) {
    const res = await AgentJournalModule_appendAgent(
      streamSession(MAIN),
      undefined,
      agentFact('CompanionBloggerLinked', {
        SessionId: sessionId(MAIN),
        BloggerSessionId: sessionId(BLOG),
        BloggerAgent: 'fast-blogger',
      }),
      journal,
    )
    assert.equal(caseOf(res), 'Ok')
  }
  const auth = await AgentJournalModule_appendAgent(
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
  assert.equal(caseOf(auth), 'Ok')
  if (material > 0) {
    const turns = []
    for (let i = 0; i < material; i++) {
      turns.push({ role: i % 2 === 0 ? 'user' : 'assistant', parts: [xTraceCapture.text(`turn-${i}`)] })
    }
    await xTraceCapture.captureProjection(journal, sessionId(MAIN), xTraceCapture.semantic({ messages: turns }))
  }

  const scope = parkedTransform.scope()
  const probe = (_d, _s, _m, _c) => 'NoRecovery'
  const fatals = []
  const origError = console.error
  console.error = (line) => {
    try {
      fatals.push(JSON.parse(String(line)))
    } catch {
      fatals.push({ raw: String(line) })
    }
  }

  const assistantStep = (id, parts, { completed = true } = {}) =>
    toList([
      {
        info: { id, role: 'assistant', ...(completed ? { time: { completed: Date.now() } } : { time: { created: Date.now() } }) },
        parts,
      },
    ])
  const outcomeMessages = (outcome) => {
    const tag = caseOf(outcome)
    if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') return listItems(outcome.fields[0])
    return []
  }
  let transcript = []
  const run = async (messages) => {
    const input = toList([...transcript, ...listItems(messages)])
    const out = await handleContinuation(parkedTransform.host(scope), journal, undefined, undefined, probe, sessionId(BLOG), input)
    transcript = [...transcript, ...outcomeMessages(out)]
    return out
  }

  try {
    await fn({
      journal,
      scope,
      dir,
      fatals,
      run,
      assistantStep,
      mainSession: () => fold.session(AgentJournalModule_snapshot(journal), MAIN),
    })
  } finally {
    console.error = origError
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const manualCtx = (overrides = {}) =>
  bloggerRequestContext.main({
    requestId: 'req-1',
    mainSession: MAIN,
    bloggerSession: BLOG,
    toml: 'work',
    previousIngested: 0,
    nextIngested: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextDigest: 'd1',
    deltaDigest: sha256Hex('work'),
    ...overrides,
  })

// ── lastAssistantStep shape variants (direct, exported) ────────────────────

test('ENFORCER_last_assistant_step_ignores_malformed_messages', () => {
  assert.equal(lastAssistantStep(toList([null])), undefined, 'null message skipped')
  assert.equal(
    lastAssistantStep(toList([{ info: { id: 'x', role: 'user' } }])),
    undefined,
    'non-assistant role skipped',
  )
  assert.equal(
    lastAssistantStep(toList([{ info: { id: 'x' } }])),
    undefined,
    'missing role skipped',
  )
  assert.equal(
    lastAssistantStep(toList([{ info: { role: 'assistant' } }])),
    undefined,
    'missing id skipped',
  )

  const bare = lastAssistantStep(toList([{ id: 'a-1', role: 'assistant' }]))
  assert.notEqual(bare, undefined, 'message without info object still parsed')
  assert.equal(bare[0], 'a-1')
  assert.equal(listItems(bare[1]).length, 0, 'missing parts → empty part list')
  assert.equal(bare[2], false, 'no time.completed')

  const full = lastAssistantStep(
    toList([
      { info: { id: 'a-2', role: 'assistant', time: { completed: 1 } }, parts: [{ type: 'text', text: 't' }] },
    ]),
  )
  assert.equal(full[0], 'a-2')
  assert.equal(listItems(full[1]).length, 1)
  assert.equal(full[2], true)
})

// ── extractCalls / validateCycle through the continuation ──────────────────

test('ENFORCER_bad_tip_decode_is_protocol_skip_and_rebuilds', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // Completed blog part whose input fails tip re-validation: the call is a
    // protocol skip (extractCalls drops it), so the empty-calls arm rebuilds.
    const out = await run(
      assistantStep('asst-skip', [
        { type: 'tool', tool: 'chronicle', callID: 'c-skip', state: { status: 'completed', input: { text: 'no tip' } } },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0, 'protocol skip is silent')
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined, 'flight kept')
  })
})

test('ENFORCER_whitespace_message_id_fails_cycle_validation', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    const out = await run(
      assistantStep('   ', [
        {
          type: 'tool',
          tool: 'chronicle',
          callID: 'c-ws',
          state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } },
        },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 1)
    assert.equal(fatals[0].operation, 'enforcer-cycle-failed')
    assert.match(fatals[0].result ?? '', /no provable provider run/)
  })
})

test('ENFORCER_blog_call_with_name_field_and_lowercase_id_commits', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep, mainSession }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
    parkedTransform.host(scope).ParkTransform = () => Promise.resolve(false)
    try {
      // Part uses `name` instead of `tool` and lowercase `callId` — both
      // accepted by blogCallFromPart.
      const out = await run(
        assistantStep('asst-name', [
          { type: 'tool', name: 'chronicle', callId: 'c-low', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'entry via name field' } } },
        ]),
      )
      assert.equal(caseOf(out), 'StopPhysicalRun')
      assert.equal(fatals.length, 0, JSON.stringify(fatals))
      assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 1)
    } finally {
      parkedTransform.host(scope).ParkTransform = original
    }
  })
})

// ── blog-part status predicates ────────────────────────────────────────────

test('ENFORCER_completed_blog_part_in_empty_arm_rebuilds', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // A completed part that fails decode AND a text part: hasFailedBlogAttempt
    // sees completed → false; hasAnyBlogToolPart → rebuild.
    const out = await run(
      assistantStep('asst-completed-skip', [
        { type: 'tool', tool: 'chronicle', callID: 'c1', state: { status: 'completed', input: {} } },
        { type: 'text', text: 'plain' },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
  })
})

test('ENFORCER_interrupted_statusless_blog_part_aabbs', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // Abort cleanup can leave a blog part with no status but
    // metadata.interrupted=true → failed attempt → one AABB repair.
    const out = await run(
      assistantStep('asst-interrupted', [
        { type: 'tool', tool: 'chronicle', callID: 'c-int', state: { metadata: { interrupted: true } } },
      ]),
    )
    const msgs = outcomeMessagesOf(out)
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(
      msgs.some((m) => m?.info?.source === 'interaction-repair'),
      true,
      'interrupted statusless part is repaired',
    )
    assert.equal(fatals.length, 0)
    assert.notEqual(parkedTransform.peekCurrentRequest(scope, BLOG), undefined)
  })
})

const outcomeMessagesOf = (outcome) => {
  const tag = caseOf(outcome)
  if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') return listItems(outcome.fields[0])
  return []
}

test('ENFORCER_uninterrupted_statusless_blog_part_rebuilds', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    const out = await run(
      assistantStep('asst-clean', [
        { type: 'tool', tool: 'chronicle', callID: 'c-clean', state: { metadata: { interrupted: false } } },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
    assert.equal(outcomeMessagesOf(out).some((m) => m?.info?.source === 'interaction-repair'), false)
  })
})

test('ENFORCER_running_blog_part_projects_raw', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    const out = await run(
      assistantStep('asst-running', [
        { type: 'tool', tool: 'chronicle', callID: 'c-run', state: { status: 'running' } },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_unknown_status_blog_part_is_not_a_failed_attempt', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // A status outside the known vocabulary is not completed/error/pending/
    // running: hasFailedBlogAttempt falls to the interrupted check (false).
    const out = await run(
      assistantStep('asst-unknown-status', [
        { type: 'tool', tool: 'chronicle', callID: 'c-un', state: { status: 'weird', metadata: { interrupted: false } } },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
    assert.equal(outcomeMessagesOf(out).some((m) => m?.info?.source === 'interaction-repair'), false)
  })
})

test('ENFORCER_stateless_blog_part_has_no_status', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // Blog tool part with no `state` at all: blogPartStatus is None.
    const out = await run(
      assistantStep('asst-nostate', [{ type: 'tool', tool: 'chronicle', callID: 'c-ns' }]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_statusless_blog_part_is_not_incomplete', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // state present but no status: not pending/running (hasIncomplete=false),
    // not completed → blogPartInterrupted(false) → rebuild path.
    const out = await run(
      assistantStep('asst-stateless', [
        { type: 'tool', tool: 'chronicle', callID: 'c-st', state: {} },
      ]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
  })
})

test('ENFORCER_null_part_in_transcript_is_ignored', async () => {
  await withHarness(async ({ journal, scope, fatals, run, assistantStep }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    // A null part is not a blog tool; a completed assistant with no blog parts
    // is pure prose → InteractionNudge (no session port → AABB fallback).
    const out = await run(assistantStep('asst-nullpart', [null]))
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.equal(fatals.length, 0)
  })
})
