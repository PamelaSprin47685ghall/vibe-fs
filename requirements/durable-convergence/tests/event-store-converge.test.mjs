// FROZEN — 2026-08-14. Historical filename retained; sync now runs in independent Git hooks.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('reference_transaction_and_pre_push_both_call_the_same_full_bidirectional_converge', async () => {
  const sync = await read('src/Wanxiangshu/Infrastructure/Git/HookSync.fs')
  assert.match(sync, /let runPrePush/)
  assert.match(sync, /converge remote None/)
  assert.match(sync, /let runReferenceTransaction/)
  assert.match(sync, /converge remote observed/)
  assert.doesNotMatch(sync, /ConvergeObserved|downloadOnly|uploadOnly/i)
})

test('reference_transaction_observed_root_changes_discovery_only_not_sync_direction', async () => {
  const gateway = await read('src/Wanxiangshu/Infrastructure/Git/GitGateway.fs')
  assert.match(gateway, /let converge/)
  assert.match(gateway, /match observedRemote with/)
  assert.match(gateway, /WriterStreamSync\.syncWriterStreams/)
  assert.match(gateway, /pushSnapshot/)
  assert.match(gateway, /discoverRemote/)
  assert.doesNotMatch(gateway, /IEventStore|CanonicalIntegrator|WorkspaceEventStore/)
})

test('lease_race_refetches_and_repeats_the_same_k_way_sync_boundedly', async () => {
  const gateway = await read('src/Wanxiangshu/Infrastructure/Git/GitGateway.fs')
  assert.match(gateway, /--force-with-lease/)
  assert.match(gateway, /retriesLeft/)
  assert.match(gateway, /discoverRemote run remote/)
  assert.match(gateway, /ConvergeRetryExhausted/)
})

test('product_process_has_no_fetch_pull_push_remote_api', async () => {
  const gateway = await read('src/Wanxiangshu/Infrastructure/Git/GitGateway.fs')
  const boot = await read('src/Wanxiangshu/Infrastructure/OpenCode/Plugin/PluginBoot.fs')
  assert.doesNotMatch(gateway, /type IGitGateway|member _\.(Fetch|Pull|Push)\(/)
  assert.match(boot, /HookDispatcher\.ensure/)
  assert.doesNotMatch(boot, /GitGateway\.converge|fetch|pull|push/i)
})

test('hook_internal_Git_commands_are_recursion_guarded_and_pre_push_is_not_reentered', async () => {
  const runner = await read('resources/git/wanxiang-hook.mjs')
  const gateway = await read('src/Wanxiangshu/Infrastructure/Git/GitGateway.fs')
  assert.match(runner, /WANXIANG_GIT_SYNC_ACTIVE/)
  assert.match(gateway, /--no-verify/)
  assert.match(gateway, /WANXIANG_GIT_SYNC_ACTIVE|SyncActiveEnv/)
})
