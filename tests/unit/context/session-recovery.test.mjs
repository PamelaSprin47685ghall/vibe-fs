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
  const code = src
    .split('\n')
    .filter((l) => !/^\s*\/\//.test(l) && !/^\s*\*/.test(l))
    .join('\n')
  assert.doesNotMatch(code, /do recoveryTask <- restoreChildren/)
  // GREEN-4: second recovery ownership deleted from HostForkRuntime.
  assert.doesNotMatch(code, /\brecoveryTask\b/)
  assert.doesNotMatch(code, /EnsureChildRestoreStarted/)
  assert.doesNotMatch(code, /member this\.AwaitRecovery/)
  assert.doesNotMatch(code, /member this\.RestoreLinkedHandles/)
  assert.doesNotMatch(code, /do!\s*this\.AwaitRecovery/)
})

test('RECOVERY_FAMILY_plugin_attaches_family_ports_not_local_gates', () => {
  const spike = readFileSync(join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs'), 'utf8')
  const scope = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs'),
    'utf8',
  )
  const ports = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Application/Reconciliation/SessionRecoveryInterpreter.fs'),
    'utf8',
  )
  assert.match(spike, /AttachFamilyRecoveryPorts/)
  assert.doesNotMatch(spike, /AttachRecoveryGate|AttachBloggerRecoveryGate/)
  // GREEN-4: real RestoreHandles/RecoverJobs; no option ports collapsing to NoRecoveryRequired.
  assert.doesNotMatch(spike, /RestoreHandles\s*=\s*None/)
  assert.doesNotMatch(spike, /RecoverJob\s*=\s*None/)
  assert.match(spike, /RestoreHandles\s*=\s*restoreHandles/)
  assert.match(spike, /RecoverJobs\s*=\s*recoverJobs/)
  assert.match(ports, /type SessionRecoveryPorts/)
  assert.match(ports, /RestoreHandles:\s*SessionId\s*->\s*Task<HandleFamilyRecovery>/)
  assert.match(scope, /RequireFamilyRecovery/)
  assert.doesNotMatch(scope, /bloggerRecoveryGate|PromptRecovery\.RecoveryGate/)
})

test('RECOVERY_FAMILY_handle_family_types_and_permit_rules', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/SessionRecovery.fs'), 'utf8')
  assert.match(src, /type HandleFamilyRecovery/)
  assert.match(src, /NoLinkedHandles/)
  assert.match(src, /HandlesRecovered/)
  assert.match(src, /HandlesWaiting/)
  assert.match(src, /HandlesBlocked/)
  assert.match(src, /type JobFamilyRecovery/)
  assert.match(src, /NoRelatedJobs/)
  assert.match(src, /JobsRecovered/)
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/ChildRecovery.fs'), 'utf8')
  assert.match(child, /type ChildRecoveryResult/)
  assert.match(child, /RecoveredActive/)
  assert.match(child, /RecoveryIncomplete/)
  assert.match(child, /RecoveryBlocked/)
  assert.doesNotMatch(child, /\| AwaitingEvidence\b/)
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

// HandlesWaiting → SessionRecovery.Waiting → FamilyWaiting (no permit, not FamilyBlocked).
test('RECOVERY_FAMILY_authorize_waiting_is_family_waiting_not_blocked', async () => {
  const { caseOf, sessionRecovery, sessionId } = await import('../support/domain.mjs')

  const root = sessionId('parent')
  const child = sessionId('child')
  const waiting = sessionRecovery.waiting(
    sessionRecovery.childRecoveryFailed(child, 'handle h waiting: awaiting terminal evidence'),
  )
  const recovered = sessionRecovery.recoveredClosure(root, { child: waiting })
  const family = sessionRecovery.authorizeFamilyResume(root, 1, recovered)
  assert.equal(caseOf(family), 'FamilyWaiting')
  assert.notEqual(caseOf(family), 'FamilyBlocked')
  assert.notEqual(caseOf(family), 'FamilyReady')
})

test('RECOVERY_FAMILY_handle_family_waiting_maps_to_waiting_not_blocked', async () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/SessionRecovery.fs'), 'utf8')
  // Extract the HandlesWaiting match arm body (up to the next arm) and assert its outcome.
  const waitingArm = src.match(
    /HandleFamilyRecovery\.HandlesWaiting[\s\S]*?(?=HandleFamilyRecovery\.HandlesBlocked)/,
  )?.[0]
  assert.ok(waitingArm, 'HandleFamilyRecovery.HandlesWaiting arm body not found')
  assert.match(waitingArm, /SessionRecovery\.Waiting/)
  assert.doesNotMatch(waitingArm, /SessionRecovery\.Blocked/)
  assert.match(src, /\| FamilyWaiting of NonEmpty<RecoveryBlock>/)
  assert.match(src, /\| Waiting of NonEmpty<RecoveryBlock>/)
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
