// tests/unit/tools/join-tool.test.mjs — JoinTool public behavior.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { agentJournal, sessionId } from '../support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JoinTool.js')
const { ToolRuntimeScope, ToolRuntimeScope__AttachFamilyRecovery_3A336721: attachFamilyRecovery } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const {
  FamilyRecovery,
  FamilyRecoveryPermit,
  NonEmpty_one: nonEmptyOne,
  RecoveryBlock,
} = await import('../../../dist/Domain/SessionRecovery.js')
const { AgentJournalModule_revision, AgentJournalModule_snapshot } =
  await import('../../../dist/Journal/AgentJournal.js')
const { JournalRevisionModule_value } = await import('../../../dist/Kernel/Identity.js')
const { discover } = await import('../../../dist/Journal/RecoveryClosureProjection.js')
const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')

const context = (session = 'ses_join') =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const scope = () =>
  new ToolRuntimeScope(
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
    undefined,
    undefined,
  )

const run = async (runtimeScope, session = 'ses_join') => {
  const wire = await spec(runtimeScope).Execute({}, context(session))
  return { wire, parsed: parseToml(wire) }
}

test('JOIN_blank_caller_is_refused_before_recovery_without_identity_leak', async () => {
  const { wire } = await run(scope(), '')

  assert.doesNotMatch(wire, /sessionID|\berror\s*=/i)
  assert.match(wire, /authority is established/i)
})

test('JOIN_without_a_recovery_permit_is_blocked_by_natural_consequence', async () => {
  const { wire } = await run(scope())

  assert.doesNotMatch(wire, /RECOVERY_BLOCKED|ses_join|\berror\s*=/)
  assert.match(wire, /recovery is blocked/i)
})

test('JOIN_waiting_recovery_is_retryable_without_machine_state', async () => {
  const runtimeScope = scope()
  attachFamilyRecovery(runtimeScope, async () =>
    new FamilyRecovery(1, [nonEmptyOne(new RecoveryBlock(1, [sessionId('ses_join')]))]),
  )

  const { wire } = await run(runtimeScope)

  assert.doesNotMatch(wire, /RECOVERY_WAITING|FamilyReady|\berror\s*=/)
  assert.match(wire, /Recovery is still in progress/)
  assert.match(wire, /Join again after the family becomes ready/)
})

test('JOIN_ready_permit_maps_empty_join_to_failure', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-jointool-'))
  const opened = agentJournal.create({ directory })
  assert.equal(opened.ok, true, 'journal must open')

  const runtimeScope = scope()
  runtimeScope.runtimes.set(
    'ses_join',
    new HostForkRuntime(sessionId('ses_join'), {}, opened.journal),
  )
  const sequence = JournalRevisionModule_value(AgentJournalModule_revision(opened.journal))
  const closure = discover(
    sessionId('ses_join'),
    AgentJournalModule_snapshot(opened.journal).AgentProjections,
    sequence,
  )
  attachFamilyRecovery(
    runtimeScope,
    async () => new FamilyRecovery(0, [new FamilyRecoveryPermit(sessionId('ses_join'), sequence, closure.Digest)]),
  )

  const { wire, parsed } = await run(runtimeScope)

  assert.match(wire, /nothing away to receive/i)
  assert.equal(parsed.status, undefined)

  opened.dispose()
  rmSync(directory, { recursive: true, force: true })
})

test('JOIN_ready_invalid_permit_surfaces_not_found', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-jointool-'))
  const opened = agentJournal.create({ directory })
  assert.equal(opened.ok, true, 'journal must open')

  const runtimeScope = scope()
  runtimeScope.runtimes.set(
    'ses_join',
    new HostForkRuntime(sessionId('ses_join'), {}, opened.journal),
  )
  attachFamilyRecovery(
    runtimeScope,
    async () => new FamilyRecovery(0, [new FamilyRecoveryPermit(sessionId('other_session'), 0n, '')]),
  )

  const { wire, parsed } = await run(runtimeScope)

  assert.match(wire, /No one by that name is away/i)
  assert.equal(parsed.status, undefined)

  opened.dispose()
  rmSync(directory, { recursive: true, force: true })
})
