// tests/unit/agent/semble-mcp.test.mjs — AGENT-027
// Internal Semble search: kernel command, launch, parse, stdio fixture. Not Host mcp. Not Strength.

import assert from 'node:assert/strict'
import test from 'node:test'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { caseOf, listItems, managedAgentConfig, payloadOf, runtimeResources } from '../../verification-system/tests/support/domain.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const dist = join(here, '../../../dist')
const fixturePath = join(here, '../../verification-system/tests/support/semble-mcp-fixture.js')
const kernel = await import(join(dist, 'Repository/Investigation/Semble/Mcp.js'))
const { serverName, defaultRef, repo, toolName, maxSnippetLines, uvxCommand, fixtureCommand } = kernel
const { parseText, parseToolResult } = await import(join(dist, 'Repository/Investigation/Semble/SearchCodec.js'))
const { launchFromVars, search } = await import(join(dist, 'Repository/Investigation/Semble/Client.js'))
const ROLES = ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Inquiry', 'Reviewer', 'DevOps', 'Distiller', 'Blogger', 'Bookkeeper']
const TIERS = ['fast', 'deep']
const agentName = (tier, role) => `${tier}-${role.toLowerCase()}`
const uvxFrom = (ref) => ['uvx', '--from', `semble[mcp] @ git+https://github.com/MinishLab/semble.git@${ref}`, 'semble']

const buildConfig = () => {
  const agent = {}
  for (const tier of TIERS) {
    for (const role of ROLES) agent[agentName(tier, role)] = { model: `${tier}-${role.toLowerCase()}-model` }
  }
  return { agent }
}

test.before(() => {
  runtimeResources.installFromPackage()
})

test('AGENT_027_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'semble')
  assert.equal(defaultRef, 'main')
  assert.equal(repo, 'https://github.com/MinishLab/semble.git')
  assert.equal(toolName, 'search')
  assert.equal(maxSnippetLines, 20)
  assert.deepEqual(uvxCommand(''), uvxFrom('main'))
  assert.deepEqual(uvxCommand(' v1.2.3 '), uvxFrom('v1.2.3'))
  assert.deepEqual(fixtureCommand('/tmp/fixture.js'), ['node', '/tmp/fixture.js'])
})

test('AGENT_027_launch_disabled_fixture_test_uvx', () => {
  assert.equal(caseOf(launchFromVars({ SEMBLE_MCP_DISABLED: '1' })), 'Disabled')
  assert.equal(caseOf(launchFromVars({ SEMBLE_MCP_DISABLED: 'true', SEMBLE_MCP_FIXTURE: '/tmp/x.js' })), 'Disabled')
  const fixture = launchFromVars({ SEMBLE_MCP_FIXTURE: '/tmp/semble-fixture.js', WANXIANGSHU_TEST: 'true' })
  assert.equal(caseOf(fixture), 'Fixture')
  assert.equal(payloadOf(fixture), '/tmp/semble-fixture.js')
  assert.equal(caseOf(launchFromVars({ WANXIANGSHU_TEST: 'true' })), 'Disabled')
  const uvx = launchFromVars({ SEMBLE_MCP_REF: 'release-1' })
  assert.equal(caseOf(uvx), 'Uvx')
  assert.equal(payloadOf(uvx), 'release-1')
  const defaults = launchFromVars({})
  assert.equal(caseOf(defaults), 'Uvx')
  assert.equal(payloadOf(defaults), defaultRef)
})

test('AGENT_027_parse_text_and_tool_result', () => {
  const hits = listItems(parseText(JSON.stringify({
    results: [
      { file_path: 'src/A.fs', start_line: 2, end_line: 8, content: 'let a = 1\nlet b = 2', score: 0.42, total_lines: 30 },
      { start_line: 1, content: 'orphan' },
      { file_path: 'src/B.fs', start_line: 4, end_line: 5, content: 'line', score: 0.1 },
    ],
  })))
  assert.equal(hits.length, 2)
  assert.equal(hits[0].FilePath, 'src/A.fs')
  assert.equal(hits[0].StartLine, 2)
  assert.equal(hits[0].EndLine, 8)
  assert.equal(hits[0].Content, 'let a = 1\nlet b = 2')
  assert.equal(hits[0].Score, 0.42)
  assert.equal(hits[0].TotalLines, 30)
  assert.equal(hits[1].FilePath, 'src/B.fs')
  assert.equal(hits[1].TotalLines, 5)
  assert.deepEqual(listItems(parseText('')), [])
  assert.deepEqual(listItems(parseText('{')), [])
  assert.deepEqual(listItems(parseText(JSON.stringify({ results: [] }))), [])
  assert.deepEqual(listItems(parseToolResult(null)), [])
  assert.deepEqual(listItems(parseToolResult({})), [])
  const fromTool = listItems(parseToolResult({
    content: [{ type: 'text', text: JSON.stringify({ results: [{ file_path: 'src/C.fs', content: 'x', score: 1 }] }) }],
  }))
  assert.equal(fromTool.length, 1)
  assert.equal(fromTool[0].FilePath, 'src/C.fs')
  assert.equal(fromTool[0].StartLine, 1)
  assert.equal(fromTool[0].TotalLines, 1)
})

test('AGENT_027_search_disabled_returns_empty_without_spawn', async () => {
  assert.deepEqual(listItems(await search(launchFromVars({ SEMBLE_MCP_DISABLED: '1' }), 'auth', '/repo', 5)), [])
})

test('AGENT_027_search_fixture_stdio_roundtrip', async () => {
  const hits = listItems(await search(
    launchFromVars({ SEMBLE_MCP_FIXTURE: fixturePath, WANXIANGSHU_TEST: 'true' }),
    'auth handler',
    '/tmp/repo',
    3,
  ))
  assert.equal(hits.length, 1)
  assert.equal(hits[0].FilePath, 'src/Example.fs')
  assert.equal(hits[0].StartLine, 10)
  assert.equal(hits[0].EndLine, 20)
  assert.equal(hits[0].Score, 0.91)
  assert.equal(hits[0].TotalLines, 40)
  assert.equal(hits[0].Content, 'query=auth handler;repo=/tmp/repo;top_k=3;max_snippet_lines=20')
})

test('AGENT_027_configure_does_not_inject_host_mcp_or_permission_keys', () => {
  const config = buildConfig()
  assert.equal(managedAgentConfig.configure(config).ok, true)
  assert.equal(config.mcp?.[serverName], undefined)
  assert.equal(config.mcp?.['stealth-browser-mcp']?.type, 'local')
  for (const tier of TIERS) {
    for (const role of ROLES) {
      const permission = config.agent[agentName(tier, role)].permission
      assert.equal(permission.semble, undefined, `${agentName(tier, role)} semble`)
      assert.equal(permission['semble_*'], undefined, `${agentName(tier, role)} semble_*`)
      assert.equal(permission['semble_search'], undefined, `${agentName(tier, role)} semble_search`)
    }
  }
})
