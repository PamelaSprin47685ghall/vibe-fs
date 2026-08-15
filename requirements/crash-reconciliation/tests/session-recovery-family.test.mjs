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
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  assert.match(src, /module SessionRecovery/)
  assert.match(src, /type FamilyRecoveryPermit\s*=\s*\n\s*private/)
    assert.match(src, /authorizeFamilyResume/)
  assert.match(src, /authorizeFamilyResume/)
  assert.doesNotMatch(src, /fromTask|Flow\.lift/)
})

test('RECOVERY_FAMILY_constructor_does_not_start_fork_restore', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs'), 'utf8')
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

test('RECOVERY_FAMILY_library_is_detached_from_ordinary_plugin_and_join_uses_current_process_permit', () => {
  const wiring = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/PluginRecoveryWiring.fs'), 'utf8')
  const spike = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/SpikePlugin.fs'), 'utf8')
  const scope = readFileSync(
    join(ROOT, 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs'),
    'utf8',
  )
  const ports = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs'),
    'utf8',
  )

  assert.doesNotMatch(spike, /PluginRecoveryWiring\.attach|AttachFamilyRecoveryPorts/)
  assert.doesNotMatch(wiring, /restoreLinkedChildren|recoverFamilyDirect|defaultRecoverPromptClaims|defaultRecoverBlogger/)
  assert.match(scope, /FamilyRecoveryPermit\.currentProcess/)
  assert.doesNotMatch(scope, /FamilyRecoveryCoordinator\.runOnce|recoverFamilyDirect/)

  // Recovery algorithms remain a detached library for a future explicit /continue path.
  assert.match(ports, /type SessionRecoveryPorts/)
  assert.match(ports, /RestoreHandles:\s*SessionId\s*->\s*Task<HandleFamilyRecovery>/)
  assert.match(ports, /recoverFamilyDirect/)
})

test('RECOVERY_FAMILY_combine_and_coordinator_ownership_moved', () => {
  const domain = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  const workflow = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs'),
    'utf8',
  )
  const coordinator = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Coordinator.fs'),
    'utf8',
  )
  const fsproj = readFileSync(join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj'), 'utf8')

  // rabbit §14.2
  assert.match(domain, /let combine \(outcomes: SessionRecovery list\)/)
  assert.match(workflow, /\bcombine\b/)
  assert.doesNotMatch(workflow, /let private mergeOutcomes/)

  // rabbit §14.3
  assert.match(coordinator, /module FamilyRecoveryCoordinator/)
  assert.match(coordinator, /let runOnce/)
  assert.doesNotMatch(coordinator, /recoverFamilyDirect|SessionRecoveryPorts|authorizeFamilyResume/)
  assert.doesNotMatch(workflow, /module Coordinator/)
  assert.match(fsproj, /Execution\/Session\/Recovery\/Coordinator\.fs/)
})

test('RECOVERY_FAMILY_handle_family_types_and_permit_rules', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  assert.match(src, /type HandleFamilyRecovery/)
  assert.match(src, /NoLinkedHandles/)
  assert.match(src, /HandlesRecovered/)
  assert.match(src, /HandlesWaiting/)
  assert.match(src, /HandlesBlocked/)
  assert.match(src, /type JobFamilyRecovery/)
  assert.match(src, /NoRelatedJobs/)
  assert.match(src, /JobsRecovered/)
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs'), 'utf8')
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
  } = await import('../../verification-system/tests/support/domain.mjs')

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
  const { caseOf, sessionRecovery, sessionId, payloadOf } = await import('../../verification-system/tests/support/domain.mjs')

  const root = sessionId('parent')
  const recovered = sessionRecovery.recoveredClosure(root, {})
  const family = sessionRecovery.authorizeFamilyResume(root, 7, recovered)
  assert.equal(caseOf(family), 'FamilyReady')
  const permit = payloadOf(family)
  assert.equal(sessionRecovery.permitRoot(permit), root)
})

// HandlesWaiting → SessionRecovery.Waiting → FamilyWaiting (no permit, not FamilyBlocked).
test('RECOVERY_FAMILY_authorize_waiting_is_family_waiting_not_blocked', async () => {
  const { caseOf, sessionRecovery, sessionId } = await import('../../verification-system/tests/support/domain.mjs')

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
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
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

test('RECOVERY_FAMILY_ready_before_business_is_type_enforced', async () => {
  const { caseOf, sessionRecovery, sessionId } = await import('../../verification-system/tests/support/domain.mjs')
  const root = sessionId('p')
  const family = sessionRecovery.authorizeFamilyResume(root, 7, sessionRecovery.recoveredClosure(root, {}))
  assert.equal(caseOf(family), 'FamilyReady')
})
