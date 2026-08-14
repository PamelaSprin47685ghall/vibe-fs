/**
 * Split from tests/unit/execution/join-abort-clean-break.test.mjs (cutover Wave 2a);
 * owner: effect-accounting. P0 Clean Break GREEN-3 false-finality 半边
 * （EFFECT-ACCOUNTING-007）：Agent finality = Completed | Failed | Abandoned ——
 * 无 AgentAborted；LegacyFalseAbort 永不 RunCompletion；假 completion 经
 * HandleFalseCompletionRejected 确定性补偿；agent join wire 永不渲染 status="aborted"。
 * 恢复/重启侧的 delayed-recovery race 断言 → crash-reconciliation。
 *
 * Final contract (user adjudication):
 *   Agent finality = Completed | Failed | Abandoned — no AgentAborted.
 *   JoinDrain: HandleRecord → blob → DurableCompletionDecode → branch.
 *   Invalid → keep waiting (no consume, no hard error).
 *   SendFailure + body is NOT a proof (body may be status=aborted).
 *   Legacy false abort → HandleFalseCompletionRejected (not retired) or
 *   deterministic replacement + ParentJoinCorrectionRequested (retired).
 *   Agent join wire never renders status = "aborted".
 */

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import {
  agentCompletion,
  agentFact,
  agentFactCaseNames,
  agentJournal,
  bootSnapshot,
  caseOf,
  childRecovery,
  completionKind,
  envelope,
  fact,
  fold,
  forkRuntime,
  handleCompletionCodec,
  handleController,
  handleId,
  handleProjection,
  idValue,
  joinDrain,
  joinResultRenderer,
  maxJoinBatch,
  nonEmptyBatch,
  roles,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'
import * as HandleControllerModule from '../../../dist/Execution/Delegation/Handle/Controller.js'
import { HandleOwnership } from '../../../dist/Composition/Durable/Fact.js'

const PARENT = sessionId('ses_parent_clean_break')
const CHILD = sessionId('ses_child_clean_break')
const AGENT_ID = 'h-false-abort'
const HANDLE = handleId.agent(AGENT_ID)
const TARGET = 'fast-coder'
const RUN_ID = 'run-legacy-abort'

/** Production HandleController.link takes Ownership (GREEN-7); the domain.mjs
 *  facade bind is stale, so tests call the dist entry directly. */
const durableLink = async (j, parentId, agentId, child, targetAgent, role) => {
  const result = await HandleControllerModule.HandleController_link(
    j,
    parentId,
    agentId,
    child,
    targetAgent,
    role,
    HandleOwnership.DurableParentHandle,
  )
  return result.tag === 0 ? { ok: true, value: result.fields[0] } : { ok: false, error: result.fields[0] }
}

/** Plant historical false terminal: blob status=aborted + HandleCompleted(SendFailure). */
const plantLegacyFalseAbort = async (journal, { agentId = AGENT_ID, child = CHILD } = {}) => {
  const body = handleCompletionCodec.legacyAbortedBody({
    runId: RUN_ID,
    code: 'CANCELLED',
    message: 'host abort observation written as finality',
    childSessionId: idValue.session(child),
  })
  assert.match(body, /"status"\s*:\s*"aborted"/)

  const written = await agentJournal.writeBlob(body, journal)
  assert.equal(written.ok, true, written.ok ? '' : written.error)
  const receipt = written.value

  const linked = await durableLink(
    journal,
    PARENT,
    agentId,
    child,
    TARGET,
    forkRuntime.role('Coder'),
  )
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)

  // Bypass JoinableCompletion: historical SendFailure cell → aborted blob.
  const completed = await agentJournal.appendAgent(
    stream.session(PARENT),
    undefined,
    agentFact('HandleCompleted', {
      ParentSessionId: PARENT,
      Handle: handleId.agent(agentId),
      Kind: completionKind.of('SendFailure'),
      CompletionRef: receipt.BlobRef,
      CompletionDigest: receipt.BlobDigest,
    }),
    journal,
  )
  assert.equal(completed.ok, true, completed.ok ? '' : JSON.stringify(completed.error))

  return {
    body,
    blobDigest: idValue.blobDigest(receipt.BlobDigest),
    receipt,
  }
}

const lifecycleOf = (projection, handle = HANDLE) =>
  handleProjection.read(handleProjection.tryFind(handle, projection)).lifecycle

// ── GREEN-3: agent join wire never renders aborted ───────────────────────────

test('P0_CLEAN_BREAK_agent_join_wire_never_renders_aborted', () => {
  const batch = nonEmptyBatch.ofHeadTail(
    agentCompletion.failedRun({
      runId: RUN_ID,
      agentId: AGENT_ID,
      agentName: TARGET,
      code: 'CANCELLED',
      message: 'host abort was observation, not finality',
    }),
  )
  const wire = joinResultRenderer.renderCompletedBatch(joinResultRenderer.stubRuntime(), batch)
  assert.ok(!wire.includes('status = "aborted"'), 'agent join wire must never render status = "aborted"')
  assert.match(wire, /# fast-coder could not complete the charge\./)
  assert.match(wire, /# host abort was observation, not finality/)
  assert.ok(!/\bstatus\s*=/.test(wire))
})

// ── 1a. Weak proof abolished (codec layer) ───────────────────────────────────

test('P0_CLEAN_BREAK_tryFromDurableCompleted_refuses_send_failure_aborted_body', () => {
  const body = handleCompletionCodec.legacyAbortedBody({ runId: RUN_ID })
  // tryFromDurableCompleted deleted: facade returns permanent Error (weak proof abolished).
  const weak = childRecovery.tryFromDurableCompleted(
    AGENT_ID,
    HANDLE,
    CHILD,
    'SendFailure',
    body,
  )
  assert.equal(
    weak.ok,
    false,
    'SendFailure + status=aborted body must not be JoinableCompletion (weak proof abolished)',
  )
})

// ── 1b. Real journal blob + restart fold + JoinDrain ─────────────────────────

test('P0_CLEAN_BREAK_legacy_aborted_blob_after_restart_join_drain_must_not_return_aborted', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-join-abort-cb-restart-'))
  const created = await agentJournal.create({ directory: dir, runtime: 'rt_pre' })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))

  await plantLegacyFalseAbort(created.journal)

  // Pre-restart: legacy abort blob is not a RunCompletion (decode → LegacyFalseAbort).
  {
    const projection = agentJournal.handleProjection(created.journal, PARENT)
    assert.equal(lifecycleOf(projection), 'CompletedAwaitingJoin')
    const record = handleProjection.tryFind(HANDLE, projection)
    const decoded = await handleCompletionCodec.tryRead(created.journal, record, AGENT_ID)
    assert.equal(decoded.ok, false, 'legacy abort must not materialise RunCompletion')
    const body = handleCompletionCodec.legacyAbortedBody({
      runId: RUN_ID,
      code: 'CANCELLED',
      message: 'host abort observation written as finality',
      childSessionId: idValue.session(CHILD),
    })
    assert.equal(caseOf(handleCompletionCodec.decodeBody(body)), 'LegacyFalseAbort')
  }

  created.dispose()

  const boot = await bootSnapshot.load(dir)
  const restarted = await agentJournal.createFromBoot({
    directory: dir,
    boot,
    runtime: 'rt_post',
  })
  assert.equal(restarted.ok, true, restarted.ok ? '' : JSON.stringify(restarted.error))

  try {
    const j = restarted.journal
    const before = agentJournal.handleProjection(j, PARENT)
    assert.equal(lifecycleOf(before), 'CompletedAwaitingJoin')
    assert.equal(handleProjection.joinable(before).length, 1)
    assert.equal(handleProjection.isRetired(HANDLE, before), false)

    // Production JoinDrain path (JoinTool / HostForkRuntime durable drain).
    const drained = await joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(drained.ok, true, drained.ok ? '' : drained.error)

    // RED: current code returns status=aborted and CAS-retires the handle.
    const abortedItems = drained.items.filter((i) => i.status === 'aborted')
    assert.equal(
      abortedItems.length,
      0,
      `JoinDrain must not surface agent aborted; got ${JSON.stringify(drained.items)}`,
    )
    assert.equal(
      drained.items.filter((i) => i.agentId === AGENT_ID).length,
      0,
      'parent join must not return agent result for false-abort handle',
    )

    const after = agentJournal.handleProjection(j, PARENT)
    assert.equal(
      handleProjection.isRetired(HANDLE, after),
      false,
      'legacy false abort must not be CAS-retired as normal completion',
    )

    // Compensation fact (not in algebra yet → RED when asserted present).
    const cases = agentFactCaseNames()
    assert.ok(
      cases.includes('HandleFalseCompletionRejected'),
      `AgentFact must include HandleFalseCompletionRejected; have: ${cases.join(', ')}`,
    )

    const life = lifecycleOf(after)
    assert.ok(
      life === 'Active' || life === 'CompletedAwaitingJoin',
      `child must stay recoverable after reject, lifecycle=${life}`,
    )
  } finally {
    restarted.dispose()
  }
})

// ── 2. Already-Retired migration ─────────────────────────────────────────────

test('P0_CLEAN_BREAK_retired_legacy_abort_creates_replacement_once', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-join-abort-cb-retired-'))
  const created = await agentJournal.create({ directory: dir, runtime: 'rt_pre' })
  assert.equal(created.ok, true)

  await plantLegacyFalseAbort(created.journal)

  // Historical path: parent already retired under old code (force tombstone; do not
  // use new JoinDrain which rejects without retiring).
  const forcedRetire = await agentJournal.appendAgent(
    stream.session(PARENT),
    undefined,
    agentFact('HandleRetired', { ParentSessionId: PARENT, Handle: HANDLE }),
    created.journal,
  )
  assert.equal(forcedRetire.ok, true, forcedRetire.ok ? '' : JSON.stringify(forcedRetire.error))
  assert.equal(
    handleProjection.isRetired(HANDLE, agentJournal.handleProjection(created.journal, PARENT)),
    true,
  )
  created.dispose()

  const boot = await bootSnapshot.load(dir)
  const restarted = await agentJournal.createFromBoot({ directory: dir, boot, runtime: 'rt_post' })
  assert.equal(restarted.ok, true, restarted.ok ? '' : JSON.stringify(restarted.error))

  try {
    const j = restarted.journal
    // Trigger clean-break reconcile (retired LegacyFalseAbort → replacement).
    const drained = await joinDrain.drainFromJournal(j, PARENT, maxJoinBatch)
    assert.equal(drained.ok, true, drained.ok ? '' : drained.error)
    assert.equal(
      drained.items.filter((i) => i.status === 'aborted').length,
      0,
      'retired false abort must not surface aborted to parent',
    )

    const after = agentJournal.handleProjection(j, PARENT)

    assert.equal(handleProjection.isRetired(HANDLE, after), true)

    const cases = agentFactCaseNames()
    assert.ok(
      cases.includes('ParentJoinCorrectionRequested'),
      'AgentFact must include ParentJoinCorrectionRequested for retired false terminal',
    )

    // Deterministic replacement: recovery:<H>:<bad-digest> — pure, once.
    const listed = handleProjection
      .listable(after)
      .map((r) => handleProjection.read(r).handle)
      .filter((h) => h !== handleId.describe(HANDLE))
    const active = handleProjection
      .activeHandles(after)
      .map((r) => handleProjection.read(r).handle)
      .filter((h) => h !== handleId.describe(HANDLE))
    const replacements = [...new Set([...listed, ...active])]
    assert.ok(
      replacements.length >= 1,
      `expected deterministic replacement handle; listed=${JSON.stringify(listed)} active=${JSON.stringify(active)}`,
    )

    // Repeat recovery: same replacement set (no second mint).
    const boot2 = await bootSnapshot.load(dir)
    const again = await agentJournal.createFromBoot({
      directory: dir,
      boot: boot2,
      runtime: 'rt_post2',
    })
    assert.equal(again.ok, true)
    try {
      const drained2 = await joinDrain.drainFromJournal(again.journal, PARENT, maxJoinBatch)
      assert.equal(drained2.ok, true)
      const after2 = agentJournal.handleProjection(again.journal, PARENT)
      const againIds = [
        ...handleProjection.listable(after2).map((r) => handleProjection.read(r).handle),
        ...handleProjection.activeHandles(after2).map((r) => handleProjection.read(r).handle),
      ].filter((h) => h !== handleId.describe(HANDLE))
      assert.deepEqual(
        [...new Set(againIds)].sort(),
        replacements.sort(),
        'repeat recovery must not mint a second replacement handle',
      )
    } finally {
      again.dispose()
    }
  } finally {
    restarted.dispose()
  }
})

// ── 4. Full-history property skeleton ────────────────────────────────────────

test('P0_CLEAN_BREAK_property_join_agent_item_implies_v2_terminal_proof', () => {
  /**
   * ∀ join result AgentItem x → history has v2 terminal blob + HandleCompleted
   * + finality ∈ {completed, failed}.
    * Legacy abort body → not Current RunCompletion (agent finality has no aborted).
    */
  const record = {
    Handle: handleId.agent('a'),
    ChildSessionId: CHILD,
    TargetAgent: TARGET,
    CanonicalRole: roles.of('Coder'),
    Lifecycle: undefined,
    CreationOrder: 0,
    LastCompletion: undefined,
  }

  const legacy = handleCompletionCodec.legacyAbortedBody({ runId: 'r1' })
  const legacyDecoded = handleCompletionCodec.tryDecode(record, 'a', legacy)
  assert.equal(legacyDecoded.ok, false, 'legacy abort must not materialise RunCompletion')
  const legacyBranch = handleCompletionCodec.decodeBody(legacy)
  assert.equal(caseOf(legacyBranch), 'LegacyFalseAbort')

  const completedBody = handleCompletionCodec.encodeOutcome(
    'r2',
    agentCompletion.completedRun({
      runId: 'r2',
      agentId: 'a2',
      agentName: TARGET,
      workRecord: 'ok',
    }).Outcome,
  )
  const decoded = handleCompletionCodec.tryDecode(record, 'a', completedBody)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  const outcomeCase = caseOf(decoded.value.Outcome)
  assert.ok(
    outcomeCase === 'AgentCompleted' || outcomeCase === 'AgentFailed' || outcomeCase === 'AgentAbandoned',
    `agent outcome must be completed|failed|abandoned; got ${outcomeCase}`,
  )
  assert.notEqual(outcomeCase, 'AgentAborted')
  const parsed = JSON.parse(completedBody)
  assert.equal(parsed.schemaVersion, 2)
  assert.ok(parsed.finality === 'completed' || parsed.finality === 'failed')
})

// ── Fold: historical SendFailure cell shape ──────────────────────────────────

test('P0_CLEAN_BREAK_fold_replays_send_failure_as_awaiting_join_no_compensation_fact_yet', () => {
  const facts = [
    fact('HandleLinked', {
      ParentSessionId: PARENT,
      ChildSessionId: CHILD,
      Handle: HANDLE,
      TargetAgent: TARGET,
      CanonicalRole: roles.of('Coder'),
    }),
    fact('HandleCompleted', {
      ParentSessionId: PARENT,
      Handle: HANDLE,
      Kind: completionKind.of('SendFailure'),
      CompletionRef: undefined,
      CompletionDigest: undefined,
    }),
  ]
  const folded = fold.apply(
    fold.empty,
    facts.map((value, index) => envelope({ seq: index + 1, stream: stream.session(PARENT), fact: value })),
  )
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const handles = fold.session(folded.value, 'ses_parent_clean_break').Handles
  const state = handleProjection.read(handleProjection.tryFind(HANDLE, handles))
  assert.equal(state.lifecycle, 'CompletedAwaitingJoin')
  assert.equal(state.completion, 'SendFailure')

  // GREEN-2: compensation fact is in the algebra.
  assert.ok(
    agentFactCaseNames().includes('HandleFalseCompletionRejected'),
    'AgentFact must include HandleFalseCompletionRejected',
  )
})
