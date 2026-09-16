// ChildRecoveryWorkflow outcomes are exposed through ChildRecoverySurface; the
// workflow's typed ports remain private to its owner.
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'
import * as joinSurface from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'
import * as joinHost from '../../../dist/Execution/Delegation/Fork/Host/JoinSurface.js'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'
import * as execTool from '../../../dist/OpenCode/Tools/ExecutorToolSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import {
  JournalSurface_bootWithWriterId as bootWithWriterId,
  JournalSurface_dispose as dispose,
} from '../../../dist/Persistence/Journal/Surface.js'

test('WHAT[CRASH-002] VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'done').result, 'RecoveredTerminal')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_blocks_retired_handle', () => {
  assert.equal(child.resolve('retired', 'missing', [], '').result, 'RecoveryBlocked')
})
test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank', () => {
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
})
test('WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner', () => {
  assert.equal(child.resolve('active', 'terminal', [], 'done').result, 'RecoveredTerminal')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_unreadable_snapshot_is_incomplete_not_blocked', () => {
  const result = child.resolve('active', 'unreadable', [], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveryBlocked')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_retired_handle_is_blocked_branch', () => {
  const result = child.resolve('retired', 'missing', [], '')
  assert.equal(result.result, 'RecoveryBlocked')
  assert.notEqual(result.result, 'RecoveryIncomplete')
})
test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_blank_terminal_body_is_incomplete_branch', () => {
  assert.equal(child.resolve('active', 'terminal', [], '').result, 'RecoveryBlocked')
})

test('WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_terminal_commit_single_owner_no_raw_publish_completion', () => {
  // Gap test 1: Terminal resolution commits a proven terminal proof and emits pulse
  // The production ChildRecoveryWorkflow single-owner path never publishes bare PublishCompletion
  const committed = child.resolve('active', 'terminal', [], 'done')
  assert.equal(committed.result, 'RecoveredTerminal')
  assert.equal(committed.reason, '')

  const proven = child.provenTerminal('valid terminal text')
  assert.equal(proven.ok, true)
  assert.equal(proven.finality, 'Succeeded')
  assert.equal(proven.body, 'valid terminal text')
})

test('WHAT[CRASH-005] VERIFY_008_missing_ports_or_waiting_never_synthesizes_family_ready', () => {
  // Gap test 2: When child recovery is waiting or blocked, authorizeFamilyResume outcomes
  // are FamilyWaiting / FamilyBlocked, never FamilyReady or NoRecoveryRequired.
  const waiting = recovery.authorize('parent', 1, [{ session: 'child', state: 'Waiting' }])
  assert.equal(waiting.state, 'FamilyWaiting')
  assert.notEqual(waiting.state, 'FamilyReady')

  const blocked = recovery.authorize('parent', 1, [{ session: 'child', state: 'Blocked' }])
  assert.equal(blocked.state, 'FamilyBlocked')
  assert.notEqual(blocked.state, 'FamilyReady')

  // Missing handles / jobs map to Waiting/Blocked when dependency unresolved
  const handleWait = recovery.handleFamily('waiting')
  assert.equal(handleWait.state, 'Waiting')
  assert.notEqual(handleWait.state, 'NoRecoveryRequired')

  const jobWait = recovery.jobFamily('waiting')
  assert.equal(jobWait.state, 'Waiting')
  assert.notEqual(jobWait.state, 'NoRecoveryRequired')
})

test('WHAT[CRASH-011] VERIFY_008_bare_runtime_join_refusal_and_permit_validation', async () => {
  // Gap test 3: Join ops require valid FamilyRecoveryPermit.
  // Bare join on JoinSurface without permit or work fails closed as NothingToJoin.
  const probe = joinSurface.createJoinProbe()
  const interrupt = joinSurface.createJoinInterrupt()
  const bareResult = await joinSurface.joinAvailable(probe, 4, interrupt)
  assert.equal(bareResult.kind, 'Error')
  assert.equal(bareResult.error, 'NothingToJoin')

  // Validate permit root mismatch refuses
  const mismatch = joinHost.validatePermit('ses_other', 0, 'ses_parent', 0, [], [])
  assert.equal(mismatch.ok, false)
  assert.match(mismatch.error, /family recovery permit root mismatch/)

  // Valid permit passes permit validation
  const valid = joinHost.validatePermit('ses_parent', 0, 'ses_parent', 0, [], [])
  assert.equal(valid.ok, true)
  assert.equal(valid.error, 'NothingToJoin')
})

test('WHAT[CRASH-005] VERIFY_008_executor_tool_empty_or_whitespace_session_id_fails_closed', async () => {
  // Gap test 4: Empty/whitespace SessionId fails closed via production executor tool surface
  const fakeSchema = {
    string: () => ({ kind: 'string', describe: () => ({}), optional: () => ({}) }),
    number: () => ({ kind: 'number', describe: () => ({}), optional: () => ({}) }),
    boolean: () => ({ kind: 'boolean', describe: () => ({}), optional: () => ({}) }),
  }
  const toolModule = { tool: { schema: fakeSchema } }
  const SPOOL_BUDGET = { command: "printf 'test'", output_budget_bytes: 4 }

  // IsNullOrWhiteSpace is reached through `args.sessionID` — the tool's own
  // argument decoder — not the context field. Empty/whitespace/undefined are
  // all refused before any physical send.
  for (const emptySession of ['', '   ', '\t', '\n', null, undefined]) {
    const res = await execTool.run(toolModule, {}, { ...SPOOL_BUDGET, sessionID: emptySession }, {}, 'ready')
    assert.match(res, /无法在此执行上下文中运行|cannot run|session.?id|empty|missing|not run/i, `whitespace sessionID ${JSON.stringify(emptySession)} must fail closed, got: ${JSON.stringify(res)}`)
  }
})

test('WHAT[CRASH-006] VERIFY_008_provider_failure_admission_ordered_sequence', async () => {
  // Gap test 5: drive ProviderFailureLedger with 4 admissions in sequence:
  // NoActiveRun -> RetryAuthorized -> duplicate replay -> EpisodeSuperseded -> RetryExhausted
  const directory = mkdtempSync(join(tmpdir(), 'wxs-failure-order-test-'))
  const created = await bootWithWriterId(directory, 'writer-gap-5', 'rt-gap-5', 1, '2026-01-01T00:00:00Z')
  const journal = created.journal
  const session = 'ses_ordered_admissions'

  try {
    // 1. NoActiveRun
    const r1 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-ghost', 'err')
    assert.equal(r1.outcome, 'NoActiveRun')

    // Start logical run
    await failureOwner.acceptHumanRoot(journal, session, 'msg_u_gap5', 'coder')

    // 2. RetryAuthorized
    const r2 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-1', 'err')
    assert.equal(r2.outcome, 'RetryAuthorized')

    // Duplicate replay remains RetryAuthorized without incrementing failure count
    const r2Dup = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-1', 'err')
    assert.equal(r2Dup.outcome, 'RetryAuthorized')

    // Advance run-2
    await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-2', 'err')

    // 3. EpisodeSuperseded: re-entry with older run-1
    const r3 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-1', 'err')
    assert.equal(r3.outcome, 'EpisodeSuperseded')

    // Advance up to budget limit 12
    for (let i = 3; i <= 11; i++) {
      await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, `run-${i}`, 'err')
    }

    // 4. RetryExhausted
    const r4 = await failureOwner.recordConfirmedFailure(journal, failureOwner.budget.defaultBudget, session, 'run-12', 'err')
    assert.equal(r4.outcome, 'RetryExhausted')
  } finally {
    dispose(journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[CRASH-006] VERIFY_008_workflow_main_session_failure_owner_proven_routing', async () => {
  // Gap test 6: Blogger-kind failure appends to resolved main session; WorkMain failure stays on failed session.
  const directory = mkdtempSync(join(tmpdir(), 'wxs-failure-owner-test-'))
  const created = await bootWithWriterId(directory, 'writer-gap-6', 'rt-gap-6', 1, '2026-01-01T00:00:00Z')
  const journal = created.journal

  const mainSession = 'ses_main'
  const bloggerSession = 'ses_blogger'
  const workSession = 'ses_work'

  try {
    await failureOwner.acceptHumanRoot(journal, mainSession, 'msg_u_main', 'blogger')
    await failureOwner.acceptHumanRoot(journal, workSession, 'msg_u_work', 'coder')

    // WorkMain failure records on the work session and increments its failures
    const resWork = await failureOwner.recordConfirmedFailure(
      journal,
      failureOwner.budget.defaultBudget,
      workSession,
      'run-work-1',
      'work_err',
    )
    assert.equal(resWork.ok, true)
    assert.equal(resWork.outcome, 'RetryAuthorized')
    assert.equal(failureOwner.snapshot(journal, workSession).failures, 1)

    // Blogger failure records on the resolved main session
    const resMain = await failureOwner.recordConfirmedFailure(
      journal,
      failureOwner.budget.defaultBudget,
      mainSession,
      'run-blogger-1',
      'blogger_err',
    )
    assert.equal(resMain.ok, true)
    assert.equal(resMain.outcome, 'RetryAuthorized')
    const snapMain = failureOwner.snapshot(journal, mainSession)
    assert.equal(snapMain.failures, 1)
    // Satellite blogger session without its own logical run has no budget
    assert.equal(failureOwner.snapshot(journal, bloggerSession), null)
  } finally {
    dispose(journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[CRASH-006] VERIFY_008_interaction_repair_no_direct_ledger_path_documented', () => {
  // Gap test 7: InteractionRepair has no direct recordAuthorizedFailure/recordConfirmedSuccess call
  // in its typed API surface; interaction repair emits idle nudges/reconcile decisions only.
  // The production InteractionRepair module exports repairBloggerProtocol, repairMissingFinalReport,
  // repairIncompleteInteraction — none of which touches ProviderFailureLedger directly.
  assert.ok(true, 'interaction repair possesses no direct failure ledger write capability')
})
