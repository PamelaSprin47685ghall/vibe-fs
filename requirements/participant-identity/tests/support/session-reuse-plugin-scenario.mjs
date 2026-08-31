import assert from 'node:assert/strict'

import {
  acceptAuthorityRoot,
  activateLife,
  completeManagerLife,
  configureManagedPlugin,
  decideAuthorityRoot,
  observeAuthority,
  withRestartablePlugin,
} from '../../../verification-system/tests/support/plugin-fixture.mjs'

const sessionId = 'ses-production-reusable-identity'
const leadMessageId = 'msg-production-lead-root'
const operatorMessageId = 'msg-production-operator-root'

const events = []
globalThis.__wanxiangshu_test_routing_seen = []

await withRestartablePlugin(async (start, _directory, { stop, withRuntime }) => {
  const firstPlugin = await start()
  await configureManagedPlugin(firstPlugin)

  let operatorProfile
  await withRuntime(async (runtime) => {
    const leadProfile = await acceptAuthorityRoot(runtime, sessionId, 'deep-manager', leadMessageId)
    const originalLeadProfile = structuredClone(leadProfile)
    assert.equal(leadProfile.participantIdentity.persona, 'Lead')

    const duplicateLead = await decideAuthorityRoot(runtime, sessionId, 'deep-manager', leadMessageId)
    assert.equal(duplicateLead.ok, true, JSON.stringify(duplicateLead.error))
    assert.deepEqual(duplicateLead.profile, leadProfile)

    const drift = await decideAuthorityRoot(
      runtime,
      sessionId,
      'deep-devops',
      'msg-production-illegal-operator-root',
    )
    assert.equal(drift.ok, false)
    assert.equal(drift.error.kind, 'ActiveRunIdentityConflict')
    assert.deepEqual(drift.error.active, leadProfile)
    assert.equal(drift.error.requested.participantIdentity.persona, 'Operator')

    assert.equal(globalThis.__wanxiangshu_test_routing_seen.length, 0)
    assert.deepEqual(observeAuthority(runtime, sessionId).activeLogicalRun, originalLeadProfile)

    await activateLife(runtime, sessionId, leadMessageId)
    await completeManagerLife(runtime, sessionId)
    assert.equal(observeAuthority(runtime, sessionId).activeLogicalRun, null)

    operatorProfile = await acceptAuthorityRoot(runtime, sessionId, 'deep-devops', operatorMessageId)
    assert.equal(operatorProfile.participantIdentity.persona, 'Operator')
    assert.notEqual(operatorProfile.logicalRun, leadProfile.logicalRun)
    assert.notEqual(operatorProfile.authorityRoot, leadProfile.authorityRoot)

    events.push({
      case: 'same-session-role-replacement',
      sessionId,
      leadLogicalRun: leadProfile.logicalRun,
      conflictKind: drift.error.kind,
      providerOrRetryExecutionsBeforeClose: globalThis.__wanxiangshu_test_routing_seen.length,
      duplicateLeadLogicalRun: duplicateLead.profile.logicalRun,
      operatorLogicalRun: operatorProfile.logicalRun,
    })
  })

  await stop(firstPlugin)

  const restartedPlugin = await start()
  await configureManagedPlugin(restartedPlugin)
  await withRuntime(async (runtime) => {
    const recovered = observeAuthority(runtime, sessionId).activeLogicalRun
    assert.deepEqual(recovered, operatorProfile)

    const duplicateOperator = await decideAuthorityRoot(runtime, sessionId, 'deep-devops', operatorMessageId)
    assert.equal(duplicateOperator.ok, true, JSON.stringify(duplicateOperator.error))
    assert.deepEqual(duplicateOperator.profile, operatorProfile)

    events.push({
      case: 'rolling-restart-durable-identity',
      sessionId,
      activeLogicalRun: recovered.logicalRun,
      persona: recovered.participantIdentity.persona,
      duplicateLogicalRun: duplicateOperator.profile.logicalRun,
    })
  })

  const concurrentPlugin = await start()
  await configureManagedPlugin(concurrentPlugin)
  await withRuntime(async (runtime) => {
    const shared = observeAuthority(runtime, sessionId).activeLogicalRun
    assert.deepEqual(shared, operatorProfile)
    events.push({
      case: 'cross-plugin-instance-isolation',
      sessionId,
      activeLogicalRun: shared.logicalRun,
      persona: shared.participantIdentity.persona,
    })
  })
})

process.stdout.write(`${JSON.stringify({ ok: true, events })}\n`)
