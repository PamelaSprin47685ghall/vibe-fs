// tests/unit/tools/join-tool-family.test.mjs — JoinTool FamilyReady orchestrator paths.
//
// Real VerdictMailbox + real JoinInterrupt; the scope carries an authority
// profile (RoleFor = Orchestrator) and a pre-seeded orchestrator host whose
// engine is the mailbox. Everything in JoinTool.execute is production code.

import assert from 'node:assert/strict'
import test from 'node:test'

import { sessionId } from '../support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JoinTool.js')
const { ToolRuntimeScope, ToolRuntimeScope__AttachFamilyRecovery_3A336721: attachFamilyRecovery } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { SessionAgentProjection } = await import('../../../dist/Journal/AgentProjection.js')
const { ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')
const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')
const { VerdictMailbox_$ctor: verdictMailbox, VerdictMailbox__Publish_Z699F102F: publish } = await import(
  '../../../dist/Application/Orchestration/ManagerJob.js'
)
const { OrchestratorVerdict } = await import('../../../dist/Application/Orchestration/Types.js')

const context = (session = 'ses_join') =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const lock = () => ({ Enter: () => ({ Exit: () => {} }) })

const sessionMap = (entries) => mapOfList(entries, { Compare: compare })

const scopeFor = ({ engineTask, mailbox }) => {
  const scope = new ToolRuntimeScope(
    {},
    undefined,
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
  // RoleFor = Orchestrator via a fake authority profile on a fake journal.
  scope.journal = {
    gate: lock(),
    projection: {
      AgentProjections: {
        Sessions: sessionMap([
          [
            sessionId('ses_join'),
            new SessionAgentProjection(
              undefined,
              undefined,
              undefined,
              undefined,
              undefined,
              undefined,
              undefined,
              undefined,
              { ActiveLogicalRun: { CanonicalRole: Role.Orchestrator, SelectedAgent: 'fast-manager' }, LastAuthorityProfile: undefined },
              undefined,
              undefined,
              undefined,
            ),
          ],
        ]),
      },
    },
  }
  return scope
}

const run = async (scope, session = 'ses_join') => spec(scope).Execute({}, context(session))

test('JOINFAM_orchestrator_drains_published_verdicts', async () => {
  const mailbox = verdictMailbox()
  publish(mailbox, OrchestratorVerdict.Empty)
  const scope = scopeFor({ mailbox })
  const wire = await run(scope)
  assert.match(wire, /There is nothing away to receive/i)
})

test('JOINFAM_orchestrator_empty_mailbox_still_reports_completed', async () => {
  const scope = scopeFor({ mailbox: verdictMailbox() })
  const wire = await run(scope)
  assert.match(wire, /There is nothing away to receive/i)
})

test('JOINFAM_orchestrator_engine_failure_is_a_natural_consequence', async () => {
  const scope = scopeFor({ engineTask: Promise.resolve({ tag: 1, fields: ['engine exploded'] }) })
  const wire = await run(scope)
  assert.match(wire, /orchestrator is not ready to join yet/i)
  assert.doesNotMatch(wire, /engine exploded|\berror\s*=/i)
})
