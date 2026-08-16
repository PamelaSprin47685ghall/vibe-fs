// XWIRE_: XWire.applyTransform / reconcileAttempt — the X-wire recovery
// adapter. Early-exit guards are driven with partial fakes; the probe
// selection path runs a REAL journal (authority + fallback cursor + blog
// frames with real blobs) so candidate materialisation, frozen-record blob
// writes, digest proofs, prefix rendering, in-place output mutation and
// arming consumption are all production code.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  authorityRoot,
  blobDigest,
  blobRef,
  fact,
  fold,
  idValue,
  listItems,
  logicalRunId,
  physicalUser,
  providerProjection,
  providerRun,
  sessionId,
  stream,
  toList,
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'
import { applyTransform, reconcileAttempt } from '../../../dist/Context/Prefix/Wire.js'
const scopeModule = await import('../../../dist/OpenCode/Host/PluginRuntimeScope.js')
const { PluginRuntimeScope, PluginRuntimeScope__TryAttemptPlan } = scopeModule
// Resolve Fable-exported members by prefix; the hash suffix is a compiler
// artifact and must not be pinned in tests (VERIFY-008).
const armRecovery = Object.entries(scopeModule).find(([k]) => k.startsWith('PluginRuntimeScope__ArmRecovery_'))?.[1]
const tryRecoveryArming = Object.entries(scopeModule).find(([k]) => k.startsWith('PluginRuntimeScope__TryRecoveryArming_'))?.[1]

const makeScope = (journal) => new PluginRuntimeScope(journal)
import { SessionMessage } from '../../../dist/OpenCode/Host/SessionSnapshotPort.js'
import { sha256Hex } from '../../../dist/Host/Digest.js'
import { buildTurn } from '../../../dist/Interaction/Repair/CompletedTurn.js'

const SESSION = 'ses_x'
const session = sessionId(SESSION)

const liveJournal = async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-xwire-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')
  const append = async (factValue, run = undefined) => {
    const result = await agentJournal.appendAgent(
      stream.session(session),
      run === undefined ? undefined : providerRun(run),
      factValue,
      opened.journal,
    )
    assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  }
  return {
    journal: opened.journal,
    append,
    snapshot: () => agentJournal.snapshot(opened.journal),
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

const seedAuthority = async (append) => {
  await append(
    agentFact('AuthorityRootAccepted', {
      SessionId: session,
      LogicalRunId: logicalRunId('run-1'),
      AuthorityRootUserMessageId: authorityRoot('user-1'),
      AuthorityKind: 'HumanRoot',
      SelectedAgent: 'fast-coder',
      PeerAgent: 'deep-coder',
      CanonicalRole: 'coder',
      SelectedTier: 'fast',
    }),
  )
}

const seedFallbackCursor = async (append) => {
  await append(
    agentFact('FallbackCursorAdvanced', {
      SessionId: session,
      LogicalRunId: logicalRunId('run-1'),
      AuthorityRootUserMessageId: authorityRoot('user-1'),
      ProviderRun: providerRun('asst-0'),
      PreviousOffset: 0,
      NextOffset: 1,
      ConsecutiveFailureCount: 1,
      Reason: 'provider_error',
    }),
  )
}

/** Blog frame whose TextRef points at a REAL blob in the journal. */
const seedBlogFrame = async (append, writeBlob, { body = 'frame body', cutoff = 2, digest } = {}) => {
  const written = await agentJournal.writeBlob(body, writeBlob)
  assert.equal(written.ok, true, written.ok ? '' : JSON.stringify(written.error))
  await append(
    agentFact('BlogObservationCommitted', {
      SessionId: session,
      BloggerSessionId: sessionId('ses_blogger'),
      RequestId: { tag: 0, fields: ['req-e1'] },
      FrameEpochId: { tag: 0, fields: [0n] },
      PreviousIngestedThroughSequence: BigInt(0),
      NextIngestedThroughSequence: BigInt(1),
      PreviousCoverableTurnCutoffExclusive: 0,
      NextCoverableTurnCutoffExclusive: cutoff,
      NextCoveredPrefixDigest: digest,
      TextRef: written.value.BlobRef,
      TextDigest: written.value.BlobDigest,
      ProviderRun: providerRun('msg_e1'),
      ToolCallIds: [],
      TipRuleId: 'enforcement-tip-1',
      FieldNameAtCommit: 'field-tip-1',
      EvidenceRef: undefined,
      ObservedPrefixEpochId: { tag: 0, fields: [0n] },
    }),
    'msg_e1',
  )
  return written.value
}

/** Transform output: session-tagged messages; `user-1` NOT at index 0. */
const transformOutput = (extra = []) => ({
  messages: [
    { info: { id: 'm0', role: 'user' }, parts: [{ type: 'text', text: 'history' }] },
    { info: { id: 'user-1', role: 'user', sessionID: SESSION }, parts: [{ type: 'text', text: 'the ask' }] },
    { info: { id: 'asst-9', role: 'assistant' }, parts: [{ type: 'text', text: 'streaming answer' }] },
    ...extra,
  ],
})

const snapshotPort = ({ messages, error } = {}) => ({
  GetMessages: async (sid) =>
    error ? { tag: 1, fields: [error] } : { tag: 0, fields: [toList(messages ?? [assistantMessage()])] },
})

const assistantMessage = ({ id = 'asst-9', parentId = 'user-1', finish = undefined, completed = false } = {}) =>
  new SessionMessage(id, 'assistant', undefined, finish, undefined, undefined, parentId, completed, false, undefined, [
    { type: 'text', text: 'streaming answer' },
  ])

/** semantic digest of the raw messages truncated at `cutoff` — what CTX-011 proves. */
const cutoffDigestOf = (rawMessages, cutoff) => {
  const current = providerProjection.toSemantic(providerProjection.decodeMessageView(toList(rawMessages)))
  return sha256Hex(
    providerProjection.renderSemantic({
      ...current,
      Messages: toList(listItems(current.Messages).slice(0, cutoff)),
    }),
  )
}

// ── early exits ─────────────────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_journal_is_a_noop', async () => {
  const scope = makeScope(undefined)
  await applyTransform(snapshotPort(), undefined, scope, transformOutput())
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_session_id_in_output_is_a_noop', async () => {
  const live = await liveJournal()
  const scope = makeScope(live.journal)
  await applyTransform(snapshotPort(), live.journal, scope, { messages: [{ info: {}, parts: [] }] })
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_unarmed_session_is_a_noop', async () => {
  const live = await liveJournal()
  await seedAuthority(live.append)
  const scope = makeScope(live.journal)
  await applyTransform(snapshotPort(), live.journal, scope, transformOutput())
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_physical_user_message_throws', async () => {
  const live = await liveJournal()
  await seedAuthority(live.append)
  const scope = makeScope(live.journal)
  armRecovery(scope, session)
  const output = { messages: [{ info: { id: 'asst-9', role: 'assistant', sessionID: SESSION }, parts: [] }] }
  await assert.rejects(() => applyTransform(snapshotPort(), live.journal, scope, output), /physical user message/)
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_snapshot_port_throws', async () => {
  const live = await liveJournal()
  await seedAuthority(live.append)
  const scope = makeScope(live.journal)
  armRecovery(scope, session)
  await assert.rejects(() => applyTransform(undefined, live.journal, scope, transformOutput()), /public session snapshot/)
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_snapshot_error_throws', async () => {
  const live = await liveJournal()
  await seedAuthority(live.append)
  const scope = makeScope(live.journal)
  armRecovery(scope, session)
  await assert.rejects(
    () => applyTransform(snapshotPort({ error: 'snapshot exploded' }), live.journal, scope, transformOutput()),
    /snapshot exploded/,
  )
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_unbindable_run_throws', async () => {
  const live = await liveJournal()
  await seedAuthority(live.append)
  const scope = makeScope(live.journal)
  armRecovery(scope, session)
  // The assistant answers a different user message: no bindable run after user-1.
  await assert.rejects(
    () => applyTransform(snapshotPort({ messages: [assistantMessage({ parentId: 'other-parent' })] }), live.journal, scope, transformOutput()),
    /X-wire run binding failed/,
  )
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_projections_throws', async () => {
  const live = await liveJournal()
  const scope = makeScope(live.journal)
  armRecovery(scope, session)
  await assert.rejects(
    () => applyTransform(snapshotPort(), live.journal, scope, transformOutput()),
    /cannot plan a retry without authority, fallback, and session projections/,
  )
  live.cleanup()
})

// ── the probe path: full journal, real blobs, arming consumed ───────────────

const armedProbeSetup = async ({ cutoff = 2, digest } = {}) => {
  const live = await liveJournal()
  await seedAuthority(live.append)
  await seedFallbackCursor(live.append)
  const output = transformOutput()
  const resolvedDigest = digest ?? cutoffDigestOf(output.messages, 1)
  await seedBlogFrame(live.append, live.journal, { cutoff, digest: resolvedDigest })
  const scope = makeScope(live.journal)
  armRecovery(scope, session)
  return { live, scope, output, resolvedDigest }
}

test('WHAT[HOST-BOUNDARY-021] XWIRE_probe_plan_renders_synthetic_prefix_and_consumes_arming', async () => {
  const { live, scope, output } = await armedProbeSetup()

  await applyTransform(snapshotPort(), live.journal, scope, output)

  // The write-back happened in place: a synthetic memory message leads the list.
  assert.equal(output.messages[0].info.role, 'user')
  assert.match(String(output.messages[0].info.id), /^[0-9a-f]{64}$/, 'companion memory message id is the seal-derived hash')
  assert.equal(output.messages[0].parts[0].type, 'text')
  assert.match(String(output.messages[0].parts[0].text), /Opening|frame body/, 'frozen record prefix becomes the memory')

  // The plan is recorded for this session+run and the one-shot arming is spent.
  const plan = PluginRuntimeScope__TryAttemptPlan(scope, session, providerRun('asst-9'))
  assert.ok(plan, 'attempt plan recorded')
  assert.equal(plan.Profile.ProjectionChoice.tag, 1, 'UsePrefixProbe: the probe is attached')
  assert.equal(tryRecoveryArming(scope, session), undefined, 'a probe was selected: arming consumed (FALLBACK-012)')
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_material_spends_slot_without_probe', async () => {
  // CTX-011: coverage not ahead of the request start → no candidate → the
  // armed slot must survive so a later main can still probe.
  const live = await liveJournal()
  await seedAuthority(live.append)
  await seedFallbackCursor(live.append)
  const output = transformOutput()
  await seedBlogFrame(live.append, live.journal, { cutoff: 2, digest: cutoffDigestOf(output.messages, 1) })
  const scope = makeScope(live.journal)
  armRecovery(scope, session)

  // Physical message at index 0 → requestStartCutoff 0 → candidateCutoff 0.
  const zeroIndexOutput = {
    messages: [
      { info: { id: 'user-1', role: 'user', sessionID: SESSION }, parts: [{ type: 'text', text: 'the ask' }] },
      { info: { id: 'asst-9', role: 'assistant' }, parts: [{ type: 'text', text: 'answer' }] },
    ],
  }
  await applyTransform(snapshotPort(), live.journal, scope, zeroIndexOutput)

  assert.equal(tryRecoveryArming(scope, session), undefined, 'no candidate: the plan is probe-less and the slot is spent (only NoCoverage is temporary)')
  const plan = PluginRuntimeScope__TryAttemptPlan(scope, session, providerRun('asst-9'))
  assert.ok(plan, 'plan still recorded for the ordinary main')
  assert.equal(plan.Profile.ProjectionChoice.tag, 0, 'UseCommittedEpoch: no probe attached')
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_probe_reconcile_promotes_prefix_rebase_fact', async () => {
  const { live, scope } = await armedProbeSetup({ cutoff: 2 })
  const output = transformOutput()

  await applyTransform(snapshotPort(), live.journal, scope, output)
  assert.equal(tryRecoveryArming(scope, session), undefined, 'precondition: probe selected, arming spent')

  const completedTurn = buildTurn(
    session,
    physicalUser('user-1'),
    authorityRoot('user-1'),
    new SessionMessage('asst-9', 'assistant', undefined, 'stop', undefined, undefined, 'user-1', true, false, undefined, [
      xTraceCapture.text('done'),
    ]),
    undefined,
    '/repo/dir',
  )

  await reconcileAttempt(live.journal, scope, completedTurn)

  const prefix = agentJournal.snapshot(live.journal).AgentProjections.Sessions.get(session).PrefixEpoch
  assert.equal(idValue.prefixEpoch(prefix.EpochId), 1n, 'epoch advanced by the promotion')
  assert.equal(prefix.Snapshot.CutoffExclusive, 1)
  assert.equal(PluginRuntimeScope__TryAttemptPlan(scope, session, providerRun('asst-9')), undefined, 'plan cleared after promotion')
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_failed_attempt_clears_plan_without_promoting', async () => {
  const { live, scope } = await armedProbeSetup({ cutoff: 2 })
  const output = transformOutput()

  await applyTransform(snapshotPort(), live.journal, scope, output)

  const failedTurn = buildTurn(
    session,
    physicalUser('user-1'),
    authorityRoot('user-1'),
    new SessionMessage('asst-9', 'assistant', undefined, 'error', 'Boom', undefined, 'user-1', true, false, undefined, [xTraceCapture.text('x')]),
    undefined,
    '/repo/dir',
  )
  await reconcileAttempt(live.journal, scope, failedTurn)

  const sessionState = agentJournal.snapshot(live.journal).AgentProjections.Sessions.get(session)
  assert.equal(sessionState.PrefixEpoch, undefined, 'no promotion from a failed attempt')
  assert.equal(PluginRuntimeScope__TryAttemptPlan(scope, session, providerRun('asst-9')), undefined, 'plan cleared')
  live.cleanup()
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_unknown_reread_keeps_the_plan', async () => {
  const { live, scope } = await armedProbeSetup({ cutoff: 2 })
  const output = transformOutput()

  await applyTransform(snapshotPort(), live.journal, scope, output)

  const unknownTurn = buildTurn(
    session,
    physicalUser('user-1'),
    authorityRoot('user-1'),
    new SessionMessage('asst-9', 'assistant', undefined, undefined, undefined, undefined, 'user-1', false, false, undefined, [xTraceCapture.text('x')]),
    undefined,
    '/repo/dir',
  )
  await reconcileAttempt(live.journal, scope, unknownTurn)

  assert.ok(PluginRuntimeScope__TryAttemptPlan(scope, session, providerRun('asst-9')), 'a provisional reread must keep the plan')
  live.cleanup()
})
