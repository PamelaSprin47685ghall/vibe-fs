import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[HOST-BOUNDARY-021] HOST_021_plugin_load_graph_has_no_semantic_recovery_or_workspace_mutation', () => {
  const boot = read('src/Wanxiangshu/OpenCode/Plugin/PluginBoot.fs')
  const signal = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const workspaceStore = read('src/Wanxiangshu/OpenCode/Host/WorkspaceEventStore.fs')
  const spike = read('src/Wanxiangshu/OpenCode/Plugin/SpikePlugin.fs')
  const recoveryWiring = read('src/Wanxiangshu/OpenCode/Plugin/PluginRecoveryWiring.fs')

  assert.doesNotMatch(boot, /restoreSessionParents/)
  assert.doesNotMatch(boot, /HookDispatcher\.ensure/)
  assert.doesNotMatch(boot, /PluginRecoveryWiring\.attach/)

  assert.doesNotMatch(signal, /\.Recover\(\)/)
  assert.doesNotMatch(signal, /FissionHost\.recoverGroups/)
  assert.doesNotMatch(workspaceStore, /JsToolsTransactionStore\.recoverCurrent/)

  // PluginRecoveryWiring.attach is registration, not recovery execution: load
  // installs one durability-activation callback and the callback owns the later
  // background lifecycle notifications. SpikePlugin must not invoke those
  // lifecycle events directly.
  assert.match(spike, /PluginRecoveryWiring\.attach boot/)
  assert.doesNotMatch(spike, /SignalChatRecovery|PluginRuntimeReloaded|CapacityProjectionReplayed/)
  assert.match(
    recoveryWiring,
    /scope\.AttachDurabilityActivation\(fun \(\) ->\s*scope\.RunBackground\(fun \(\) ->/,
  )
  assert.equal([...recoveryWiring.matchAll(/AttachDurabilityActivation/g)].length, 1)
  assert.doesNotMatch(recoveryWiring, /restoreLinkedChildren|recoverFamilyDirect|defaultRecoverPromptClaims|defaultRecoverBlogger/)
})

test('WHAT[HOST-BOUNDARY-021] HOST_021_broken_tool_recovery_APIs_do_not_exist', () => {
  const fission = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')
  const jsStore = read('src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs')

  assert.doesNotMatch(fission, /let\s+recoverGroups\b/)
  assert.doesNotMatch(jsStore, /let\s+recoverCurrent\b/)
})

test('WHAT[HOST-BOUNDARY-021] HOST_021_ordinary_join_does_not_reenlist_old_durable_tool_state', () => {
  const join = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/Join.fs')
  const joinTool = read('src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinTool.fs')

  assert.match(join, /currentProcessHandle/)
  assert.match(join, /drainFromJournalWhere/)
  assert.doesNotMatch(joinTool, /tryMembershipOfLane/)
})

test('WHAT[HOST-BOUNDARY-021] HOST_021_plugin_load_does_not_append_RuntimeStarted', () => {
  const boot = read('src/Wanxiangshu/OpenCode/Plugin/PluginBoot.fs')
  const journalWriter = read('src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs')

  assert.doesNotMatch(boot, /RuntimeStarted/)
  assert.doesNotMatch(journalWriter, /resumeOrCreate[\s\S]{0,1800}appendInitial[\s\S]{0,600}RuntimeStarted/)
})
