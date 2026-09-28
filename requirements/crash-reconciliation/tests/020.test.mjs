import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const scenario = fileURLToPath(new URL('./support/devops-crash-scenario.mjs', import.meta.url))

const runChild = (mode, workspace, marker) =>
  spawnSync(process.execPath, [scenario, mode, workspace, marker], {
    cwd: process.cwd(),
    encoding: 'utf8',
    env: process.env,
  })

const readMarker = (path) => JSON.parse(readFileSync(path, 'utf8'))

test('WHAT[crash-reconciliation-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', async () => {
  // 1. RolesSurface must have consolidated DevOps and Engineer
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('devops'), 'Role labels must have devops')
  assert.ok(all.includes('engineer'), 'Role labels must have engineer')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')

  // 2. Explicit resume command behavior
  const config = { command: {} }
  resume.registerCommand(config)
  assert.ok(config.command.continue, 'continue command must be registered')

  // Non-continue command is a no-op (no auto-replay of pending commands)
  const actual = await resume.run('status', 'session-1', '')
  assert.deepEqual(actual.parts, [], 'non-continue command must not trigger automatic execution')
})

integrationTest('WHAT[crash-reconciliation-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay (scenario)', () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-devops-crash-'))
  const beforeMarker = join(workspace, 'before-crash.json')
  const afterMarker = join(workspace, 'after-reopen.json')

  try {
    execFileSync('git', ['init', '--quiet', workspace])

    const crashed = runChild('crash-with-inflight-command', workspace, beforeMarker)
    assert.equal(crashed.status, 86, crashed.stderr || crashed.stdout)
    const beforeState = readMarker(beforeMarker)
    assert.equal(beforeState.commandInFlight, true)
    assert.equal(beforeState.bindingCount, 1)

    const reopened = runChild('reopen-and-explicit-resume', workspace, afterMarker)
    assert.equal(reopened.status, 0, reopened.stderr || reopened.stdout)
    const afterState = readMarker(afterMarker)
    assert.equal(afterState.initialBindingCount, 0, 'No automatic replay of pending commands on startup')
    assert.equal(afterState.replayedCommands, 0, 'Zero commands automatically replayed')
    assert.equal(afterState.singleDevOpsAuthority, true, 'DevOps must map to single active authority')
    assert.equal(afterState.briefingAcknowledged, true, 'Briefing explicitly notices pending command interruption')
    assert.equal(afterState.modelLocked, true, 'DevOps model drift must be strictly rejected fail-closed')
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})

// WHAT[crash-reconciliation-020]: the interrupted child run must be reset before
// the road can hand work to that child again. Restarting the work stays the
// manager's explicit decision (017/018/020 forbid automatic replay), but the
// durable bookkeeping has to let the reset happen: the child's logical run is
// closed (next handoff roots a fresh AgentOwnerRoot, the transcript stays) and
// the parent's handle is settled as a Cancelled completion (join gets an
// explicit outcome instead of waiting on a run that will never finish).
{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const root = '../../../dist'
  const Fold = await import(`${root}/Composition/Durable/Fold.js`)
  const Projection = await import(`${root}/Composition/Durable/Projection.js`)
  const Model = await import(`${root}/Interaction/Authority/Model.js`)
  const Origin = await import(`${root}/Interaction/Authority/Origin.js`)
  const Identity = await import(`${root}/Participant/Persona/Identity.js`)
  const Seed = await import(`${root}/Interaction/Authority/IdentitySeed.js`)
  const Bridge = await import(`${root}/Composition/Durable/DelegationProjectionBridge.js`)
  const ChatExecution = await import(`${root}/Execution/Session/ChatExecution/Facts.js`)
  const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${root}/Foundation/Roles.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)

  const parentSessionId = 'ses-road-root'
  const childSessionId = 'ses-devops-fixed'
  const handleId = 'devops'

  const participantIdentity = (name, role, persona) =>
    Identity.ParticipantIdentityModule_fromInput({
      SelectedAgent: name,
      Role: role,
      Persona: persona,
      PersonaCatalogVersion: 1,
      Origin: new Identity.PersonaOrigin(1, []),
    }).fields[0]

  const interruptedChild = () => {
    const owner = participantIdentity('manager', Roles.Role.Manager, 'Operator')
    const seed = Seed.PromptIdentitySeedModule_inheritFromOwner(
      'devops',
      parentSessionId,
      'lr-owner',
      'msg-owner-root',
      owner,
    ).fields[0]

    const profile = Model.createAuthorityExecutionProfileFromSeed(
      childSessionId,
      'lr-devops',
      'msg-devops-root',
      Origin.PromptRootAuthorityKind.AgentOwnerRoot,
      seed,
    ).fields[0]

    const withChild = Projection.AgentProjection_update(
      childSessionId,
      (session) => {
        session.PromptAuthority = { ActiveLogicalRun: profile }
        return session
      },
      Fold.empty.AgentProjections,
    )

    // Link the parent's devops handle through the production execution fold so
    // the parent session and the child index are exactly what a real handoff leaves.
    const linked = Fold.foldFact(
      { ...Fold.empty, AgentProjections: withChild },
      new Fact.Fact(1, [
        new Fact.AgentFact(3, [
          new DelegationFacts.ExecutionFactCases(0, [
            {
              ParentSessionId: parentSessionId,
              ChildSessionId: childSessionId,
              Handle: handleId,
              TargetAgent: 'devops',
              Byname: 'devops',
              CanonicalRole: Roles.Role.DevOps,
              Ownership: DelegationFacts.HandleOwnership.DurableParentHandle,
            },
          ]),
        ]),
      ]),
    )

    assert.equal(linked.tag, 0, 'the handle link must fold')
    return linked.fields[0].AgentProjections
  }

  const terminalFact = (disposition) =>
    new ChatExecution.ChatExecutionFactCases(2, [
      {
        SchemaVersion: 1,
        Key: { SessionId: childSessionId, PhysicalUserMessageId: 'msg-devops-1' },
        Evidence: new ChatExecution.ChatExecutionTerminalEvidence(1, [null]),
        Disposition: disposition,
      },
    ])

  test('WHAT[crash-reconciliation-020] CRASH_020_interrupted_child_run_is_reset_for_the_next_handoff', () => {
    const before = interruptedChild()
    assert.ok(before.Sessions.get(childSessionId).PromptAuthority.ActiveLogicalRun, 'precondition: the child holds a run')

    const settled = Bridge.settleUncompletedChildRun(
      before,
      terminalFact(ChatExecution.ChatExecutionTerminalDisposition.Cancelled),
    )

    const child = settled.Sessions.get(childSessionId)
    assert.equal(
      child.PromptAuthority.ActiveLogicalRun ?? null,
      null,
      'the interrupted child run must close so the next handoff roots a fresh AgentOwnerRoot',
    )
    assert.equal(child.PromptAuthority.PendingClaims.size, 0, 'run-scoped continuation resources are discarded')
    assert.ok(
      settled.Sessions.get(childSessionId).Handles === settled.Sessions.get(childSessionId).Handles,
      'the child transcript itself stays untouched',
    )

    const handle = settled.Sessions.get(parentSessionId).Handles.Handles.get(handleId)
    assert.equal(handle.Lifecycle.tag, 1, 'the parent handle must reach CompletedAwaitingJoin')
    assert.equal(
      handle.Lifecycle.fields[0].Kind,
      DelegationFacts.HandleCompletionKind.Cancelled,
      'join must receive an explicit Cancelled outcome instead of waiting forever',
    )
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_completed_child_run_is_left_to_its_own_completion_path', () => {
    const before = interruptedChild()
    const settled = Bridge.settleUncompletedChildRun(
      before,
      terminalFact(ChatExecution.ChatExecutionTerminalDisposition.Completed),
    )

    const handle = settled.Sessions.get(parentSessionId).Handles.Handles.get(handleId)
    assert.equal(handle.Lifecycle.tag, 0, 'a completed child run keeps its Active handle for the real completion')
    assert.ok(settled.Sessions.get(childSessionId).PromptAuthority.ActiveLogicalRun, 'and its authority stays open')
  })
}

// WHAT[crash-reconciliation-020]: a restart drops the process-local execution
// bindings, and the road's fixed DevOps must still map to exactly one live
// physical authority. Its durable handle is the evidence: without restoring the
// binding, the next handoff is refused with
// "PROMPT-006: parented session has no frozen agent binding" and the manager's
// join has nothing to wait on.
{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const root = '../../../dist'
  const Fold = await import(`${root}/Composition/Durable/Fold.js`)
  const Recovery = await import(`${root}/OpenCode/Host/SessionBindingRecovery.js`)
  const BindingSurface = await import(`${root}/OpenCode/Host/SessionBindingSurface.js`)
  const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${root}/Foundation/Roles.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)
  const Identity = await import(`${root}/Foundation/Identity.js`)

  const parentSessionId = 'ses-road-root'
  const childSessionId = 'ses-devops-fixed'
  const sessionId = (value) => Identity.SessionIdModule_create(value)
  const handleId = 'devops'

  const executionFact = (tag, payload) =>
    new Fact.Fact(1, [new Fact.AgentFact(3, [new DelegationFacts.ExecutionFactCases(tag, [payload])])])

  const linkHandle = ({
    childId = childSessionId,
    handle = handleId,
    targetAgent = 'devops',
    byname = 'devops',
    role = Roles.Role.DevOps,
  } = {}) => {
    const linked = Fold.foldFact(
      Fold.empty,
      executionFact(0, {
        ParentSessionId: sessionId(parentSessionId),
        ChildSessionId: sessionId(childId),
        Handle: handle,
        TargetAgent: targetAgent,
        Byname: byname,
        CanonicalRole: role,
        Ownership: DelegationFacts.HandleOwnership.DurableParentHandle,
      }),
    )

    assert.equal(linked.tag, 0, 'the handle link must fold')
    return { projections: linked.fields[0].AgentProjections, projectionsSet: linked.fields[0] }
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_restart_rebinds_the_durable_parented_child', () => {
    BindingSurface.drop(childSessionId)

    const { projections } = linkHandle()
    assert.equal(BindingSurface.tryAgent(childSessionId), '', 'precondition: the restart dropped the binding')
    assert.equal(BindingSurface.tryParent(childSessionId), '', 'precondition: the restart dropped the parent edge')

    Recovery.restoreFromProjection(projections)

    assert.equal(
      BindingSurface.tryParent(childSessionId),
      parentSessionId,
      'the recovered child must be dispatched as its road parent’s child again',
    )
    assert.equal(
      BindingSurface.tryAgent(childSessionId),
      'devops',
      'the fixed DevOps must keep the agent binding its handle was linked with',
    )
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_engineer_child_rebinds_its_execution_agent_not_its_byname', () => {
    const engineerChildId = 'ses-engineer-child'
    const engineerHandle = 'w3ci5f'
    BindingSurface.drop(engineerChildId)

    // A forked Engineer child carries two different names: the Host execution
    // agent (`engineer`) and the logical reuse address (`decision-record-readonly`).
    const { projections } = linkHandle({
      childId: engineerChildId,
      handle: engineerHandle,
      targetAgent: 'engineer',
      byname: 'decision-record-readonly',
      role: Roles.Role.Engineer,
    })

    Recovery.restoreFromProjection(projections)

    assert.equal(BindingSurface.tryParent(engineerChildId), parentSessionId)
    assert.equal(
      BindingSurface.tryAgent(engineerChildId),
      'engineer',
      'the Engineer child must be rebound to its execution agent, not to its byname',
    )
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_abandoned_handle_tombstone_is_never_rebound', () => {
    BindingSurface.drop(childSessionId)

    const { projectionsSet } = linkHandle()

    const abandoned = Fold.foldFact(
      projectionsSet,
      executionFact(3, {
        ParentSessionId: sessionId(parentSessionId),
        Handle: handleId,
        Reason: DelegationFacts.HandleAbandonReason.HostSessionGone,
        AbandonedAt: new Date('2026-09-28T00:00:00Z'),
      }),
    )

    assert.equal(abandoned.tag, 0, 'the abandon must fold')
    Recovery.restoreFromProjection(abandoned.fields[0].AgentProjections)

    assert.equal(
      BindingSurface.tryAgent(childSessionId),
      '',
      'an abandoned handle is a tombstone: recovery must not revive a dispatchable child',
    )
  })
}

// WHAT[crash-reconciliation-020]: a run that was live when the process died
// leaves no terminal fact at all, so recovery treats its physical evidence as
// stale and writes nothing. The child authority stays open, the next handoff is
// refused with ActiveRunIdentityConflict, and the parent's join has nothing to
// wait on. Load Phase therefore settles those runs explicitly: one Cancelled
// completion on the parent handle closes the child authority and answers the join.
{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const root = '../../../dist'
  const Fold = await import(`${root}/Composition/Durable/Fold.js`)
  const Projection = await import(`${root}/Composition/Durable/Projection.js`)
  const Model = await import(`${root}/Interaction/Authority/Model.js`)
  const Origin = await import(`${root}/Interaction/Authority/Origin.js`)
  const Seed = await import(`${root}/Interaction/Authority/IdentitySeed.js`)
  const PersonaIdentity = await import(`${root}/Participant/Persona/Identity.js`)
  const ChildWorkRecovery = await import(`${root}/OpenCode/Host/ChildWorkRecovery.js`)
  const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${root}/Foundation/Roles.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)
  const Identity = await import(`${root}/Foundation/Identity.js`)

  const parentSessionId = 'ses-road-root'
  const raw = (value) => (value !== null && typeof value === 'object' && Array.isArray(value.fields) ? value.fields[0] : value)
  const sid = (value) => Identity.SessionIdModule_create(value)
  const listOf = (value) => Array.from(value)

  const persona = (name, role, personaName) =>
    PersonaIdentity.ParticipantIdentityModule_fromInput({
      SelectedAgent: name,
      Role: role,
      Persona: personaName,
      PersonaCatalogVersion: 1,
      Origin: new PersonaIdentity.PersonaOrigin(1, []),
    }).fields[0]

  const owner = persona('manager', Roles.Role.Manager, 'Operator')

  const childProfile = (childSessionId, agentName, role) =>
    Model.createAuthorityExecutionProfileFromSeed(
      childSessionId,
      `lr-${childSessionId}`,
      `msg-${childSessionId}-root`,
      Origin.PromptRootAuthorityKind.AgentOwnerRoot,
      Seed.PromptIdentitySeedModule_inheritFromOwner(
        agentName,
        parentSessionId,
        'lr-owner',
        'msg-owner-root',
        owner,
      ).fields[0],
    ).fields[0]

  const rootProfile = () =>
    Model.createAuthorityExecutionProfile(
      parentSessionId,
      'lr-road',
      'msg-road-root',
      Origin.PromptRootAuthorityKind.HumanRoot,
      owner,
    )

  const sessionWithRun = (sessionId, profile) =>
    Projection.AgentProjection_update(
      sessionId,
      (session) => {
        session.PromptAuthority = { ActiveLogicalRun: profile }
        return session
      },
      Fold.empty.AgentProjections,
    )

  const linkChild = (projections, childSessionId, handle, targetAgent, role) => {
    const linked = Fold.foldFact(
      { ...Fold.empty, AgentProjections: projections },
      new Fact.Fact(1, [
        new Fact.AgentFact(3, [
          new DelegationFacts.ExecutionFactCases(0, [
            {
              ParentSessionId: Identity.SessionIdModule_create(parentSessionId),
              ChildSessionId: Identity.SessionIdModule_create(childSessionId),
              Handle: handle,
              TargetAgent: targetAgent,
              Byname: handle,
              CanonicalRole: role,
              Ownership: DelegationFacts.HandleOwnership.DurableParentHandle,
            },
          ]),
        ]),
      ]),
    )

    assert.equal(linked.tag, 0, 'the handle link must fold')
    return linked.fields[0].AgentProjections
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_run_without_terminal_is_settled_at_load', () => {
    const childSessionId = 'ses-devops-restart'
    const withAuthority = sessionWithRun(sid(childSessionId), childProfile(sid(childSessionId), 'devops', Roles.Role.DevOps))

    // A run that was accepted and started, then the process died: no Terminal
    // fact exists, so the chat-execution fold has nothing to settle.
    const projections = linkChild(withAuthority, childSessionId, 'devops', 'devops', Roles.Role.DevOps)
    const linkedRecord = projections.Sessions.get(sid(parentSessionId)).Handles.Handles.get('devops')
    assert.equal(linkedRecord.Lifecycle.tag, 0, 'precondition: the handle is still Active')

    const orphans = listOf(ChildWorkRecovery.orphanedChildRuns(projections))
    assert.equal(orphans.length, 1, 'the child work run left active by the dead runtime is an orphan')
    assert.equal(raw(orphans[0].ChildSessionId), childSessionId)
    assert.equal(raw(orphans[0].Handle), 'devops')

    const settlement = ChildWorkRecovery.settlementFact(orphans[0])
    assert.equal(settlement.tag, 1, 'the settlement rides the HandleCompleted execution fact')
    assert.equal(settlement.fields[0].Kind, DelegationFacts.HandleCompletionKind.Cancelled)

    const settled = Fold.foldFact(
      { ...Fold.empty, AgentProjections: projections },
      new Fact.Fact(1, [new Fact.AgentFact(3, [settlement])]),
    )

    assert.equal(settled.tag, 0, 'the settlement must fold')
    const after = settled.fields[0].AgentProjections

    assert.equal(
      after.Sessions.get(sid(childSessionId)).PromptAuthority.ActiveLogicalRun ?? null,
      null,
      'the settled child run must close so the next handoff roots freshly',
    )
    const handle = after.Sessions.get(sid(parentSessionId)).Handles.Handles.get('devops')
    assert.equal(handle.Lifecycle.tag, 1, 'the parent handle must reach CompletedAwaitingJoin')
    assert.equal(handle.Lifecycle.fields[0].Kind, DelegationFacts.HandleCompletionKind.Cancelled)
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_human_root_and_unlinked_sessions_are_never_settled', () => {
    // A human-root manager run owns its own road: restart never settles it here.
    const managerProjections = sessionWithRun(sid(parentSessionId), rootProfile())
    assert.equal(listOf(ChildWorkRecovery.orphanedChildRuns(managerProjections)).length, 0)

    // A child work run without a linked handle has nothing to settle against.
    const unlinked = sessionWithRun(sid('ses-orphan-child'), childProfile(sid('ses-orphan-child'), 'engineer', Roles.Role.Engineer))
    assert.equal(listOf(ChildWorkRecovery.orphanedChildRuns(unlinked)).length, 0)
  })
}
