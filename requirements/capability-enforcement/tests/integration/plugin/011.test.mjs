import assert from 'node:assert/strict'
import test from 'node:test'
import {
  markerSource,
  markerToolName,
} from '../../../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { permissions } from '../../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import {
  acceptAuthorityRoot,
  grantWorkOwned,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const TOOL_NAMES = [
  'fork', 'resume', 'commission', 'join', 'horizon', 'todowrite', 'fission',
  'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm',
  'bash-honeypot', 'assume',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'run', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'review', 'chronicle', 'fetch', 'suicide',
]

const PLUGIN_TOOL_NAMES = [
  'fork', 'resume', 'commission', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
  'join', 'horizon', 'fission', 'review', 'suicide', 'run',
  'mv', 'rm', 'bash-honeypot', 'assume', 'chronicle',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'js-engineer', 'js-devops',
]

const HOST_OWNED_TOOL_NAMES = [
  'todowrite', 'read', 'write', 'edit', 'glob', 'grep', 'skill',
]

const ROLE_NAMES = ['orchestrator', 'manager', 'engineer', 'devops', 'blogger']

const COGNITIVE_TOOLS = ['enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']

const ALLOWED = {
  orchestrator: ['commission', 'join', 'horizon', 'assume', ...COGNITIVE_TOOLS],
  manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'review', 'suicide', 'assume', ...COGNITIVE_TOOLS],
  engineer: ['fission', 'read', 'write', 'edit', 'glob', 'grep', 'fetch', 'mv', 'rm', 'bash-honeypot', 'assume', ...COGNITIVE_TOOLS],
  devops: [
    'join', 'horizon', 'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm', 'run',
    'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
    'assume', ...COGNITIVE_TOOLS,
  ],
  blogger: ['chronicle'],
}

const withSession = (messages, sessionID = 'ses-capability-manager') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

const fullConfig = () => ({
  agent: Object.fromEntries(
    ROLE_NAMES.map((role) => [role, {}]),
  ),
})

test('WHAT[ENF-011] MANAGER_config_projects_owned_permissions_with_default_deny', async () => {
  await withPlugin(async (hooks) => {
    const config = fullConfig()
    hooks.config(config)
    assert.equal(config.compaction.auto, false)
    for (const role of ROLE_NAMES) {
      const permission = config.agent[role].permission
      for (const toolName of TOOL_NAMES) {
        const expected = ALLOWED[role].includes(toolName) ? 'allow' : 'deny'
        const key = toolName
        assert.equal(permission[key], expected, `${role}.${key}`)
      }
      assert.equal(permission.external_directory, 'allow', `${role}.external_directory`)
      assert.equal(config.agent[role].model, undefined)
    }
  })
})
