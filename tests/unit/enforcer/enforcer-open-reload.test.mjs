/**
 * ENFORCER durable-open reload (tryReloadRequestContext / resolveCycleContext)
 * and the squash-commit refusal branches (commitSquash fail-closed checks).
 *
 * The open materialization (BloggerRequestMaterialized) is the crash-resume
 * source of truth: ContextRef blob → typed BloggerRequestContext. These tests
 * drive the reload decoder directly with controlled blob payloads, and the
 * squash refusals through handleContinuation.
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
  mapEntries,
  bloggerRequestContext,
  bloggerRequestId,
  blobDigest,
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
  resolveCycleContext,
} = await import('../../../dist/Session/EnforcerHost.js')

const MAIN = 'ses-main'
const BLOG = 'ses-blog'
const streamSession = (sid) =>
  stream.session(typeof sid === 'string' ? sessionId(sid) : sid)
const sha256Hex = (input) => createHash('sha256').update(input, 'utf8').digest('hex')

const withHarness = async (fn, { material = 0 } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'enforcer-open-reload-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const journal = created.journal
  for (const [sid, fact] of [
    [
      MAIN,
      agentFact('CompanionBloggerLinked', {
        SessionId: sessionId(MAIN),
        BloggerSessionId: sessionId(BLOG),
        BloggerAgent: 'fast-blogger',
      }),
    ],
    [
      BLOG,
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
    ],
  ]) {
    const res = AgentJournalModule_appendAgent(streamSession(sid), undefined, fact, journal)
    assert.equal(caseOf(res), 'Ok')
  }
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

  const blogStep = (id, callId, text) =>
    toList([
      {
        info: { id, role: 'assistant', time: { completed: Date.now() } },
        parts: [
          {
            type: 'tool',
            tool: 'blog',
            callID: callId,
            state: { status: 'completed', input: { tip: 'primitive-obsession', text } },
          },
        ],
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
    const out = await handleContinuation(scope, journal, undefined, probe, sessionId(BLOG), input)
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
      mainSession: () => fold.session(AgentJournalModule_snapshot(journal), MAIN),
    })
  } finally {
    console.error = origError
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const withImmediatePark = async (scope, fn) => {
  const original = scope.ParkTransform.bind(scope)
  scope.ParkTransform = (_sessionId, _lifetime) => Promise.resolve(false)
  try {
    return await fn()
  } finally {
    scope.ParkTransform = original
  }
}

const mainSessionOf = (journal) => fold.session(AgentJournalModule_snapshot(journal), MAIN)

/** The single open request currently recorded for the blogger. */
const currentOpen = (journal) => {
  const cycles = mainSessionOf(journal).BloggerCycles
  const byBlogger = [...mapEntries(cycles.OpenByBlogger)]
  assert.equal(byBlogger.length, 1)
  return cycles.OpenByRequestId.get(byBlogger[0][1])
}

/**
 * Materialize an open request for the blogger with a hand-written context blob.
 */
const materializeOpen = (journal, { requestId, json, kind = 'main', promptKeyValue = undefined, selectedDigests = undefined }) => {
  const written = AgentJournal__WriteBlob_Z721C83C5(journal, JSON.stringify(json))
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
      SelectedFrameDigests: selectedDigests ? toList(selectedDigests.map(blobDigest)) : toList([]),
      PromptKey: promptKeyValue === undefined ? undefined : promptKey(promptKeyValue),
    }),
    journal,
  )
  assert.equal(caseOf(res), 'Ok', JSON.stringify(res))
  return currentOpen(journal)
}

const mainJson = (overrides = {}) => ({
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
  ...overrides,
})

// ── reload: Main ────────────────────────────────────────────────────────────

test('ENFORCER_reload_main_context_from_open_materialization', async () => {
  await withHarness(async ({ journal, scope }) => {
    materializeOpen(journal, { requestId: 'req-m', json: mainJson() })
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.notEqual(reloaded, undefined)
    assert.equal(caseOf(reloaded), 'Main')
    const m = reloaded.fields[0]
    assert.equal(m.Toml, 'work')
    assert.equal(m.PreviousIngestedThroughSequence, 0n)
    assert.equal(m.NextIngestedThroughSequence, 1n)
    assert.equal(m.PreviousCoverableTurnCutoffExclusive, 0)
    assert.equal(m.NextCoverableTurnCutoffExclusive, 1)
    assert.equal(m.NextCoveredPrefixDigest, 'nd')
    assert.equal(m.DeltaDigest.fields[0], sha256Hex('work'))
    assert.equal(m.FrameEpochId.fields[0], 0n)
    assert.equal(m.ObservedPrefixEpochId.fields[0], 0n)
  })
})

test('ENFORCER_reload_squash_context_from_open_materialization', async () => {
  await withHarness(async ({ journal, scope }) => {
    materializeOpen(journal, {
      requestId: 'req-sq',
      kind: 'squash',
      selectedDigests: ['sha-a', 'sha-b'],
      json: {
        kind: 'squash',
        frame_epoch: 0,
        covered_frame_count: 2,
        observed_prefix_epoch: 0,
      },
    })
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.notEqual(reloaded, undefined)
    assert.equal(caseOf(reloaded), 'Squash')
    const s = reloaded.fields[0]
    assert.equal(s.CoveredFrameCount, 2)
    assert.deepEqual(listItems(s.FrameDigests).map((d) => d.fields[0]), ['sha-a', 'sha-b'])
    assert.equal(s.FrameEpochId.fields[0], 0n)
  })
})

test('ENFORCER_reload_defaults_when_blob_is_sparse', async () => {
  await withHarness(async ({ journal, scope }) => {
    // Only kind present: every field falls back to the open-request defaults.
    materializeOpen(journal, { requestId: 'req-sparse', json: { kind: 'main' } })
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.equal(caseOf(reloaded), 'Main')
    const m = reloaded.fields[0]
    assert.equal(m.Toml, '')
    assert.equal(m.PreviousIngestedThroughSequence, 0n, 'open default prev ingest')
    assert.equal(m.NextIngestedThroughSequence, 1n, 'open default next ingest')
    assert.equal(m.PreviousCoverableTurnCutoffExclusive, 0)
    assert.equal(m.NextCoveredPrefixDigest, '')
    assert.equal(m.DeltaDigest.fields[0], currentOpen(journal).ContextDigest.fields[0], 'sparse: context digest fallback')
  })
})

test('ENFORCER_reload_parses_string_numbers_and_derives_delta_digest', async () => {
  await withHarness(async ({ journal, scope }) => {
    // JSON numbers can arrive as strings; the decoder must accept both.
    // delta_digest absent → sha256(toml) when toml present.
    const toml = 'work-string'
    const json = mainJson({
      toml,
      prev_ingest: '4',
      next_ingest: '7',
      prev_cutoff: '3',
      next_cutoff: '7',
      delta_digest: undefined,
    })
    delete json.delta_digest
    materializeOpen(journal, { requestId: 'req-str', json })
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.equal(caseOf(reloaded), 'Main')
    const m = reloaded.fields[0]
    assert.equal(m.PreviousIngestedThroughSequence, 4n)
    assert.equal(m.NextIngestedThroughSequence, 7n)
    assert.equal(m.PreviousCoverableTurnCutoffExclusive, 3)
    assert.equal(m.NextCoverableTurnCutoffExclusive, 7)
    assert.equal(m.DeltaDigest.fields[0], sha256Hex(toml))
  })
})

test('ENFORCER_reload_derives_delta_digest_from_context_digest_when_toml_empty', async () => {
  await withHarness(async ({ journal, scope }) => {
    const json = mainJson({ toml: '' })
    delete json.delta_digest
    const openReq = materializeOpen(journal, { requestId: 'req-nodigest', json })
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    const m = reloaded.fields[0]
    assert.equal(m.Toml, '')
    assert.equal(m.DeltaDigest.fields[0], openReq.ContextDigest.fields[0], 'ContextDigest is the fallback')
  })
})

test('ENFORCER_reload_unreadable_blob_returns_none', async () => {
  await withHarness(async ({ journal, scope }) => {
    const openReq = materializeOpen(journal, { requestId: 'req-gone', json: mainJson() })
    // EventStore BlobRef is blobs/<gitOid> in IGitRawStore — not a RuntimePath file.
    agentJournal.deleteBlob(journal, openReq.ContextRef)
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.equal(reloaded, undefined)
  })
})

test('ENFORCER_reload_corrupt_json_returns_none', async () => {
  await withHarness(async ({ journal, scope }) => {
    const openReq = materializeOpen(journal, { requestId: 'req-badjson', json: mainJson() })
    // Unterminated JSON → JSON.parse throws → decoder returns None (fail closed).
    agentJournal.replaceBlobContent(journal, openReq.ContextRef, '{"kind": "main", "toml": ')
    const reloaded = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.equal(reloaded, undefined)
  })
})

// ── reload: live request wins over open ────────────────────────────────────

test('ENFORCER_resolve_cycle_prefers_live_request_over_open', async () => {
  await withHarness(async ({ journal, scope }) => {
    materializeOpen(journal, { requestId: 'req-open', json: mainJson({ toml: 'open-toml' }) })
    const live = bloggerRequestContext.main({
      requestId: 'req-live',
      mainSession: MAIN,
      bloggerSession: BLOG,
      toml: 'live-toml',
      previousIngested: 0,
      nextIngested: 1,
      previousCutoff: 0,
      nextCutoff: 1,
      nextDigest: 'd1',
      deltaDigest: sha256Hex('live-toml'),
    })
    parkedTransform.setCurrentRequest(scope, BLOG, live)
    const resolved = resolveCycleContext(scope, journal, sessionId(MAIN), sessionId(BLOG))
    assert.equal(caseOf(resolved), 'Main')
    assert.equal(resolved.fields[0].Toml, 'live-toml')
  })
})

// ── squash refusals through the cycle path ─────────────────────────────────

const primeCycle = (scope, journal) => {
  const ctx = tryRefreshMainContextFromJournal(scope, journal, sessionId(MAIN), sessionId(BLOG))
  assert.notEqual(ctx, undefined)
  parkedTransform.setCurrentRequest(scope, BLOG, ctx)
}

const seedEntryFrame = async ({ journal, scope, run, blogStep, mainSession }) => {
  primeCycle(scope, journal)
  await withImmediatePark(scope, () => run(blogStep('asst-1', 'c1', 'window one')))
  const frames = listItems(mainSession().Blog.Frames)
  assert.equal(frames.length, 1)
  return frames[0].Digest.fields[0]
}

const squashRun = async ({ journal, scope, run, blogStep, mainSession, squash }) => {
  parkedTransform.setCurrentRequest(scope, BLOG, squash)
  const before = mainSession().BloggerCycles.ByProviderRun.size
  const out = await run(blogStep('asst-sq', 'c-sq', 'squash body'))
  assert.equal(caseOf(out), 'StopPhysicalRun')
  assert.equal(out.fields[1], 'stale-cycle-catch-up-complete')
  assert.equal(mainSession().BloggerCycles.ByProviderRun.size, before, 'no receipt written')
  assert.equal(listItems(mainSession().Blog.Frames).length, 1, 'no frame appended')
}

test('ENFORCER_squash_frame_count_beyond_existing_frames_abandons', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
      const digest = await seedEntryFrame({ journal, scope, run, blogStep, mainSession })
      await squashRun({
        journal,
        scope,
        run,
        blogStep,
        mainSession,
        squash: bloggerRequestContext.squash({
          requestId: 'req-sq-k',
          mainSession: MAIN,
          bloggerSession: BLOG,
          frameEpoch: 0,
          coveredFrameCount: 2,
          digests: [digest, digest],
        }),
      })
    },
    { material: 3 },
  )
})

test('ENFORCER_squash_frame_epoch_mismatch_abandons', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
      const digest = await seedEntryFrame({ journal, scope, run, blogStep, mainSession })
      await squashRun({
        journal,
        scope,
        run,
        blogStep,
        mainSession,
        squash: bloggerRequestContext.squash({
          requestId: 'req-sq-epoch',
          mainSession: MAIN,
          bloggerSession: BLOG,
          frameEpoch: 7,
          coveredFrameCount: 1,
          digests: [digest],
        }),
      })
    },
    { material: 3 },
  )
})

test('ENFORCER_squash_frame_digests_mismatch_abandons', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
      await seedEntryFrame({ journal, scope, run, blogStep, mainSession })
      await squashRun({
        journal,
        scope,
        run,
        blogStep,
        mainSession,
        squash: bloggerRequestContext.squash({
          requestId: 'req-sq-digest',
          mainSession: MAIN,
          bloggerSession: BLOG,
          frameEpoch: 0,
          coveredFrameCount: 1,
          digests: ['sha-completely-wrong'],
        }),
      })
    },
    { material: 3 },
  )
})

test('ENFORCER_squash_other_blogger_session_abandons', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
      await seedEntryFrame({ journal, scope, run, blogStep, mainSession })
      await squashRun({
        journal,
        scope,
        run,
        blogStep,
        mainSession,
        squash: bloggerRequestContext.squash({
          requestId: 'req-sq-other',
          mainSession: MAIN,
          bloggerSession: 'ses-other-blogger',
          frameEpoch: 0,
          coveredFrameCount: 1,
          digests: ['sha-x'],
        }),
      })
    },
    { material: 3 },
  )
})
