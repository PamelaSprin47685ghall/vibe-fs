import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'
import { mkdtempSync as recoveryMkdtemp, rmSync as recoveryRm } from 'node:fs'
import { tmpdir as recoveryTmpdir } from 'node:os'
import { join as recoveryJoin } from 'node:path'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname

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
