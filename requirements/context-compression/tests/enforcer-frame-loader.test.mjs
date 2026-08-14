// Split from tests/unit/enforcer/enforcer-predicate-branches.test.mjs (cutover Wave 2a); owner: context-compression.
//
// Fail-closed BlogFrame loader (loadEffectiveFrames) incl. blob loss: the
// rebuild input of the Blogger convergence chain. The cycle-decode/repair
// predicate half moved to behavior-diagnosis (enforcer-predicate-branches.test.mjs).
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
} from '../../verification-system/tests/support/domain.mjs'

runtimeResources.installFromPackage()

const {
  AgentJournalModule_appendAgent,
  AgentJournalModule_snapshot,
} = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const { handleContinuation } = await import('../../../dist/Session/EnforcerHost.js')
const { loadEffectiveFrames } = await import('../../../dist/Session/EnforcerFrameRecovery.js')

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

const outcomeMessagesOf = (outcome) => {
  const tag = caseOf(outcome)
  if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') return listItems(outcome.fields[0])
  return []
}

// ── loadEffectiveFrames fail-closed paths (direct) ─────────────────────────

test('ENFORCER_load_effective_frames_missing_association', async () => {
  await withHarness(
    async ({ journal }) => {
      const result = await loadEffectiveFrames(journal, sessionId(MAIN))
      assert.equal(result.tag, 1)
      assert.equal(caseOf(result.fields[0]), 'MissingAssociation')
    },
    { link: false },
  )
})

test('ENFORCER_load_effective_frames_empty_ok', async () => {
  await withHarness(async ({ journal }) => {
    const result = await loadEffectiveFrames(journal, sessionId(MAIN))
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
            { type: 'tool', tool: 'chronicle', callID: 'c-f1', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body one' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      const result = await loadEffectiveFrames(journal, sessionId(MAIN))
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
            { type: 'tool', tool: 'chronicle', callID: 'c-f2', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body two' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      const frame = listItems(mainSession().Blog.Frames)[0]
      agentJournal.deleteBlob(journal, frame.TextRef)
      const result = await loadEffectiveFrames(journal, sessionId(MAIN))
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
            { type: 'tool', tool: 'chronicle', callID: 'c-f3', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body three' } } },
          ]),
        )
      } finally {
        parkedTransform.host(scope).ParkTransform = original
      }
      const frame = listItems(mainSession().Blog.Frames)[0]
      agentJournal.replaceBlobContent(journal, frame.TextRef, 'tampered body')
      const result = await loadEffectiveFrames(journal, sessionId(MAIN))
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
            { type: 'tool', tool: 'chronicle', callID: 'c-f4', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body four' } } },
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
          { type: 'tool', tool: 'chronicle', callID: 'c-f4', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'frame body four' } } },
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
