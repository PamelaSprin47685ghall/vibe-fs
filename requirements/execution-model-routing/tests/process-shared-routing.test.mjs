import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const root = mkdtempSync(join(tmpdir(), 'wxs-routing-shared-'))
const home = join(root, 'home')
const routingDir = join(home, '.config', 'opencode')
mkdirSync(routingDir, { recursive: true })
writeFileSync(
  join(routingDir, 'wanxiangshu.mjs'),
  `export default function route(role, running) {
  if (role !== 'fast-coder') throw new Error('unexpected role: ' + role)
  const occupied = running.filter((item) => item.model === 'provider/model-a' && item.reasoning === 'none').length
  return occupied === 0
    ? { model: 'provider/model-a', reasoning: 'none' }
    : { model: 'provider/model-b', reasoning: 'none' }
}\n`,
  'utf8',
)

const { initSpikePlugin } = await import('../../../dist/OpenCode/Plugin/SpikePlugin.js')

const managedConfig = () => {
  const agent = {}
  for (const role of ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller', 'bookkeeper']) {
    agent[`fast-${role}`] = {}
    agent[`deep-${role}`] = {}
  }
  return { agent }
}

const createPlugin = async (name) => {
  const directory = join(root, name)
  mkdirSync(directory, { recursive: true })
  execFileSync('git', ['init', '--quiet', directory])
  return initSpikePlugin({
    directory,
    client: {},
    events: { listen: () => () => {} },
  })
}

const routeMessage = async (hooks, sessionID) => {
  const output = {
    message: {
      id: `msg_${sessionID}`,
      role: 'user',
      sessionID,
      agent: 'fast-coder',
      model: { providerID: 'host', modelID: 'placeholder' },
    },
    parts: [],
  }

  await hooks['chat.message']({ sessionID, agent: 'fast-coder' }, output)
  return output.message.model
}

test('WHAT[EMR-003] EMR_003_two_plugin_instances_share_one_process_running_multiset', async () => {
  const previousHome = process.env.HOME
  process.env.HOME = home
  let first
  let second

  try {
    first = await createPlugin('root-workspace')
    second = await createPlugin('worktree-workspace')
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
    rmSync(root, { recursive: true, force: true })
  }
})
