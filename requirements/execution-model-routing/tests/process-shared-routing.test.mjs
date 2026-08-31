import assert from 'node:assert/strict'
import test from 'node:test'

import plugin from '../../../dist/OpenCode/Plugin/Plugin.js'
import { createEnvironment, managedConfig, routeMessage } from './support/process-shared-routing.mjs'

test('WHAT[EMR-003] EMR_003_two_plugin_instances_share_one_process_running_multiset', async () => {
  const environment = createEnvironment(plugin.server)
  const previousHome = process.env.HOME
  process.env.HOME = environment.home
  let first
  let second

  try {
    first = await environment.createPlugin('root-workspace')
    second = await environment.createPlugin('worktree-workspace')
    await first.config(managedConfig())
    await second.config(managedConfig())

    const a = await routeMessage(first, 'ses_shared_a')
    const b = await routeMessage(second, 'ses_shared_b')

    assert.deepEqual([a.providerID, a.modelID, a.variant], ['provider', 'model-a', 'none'])
    assert.deepEqual([b.providerID, b.modelID, b.variant], ['provider', 'model-b', 'none'])
  } finally {
    if (second) await second.dispose()
    if (first) await first.dispose()
    process.env.HOME = previousHome
    environment.dispose()
  }
})
