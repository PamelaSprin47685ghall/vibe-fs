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

const run = async (runtimeScope, session = 'ses_join') => parseToml(await spec(runtimeScope).Execute({}, context(session)))

test('JOIN_blank_session_is_refused_before_recovery', async () => {
  const result = await run(scope(), '')

  assert.equal(result.error, 'Missing sessionID')
})

test('JOIN_without_a_recovery_permit_is_blocked', async () => {
  const result = await run(scope())

  assert.equal(result.error.code, 'RECOVERY_BLOCKED')
  assert.match(result.error.message, /coordinator unavailable for ses_join/)
})

test('JOIN_waiting_recovery_is_retryable', async () => {
  const runtimeScope = scope()
  attachFamilyRecovery(runtimeScope, async () =>
    new FamilyRecovery(1, [nonEmptyOne(new RecoveryBlock(1, [sessionId('ses_join')]))]),
  )

  const result = await run(runtimeScope)

  assert.equal(result.error.code, 'RECOVERY_WAITING')
  assert.match(result.error.message, /FamilyReady/)
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

  const result = await run(runtimeScope)

  assert.equal(result.status, 'failed')
  assert.equal(result.error.code, 'NOTHING_TO_JOIN')

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

  const result = await run(runtimeScope)

  assert.equal(result.status, 'failed')
  assert.match(result.error.code, /^NOT_FOUND:/)

  opened.dispose()
  rmSync(directory, { recursive: true, force: true })
})
