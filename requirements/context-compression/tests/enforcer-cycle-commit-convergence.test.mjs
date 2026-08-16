// Split from tests/unit/enforcer/enforcer-cycle-commit-branches.test.mjs (cutover Wave 2a); owner: context-compression.
//
// Blogger commit-chain convergence: unified receipt replay, open-request
// PromptKey binding, post-commit catch-up drain, park resume/expiry paths,
// no-journal fallbacks, and first-request typed-context rebuild. The PERSIST-010
// prechecks + DU reflection half moved to behavior-diagnosis
// (enforcer-cycle-commit-branches.test.mjs).
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
  bloggerRequestId,
  parkedTransform,
  xTraceCapture,
  runtimeResources,
  authorityRoot,
  logicalRunId,
  promptKey,
} from '../../verification-system/tests/support/domain.mjs'

runtimeResources.installFromPackage()

const {
  AgentJournalModule_appendAgent,
  AgentJournalModule_snapshot,
  AgentJournal__WriteBlob_Z721C83C5,
} = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const {
  handleContinuation,
  tryRefreshMainContextFromJournal,
} = await import('../../../dist/Enforcer/Host.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')

const seedHarness = async (journal, { material = 0 } = {}) => {
  const link = await AgentJournalModule_appendAgent(
    streamSession(MAIN),
    undefined,
    agentFact('CompanionBloggerLinked', {
      SessionId: sessionId(MAIN),
      BloggerSessionId: sessionId(BLOG),
      BloggerAgent: 'fast-blogger',
    }),
    journal,
  )
  assert.equal(caseOf(link), 'Ok')
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
      turns.push(
        { role: i % 2 === 0 ? 'user' : 'assistant', parts: [xTraceCapture.text(`turn-${i}`)] },
      )
    }
    await xTraceCapture.captureProjection(
      journal,
      sessionId(MAIN),
      xTraceCapture.semantic({ messages: turns }),
    )
  }
}

const withHarness = async (fn, { material = 0 } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-commit-branches-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  await seedHarness(journal, { material })

  const scope = parkedTransform.scope()
  // RecoveryStageProbe is a direct 4-arg call in the compiled dist (no closure).
  const probe = (_durable, _sid, _messages, _ctx) => 'NoRecovery'

  const fatals = []
  const origError = console.error
  console.error = (line) => {
    try {
      fatals.push(JSON.parse(String(line)))
    } catch {
      fatals.push({ raw: String(line) })
    }
  }

  const assistantStep = (id, parts) =>
    toList([
      { info: { id, role: 'assistant', time: { completed: Date.now() } }, parts },
    ])
  const blogStep = (id, callId, text) =>
    assistantStep(id, [
      {
        type: 'tool',
        tool: 'chronicle',
        callID: callId,
        state: { status: 'completed', input: { tip: 'primitive-obsession', text } },
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
      blogStep,
      assistantStep,
      mainSession: () => fold.session(AgentJournalModule_snapshot(journal), MAIN),
      blogSession: () => fold.session(AgentJournalModule_snapshot(journal), BLOG),
    })
  } finally {
    console.error = origError
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

/** Commit one cycle with ParkTransform settling immediately (resumed=false). */
const withImmediatePark = async (scope, fn) => {
  const host = parkedTransform.host(scope)
  const original = host.ParkTransform.bind(host)
  host.ParkTransform = (_sessionId, _lifetime) => Promise.resolve(false)
  try {
    return await fn()
  } finally {
    host.ParkTransform = original
  }
}

const withObservedImmediatePark = async (scope, fn) => {
  const host = parkedTransform.host(scope)
  const original = host.ParkTransform
  let calls = 0
  host.ParkTransform = (_sessionId, _lifetime) => {
    calls += 1
    return Promise.resolve(false)
  }
  try {
    return { outcome: await fn(), calls }
  } finally {
    host.ParkTransform = original
  }
}

const stopReason = (outcome) => {
  assert.equal(caseOf(outcome), 'StopPhysicalRun')
  return outcome.fields[1]
}

/** Stage the next coverage window (from durable XTrace) as the live request. */
const primeCycle = async (scope, journal) => {
  const ctx = await tryRefreshMainContextFromJournal(parkedTransform.host(scope), journal, sessionId(MAIN), sessionId(BLOG))
  assert.notEqual(ctx, undefined, 'material must yield a window')
  parkedTransform.setCurrentRequest(scope, BLOG, ctx)
  return ctx
}

/** Manual first-window context when the journal has no XTrace material. */
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

// ── duplicate provider run across kinds → fold rejection → classifyAppendFailure ──

test('ENFORCER_same_run_after_squash_rejected_as_known_not_committed', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
    await primeCycle(scope, journal)
    await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
    const frames = listItems(mainSession().Blog.Frames)
    const squash = bloggerRequestContext.squash({
      requestId: 'req-sq',
      mainSession: MAIN,
      bloggerSession: BLOG,
      frameEpoch: 0,
      coveredFrameCount: 1,
      digests: [frames[0].Digest.fields[0]],
    })
    parkedTransform.setCurrentRequest(scope, BLOG, squash)
    // The squash commit consumes providerRun 'asst-sq'.
    await withImmediatePark(scope, () => run(blogStep('asst-sq', 'c-sq', 'squash body')))
    assert.equal(mainSession().Blog.FrameEpochId.fields[0], 1n)

    // Replay the SAME provider run as an Entry: the receipt map already holds a
    // Squash kind for it, so the append is refused after blobs are written —
    // classifyAppendFailure turns that into KnownNotCommitted (recoverable).
    const mainCtx = bloggerRequestContext.main({
      requestId: 'req-replay',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'work-replay',
      previousIngested: 3,
      nextIngested: 4,
      previousCutoff: 3,
      nextCutoff: 4,
      nextDigest: 'd4',
      frameEpoch: 1,
      deltaDigest: sha256Hex('work-replay'),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, mainCtx)

    const replay = await withObservedImmediatePark(scope, () =>
      run(blogStep('asst-sq', 'c-sq', 'same run again')),
    )
    // ENFORCER-154: the unified receipt (Squash kind) already binds this
    // provider run — the replay drains instead of re-committing as an Entry.
    // CTX-018: temporary quiet must cross the SAME park boundary as normal commit;
    // only the simulated physical park expiry may stop the run.
    assert.equal(replay.calls, 1, 'idempotent receipt quiet must park before physical stop')
    assert.equal(stopReason(replay.outcome), 'idempotent-receipt-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3, 'no entry commit')
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1, 'only the first entry remains')
    },
    { material: 3 },
  )
})

// ── open request PromptKey binding (commit authority proof) ────────────────

const materializeOpen = async (journal, { requestId, promptKeyValue, kind = 'main' }) => {
  const payload =
    kind === 'squash'
      ? JSON.stringify({
          kind: 'squash',
          frame_epoch: 0,
          covered_frame_count: 1,
          frame_digests: ['sha-f0'],
          observed_prefix_epoch: 0,
        })
      : JSON.stringify({
          kind: 'main',
          toml: 'work',
          delta_digest: sha256Hex('work'),
          prev_ingest: 0,
          next_ingest: 1,
          prev_cutoff: 0,
          next_cutoff: 1,
          next_prefix_digest: 'nd',
          frame_epoch: 0,
          observed_prefix_epoch: 0,
        })
  const written = await AgentJournal__WriteBlob_Z721C83C5(journal, payload)
  assert.equal(written.tag, 0, JSON.stringify(written))
  const res = await AgentJournalModule_appendAgent(
    streamSession(MAIN),
    undefined,
    agentFact('BloggerRequestMaterialized', {
      RequestId: bloggerRequestId(requestId),
      MainSessionId: sessionId(MAIN),
      BloggerSessionId: sessionId(BLOG),
      RequestKind: kind,
      ContextRef: written.fields[0].BlobRef,
      ContextDigest: written.fields[0].BlobDigest,
      ObservedPrefixEpochId: { tag: 0, fields: [0n] },
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: 1n,
      FrameEpochId: { tag: 0, fields: [0n] },
      SelectedFrameDigests: toList([]),
      PromptKey: promptKeyValue === undefined ? undefined : promptKey(promptKeyValue),
    }),
    journal,
  )
  assert.equal(caseOf(res), 'Ok', JSON.stringify(res))
}

test('ENFORCER_open_without_promptkey_binding_is_unexpected_end', async () => {
  await withHarness(async ({ journal, scope, run, blogStep, mainSession, fatals }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx({ requestId: 'req-open' }))
    await materializeOpen(journal, { requestId: 'req-open' })

    const out = await run(blogStep('asst-1', 'c1', 'window one'))
    assert.equal(caseOf(out), 'ProjectMessages', 'unexpected end projects raw')
    assert.equal(fatals.length, 1, 'open-without-PromptKey is a fatal protocol gap')
    assert.equal(fatals[0].operation, 'enforcer-cycle-failed')
    assert.match(fatals[0].result ?? '', /no PromptKey binding/)
    assert.equal(mainSession().Enforcement?.ByProviderRun?.size ?? 0, 0, 'nothing committed')
    assert.equal(parkedTransform.hasFlight(scope, BLOG), false)
  })
})

test('ENFORCER_open_bound_promptkey_commits_and_clears_open', async () => {
  await withHarness(async ({ journal, scope, run, blogStep, mainSession }) => {
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx({ requestId: 'req-open-bound' }))
    await materializeOpen(journal, { requestId: 'req-open-bound', promptKeyValue: 'pk-1' })

    const out = await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
    assert.equal(stopReason(out), 'park-ended-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 1)
    assert.equal(mainSession().BloggerCycles.OpenByRequestId.size, 0, 'commit clears the open slot')
  })
})

// ── post-commit drain: re-chunk from durable coverage before park ──────────

test('ENFORCER_catchup_drains_next_window_after_idempotent_receipt', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
      // 6 XTrace turns but the FIRST staged window cuts at sequence 1, leaving
      // 1..6 for the catch-up drain (windows are byte-chunked, not turn-counted).
      parkedTransform.setCurrentRequest(
        scope,
        BLOG,
        bloggerRequestContext.main({
          requestId: 'req-narrow',
          mainSession: MAIN,
          bloggerSession: BLOG,
          toml: 'work',
          previousIngested: 0,
          nextIngested: 1,
          previousCutoff: 0,
          nextCutoff: 1,
          nextDigest: 'd1',
          deltaDigest: sha256Hex('work'),
        }),
      )
      await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
      assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 1)

      // Same provider run re-transformed: alreadyReceipt → resumeCatchUp drains
      // the remaining material instead of re-committing.
      const out = await run(blogStep('asst-1', 'c1', 'window one replay'))
      assert.equal(caseOf(out), 'ProjectMessages', 'drain projects the next rebuilt window')
      const session = mainSession()
      assert.equal(session.Enforcement.ByProviderRun.size, 1, 'still one receipt')
      assert.equal(Number(session.Blog.Coverage.IngestedThroughSequence), 1)
      // The drained context must stage the NEXT window (prev=1 → next=6).
      const next = parkedTransform.peekCurrentRequest(scope, BLOG)
      assert.equal(next.kind, 'Main')
      assert.equal(next.previousIngested, 1n)
      assert.equal(next.nextIngested, 6n)
    },
    { material: 6 },
  )
})

// ── park lifecycle: resume (offer wake) vs expiry ──────────────────────────

test('ENFORCER_park_resumed_without_material_projects_raw', async () => {
  await withHarness(async ({ journal, scope, run, blogStep }) => {
    // No XTrace material: after commit the transform parks; the wake resolves
    // true (main offered), but re-chunk still finds nothing → project raw.
    parkedTransform.setCurrentRequest(scope, BLOG, manualCtx())
    const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
    parkedTransform.host(scope).ParkTransform = (_sid, _lifetime) => Promise.resolve(true)
    try {
      const out = await run(blogStep('asst-1', 'c1', 'window one'))
      assert.equal(caseOf(out), 'ProjectMessages')
      assert.notEqual(out, undefined)
    } finally {
      parkedTransform.host(scope).ParkTransform = original
    }
  })
})

test('ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep }) => {
      await primeCycle(scope, journal)
      const previousHead = 2n
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      // The first cycle catches up through sequence 2. While the continuation is
      // parked, main produces sequence 3..4. A frozen wake-time/head-time frontier
      // would exclude them; live-Current catch-up must consume them immediately.
      parkedTransform.host(scope).ParkTransform = async (_sid, _lifetime) => {
        await xTraceCapture.captureProjection(
          journal,
          sessionId(MAIN),
          xTraceCapture.semantic({
            messages: [
              { role: 'user', parts: [xTraceCapture.text('turn-0')] },
              { role: 'assistant', parts: [xTraceCapture.text('turn-1')] },
              { role: 'user', parts: [xTraceCapture.text('u1')] },
              { role: 'assistant', parts: [xTraceCapture.text('a1')] },
            ],
          }),
        )
        return true
      }
      try {
        const out = await run(blogStep('asst-1', 'c1', 'window one'))
        assert.equal(caseOf(out), 'ProjectMessages')
        const next = parkedTransform.peekCurrentRequest(scope, BLOG)
        assert.notEqual(next, undefined, 'future material must re-arm the same catch-up')
        assert.equal(next.kind, 'Main')
        assert.equal(next.previousIngested, previousHead)
        assert.equal(next.nextIngested, 4n)
        assert.ok(next.nextIngested > previousHead, 'same catch-up must cross the head observed before park')
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
    },
    { material: 2 },
  )
})

test('ENFORCER_park_resumed_with_flight_projects_directly', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep }) => {
      await primeCycle(scope, journal)
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      // A concurrent physical send re-armed the flight while parked: the
      // resumed transform must project the live view without re-binding.
      parkedTransform.host(scope).ParkTransform = async (_sid, _lifetime) => {
        const fresh = bloggerRequestContext.main({
          requestId: 'req-flight',
          mainSession: MAIN,
          bloggerSession: BLOG,
          toml: 'work-flight',
          previousIngested: 0,
          nextIngested: 2,
          previousCutoff: 0,
          nextCutoff: 2,
          nextDigest: 'df',
          deltaDigest: sha256Hex('work-flight'),
        })
        parkedTransform.setCurrentRequest(scope, BLOG, fresh)
        return true
      }
      try {
        const out = await run(blogStep('asst-1', 'c1', 'window one'))
        assert.equal(caseOf(out), 'ProjectMessages')
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
    },
    { material: 2 },
  )
})

test('ENFORCER_park_expired_with_fresh_material_drains', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep }) => {
      await primeCycle(scope, journal)
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      parkedTransform.host(scope).ParkTransform = async (_sid, _lifetime) => {
        await xTraceCapture.captureProjection(
          journal,
          sessionId(MAIN),
          xTraceCapture.semantic({
            messages: [
              { role: 'user', parts: [xTraceCapture.text('turn-0')] },
              { role: 'assistant', parts: [xTraceCapture.text('turn-1')] },
              { role: 'user', parts: [xTraceCapture.text('late turn')] },
            ],
          }),
        )
        return false
      }
      try {
        const out = await run(blogStep('asst-1', 'c1', 'window one'))
        assert.equal(caseOf(out), 'ProjectMessages')
        const next = parkedTransform.peekCurrentRequest(scope, BLOG)
        assert.notEqual(next, undefined)
        assert.equal(next.kind, 'Main')
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
    },
    { material: 2 },
  )
})

// ── no-journal / first-request fallbacks ───────────────────────────────────

test('ENFORCER_no_journal_projects_raw_messages', async () => {
  await withHarness(async ({ scope, run }) => {
    const scope2 = parkedTransform.scope()
    const probe = () => 'NoRecovery'
    const input = toList([
      { info: { id: 'u-1', role: 'user' }, parts: [{ type: 'text', text: 'hello' }] },
    ])
    const out = await handleContinuation(parkedTransform.host(scope2), undefined, undefined, undefined, probe, sessionId(BLOG), input)
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.deepEqual(listItems(out.fields[0]), listItems(input))
  })
})

test('ENFORCER_no_journal_empty_messages_is_empty_projection_fatal', async () => {
  await withHarness(async ({ scope, fatals }) => {
    const probe = () => 'NoRecovery'
    const out = await handleContinuation(
      parkedTransform.host(parkedTransform.scope()),
      undefined,
      undefined,
      undefined,
      probe,
      sessionId(BLOG),
      toList([]),
    )
    assert.equal(caseOf(out), 'ProjectMessages')
    assert.deepEqual(listItems(out.fields[0]), [])
    assert.equal(fatals.length, 1)
    assert.equal(fatals[0].operation, 'enforcer-empty-projection')
  })
})

test('ENFORCER_first_request_rebuilds_from_typed_context', async () => {
  await withHarness(async ({ journal, scope, run, mainSession }) => {
    // COMPANION-005: no assistant step yet (first request) — the transform
    // rebuilds provider frames + typed context, never raw user TOML.
    const ctx = bloggerRequestContext.main({
      requestId: 'req-first',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'work',
      previousIngested: 0,
      nextIngested: 1,
      previousCutoff: 0,
      nextCutoff: 1,
      nextDigest: 'd1',
      deltaDigest: sha256Hex('work'),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, ctx)
    const input = toList([
      { info: { id: 'u-1', role: 'user' }, parts: [{ type: 'text', text: 'raw toml here' }] },
    ])
    const out = await handleContinuation(parkedTransform.host(scope), journal, undefined, undefined, () => 'NoRecovery', sessionId(BLOG), input)
    assert.equal(caseOf(out), 'ProjectMessages')
    const msgs = listItems(out.fields[0])
    assert.ok(msgs.length > 0, 'rebuilt view is non-empty')
    const joined = msgs.map((m) => m?.parts?.[0]?.text ?? '').join(' ')
    assert.equal(joined.includes('raw toml here'), false, 'never extracts TOML from raw user text')
    assert.equal(joined.includes('work'), true, 'typed context toml is the rebuild source')
  })
})
