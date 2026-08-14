// Split from tests/unit/agent/sphinx-mcp.test.mjs (cutover Wave 2a); owner: epistemic-reasoning
//
// Sphinx MCP kernel identity/commands 事实：serverName / `sphinx_*` permissionKey /
// `dist/Sphinx/McpServer.js` 入口 / isTool 判定 / local + fixture 命令形态。
// （launch/env/apply → host-boundary；Inquiry-only wildcard → capability-enforcement。）

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  serverName,
  permissionKey,
  relativeServerEntry,
  isTool,
  localCommand,
  fixtureCommand,
} from '../../../dist/Kernel/SphinxMcp.js'

test('AGENT_030_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'sphinx')
  assert.equal(permissionKey, 'sphinx_*')
  assert.equal(relativeServerEntry, 'dist/Sphinx/McpServer.js')
  assert.equal(isTool('sphinx_start'), true)
  assert.equal(isTool('sphinx_resume'), true)
  assert.equal(isTool('stealth-browser-mcp_get_debug_view'), false)
  assert.equal(isTool('inspect'), false)
  assert.deepEqual(localCommand('/tmp/entry.js'), ['node', '/tmp/entry.js'])
  assert.deepEqual(fixtureCommand('/tmp/sphinx-fixture.js'), ['node', '/tmp/sphinx-fixture.js'])
})
