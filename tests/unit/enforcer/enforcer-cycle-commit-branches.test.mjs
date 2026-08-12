/**
 * ENFORCER commit-branch coverage: PERSIST-010 prechecks (stale staged
 * coverage → abandon → catch-up drain), open-request PromptKey binding,
 * post-commit drain / park resume paths, and the no-journal fallback.
 *
 * These paths sit between the paths already covered by
 * enforcer-cycle-protocol.test.mjs (clean commit + prose/AABB) and
 * blogger-crash-recovery.test.mjs: the *recoverable* failures that must
 * abandon the staged cycle instead of committing.
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
} from '../support/domain.mjs'

runtimeResources.installFromPackage()

const {
  AgentJournalModule_appendAgent,
  AgentJournalModule_snapshot,
  AgentJournal__WriteBlob_Z721C83C5,
} = await import('../../../dist/Journal/AgentJournal.js')
const {
  handleContinuation,
  tryRefreshMainContextFromJournal,
} = await import('../../../dist/Session/EnforcerHost.js')
const {
  CycleDisposition_$reflection,
  ContinuationOutcome_$reflection,
} = await import('../../../dist/Session/EnforcerContinuation.js')
const {
  CycleCommitOutcome_$reflection,
} = await import('../../../dist/Session/EnforcerCycleCommit.js')
const {
  FrameLoadError_$reflection,
  FrameLoadError,
} = await import('../../../dist/Session/EnforcerFrameRecovery.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')

const seedHarness = (journal, { material = 0 } = {}) => {
  const link = AgentJournalModule_appendAgent(
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
      turns.push(
        { role: i % 2 === 0 ? 'user' : 'assistant', parts: [xTraceCapture.text(`turn-${i}`)] },
      )
    }
    xTraceCapture.captureProjection(
      journal,
      sessionId(MAIN),
      xTraceCapture.semantic({ messages: turns }),
    )
  }
}

const withHarness = async (fn, { material = 0 } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-commit-branches-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  seedHarness(journal, { material })

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

const stopReason = (outcome) => {
  assert.equal(caseOf(outcome), 'StopPhysicalRun')
  return outcome.fields[1]
}

/** Stage the next coverage window (from durable XTrace) as the live request. */
const primeCycle = (scope, journal) => {
  const ctx = tryRefreshMainContextFromJournal(parkedTransform.host(scope), journal, sessionId(MAIN), sessionId(BLOG))
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

// ── PERSIST-010 prechecks: staged coverage disagrees with the projection ────

test('ENFORCER_precheck_stale_ingest_abandons_then_catchup', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
    // First window commits (ingest 0→3 when 3 XTrace turns exist).
    primeCycle(scope, journal)
    const first = await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
    assert.equal(stopReason(first), 'park-ended-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3)

    // Re-stage a context frozen at the ORIGIN cursor (prev=0) — the writer-side
    // PERSIST-010 precheck must refuse before append and abandon the cycle.
    const stale = bloggerRequestContext.main({
      requestId: 'req-stale',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'work-stale',
      previousIngested: 0,
      nextIngested: 1,
      previousCutoff: 0,
      nextCutoff: 1,
      nextDigest: 'd1',
      deltaDigest: sha256Hex('work-stale'),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, stale)

    const out = await run(blogStep('asst-2', 'c2', 'second window'))
    assert.equal(stopReason(out), 'stale-cycle-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3, 'no double commit')
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1, 'only the first run committed')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined, 'stale request cleared')
    assert.equal(parkedTransform.hasFlight(scope, BLOG), false)
    },
    { material: 3 },
  )
})

test('ENFORCER_precheck_cutoff_mismatch_abandons', async () => {
  await withHarness(async ({ journal, scope, run, blogStep, mainSession }) => {
    primeCycle(scope, journal)
    await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
    assert.equal(Number(mainSession().Blog.Coverage.CoverableTurnCutoffExclusive), 3)

    // Correct ingest cursor but a previous cutoff frozen at 0.
    const stale = bloggerRequestContext.main({
      requestId: 'req-cutoff',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'work-cutoff',
      previousIngested: 3,
      nextIngested: 4,
      previousCutoff: 0,
      nextCutoff: 4,
      nextDigest: 'd2',
      deltaDigest: sha256Hex('work-cutoff'),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, stale)

    const out = await run(blogStep('asst-2', 'c2', 'window two'))
    assert.equal(stopReason(out), 'stale-cycle-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3)
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1)
    },
    { material: 3 },
  )
})

test('ENFORCER_precheck_epoch_mismatch_after_squash_abandons', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession, blogSession }) => {
    primeCycle(scope, journal)
    await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
    const frames = listItems(mainSession().Blog.Frames)
    assert.equal(frames.length, 1)

    // Squash advances the frame epoch; a Main context frozen at the old epoch
    // is refused by the epoch precheck.
    const squash = bloggerRequestContext.squash({
      requestId: 'req-sq',
      mainSession: MAIN,
      bloggerSession: BLOG,
      frameEpoch: 0,
      coveredFrameCount: 1,
      digests: [frames[0].Digest.fields[0]],
    })
    parkedTransform.setCurrentRequest(scope, BLOG, squash)
    await withImmediatePark(scope, () => run(blogStep('asst-sq', 'c-sq', 'squash body')))
    assert.equal(mainSession().Blog.FrameEpochId.fields[0], 1n, 'squash advances frame epoch')
    assert.equal(mainSession().BloggerCycles.ByProviderRun.size, 2, 'squash receipt recorded')

    const staleEpoch = bloggerRequestContext.main({
      requestId: 'req-epoch',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'work-epoch',
      previousIngested: 3,
      nextIngested: 4,
      previousCutoff: 3,
      nextCutoff: 4,
      nextDigest: 'd3',
      frameEpoch: 0,
      deltaDigest: sha256Hex('work-epoch'),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, staleEpoch)

    const out = await run(blogStep('asst-3', 'c3', 'window three'))
    assert.equal(stopReason(out), 'stale-cycle-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3)
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1, 'epoch-stale entry not committed')
    },
    { material: 3 },
  )
})

// ── duplicate provider run across kinds → fold rejection → classifyAppendFailure ──

test('ENFORCER_same_run_after_squash_rejected_as_known_not_committed', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
    primeCycle(scope, journal)
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

    const out = await run(blogStep('asst-sq', 'c-sq', 'same run again'))
    // ENFORCER-154: the unified receipt (Squash kind) already binds this
    // provider run — the replay drains instead of re-committing as an Entry.
    assert.equal(stopReason(out), 'idempotent-receipt-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3, 'no entry commit')
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1, 'only the first entry remains')
    },
    { material: 3 },
  )
})

// ── open request PromptKey binding (commit authority proof) ────────────────

const materializeOpen = (journal, { requestId, promptKeyValue, kind = 'main' }) => {
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
  const written = AgentJournal__WriteBlob_Z721C83C5(journal, payload)
  assert.equal(written.tag, 0, JSON.stringify(written))
  const res = AgentJournalModule_appendAgent(
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
    materializeOpen(journal, { requestId: 'req-open' })

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
    materializeOpen(journal, { requestId: 'req-open-bound', promptKeyValue: 'pk-1' })

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

test('ENFORCER_park_resumed_with_fresh_material_drains', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep }) => {
      primeCycle(scope, journal)
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      // Main session offers material while the transform is parked: the wake
      // resolves true and the re-chunk finds the new window.
      parkedTransform.host(scope).ParkTransform = async (_sid, _lifetime) => {
        // Re-capture the seed turns plus two NEW turns (capture dedupes by
        // provenance, so only the fresh turns land in the XTrace).
        xTraceCapture.captureProjection(
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
        assert.notEqual(next, undefined, 'drained context re-armed as live request')
        assert.equal(next.kind, 'Main')
        assert.equal(next.previousIngested, 2n)
        assert.equal(next.nextIngested, 4n, 'new turns covered')
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
      primeCycle(scope, journal)
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
      primeCycle(scope, journal)
      const original = parkedTransform.host(scope).ParkTransform.bind(parkedTransform.host(scope))
      parkedTransform.host(scope).ParkTransform = async (_sid, _lifetime) => {
        xTraceCapture.captureProjection(
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

// ── DU metadata surfaces (reflection / cases) ──────────────────────────────

test('ENFORCER_du_reflection_surfaces_are_constructible', () => {
  assert.equal(typeof CycleCommitOutcome_$reflection, 'function')
  assert.equal(typeof CycleDisposition_$reflection, 'function')
  assert.equal(typeof ContinuationOutcome_$reflection, 'function')
  assert.equal(typeof FrameLoadError_$reflection, 'function')
  assert.deepEqual(FrameLoadError.MissingAssociation.cases(), [
    'MissingAssociation',
    'MissingBlogSession',
    'MissingFrameBlob',
    'DigestMismatch',
    'EpochMismatch',
  ])
  // Reflection helpers build the union metadata tables (lazy in Fable).
  const refs = [
    CycleCommitOutcome_$reflection,
    CycleDisposition_$reflection,
    ContinuationOutcome_$reflection,
    FrameLoadError_$reflection,
  ]
  const names = ['CycleCommitOutcome', 'CycleDisposition', 'ContinuationOutcome', 'FrameLoadError']
  for (let i = 0; i < refs.length; i++) {
    const meta = refs[i]()
    assert.equal(typeof meta, 'object')
    assert.equal(typeof meta.fullname, 'string')
    assert.ok(meta.fullname.includes(names[i]), `${names[i]} reflection`)
    assert.equal(typeof meta.cases, 'function', `${names[i]} cases thunk`)
    assert.equal(meta.cases().length > 0, true)
  }
})
