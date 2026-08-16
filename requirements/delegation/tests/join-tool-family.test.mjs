// tests/unit/tools/join-tool-family.test.mjs — JoinTool FamilyReady orchestrator paths.
//
// Real VerdictMailbox + real JoinInterrupt; the scope carries an authority
// profile (RoleFor = Orchestrator) and a pre-seeded orchestrator host whose
// engine is the mailbox. Everything in JoinTool.execute is production code.

import assert from 'node:assert/strict'
import test from 'node:test'

import { agentJournal, attemptPlanner, sessionId } from '../../verification-system/tests/support/domain.mjs'

const { HostToolContext } = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/JoinTool.js')
const toolRuntimeModule = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')
const { ToolRuntimeScope } = toolRuntimeModule
const attachFamilyRecovery = Object.entries(toolRuntimeModule).find(([k]) => k.startsWith('ToolRuntimeScope__AttachFamilyRecovery_'))?.[1]
const roleFor = Object.entries(toolRuntimeModule).find(([k]) => k.startsWith('ToolRuntimeScope__RoleFor_'))?.[1]
const dispatcherModule = await import('../../../dist/Interaction/Dispatch/Dispatcher.js')
const { forJournal } = dispatcherModule
const registerAuthority = Object.entries(dispatcherModule).find(([k]) => k.startsWith('Runtime__RegisterAuthority_'))?.[1]
const { Role } = await import('../../../dist/Foundation/Roles.js')
const jobModule = await import('../../../dist/Change/Job.js')
const verdictMailbox = Object.entries(jobModule).find(([k]) => k.startsWith('VerdictMailbox_$ctor'))?.[1]
const publish = Object.entries(jobModule).find(([k]) => k.startsWith('VerdictMailbox__Publish_'))?.[1]
const { OrchestratorVerdict } = await import('../../../dist/Change/Types.js')
const { FamilyRecovery, FamilyRecoveryPermit } = await import('../../../dist/Execution/Session/Recovery/Model.js')
const { AgentJournalModule_revision, AgentJournalModule_snapshot } = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const { JournalRevisionModule_value } = await import('../../../dist/Foundation/Identity.js')
const { discover } = await import('../../../dist/Execution/Session/RecoveryClosureProjection.js')

const context = (session = 'ses_join') =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => { })

const lock = () => ({ Enter: () => ({ Exit: () => { } }) })

const scopeFor = async ({ engineTask, mailbox }) => {
  const opened = await agentJournal.create({ directory: `join-family-${Math.random()}` })
  assert.equal(opened.ok, true, opened.ok ? '' : opened.error)
  const dispatcher = forJournal(opened.journal)
  const accepted = await registerAuthority(
    dispatcher,
    attemptPlanner.authority({
      session: 'ses_join',
      run: 'run_join',
      root: 'msg_join_root',
      selected: 'fast-orchestrator',
      peer: 'deep-orchestrator',
      role: 'Orchestrator',
      tier: 'Fast',
    }),
  )
  assert.equal(accepted.tag, 0, accepted.fields?.[0])

  const scope = new ToolRuntimeScope(
    {},
    opened.journal,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(opened.journal))
  const closure = discover(
    sessionId('ses_join'),
    AgentJournalModule_snapshot(opened.journal).AgentProjections,
    sequence,
  )
  attachFamilyRecovery(
    scope,
    async () => new FamilyRecovery(0, [new FamilyRecoveryPermit(sessionId('ses_join'), sequence, closure.Digest)]),
  )
  assert.equal(roleFor(scope, context()).tag, Role.Orchestrator.tag, 'fixture must exercise the Orchestrator join branch')
  const host = {
    joinGate: lock(),
    joinInFlight: false,
    engineGate: lock(),
    engineInstance: mailbox ? { mailbox } : undefined,
    engineTask: engineTask ?? undefined,
  }
  scope.orchestratorHosts.set('ses_join', host)
  return { scope, cleanup: opened.dispose }
}

const run = async (scope, session = 'ses_join') => spec(scope).Execute({}, context(session))

test('WHAT[DELEG-014] JOINFAM_orchestrator_drains_published_verdicts', async () => {
  const mailbox = verdictMailbox()
  publish(mailbox, OrchestratorVerdict.Empty)
  const live = await scopeFor({ mailbox })
  const wire = await run(live.scope)
  assert.match(wire, /There is nothing away to receive/i)
  live.cleanup()
})

test('WHAT[DELEG-014] JOINFAM_orchestrator_empty_mailbox_still_reports_completed', async () => {
  const live = await scopeFor({ mailbox: verdictMailbox() })
  const wire = await run(live.scope)
  assert.match(wire, /There is nothing away to receive/i)
  live.cleanup()
})

test('WHAT[DELEG-014] JOINFAM_orchestrator_engine_failure_is_a_natural_consequence', async () => {
  const live = await scopeFor({ engineTask: Promise.resolve({ tag: 1, fields: ['engine exploded'] }) })
  const wire = await run(live.scope)
  assert.match(wire, /orchestrator is not ready to join yet/i)
  assert.doesNotMatch(wire, /engine exploded|\berror\s*=/i)
  live.cleanup()
})
