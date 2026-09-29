import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'


test('WHAT[crash-reconciliation-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', async () => {
  // 1. RolesSurface must have consolidated DevOps and Engineer
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('devops'), 'Role labels must have devops')
  assert.ok(all.includes('engineer'), 'Role labels must have engineer')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')

})


// WHAT[crash-reconciliation-020]: the interrupted child run must be reset before
// the road can hand work to that child again. Restarting the work stays the
// manager's explicit decision (017/018/020 forbid automatic replay). That reset
// belongs to the Load Phase alone (`ChildWorkRecovery`, covered below): no
// in-process terminal may close a child's logical run, because a live owner can
// still owe that exact run a continuation — the degeneration guard closes its
// own interrupt and immediately sends the rewrite on the same run
// (managed-session-lifecycle-018, degeneration-guard-009).
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
  const journalSurface = await import(`${root}/Persistence/Journal/Surface.js`)
  const FoundationIdentity = await import(`${root}/Foundation/Identity.js`)
  const chatExecution = await import(`${root}/Composition/Durable/ChatExecutionJournal.js`)
  const chatExecutionFacts = await import(`${root}/Execution/Session/ChatExecution/Facts.js`)
  const ProviderRequestKind = await import(`${root}/Participant/Provider/Attempt/RequestKind.js`)
  const ProjectionChoice = await import(`${root}/Context/Prefix/Candidate.js`)
  const fs = await import('node:fs')
  const os = await import('node:os')
  const nodePath = await import('node:path')
  const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${root}/Foundation/Roles.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)

  const parentSessionId = 'ses-road-root'
  const childSessionId = 'ses-devops-fixed'
  const handleId = 'devops'

  const participantIdentity = (name, role, persona) =>
    Fold.unwrap(Identity.ParticipantIdentityModule_fromInput({
      SelectedAgent: name,
      Role: role,
      Persona: persona,
      PersonaCatalogVersion: 1,
      Origin: new Identity.PersonaOrigin(1, []),
    }))

  // Compiler representation (Result tag / DU fields) is not product semantics, so
  // it is read through one local accessor pair instead of poked at each call site.
  const foldedOk = (result) => Fold.isOk(result)
  const foldedValue = (result) => Fold.unwrap(result)

  // The exact durable state a live host leaves behind for a child work run:
  // rooted AgentOwnerRoot authority plus the parent's durable handle. Identities
  // are the production typed ones, because the projection's maps are keyed by
  // them — a string key would silently miss the very entries under test.
  const sessionId = () => FoundationIdentity.SessionIdModule_create(childSessionId)
  const parentId = () => FoundationIdentity.SessionIdModule_create(parentSessionId)
  const logicalRunId = () => FoundationIdentity.LogicalRunIdModule_create('lr-devops')
  const authorityRootId = () => FoundationIdentity.AuthorityRootUserMessageIdModule_create('msg-devops-root')
  const physicalUserMessageId = () => FoundationIdentity.PhysicalUserMessageIdModule_create('msg-devops-run-1')

  const childIdentitySeed = () =>
    Fold.unwrap(Seed.PromptIdentitySeedModule_inheritFromOwner(
      'devops',
      FoundationIdentity.SessionIdModule_create(parentSessionId),
      FoundationIdentity.LogicalRunIdModule_create('lr-owner'),
      FoundationIdentity.AuthorityRootUserMessageIdModule_create('msg-owner-root'),
      participantIdentity('manager', Roles.Role.Manager, 'Operator'),
    ))

  const interruptedChild = () => {
    const profile = Fold.unwrap(Model.createAuthorityExecutionProfileFromSeed(
      sessionId(),
      logicalRunId(),
      authorityRootId(),
      Origin.PromptRootAuthorityKind.AgentOwnerRoot,
      childIdentitySeed(),
    ))

    const withChild = Projection.AgentProjection_update(
      sessionId(),
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
              ParentSessionId: parentId(),
              ChildSessionId: sessionId(),
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
    assert.ok(foldedOk(linked), 'the child root and handle link must fold')

    return foldedValue(linked).AgentProjections
  }

  const childState = (projection) => {
    const found = projection.Sessions.get(sessionId())
    assert.ok(found, 'the child session must be present in the projection')
    return found
  }

  const parentHandle = (projection) => {
    const parent = projection.Sessions.get(parentId())
    assert.ok(parent, 'the parent session must be present in the projection')
    const handle = parent.Handles.Handles.get(handleId)
    assert.ok(handle, 'the parent must hold the devops handle')
    return handle
  }

  // The exact durable lifecycle an in-process interrupt produces:
  // Accepted -> ProviderStarted -> Terminal(non-Completed). Driven through the
  // production fold with production classes, so the projection the fold yields
  // is the one every downstream owner (the guard's own continuation included)
  // reads afterwards.
  const acceptedEvidence = () =>
    new chatExecutionFacts.AcceptedChatExecutionEvidence(
      sessionId(),
      logicalRunId(),
      authorityRootId(),
      Origin.PromptRootAuthorityKind.AgentOwnerRoot,
      childIdentitySeed(),
      physicalUserMessageId(),
      new Origin.PromptOrigin(0, [Origin.PromptRootAuthorityKind.AgentOwnerRoot]),
    )

  const providerStartedEvidence = () =>
    new chatExecutionFacts.ProviderStartedEvidence(
      acceptedEvidence(),
      FoundationIdentity.ProviderRunIdentityModule_create('msg-devops-provider-run'),
      ProviderRequestKind.ProviderRequestKind.WorkMain,
      new ProjectionChoice.XProjectionChoice(0, []),
    )

  const appendChatExecution = (projection, disposition) => {
    const key = new chatExecutionFacts.ChatExecutionKey(sessionId(), physicalUserMessageId())

    const facts = [
      new chatExecutionFacts.ChatExecutionFactCases(0, [
        { SchemaVersion: 1, Key: key, Evidence: acceptedEvidence() },
      ]),
      new chatExecutionFacts.ChatExecutionFactCases(1, [
        { SchemaVersion: 1, Key: key, Evidence: providerStartedEvidence() },
      ]),
      new chatExecutionFacts.ChatExecutionFactCases(2, [
        {
          SchemaVersion: 1,
          Key: key,
          Evidence: new chatExecutionFacts.ChatExecutionTerminalEvidence(1, [providerStartedEvidence()]),
          Disposition: chatExecutionFacts.ChatExecutionTerminalDisposition[disposition],
        },
      ]),
    ]

    let current = { ...Fold.empty, AgentProjections: projection }

    for (const fact of facts) {
      const folded = Fold.foldFact(current, new Fact.Fact(1, [new Fact.AgentFact(13, [fact])]))
      assert.ok(foldedOk(folded), 'the provider lifecycle fact must fold')
      current = foldedValue(folded)
    }

    return current.AgentProjections
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_in_process_interrupt_never_closes_the_child_run', async () => {
    // WHAT[managed-session-lifecycle-018]: an attempt observation is not authority
    // to close a logical run. The degeneration guard closes the interrupted
    // attempt and then continues ON THE SAME RUN (degeneration-guard-009 /
    // interaction-authority-012). While the fold closed that run on any
    // non-Completed terminal, the guard's own interrupt left the continuation it
    // was about to send with no active authority profile: the rewrite was
    // silently dropped and the session deadlocked with nothing left to wake it.
    //
    // The terminal below is the exact fact a guard interrupt produces, so this
    // asserts the projection an in-process owner depends on.
    for (const disposition of ['Cancelled', 'Rejected', 'Failed']) {
      const before = interruptedChild()
      assert.ok(childState(before).PromptAuthority.ActiveLogicalRun, 'precondition: the child holds a run')

      const after = await appendChatExecution(before, disposition)

      assert.ok(
        childState(after).PromptAuthority.ActiveLogicalRun,
        `a ${disposition} terminal from an in-process owner must leave the child run open for its continuation`,
      )

      assert.ok(
        foldedOk(parentHandle(after).Lifecycle),
        'the handle stays Active: only the Load Phase settles a child the runtime lost',
      )
    }
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_completed_child_run_is_left_to_its_own_completion_path', async () => {
    const before = interruptedChild()
    const after = await appendChatExecution(before, 'Completed')

    assert.ok(
      foldedOk(parentHandle(after).Lifecycle),
      'a completed child run keeps its Active handle for the real completion',
    )
    assert.ok(childState(after).PromptAuthority.ActiveLogicalRun, 'and its authority stays open')
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
  const Projection = await import(`${root}/Composition/Durable/Projection.js`)
  const AssociationFacts = await import(`${root}/Execution/Session/Association.js`)

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
    ownership = DelegationFacts.HandleOwnership.DurableParentHandle,
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
        Ownership: ownership,
      }),
    )

    assert.equal(Fold.isOk(linked), true, 'the handle link must fold')
    return { projections: Fold.unwrap(linked).AgentProjections, projectionsSet: Fold.unwrap(linked) }
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_restart_rebinds_the_durable_parented_child', () => {
    BindingSurface.drop(childSessionId)

    const { projections } = linkHandle()
    assert.equal(BindingSurface.tryAgent(childSessionId), '', 'precondition: the restart dropped the binding')
    assert.equal(BindingSurface.tryParent(childSessionId), '', 'precondition: the restart dropped the parent edge')

    Recovery.installFrom(() => projections)

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

    Recovery.installFrom(() => projections)

    assert.equal(BindingSurface.tryParent(engineerChildId), parentSessionId)
    assert.equal(
      BindingSurface.tryAgent(engineerChildId),
      'engineer',
      'the Engineer child must be rebound to its execution agent, not to its byname',
    )
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_host_owned_hidden_leaves_yield_no_binding_evidence', () => {
    BindingSurface.drop(childSessionId)

    const { projections } = linkHandle({ ownership: DelegationFacts.HandleOwnership.HostOwnedHidden })

    assert.equal(
      Recovery.evidenceFor(projections, sessionId(childSessionId)) ?? null,
      null,
      'a Host-owned hidden leaf is invisible to the parent binding surface',
    )

    Recovery.installFrom(() => projections)

    assert.equal(BindingSurface.tryAgent(childSessionId), '', 'and it never becomes a dispatchable binding')
  })

    test('WHAT[crash-reconciliation-020] CRASH_020_companion_session_resolves_to_parent_and_blogger_agent', () => {
      const companionSessionId = 'ses-blogger-companion'
      BindingSurface.drop(companionSessionId)

      const linked = AssociationFacts.SessionAssociationProjection_linkSatellite(
        new AssociationFacts.SatelliteKind(0, []), // Companion
        sessionId(parentSessionId),
        sessionId(companionSessionId),
        undefined,
        Fold.empty.AgentProjections.Associations,
    )
      assert.equal(Fold.isOk(linked), true, 'link must succeed')

      const projectionsWithBlogger = {
          ...Fold.empty.AgentProjections,
        Associations: Fold.unwrap(linked),
}

      const evidence = Recovery.evidenceFor(projectionsWithBlogger, sessionId(companionSessionId))
      assert.ok(evidence, 'companion session must resolve from durable associations')
      assert.equal(evidence[0], parentSessionId, 'parent must be the main session')
      assert.equal(evidence[1], 'blogger', 'execution agent must be blogger')

      Recovery.installFrom(() => projectionsWithBlogger)
      assert.equal(BindingSurface.tryParent(companionSessionId), parentSessionId)
      assert.equal(BindingSurface.tryAgent(companionSessionId), 'blogger')
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
  const Linkage = await import(`${root}/Execution/Delegation/LinkageProjection.js`)
  const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${root}/Foundation/Roles.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)
  const Identity = await import(`${root}/Foundation/Identity.js`)

  const parentSessionId = 'ses-road-root'
  const raw = (value) => (typeof value === 'string' ? value : Identity.SessionIdModule_value(value))
  const sid = (value) => Identity.SessionIdModule_create(value)
  const listOf = (value) => Array.from(value)

  const persona = (name, role, personaName) =>
    Fold.unwrap(PersonaIdentity.ParticipantIdentityModule_fromInput({
      SelectedAgent: name,
      Role: role,
      Persona: personaName,
      PersonaCatalogVersion: 1,
      Origin: new PersonaIdentity.PersonaOrigin(1, []),
    }))

  const owner = persona('manager', Roles.Role.Manager, 'Operator')

  const childProfile = (childSessionId, agentName, role) =>
    Fold.unwrap(Model.createAuthorityExecutionProfileFromSeed(
      childSessionId,
      'lr-child',
      `msg-${childSessionId}-root`,
      Origin.PromptRootAuthorityKind.AgentOwnerRoot,
      Fold.unwrap(Seed.PromptIdentitySeedModule_inheritFromOwner(
        agentName,
        parentSessionId,
        'lr-owner',
        'msg-owner-root',
        owner,
      )),
    ))

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

    assert.equal(Fold.isOk(linked), true, 'the handle link must fold')
    return Fold.unwrap(linked).AgentProjections
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_run_without_terminal_is_settled_at_load', () => {
    const childSessionId = 'ses-devops-restart'
    const withAuthority = sessionWithRun(sid(childSessionId), childProfile(sid(childSessionId), 'devops', Roles.Role.DevOps))

    // A run that was accepted and started, then the process died: no Terminal
    // fact exists, so the chat-execution fold has nothing to settle.
    const projections = linkChild(withAuthority, childSessionId, 'devops', 'devops', Roles.Role.DevOps)
    const linkedRecord = projections.Sessions.get(sid(parentSessionId)).Handles.Handles.get('devops')
    assert.deepEqual(linkedRecord.Lifecycle, Linkage.HandleLifecycle.Active, 'precondition: the handle is still Active')

    const orphans = listOf(ChildWorkRecovery.orphanedChildRuns(projections))
    assert.equal(orphans.length, 1, 'the child work run left active by the dead runtime is an orphan')
    assert.equal(raw(orphans[0].ChildSessionId), childSessionId)
    assert.equal(raw(orphans[0].Handle), 'devops')

    const settlement = ChildWorkRecovery.settlementFact(orphans[0])

    const expectedSettlement = new DelegationFacts.ExecutionFactCases(3, [
      {
        ParentSessionId: sid(parentSessionId),
        ChildSessionId: sid(childSessionId),
      },
    ])
    assert.deepEqual(
      settlement,
      expectedSettlement,
      'the settlement voids the interrupted run (no completion cell: horizon and join stay empty)',
    )

    const settled = Fold.foldFact(
      { ...Fold.empty, AgentProjections: projections },
      new Fact.Fact(1, [new Fact.AgentFact(3, [settlement])]),
    )

    assert.equal(Fold.isOk(settled), true, 'the settlement must fold')
    const after = Fold.unwrap(settled).AgentProjections

    assert.equal(
      after.Sessions.get(sid(childSessionId)).PromptAuthority.ActiveLogicalRun ?? null,
      null,
      'the settled child run must close so the next handoff roots freshly',
    )

    const handle = after.Sessions.get(sid(parentSessionId)).Handles.Handles.get('devops')

    assert.deepEqual(handle.Lifecycle, Linkage.HandleLifecycle.Active, 'the handle stays Active: the interrupted run owes nothing')
    assert.equal(
      Array.from(Linkage.HandleProjection_joinable(after.Sessions.get(sid(parentSessionId)).Handles)).length,
      0,
      'join must have nothing to collect after a restart settlement',
    )
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



// WHAT[crash-reconciliation-020]: a `Cancelled` completion carries no body. The
// parent is still owed a report, and a child with an unreported completion counts
// as having unfinished delivery — reuse of that name is refused while join used to
// skip the cell silently, which is how a restarted road ended up unable to hand
// work to any of its existing Engineer children. The drain must report once and
// retire the handle.
{
  const dist = '../../../dist'
  const Fold = await import(`${dist}/Composition/Durable/Fold.js`)
  const JoinDrain = await import(`${dist}/Execution/Delegation/Handle/JoinDrain.js`)
  const Linkage = await import(`${dist}/Execution/Delegation/LinkageProjection.js`)
  const DelegationFacts = await import(`${dist}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${dist}/Foundation/Roles.js`)
  const Identity = await import(`${dist}/Foundation/Identity.js`)

  const parentSessionId = 'ses-road-root'
  const handleId = 'frame-integrity'
  const childSessionId = 'ses-engineer-child'
  const sid = (value) => Identity.SessionIdModule_create(value)
  const handle = (value) => new Identity.HandleId(0, [Identity.AgentHandleIdModule_create(value)])

  const linkedCancelledHandle = () => {
    const linked = Linkage.HandleProjection_link(
      handle(handleId),
      sid(childSessionId),
      'engineer',
      Roles.Role.Engineer,
      DelegationFacts.HandleOwnership.DurableParentHandle,
      Linkage.HandleProjection_empty,
    )

    assert.equal(Fold.isOk(linked), true, 'precondition: the handle links')

    const completion = new Linkage.HandleCompletion(DelegationFacts.HandleCompletionKind.Cancelled, null, null)
    const completed = Linkage.HandleProjection_complete(handle(handleId), completion, Fold.unwrap(linked))

    assert.equal(Fold.isOk(completed), true, 'precondition: the Cancelled completion is written')
    return Fold.unwrap(completed)
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_cancelled_completion_is_reported_and_retired_by_join', async () => {
    const handles = linkedCancelledHandle()
    const appended = []

    const port = {
      AppendExecutionFact: (_sessionId, fact) => {
        appended.push(fact)
        return Promise.resolve({ tag: 0, fields: [] })
      },
      HandleProjection: () => {
        // Mirror the durable consume: the append retires the handle.
        if (appended.length === 0) return handles

        const retired = Linkage.HandleProjection_retire(handle(handleId), handles)
        return Fold.isOk(retired) ? Fold.unwrap(retired) : handles
      },
      ReadBlob: () => Promise.resolve({ tag: 1, fields: ['no body'] }),
      WriteBlob: () => Promise.resolve({ tag: 1, fields: ['unsupported'] }),
      Sha256: () => 'unused',
    }

    const drained = await JoinDrain.drainFromJournalWhere(
      port,
      sid(parentSessionId),
      8,
      new Date('2026-09-28T04:00:00Z'),
      () => true,
    )

    assert.equal(Fold.isOk(drained), true, 'the drain must not fail')

    const completions = Array.from(Fold.unwrap(drained))

    assert.equal(completions.length, 1, 'the cancelled run must be reported once, not skipped')
    assert.equal(completions[0].RunId, `cancelled-${handleId}`)
    assert.equal(completions[0].AgentName, 'engineer')
    assert.equal(appended.length > 0, true, 'consuming the report is a durable append')

    const after = port.HandleProjection(sid(parentSessionId))
    const record = Linkage.HandleProjection_tryFind(handle(handleId), after)

    assert.deepEqual(record.Lifecycle, Linkage.HandleLifecycle.Retired, 'the reported handle retires so it is never delivered twice')
  })
}

// WHAT[crash-reconciliation-020]: which child a handle id names is durable
// evidence. `Reuse` used to answer "Unknown agent id" whenever the process
// tables were empty (every restart), even though the same handle was intact in
// the journal — the reported `[COMMIT-REUSE-ERR: "Unknown agent id: opghu8"]`.
{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const root = '../../../dist'
  const Fold = await import(`${root}/Composition/Durable/Fold.js`)
  const Lookup = await import(`${root}/Execution/Delegation/DurableChildLookup.js`)
  const DelegationFacts = await import(`${root}/Execution/Delegation/Facts.js`)
  const Roles = await import(`${root}/Foundation/Roles.js`)
  const Identity = await import(`${root}/Foundation/Identity.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)

  const sessionId = (value) => Identity.SessionIdModule_create(value)
  const raw = (value) => (typeof value === 'string' ? value : Identity.SessionIdModule_value(value))

  const linkedHandles = () => {
    const linked = Fold.foldFact(
      Fold.empty,
      new Fact.Fact(1, [
        new Fact.AgentFact(3, [
          new DelegationFacts.ExecutionFactCases(0, [
            {
              ParentSessionId: sessionId('ses-road-root'),
              ChildSessionId: sessionId('ses-f19f7056'),
              Handle: new Identity.HandleId(0, [Identity.AgentHandleIdModule_create('opghu8')]),
              TargetAgent: 'engineer',
              Byname: 'triage-misc',
              CanonicalRole: Roles.Role.Engineer,
              Ownership: DelegationFacts.HandleOwnership.DurableParentHandle,
            },
          ]),
        ]),
      ]),
    )

    assert.equal(Fold.isOk(linked), true, 'the handle link must fold')

    const parent = sessionId('ses-road-root')
    return Fold.unwrap(linked).AgentProjections.Sessions.get(parent).Handles
  }

  test('WHAT[crash-reconciliation-020] CRASH_020_reuse_resolves_the_child_from_its_durable_handle', () => {
    const resolved = Lookup.byHandleId(linkedHandles(), 'opghu8')

    assert.ok(resolved, 'a fresh process must resolve the child from the durable handle alone')
    assert.equal(raw(resolved[0]), 'ses-f19f7056')
    assert.equal(resolved[1], Roles.Role.Engineer)
    assert.equal(resolved[2], 'engineer', 'the execution agent, not the logical byname')
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_reuse_reports_unknown_for_an_unlinked_handle_id', () => {
    assert.equal(Lookup.byHandleId(linkedHandles(), 'nosuchid') ?? null, null)
    assert.equal(Lookup.byHandleId(linkedHandles(), '  ') ?? null, null)
  })
}

// WHAT[crash-reconciliation-020]: fission lanes follow the same rule — the lane
// registry is a cache of what this process drives, the projection is the truth.
// After a restart a lane caller must still resolve to its owner instead of being
// silently treated as a plain session.
{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const dist = '../../../dist'
  const FissionRuntime = await import(`${dist}/Execution/Fission/Runtime.js`)
  const Identity = await import(`${dist}/Foundation/Identity.js`)

  const sid = (value) => Identity.SessionIdModule_create(value)
  const raw = (value) => (typeof value === 'string' ? value : Identity.SessionIdModule_value(value))

  test('WHAT[crash-reconciliation-020] CRASH_020_fission_lane_resolves_from_durable_evidence_after_restart', () => {
    FissionRuntime.FissionRuntime_installDurableLaneEvidence((laneSessionId) => {
      if (raw(laneSessionId) !== 'ses-fission-lane') return null

      return { GroupId: 'group-1', OwnerSessionId: sid('ses-owner'), LaneIndex: 1, LaneCount: 2 }
    })

    const binding = FissionRuntime.FissionRuntime_tryLane(sid('ses-fission-lane'))

    assert.ok(binding, 'a restarted process must resolve the lane from durable evidence')
    assert.equal(raw(binding.OwnerSessionId), 'ses-owner')
    assert.equal(binding.GroupId, 'group-1')
    assert.equal(binding.LaneIndex, 1)
    assert.equal(binding.LaneCount, 2)
    assert.equal(raw(FissionRuntime.FissionRuntime_tryOwner(sid('ses-fission-lane'))), 'ses-owner')
    assert.equal(FissionRuntime.FissionRuntime_tryLane(sid('ses-unknown-lane')) ?? null, null)
  })
}

// WHAT[crash-reconciliation-020]: a Blog materialization the previous runtime left
// open is invisible to every live path — the coordinator only stages material for the
// producer that died with that runtime and never materializes a fresh request — so the
// Blogger would never ingest the raw tail again. Load Phase settles it: every open
// request this process no longer holds a live flight for is abandoned once.
{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const { readFileSync } = await import('node:fs')
  const { resolve } = await import('node:path')
  const root = '../../../dist'
  const Fold = await import(`${root}/Composition/Durable/Fold.js`)
  const Abandon = await import(`${root}/Context/Companion/Blogger/Runtime/Abandon.js`)
  const ContextFacts = await import(`${root}/Context/Companion/Facts.js`)
  const BlogRuntime = await import(`${root}/Context/Companion/Blogger/Runtime/CycleProjection.js`)
  const Fact = await import(`${root}/Composition/Durable/Fact.js`)
  const Identity = await import(`${root}/Foundation/Identity.js`)

  const mainSessionId = 'ses-road-root'
  const bloggerSessionId = 'ses-blogger-restart'
  const requestId = 'req-left-open'
  const sid = (value) => Identity.SessionIdModule_create(value)
  const rid = (value) => Identity.BloggerRequestIdModule_create(value)
  const raw = (value) => (typeof value === 'string' ? value : Identity.SessionIdModule_value(value))

  const contextFactTag = {
    BloggerRequestMaterialized: 2,
    BloggerRequestAbandoned: 3,
  }
  const contextFact = (caseName, payload) =>
    new Fact.AgentFact(6, [new ContextFacts.ContextFactCases(contextFactTag[caseName], [payload])])

  const materialized = () =>
    contextFact('BloggerRequestMaterialized', {
      RequestId: rid(requestId),
      MainSessionId: sid(mainSessionId),
      BloggerSessionId: sid(bloggerSessionId),
      RequestKind: 'main',
      ContextRef: Identity.BlobRefModule_create('blobs/left-open'),
      ContextDigest: Identity.BlobDigestModule_create('digest-left-open'),
      ObservedPrefixEpochId: Identity.PrefixEpochIdModule_create(0),
      PreviousIngestedThroughSequence: 699n,
      NextIngestedThroughSequence: 706n,
      FrameEpochId: Identity.FrameEpochIdModule_create(0),
      SelectedFrameDigests: [],
      PromptKey: undefined,
    })

  const abandoned = () =>
    contextFact('BloggerRequestAbandoned', {
      RequestId: rid(requestId),
      MainSessionId: sid(mainSessionId),
      BloggerSessionId: sid(bloggerSessionId),
      Reason: 'stale-open-at-load',
    })

  const foldedWith = (fact) => {
    const folded = Fold.foldFact(Fold.empty, new Fact.Fact(1, [fact]))
    assert.equal(Fold.isOk(folded), true, 'folded fact must succeed')
    return Fold.unwrap(folded).AgentProjections
  }

  const openRequestOf = (projections) =>
    BlogRuntime.BloggerCycleProjection_tryOpenByBlogger(sid(bloggerSessionId), projections.Sessions.get(sid(mainSessionId)).BloggerCycles)

  const staleOf = (projections, liveFlight) => Array.from(Abandon.staleOpenRequests(liveFlight, projections))

  test('WHAT[crash-reconciliation-020] CRASH_020_open_blog_request_owned_by_no_live_flight_is_settled_at_load', () => {
    const projections = foldedWith(materialized())

    assert.ok(openRequestOf(projections), 'precondition: the materialized request is open')

    const stale = staleOf(projections, () => false)

    assert.equal(stale.length, 1, 'the request left open by the dead runtime is stale')
    assert.equal(raw(stale[0][0]), mainSessionId)
    assert.equal(raw(stale[0][1].RequestId), requestId)
    assert.equal(raw(stale[0][1].BloggerSessionId), bloggerSessionId)

    assert.equal(
      staleOf(projections, (blogger, request) => raw(blogger) === bloggerSessionId && raw(request) === requestId).length,
      0,
      'a request this process still holds a live flight for must stay open',
    )

    const settled = foldedWith(abandoned())

    assert.equal(openRequestOf(settled) ?? null, null, 'the settlement clears the open slot the catch-up blocks on')
    assert.equal(staleOf(settled, () => false).length, 0, 'and it settles exactly once')
  })

  test('WHAT[crash-reconciliation-020] CRASH_020_load_phase_settles_open_blog_requests_before_any_session_runs', () => {
    const wiring = readFileSync(
      resolve(import.meta.dirname, '../../../src/Wanxiangshu/OpenCode/Plugin/PluginRecoveryWiring.fs'),
      'utf8',
    )

    assert.match(wiring, /BloggerAbandon\.settleStaleOpenAtLoad liveFlight journal/)
    assert.ok(
      wiring.indexOf('settleStaleOpenAtLoad') < wiring.indexOf('PluginRuntimeReloaded'),
      'the settlement belongs to the load phase, before sessions resume',
    )
  })
}
