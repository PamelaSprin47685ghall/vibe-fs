// tests/unit/tools/join-tool-family.test.mjs — JoinTool FamilyReady orchestrator paths.
//
// Real VerdictMailbox + real JoinInterrupt; the scope carries an authority
// profile (RoleFor = Orchestrator) and a pre-seeded orchestrator host whose
// engine is the mailbox. Everything in JoinTool.execute is production code.

import assert from 'node:assert/strict'
import test from 'node:test'

import { agentJournal, attemptPlanner, sessionId } from '../../verification-system/tests/support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JoinTool.js')
const { ToolRuntimeScope, ToolRuntimeScope__AttachFamilyRecovery_3A336721: attachFamilyRecovery } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { forJournal, Runtime__RegisterAuthority } = await import('../../../dist/Application/Prompting/PromptDispatcher.js')
const { VerdictMailbox_$ctor: verdictMailbox, VerdictMailbox__Publish_Z699F102F: publish } = await import(
  '../../../dist/Application/Orchestration/ManagerJob.js'
)
const { OrchestratorVerdict } = await import('../../../dist/Application/Orchestration/Types.js')

const context = (session = 'ses_join') =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const lock = () => ({ Enter: () => ({ Exit: () => {} }) })

const scopeFor = async ({ engineTask, mailbox }) => {
  const opened = await agentJournal.create({ directory: `join-family-${Math.random()}` })
  assert.equal(opened.ok, true, opened.ok ? '' : opened.error)
  const dispatcher = forJournal(opened.journal)
  const accepted = await Runtime__RegisterAuthority(
    dispatcher,
    attemptPlanner.authority({
      session: 'ses_join',
      run: 'run_join',
      root: 'msg_join_root',
      selected: 'orchestrator',
      peer: 'orchestrator',
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
  attachFamilyRecovery(scope, async () => ({ tag: 0, fields: [{}] })) // FamilyReady permit
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

test('JOINFAM_orchestrator_drains_published_verdicts', async () => {
  const mailbox = verdictMailbox()
  publish(mailbox, OrchestratorVerdict.Empty)
  const live = await scopeFor({ mailbox })
  const wire = await run(live.scope)
  assert.match(wire, /There is nothing away to receive/i)
  live.cleanup()
})

test('JOINFAM_orchestrator_empty_mailbox_still_reports_completed', async () => {
  const live = await scopeFor({ mailbox: verdictMailbox() })
  const wire = await run(live.scope)
  assert.match(wire, /There is nothing away to receive/i)
  live.cleanup()
})

test('JOINFAM_orchestrator_engine_failure_is_a_natural_consequence', async () => {
  const live = await scopeFor({ engineTask: Promise.resolve({ tag: 1, fields: ['engine exploded'] }) })
  const wire = await run(live.scope)
  assert.match(wire, /orchestrator is not ready to join yet/i)
  assert.doesNotMatch(wire, /engine exploded|\berror\s*=/i)
  live.cleanup()
})
