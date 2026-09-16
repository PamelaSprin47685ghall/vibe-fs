import assert from 'node:assert/strict'
import test from 'node:test'
import { withPlugin } from '../../../../verification-system/tests/support/plugin-fixture.mjs'

const TOOL_NAMES = [
  'fork', 'resume', 'commission', 'join', 'horizon', 'todowrite', 'fission',
  'read', 'write', 'edit', 'glob', 'grep', 'mv', 'rm',
  'bash-honeypot', 'assume', 'inspect', 'establish-behavior', 'repair-behavior',
  'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret',
  'run', 'query-shell', 'stealth-browser-mcp', 'sphinx', 'review',
  'chronicle', 'fetch',
]

const ROLE_NAMES = ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'blogger', 'distiller']
const COGNITIVE_TOOLS = ['enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']
const ALLOWED = {
  orchestrator: ['commission', 'join', 'horizon', 'assume', ...COGNITIVE_TOOLS],
  manager: ['fork', 'resume', 'join', 'horizon', 'todowrite', 'fission', 'review', 'assume', ...COGNITIVE_TOOLS],
  coder: ['fission', 'read', 'write', 'edit', 'glob', 'grep', 'inspect', 'fetch', 'mv', 'rm', 'bash-honeypot', 'assume', ...COGNITIVE_TOOLS],
  inspector: ['fission', 'read', 'glob', 'grep', 'fetch', 'assume', ...COGNITIVE_TOOLS],
  devops: ['join', 'horizon', 'read', 'glob', 'grep', 'inspect', 'run', 'establish-behavior', 'repair-behavior', 'assume', ...COGNITIVE_TOOLS],
  browser: ['fission', 'read', 'glob', 'grep', 'stealth-browser-mcp', 'assume', ...COGNITIVE_TOOLS],
  inquiry: ['fission', 'inspect', 'sphinx', 'assume', ...COGNITIVE_TOOLS],
  blogger: ['chronicle'],
  distiller: [],
}

test('WHAT[ENF-011] MANAGER_config_projects_owned_permissions_with_default_deny', async () => {
  const fullConfigObj = () => ({
    agent: Object.fromEntries(
      ROLE_NAMES.map((role) => [role, {}]),
    ),
  })
  await withPlugin(async (hooks) => {
    const config = fullConfigObj()
    hooks.config(config)
    assert.equal(config.compaction.auto, false)
    for (const role of ROLE_NAMES) {
      const permission = config.agent[role].permission
      for (const toolName of TOOL_NAMES) {
        const expected = ALLOWED[role].includes(toolName) ? 'allow' : 'deny'
        const key = toolName === 'stealth-browser-mcp' ? 'stealth-browser-mcp_*' : toolName === 'sphinx' ? 'sphinx_*' : toolName
        assert.equal(permission[key], expected, `${role}.${key}`)
      }
      assert.equal(permission.external_directory, 'allow', `${role}.external_directory`)
      assert.equal(config.agent[role].model, undefined)
    }
  })
})
