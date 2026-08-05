/**
 * RECOVERY-FAMILY / FLOW: closed SessionRecovery DSL + private permit +
 * child-first authorize properties.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = new URL('../../../', import.meta.url).pathname

test('RECOVERY_FAMILY_dsl_module_and_private_permit_exist', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/SessionRecovery.fs'), 'utf8')
  assert.match(src, /module SessionRecovery/)
  assert.match(src, /type FamilyRecoveryPermit\s*=\s*\n\s*private/)
  assert.match(src, /type SessionRecoveryProgram/)
  assert.match(src, /recoverFamily/)
  assert.match(src, /authorizeFamilyResume/)
  assert.doesNotMatch(src, /fromTask|Flow\.lift/)
})

test('RECOVERY_FAMILY_constructor_does_not_start_fork_restore', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Session/HostForkRuntime.fs'), 'utf8')
  assert.doesNotMatch(src, /do recoveryTask <- restoreChildren/)
  assert.match(src, /EnsureChildRestoreStarted/)
  assert.match(src, /RestoreLinkedHandles/)
})

test('RECOVERY_FAMILY_plugin_attaches_family_ports_not_local_gates', () => {
  const spike = readFileSync(join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs'), 'utf8')
  const scope = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs'),
    'utf8',
  )
  assert.match(spike, /AttachFamilyRecoveryPorts/)
  assert.doesNotMatch(spike, /AttachRecoveryGate|AttachBloggerRecoveryGate/)
  assert.match(scope, /RequireFamilyRecovery/)
  assert.doesNotMatch(scope, /bloggerRecoveryGate|PromptRecovery\.RecoveryGate/)
})

test('RECOVERY_FAMILY_authorize_blocks_on_child_block', async () => {
  const {
    caseOf,
    sessionRecovery,
    sessionId,
  } = await import('../support/domain.mjs')

  const root = sessionId('parent')
  const child = sessionId('child')
  const blocked = sessionRecovery.blocked(sessionRecovery.snapshotUnreadable(child, 'timeout'))
  const recovered = sessionRecovery.recoveredClosure(root, {
    child: blocked,
  })
  const family = sessionRecovery.authorizeFamilyResume(root, 1, recovered)
  assert.equal(caseOf(family), 'FamilyBlocked')
})

test('RECOVERY_FAMILY_authorize_ready_issues_private_permit', async () => {
  const { caseOf, sessionRecovery, sessionId, payloadOf } = await import('../support/domain.mjs')

  const root = sessionId('parent')
  const recovered = sessionRecovery.recoveredClosure(root, {})
  const family = sessionRecovery.authorizeFamilyResume(root, 7, recovered)
  assert.equal(caseOf(family), 'FamilyReady')
  const permit = payloadOf(family)
  assert.equal(sessionRecovery.permitRoot(permit), root)
})

test('RECOVERY_FAMILY_trace_ready_before_business_property', async () => {
  const { sessionRecovery } = await import('../support/domain.mjs')

  const ok = sessionRecovery.familyReadyBeforeBusiness([
    sessionRecovery.traceDiscover('p'),
    sessionRecovery.traceFamilyReady('p', 'd'),
    sessionRecovery.traceBusiness('fork'),
  ])
  assert.equal(ok, true)

  const bad = sessionRecovery.familyReadyBeforeBusiness([
    sessionRecovery.traceDiscover('p'),
    sessionRecovery.traceBusiness('fork'),
    sessionRecovery.traceFamilyReady('p', 'd'),
  ])
  assert.equal(bad, false)
})
