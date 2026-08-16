// Split from tests/unit/enforcer/enforcer-cycle-commit-branches.test.mjs (cutover Wave 2a); owner: behavior-diagnosis.
//
// BD-013: the PERSIST-010 prechecks (stale staged coverage → abandon →
// catch-up drain) and the cycle-commit DU type surfaces. The commit/drain/park
// lifecycle half moved to context-compression
// (enforcer-cycle-commit-convergence.test.mjs).
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
} = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const {
  handleContinuation,
  tryRefreshMainContextFromJournal,
} = await import('../../../dist/Enforcer/Host.js')
const {
  CycleDisposition_$reflection,
  ContinuationOutcome_$reflection,
} = await import('../../../dist/Enforcer/Continuation.js')
const {
  CycleCommitOutcome_$reflection,
} = await import('../../../dist/Enforcer/Cycle/Commit.js')
const {
  FrameLoadError_$reflection,
  FrameLoadError,
} = await import('../../../dist/Enforcer/Cycle/Recovery.js')

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

// ── PERSIST-010 prechecks: staged coverage disagrees with the projection ────

test('WHAT[BD-013] ENFORCER_precheck_stale_ingest_abandons_then_catchup', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession }) => {
    // First window commits (ingest 0→3 when 3 XTrace turns exist).
    await primeCycle(scope, journal)
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

    const out = await withImmediatePark(scope, () => run(blogStep('asst-2', 'c2', 'second window')))
    assert.equal(stopReason(out), 'stale-cycle-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3, 'no double commit')
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1, 'only the first run committed')
    assert.equal(parkedTransform.peekCurrentRequest(scope, BLOG), undefined, 'stale request cleared')
    assert.equal(parkedTransform.hasFlight(scope, BLOG), false)
    },
    { material: 3 },
  )
})

test('WHAT[BD-013] ENFORCER_precheck_cutoff_mismatch_abandons', async () => {
  await withHarness(async ({ journal, scope, run, blogStep, mainSession }) => {
    await primeCycle(scope, journal)
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

    const out = await withImmediatePark(scope, () => run(blogStep('asst-2', 'c2', 'window two')))
    assert.equal(stopReason(out), 'stale-cycle-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3)
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1)
    },
    { material: 3 },
  )
})

test('WHAT[BD-013] ENFORCER_precheck_epoch_mismatch_after_squash_abandons', async () => {
  await withHarness(
    async ({ journal, scope, run, blogStep, mainSession, blogSession }) => {
    await primeCycle(scope, journal)
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

    const out = await withImmediatePark(scope, () => run(blogStep('asst-3', 'c3', 'window three')))
    assert.equal(stopReason(out), 'stale-cycle-catch-up-complete')
    assert.equal(Number(mainSession().Blog.Coverage.IngestedThroughSequence), 3)
    assert.equal(mainSession().Enforcement.ByProviderRun.size, 1, 'epoch-stale entry not committed')
    },
    { material: 3 },
  )
})

// ── DU metadata surfaces (reflection / cases) ──────────────────────────────

test('WHAT[BD-013] ENFORCER_du_reflection_surfaces_are_constructible', () => {
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
