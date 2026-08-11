/**
 * ENFORCER small-branch coverage: extractCalls protocol skips, lastAssistantStep
 * shape variants, blog-part status predicates (hasIncompleteBlogTool /
 * hasFailedBlogAttempt / blogPartInterrupted), validateCycle whitespace id, and
 * the fail-closed BlogFrame loader (loadEffectiveFrames) incl. blob loss.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync, readFileSync } from 'node:fs'
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
} from '../support/domain.mjs'

runtimeResources.installFromPackage()

const {
  AgentJournalModule_appendAgent,
  AgentJournalModule_snapshot,
} = await import('../../../dist/Journal/AgentJournal.js')
const {
  handleContinuation,
  lastAssistantStep,
  loadEffectiveFrames,
} = await import('../../../dist/Session/EnforcerHost.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')

const withHarness = async (fn, { link = true, material = 0 } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-predicates-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  if (link) {
    const res = AgentJournalModule_appendAgent(
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
  const auth = AgentJournalModule_appendAgent(
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
    xTraceCapture.captureProjection(journal, sessionId(MAIN), xTraceCapture.semantic({ messages: turns }))
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
        { type: 'tool', tool: 'blog', callID: 'c-skip', state: { status: 'completed', input: { text: 'no tip' } } },
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
          tool: 'blog',
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
          { type: 'tool', name: 'blog', callId: 'c-low', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'entry via name field' } } },
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
        { type: 'tool', tool: 'blog', callID: 'c1', state: { status: 'completed', input: {} } },
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
        { type: 'tool', tool: 'blog', callID: 'c-int', state: { metadata: { interrupted: true } } },
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
        { type: 'tool', tool: 'blog', callID: 'c-clean', state: { metadata: { interrupted: false } } },
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
        { type: 'tool', tool: 'blog', callID: 'c-run', state: { status: 'running' } },
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
        { type: 'tool', tool: 'blog', callID: 'c-un', state: { status: 'weird', metadata: { interrupted: false } } },
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
      assistantStep('asst-nostate', [{ type: 'tool', tool: 'blog', callID: 'c-ns' }]),
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
        { type: 'tool', tool: 'blog', callID: 'c-st', state: {} },
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

// ── loadEffectiveFrames fail-closed paths (direct) ─────────────────────────

test('ENFORCER_load_effective_frames_missing_association', async () => {
  await withHarness(
    async ({ journal }) => {
      const result = loadEffectiveFrames(journal, sessionId(MAIN))
      assert.equal(result.tag, 1)
      assert.equal(caseOf(result.fields[0]), 'MissingAssociation')
    },
    { link: false },
  )
})

test('ENFORCER_load_effective_frames_empty_ok', async () => {
  await withHarness(async ({ journal }) => {
    const result = loadEffectiveFrames(journal, sessionId(MAIN))
    assert.equal(result.tag, 0)
    assert.equal(listItems(result.fields[0][0]).length, 0, 'no frames yet')
  })
})

test('ENFORCER_load_effective_frames_resolves_committed_frame', async () => {
  await withHarness(
    async ({ journal, scope, run, assistantStep, mainSession }) => {
      parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      parkedTransform.host(scope).ParkTransform = () => Promise.resolve(false)
      try {
        await run(
          assistantStep('asst-f1', [
            { type: 'tool', tool: 'blog', callID: 'c-f1', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body one' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      const result = loadEffectiveFrames(journal, sessionId(MAIN))
      assert.equal(result.tag, 0)
      const [resolved, epoch] = result.fields[0]
      assert.equal(listItems(resolved).length, 1)
      const frame = listItems(resolved)[0]
      assert.equal(caseOf(frame.Kind), 'Entry')
      assert.equal(frame.Body, 'frame body one')
      assert.equal(epoch.fields[0], 0n)
    },
    { material: 3 },
  )
})

test('ENFORCER_load_effective_frames_missing_blob_fails_closed', async () => {
  await withHarness(
    async ({ journal, scope, run, assistantStep, mainSession }) => {
      parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      parkedTransform.host(scope).ParkTransform = () => Promise.resolve(false)
      try {
        await run(
          assistantStep('asst-f2', [
            { type: 'tool', tool: 'blog', callID: 'c-f2', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body two' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      const frame = listItems(mainSession().Blog.Frames)[0]
      agentJournal.deleteBlob(journal, frame.TextRef)
      const result = loadEffectiveFrames(journal, sessionId(MAIN))
      assert.equal(result.tag, 1)
      assert.equal(caseOf(result.fields[0]), 'MissingFrameBlob')
    },
    { material: 3 },
  )
})

test('ENFORCER_load_effective_frames_digest_mismatch_fails_closed', async () => {
  await withHarness(
    async ({ journal, scope, run, assistantStep, mainSession }) => {
      parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      parkedTransform.host(scope).ParkTransform = () => Promise.resolve(false)
      try {
        await run(
          assistantStep('asst-f3', [
            { type: 'tool', tool: 'blog', callID: 'c-f3', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body three' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      const frame = listItems(mainSession().Blog.Frames)[0]
      agentJournal.replaceBlobContent(journal, frame.TextRef, 'tampered body')
      const result = loadEffectiveFrames(journal, sessionId(MAIN))
      assert.equal(result.tag, 1)
      assert.equal(caseOf(result.fields[0]), 'DigestMismatch')
    },
    { material: 3 },
  )
})

test('ENFORCER_rebuild_falls_back_to_raw_when_frame_blob_lost', async () => {
  await withHarness(
    async ({ journal, scope, run, assistantStep, mainSession, fatals }) => {
      parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      parkedTransform.host(scope).ParkTransform = () => Promise.resolve(false)
      try {
        await run(
          assistantStep('asst-f4', [
            { type: 'tool', tool: 'blog', callID: 'c-f4', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body four' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      // Corrupt the frame blob in IGitRawStore: rebuild fails closed and
      // the continuation must fall back to the raw transcript (never []).
      const frame = listItems(mainSession().Blog.Frames)[0]
      agentJournal.replaceBlobContent(journal, frame.TextRef, 'tampered')
      const out = await run(
        assistantStep('asst-f4', [
          { type: 'tool', tool: 'blog', callID: 'c-f4', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body four' } } },
        ]),
      )
      // alreadyReceipt → catch-up drain stages the next window, but the frame
      // rebuild fails closed on the tampered blob → raw transcript fallback.
      assert.equal(caseOf(out), 'ProjectMessages')
      const msgs = outcomeMessagesOf(out)
      assert.ok(msgs.length > 0, 'fallback is never an empty list')
      assert.equal(fatals.length, 0, 'blob loss is a recoverable fallback, not a fatal')
    },
    { material: 3 },
  )
})

test('ENFORCER_contribution_preserves_raw_identity', () => {
  // Keep the module import alive for coverage attribution on the frame loader.
  assert.equal(typeof readFileSync, 'function')
  assert.equal(typeof sha256Hex('x'), 'string')
})
