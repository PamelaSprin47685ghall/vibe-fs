/**
 * RECOVERY-FAMILY / FLOW: closed SessionRecovery DSL + private permit +
 * child-first authorize properties.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

const ROOT = new URL('../../../', import.meta.url).pathname

test('WHAT[CRASH-004] RECOVERY_FAMILY_dsl_module_and_private_permit_exist', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  assert.match(src, /module SessionRecovery/)
  assert.match(src, /type FamilyRecoveryPermit\s*=\s*\n\s*private/)
  assert.match(src, /authorizeFamilyResume/)
  assert.doesNotMatch(src, /fromTask|Flow\.lift/)
})

test('WHAT[CRASH-006] RECOVERY_FAMILY_constructor_does_not_start_fork_restore', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs'), 'utf8')
  const code = src.split('\n').filter((line) => !/^\s*\/\//.test(line) && !/^\s*\*/.test(line)).join('\n')
  assert.doesNotMatch(code, /do recoveryTask <- restoreChildren/)
  assert.doesNotMatch(code, /\brecoveryTask\b/)
  assert.doesNotMatch(code, /EnsureChildRestoreStarted/)
  assert.doesNotMatch(code, /member this\.AwaitRecovery/)
  assert.doesNotMatch(code, /member this\.RestoreLinkedHandles/)
  assert.doesNotMatch(code, /do!\s*this\.AwaitRecovery/)
})

test('WHAT[CRASH-017] RECOVERY_FAMILY_plugin_load_only_attaches_physical_recovery_wiring_and_join_uses_current_process_permit', () => {
  const wiring = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/PluginRecoveryWiring.fs'), 'utf8')
  const spike = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/SpikePlugin.fs'), 'utf8')
  const scope = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs'), 'utf8')
  assert.match(spike, /PluginRecoveryWiring\.attach boot/)
  assert.match(wiring, /scope\.AttachDurabilityActivation\(fun \(\) ->\s*scope\.RunBackground/)
  assert.doesNotMatch(spike, /SignalChatRecovery|FamilyRecoveryCoordinator\.runOnce|recoverFamilyDirect|AttachFamilyRecoveryPorts/)
  assert.doesNotMatch(wiring, /restoreLinkedChildren|recoverFamilyDirect|defaultRecoverPromptClaims|defaultRecoverBlogger/)
  assert.match(scope, /FamilyRecoveryPermit\.currentProcess/)
  assert.doesNotMatch(scope, /FamilyRecoveryCoordinator\.runOnce|recoverFamilyDirect/)
})

test('WHAT[CRASH-013] RECOVERY_FAMILY_combine_and_coordinator_ownership_moved', () => {
  const domain = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  const coordinator = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Coordinator.fs'), 'utf8')
  const fsproj = readFileSync(join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj'), 'utf8')
  assert.match(domain, /let combine \(outcomes: SessionRecovery list\)/)
  assert.match(coordinator, /module FamilyRecoveryCoordinator/)
  assert.match(coordinator, /let runOnce/)
  assert.doesNotMatch(coordinator, /recoverFamilyDirect|SessionRecoveryPorts|authorizeFamilyResume/)
  assert.match(fsproj, /Execution\/Session\/Recovery\/Coordinator\.fs/)
})

test('WHAT[CRASH-010] RECOVERY_FAMILY_handle_family_types_and_permit_rules', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  assert.match(src, /type HandleFamilyRecovery/)
  assert.match(src, /NoLinkedHandles/)
  assert.match(src, /HandlesRecovered/)
  assert.match(src, /HandlesWaiting/)
  assert.match(src, /HandlesBlocked/)
  assert.match(src, /type JobFamilyRecovery/)
  assert.match(src, /NoRelatedJobs/)
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs'), 'utf8')
  assert.match(child, /type ChildRecoveryResult/)
  assert.match(child, /RecoveredActive/)
  assert.match(child, /RecoveryIncomplete/)
  assert.match(child, /RecoveryBlocked/)
  assert.doesNotMatch(child, /\| AwaitingEvidence\b/)
})

test('WHAT[CRASH-005] RECOVERY_FAMILY_authorize_blocks_on_child_block', () => {
  assert.equal(recovery.authorize('parent', 1, [{ session: 'child', state: 'Blocked' }]).state, 'FamilyBlocked')
})

test('WHAT[CRASH-006] RECOVERY_FAMILY_authorize_ready_issues_private_permit', () => {
  const result = recovery.authorize('parent', 7, [])
  assert.equal(result.state, 'FamilyReady')
  assert.equal(result.root, 'parent')
})

test('WHAT[CRASH-005] RECOVERY_FAMILY_authorize_waiting_is_family_waiting_not_blocked', () => {
  const result = recovery.authorize('parent', 1, [{ session: 'child', state: 'Waiting' }])
  assert.equal(result.state, 'FamilyWaiting')
  assert.notEqual(result.state, 'FamilyBlocked')
  assert.notEqual(result.state, 'FamilyReady')
})

test('WHAT[CRASH-005] RECOVERY_FAMILY_handle_family_waiting_maps_to_waiting_not_blocked', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  const waitingArm = src.match(/HandleFamilyRecovery\.HandlesWaiting[\s\S]*?(?=HandleFamilyRecovery\.HandlesBlocked)/)?.[0]
  assert.ok(waitingArm, 'HandleFamilyRecovery.HandlesWaiting arm body not found')
  assert.match(waitingArm, /waitingRecovery/)
  assert.doesNotMatch(waitingArm, /SessionRecovery\.Blocked/)
  const waitingMapping = src.match(/let private waitingRecovery[\s\S]*?SessionRecovery\.Waiting/)?.[0]
  assert.ok(waitingMapping, 'waitingRecovery must map to SessionRecovery.Waiting')
  assert.match(src, /\| FamilyWaiting of NonEmpty<RecoveryBlock>/)
  assert.match(src, /\| Waiting of NonEmpty<RecoveryBlock>/)
})

test('WHAT[CRASH-006] RECOVERY_FAMILY_ready_before_business_is_type_enforced', () => {
  assert.equal(recovery.authorize('p', 7, []).state, 'FamilyReady')
})

// CRASH-018 / R10: explicit /continue owns resume. The host below is the
// compiled production owner behind the registered SessionRecoveryHostSurface
// (opaque host/scope/journal handles, plain-JSON resume views); only the
// accept/reject port answer is a test input. Publications are counted on the
// production registry, never emulated in JS.
import { mkdtempSync as recoveryMkdtemp, rmSync as recoveryRm } from 'node:fs'
import { tmpdir as recoveryTmpdir } from 'node:os'
import { join as recoveryJoin } from 'node:path'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

const withContinueHost = async (label, portOutcome, action) => {
  const directory = recoveryMkdtemp(recoveryJoin(recoveryTmpdir(), `wxs-continue-${label}-`))
  const host = await recoveryHost.bootRecoveryHost(directory, portOutcome)

  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    recoveryRm(directory, { recursive: true, force: true })
  }
}

const continueSessionOf = (suffix) => `ses-continue-${suffix}`
const continuePhysicalOf = (suffix) => `msg-continue-${suffix}`

test('WHAT[CRASH-018] CRASH_018_absent_port_blocks_with_one_manual_and_no_background_command', async () => {
  await withContinueHost('absent', 'absent', async (host) => {
    const result = await recoveryHost.resumeAccepted(
      host,
      continueSessionOf('absent'),
      continuePhysicalOf('absent'),
    )

    assert.equal(result.calls, 0, 'absent port makes no acceptance call')
    assert.equal(result.manuals.length, 1)
    assert.equal(result.manuals[0].reason, 'NoAuthorizedProviderDisposition')
    assert.equal(result.manuals[0].observation, 'ProviderAbsent')
  })
})

test('WHAT[CRASH-018] CRASH_018_duplicate_continue_keeps_a_single_manual', async () => {
  await withContinueHost('duplicate', 'absent', async (host) => {
    await recoveryHost.resumeAccepted(host, continueSessionOf('duplicate'), continuePhysicalOf('duplicate'))
    const result = await recoveryHost.resumeAccepted(
      host,
      continueSessionOf('duplicate'),
      continuePhysicalOf('duplicate'),
    )

    assert.equal(result.manuals.length, 1)
    assert.equal(result.manuals[0].reason, 'NoAuthorizedProviderDisposition')
    assert.equal(result.manuals[0].observation, 'ProviderAbsent')
  })
})

test('WHAT[CRASH-018] CRASH_018_accepted_continue_emits_no_manual_block', async () => {
  await withContinueHost('accept', 'accept', async (host) => {
    const result = await recoveryHost.resumeAccepted(
      host,
      continueSessionOf('accept'),
      continuePhysicalOf('accept'),
    )

    assert.equal(result.calls, 1, 'acceptance must be awaited exactly once')
    assert.deepEqual(result.manuals, [])
  })
})
