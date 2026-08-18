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
  if (role === 'fast-coder') {
    const occupied = running.filter((item) => item.model === 'provider/model-a' && item.reasoning === 'none').length
    return occupied === 0
      ? { model: 'provider/model-a', reasoning: 'none' }
      : { model: 'provider/model-b', reasoning: 'none' }
  }
  if (role === 'deep-coder') return { model: 'provider/holder', reasoning: 'none' }
  if (role === 'fast-manager') {
    return running.some((item) => item.model === 'provider/holder')
      ? null
      : { model: 'provider/waiter', reasoning: 'none' }
  }
  if (role === 'fast-inspector') return { model: 'provider/fresh', reasoning: 'none' }
  throw new Error('unexpected role: ' + role)
}\n`,
  'utf8',
)

const { default: plugin } = await import('../../../dist/OpenCode/Plugin/Plugin.js')

const { server: initPlugin } = plugin

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
  return initPlugin({
    directory,
    client: {},
    events: { listen: () => () => {} },
  })
}

const messageOutput = (sessionID, agent, messageID = `msg_${sessionID}`) => ({
  message: {
    id: messageID,
    role: 'user',
    sessionID,
    agent,
    model: { providerID: 'host', modelID: 'placeholder' },
  },
  parts: [],
})

const routeMessage = async (hooks, sessionID, agent = 'fast-coder', messageID = `msg_${sessionID}`) => {
  const output = messageOutput(sessionID, agent, messageID)

  await hooks['chat.message']({ sessionID, agent }, output)
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

test('WHAT[EMR-004] EMR_004_superseded_pending_chat_message_resolves_without_fatal', async () => {
  const previousHome = process.env.HOME
  const previousFatalExit = process.env.WANXIANGSHU_NO_FATAL_EXIT
  const originalConsoleError = console.error
  const fatalLines = []
  process.env.HOME = home
  process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
  console.error = (...args) => fatalLines.push(args.join(' '))
  let hooks

  try {
    hooks = await createPlugin('supersession-workspace')
    await hooks.config(managedConfig())

    const holder = await routeMessage(hooks, 'holder-session', 'deep-coder', 'msg-holder')
    assert.deepEqual([holder.providerID, holder.modelID], ['provider', 'holder'])

    const oldOutput = messageOutput('reused-session', 'fast-manager', 'msg-old')
    const oldHook = hooks['chat.message']({ sessionID: 'reused-session', agent: 'fast-manager' }, oldOutput)
    await Promise.resolve()

    const fresh = await routeMessage(hooks, 'reused-session', 'fast-inspector', 'msg-new')
    assert.deepEqual([fresh.providerID, fresh.modelID], ['provider', 'fresh'])
    await assert.doesNotReject(oldHook)
    assert.deepEqual(oldOutput.message.model, { providerID: 'host', modelID: 'placeholder' })
    assert.equal(
      fatalLines.some((line) => line.includes('plugin-hook-chat-message-failed')),
      false,
      'expected pending supersession must not cross the plugin fatal membrane',
    )
  } finally {
    if (hooks) await hooks.dispose()
    console.error = originalConsoleError
    process.env.HOME = previousHome
    if (previousFatalExit === undefined) delete process.env.WANXIANGSHU_NO_FATAL_EXIT
    else process.env.WANXIANGSHU_NO_FATAL_EXIT = previousFatalExit
  }
})

test('WHAT[EMR-009] EMR_009_chat_message_routes_when_session_id_is_carried_on_output_message', async () => {
  const previousHome = process.env.HOME
  process.env.HOME = home
  let hooks

  try {
    hooks = await createPlugin('output-session-workspace')
    await hooks.config(managedConfig())

    const output = {
      message: {
        id: 'msg-output-only',
        role: 'user',
        sessionID: 'ses-output-only',
        agent: 'fast-coder',
        model: { providerID: 'host', modelID: 'placeholder' },
      },
      parts: [],
    }

    await hooks['chat.message']({ messageID: 'msg-output-only' }, output)
    assert.deepEqual(
      [output.message.model.providerID, output.message.model.modelID, output.message.model.variant],
      ['provider', 'model-a', 'none'],
      'chat.message must decode sessionID from output.message and route successfully',
    )
  } finally {
    if (hooks) await hooks.dispose()
    process.env.HOME = previousHome
  }
})
