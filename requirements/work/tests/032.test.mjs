import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as completedTurn from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'

// WHAT[work-032] keeps unrelated offices unchanged. Manager shares human Ask;
// it must not inherit Plan's DevOps consultation or draft capabilities. Two
// observable faces are asserted here: non-Plan roles never receive the
// Plan-only permission projection, and non-Plan empty stops keep requiring
// interaction repair (the full classification suite lives in 027.test.mjs
// under work-027). Manager's review tool is reached through the
// ReviewAssessment permission and the shared shell of work-038; the
// Plan-backend Review label itself never appears in a non-Plan projection.
// The Baton isolation trigger is gated by ownerRole = Plan inside the
// transform pipeline (PluginTransforms planSessionOf); its live proof
// belongs to the physical host canary tracked by GAP-230, not to this pure
// surface.

test('WHAT[work-032] non-Plan roles never receive the Plan-only tool projection', () => {
  const planOnlyPermissions = ['JsPlan', 'DevOpsConsultation', 'Deliver', 'Review']
  const nonPlanRoles = ['manager', 'engineer', 'devops', 'orchestrator']
  // Each role label must resolve to a real role, so the deny below is a
  // genuine projection fact and not an unknown-role fallback.
  const roleWitness = {
    manager: 'Fork',
    engineer: 'Write',
    devops: 'Exec',
    orchestrator: 'Fork',
  }
  for (const role of nonPlanRoles) {
    assert.equal(isAllowed(role, roleWitness[role]), true, `${role} must resolve as a real role`)
    for (const perm of planOnlyPermissions) {
      assert.equal(isAllowed(role, perm), false, `${role} must be denied ${perm}`)
    }
  }
})

test('WHAT[work-032] non-Plan empty stops keep requiring interaction repair', () => {
  // There is no Plan empty-stop exemption to propagate to another role.
  assert.equal(completedTurn.needsInteractionRepair('engineer', true, 'stop', []), true)
  assert.equal(completedTurn.needsInteractionRepair('manager', true, 'stop', []), true)
})

test('WHAT[work-032] only Plan may ask the human; Manager and other roles are denied ask', () => {
  assert.equal(isAllowed('plan', 'Ask'), true)
  for (const role of ['manager', 'engineer', 'devops', 'orchestrator', 'coder', 'inspector']) {
    assert.equal(isAllowed(role, 'Ask'), false, role)
    assert.equal(isAllowed(role, 'DevOpsConsultation'), false, role)
  }
})

test('WHAT[work-032] Engineer, DevOps, and Orchestrator progress stops do not record completion fact without delivery witness', async () => {
  // 合法进度 stop 不能当完成：无 witness 的 TurnCompleted 不写完成事实。
  const { TerminalReporter_completeWithEvidence, XTraceTerminalCompletion } = await import('../../../dist/Context/Trace/TerminalReporter.js')
  const { ReconciledTurn } = await import('../../../dist/Composition/Turn/Observation.js')
  const { Role } = await import('../../../dist/Foundation/Roles.js')
  const { SessionIdModule_create, PhysicalUserMessageIdModule_create, AuthorityRootUserMessageIdModule_create, ProviderRunIdentityModule_create } = await import('../../../dist/Foundation/Identity.js')
  const { TurnOutcome } = await import('../../../dist/Composition/Turn/Program.js')
  const { MessagePart } = await import('../../../dist/OpenCode/Host/Message.js')
  const eventsSurface = await import('../../../dist/OpenCode/Host/EventsSurface.js')

  for (const [name, role] of [['engineer', Role.Engineer], ['devops', Role.DevOps], ['orchestrator', Role.Orchestrator]]) {
    const eventPort = eventsSurface.create()
    const completedEvents = []
    eventPort.SubscribeTerminalListener((sid, outcome) => {
      if (outcome.kind === 'Completed') completedEvents.push({ sid, outcome })
    })
    const turn = new ReconciledTurn(
      SessionIdModule_create(`ses-${name}-progress`),
      PhysicalUserMessageIdModule_create(`msg-${name}-1`),
      AuthorityRootUserMessageIdModule_create(`root-${name}-1`),
      ProviderRunIdentityModule_create(`run-${name}-1`),
      role,
      '/repo',
      [new MessagePart(0, [`[${name}] Finished subtask progress summary.`])],
      'stop',
      undefined,
      undefined,
      TurnOutcome.TurnCompleted,
      undefined,
    )
    const outcome = await TerminalReporter_completeWithEvidence(eventPort, undefined, turn, false)
    assert.equal(outcome.tag, XTraceTerminalCompletion.RejectedUndelivered.tag, `${name} progress stop without witness must be RejectedUndelivered`)
    assert.equal(completedEvents.length, 0, `${name} progress stop without delivery witness must not notify Completed`)
  }
})
