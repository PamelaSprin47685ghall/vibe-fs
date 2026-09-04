// AGENT-027 — internal Semble search: kernel command, launch, parse, stdio fixture.
// Not Host MCP. Not Strength. The fixture is an opaque external MCP process.

import assert from 'node:assert/strict'
import test from 'node:test'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import * as managedAgentConfig from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import * as semble from '../../../dist/Repository/Investigation/SembleSurface.js'

const here = dirname(fileURLToPath(import.meta.url))
const fixturePath = join(here, '../../verification-system/tests/support/semble-mcp-fixture.js')
const ROLES = ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Inquiry', 'DevOps', 'Distiller', 'Blogger', 'Bookkeeper']
const agentName = (role) => `${role.toLowerCase()}`
const uvxFrom = (ref) => ['uvx', '--from', `semble[mcp] @ git+https://github.com/MinishLab/semble.git@${ref}`, 'semble']

const buildConfig = () => {
  const agent = {}
  for (const role of ROLES) agent[agentName(role)] = { model: `${agentName(role)}-model` }
  return { agent }
}

test.before(() => {
  managedAgentConfig.installDefaultResources()
})

test('WHAT[REPOSITORY-INVESTIGATION-006] AGENT_027_kernel_identity_and_commands', () => {
  assert.equal(semble.serverName, 'semble')
  assert.equal(semble.defaultRef, 'main')
  assert.equal(semble.repo, 'https://github.com/MinishLab/semble.git')
  assert.equal(semble.toolName, 'search')
  assert.equal(semble.maxSnippetLines, 20)
  assert.deepEqual(semble.uvxCommand(''), uvxFrom('main'))
  assert.deepEqual(semble.uvxCommand(' v1.2.3 '), uvxFrom('v1.2.3'))
  assert.deepEqual(semble.fixtureCommand('/tmp/fixture.js'), ['node', '/tmp/fixture.js'])
})

test('WHAT[REPOSITORY-INVESTIGATION-006] AGENT_027_launch_disabled_fixture_test_uvx', () => {
  assert.equal(semble.launchFromVars({ SEMBLE_MCP_DISABLED: '1' }).kind, 'Disabled')
  assert.equal(semble.launchFromVars({ SEMBLE_MCP_DISABLED: 'true', SEMBLE_MCP_FIXTURE: '/tmp/x.js' }).kind, 'Disabled')
  const fixture = semble.launchFromVars({ SEMBLE_MCP_FIXTURE: '/tmp/semble-fixture.js', WANXIANGSHU_TEST: 'true' })
  assert.equal(fixture.kind, 'Fixture')
  assert.equal(fixture.value, '/tmp/semble-fixture.js')
  assert.equal(semble.launchFromVars({ WANXIANGSHU_TEST: 'true' }).kind, 'Disabled')
  const uvx = semble.launchFromVars({ SEMBLE_MCP_REF: 'release-1' })
  assert.equal(uvx.kind, 'Uvx')
  assert.equal(uvx.value, 'release-1')
  const defaults = semble.launchFromVars({})
  assert.equal(defaults.kind, 'Uvx')
  assert.equal(defaults.value, semble.defaultRef)
})

test('WHAT[REPOSITORY-INVESTIGATION-002] AGENT_027_parse_text_and_tool_result', () => {
  const hits = semble.parseText(JSON.stringify({
    results: [
      { file_path: 'src/A.fs', start_line: 2, end_line: 8, content: 'let a = 1\nlet b = 2', score: 0.42, total_lines: 30 },
      { start_line: 1, content: 'orphan' },
      { file_path: 'src/B.fs', start_line: 4, end_line: 5, content: 'line', score: 0.1 },
    ],
  }))
  assert.equal(hits.length, 2)
  assert.equal(hits[0].filePath, 'src/A.fs')
  assert.equal(hits[0].startLine, 2)
  assert.equal(hits[0].endLine, 8)
  assert.equal(hits[0].content, 'let a = 1\nlet b = 2')
  assert.equal(hits[0].score, 0.42)
  assert.equal(hits[0].totalLines, 30)
  assert.equal(hits[1].filePath, 'src/B.fs')
  assert.equal(hits[1].totalLines, 5)
  assert.deepEqual(semble.parseText(''), [])
  assert.deepEqual(semble.parseText('{'), [])
  assert.deepEqual(semble.parseText(JSON.stringify({ results: [] })), [])
  assert.deepEqual(semble.parseToolResult(null), [])
  assert.deepEqual(semble.parseToolResult({}), [])
  const fromTool = semble.parseToolResult({
    content: [{ type: 'text', text: JSON.stringify({ results: [{ file_path: 'src/C.fs', content: 'x', score: 1 }] }) }],
  })
  assert.equal(fromTool.length, 1)
  assert.equal(fromTool[0].filePath, 'src/C.fs')
  assert.equal(fromTool[0].startLine, 1)
  assert.equal(fromTool[0].totalLines, 1)
})

test('WHAT[REPOSITORY-INVESTIGATION-006] AGENT_027_search_disabled_returns_empty_without_spawn', async () => {
  assert.deepEqual(await semble.search(semble.launchFromVars({ SEMBLE_MCP_DISABLED: '1' }), 'auth', '/repo', 5), [])
})

test('WHAT[REPOSITORY-INVESTIGATION-002] AGENT_027_search_fixture_stdio_roundtrip', async () => {
  const hits = await semble.search(
    semble.launchFromVars({ SEMBLE_MCP_FIXTURE: fixturePath, WANXIANGSHU_TEST: 'true' }),
    'auth handler',
    '/tmp/repo',
    3,
  )
  assert.equal(hits.length, 1)
  assert.equal(hits[0].filePath, 'src/Example.fs')
  assert.equal(hits[0].startLine, 10)
  assert.equal(hits[0].endLine, 20)
  assert.equal(hits[0].score, 0.91)
  assert.equal(hits[0].totalLines, 40)
  assert.equal(hits[0].content, 'query=auth handler;repo=/tmp/repo;top_k=3;max_snippet_lines=20')
})

test('WHAT[REPOSITORY-INVESTIGATION-001] AGENT_027_configure_does_not_inject_host_mcp_or_permission_keys', () => {
  const config = buildConfig()
  assert.equal(managedAgentConfig.configure(config).ok, true)
  assert.equal(config.mcp?.[semble.serverName], undefined)
  assert.equal(config.mcp?.['stealth-browser-mcp']?.type, 'local')
  for (const role of ROLES) {
    const permission = config.agent[agentName(role)].permission
    assert.equal(permission.semble, undefined, `${agentName(role)} semble`)
    assert.equal(permission['semble_*'], undefined, `${agentName(role)} semble_*`)
    assert.equal(permission['semble_search'], undefined, `${agentName(role)} semble_search`)
  }
})
