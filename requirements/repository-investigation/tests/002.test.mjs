import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as managedAgentConfig from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import * as semble from '../../../dist/Repository/Investigation/SembleSurface.js'

// requirements/repository-investigation/tests/investigation-resource-laws.test.mjs
//
// Package-owned prose-law oracle: the repository-claim evidence contract must be
// stated in the provider-facing laws that an Inspector actually consumes.
// repository-investigation OWNS the acquisition contract (real observation,
// locatability, causal read-only, cheapest adequate observation, low-trust
// warm-start hints); this test pins those laws into the shipped resources so the
// contract cannot silently drift while the implementation stays green.

const here = dirname(fileURLToPath(import.meta.url))
const providerRoot = join(here, '../../../resources/provider')
const readLaw = (semanticPath, locale) => readFileSync(join(providerRoot, semanticPath, `${locale}.md`), 'utf8')

// Every assertion below must hold in BOTH locales: the provider-facing evidence
// contract is language-invariant (PROMPT-017 invariant face).
const LOCALES = ['en', 'zh-CN']

// AGENT-027 — internal Semble search: kernel command, launch, parse, stdio fixture.
// Not Host MCP. Not Strength. The fixture is an opaque external MCP process.

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

test('WHAT[REPOSITORY-INVESTIGATION-002] INVESTIGATE_inspector_role_law_makes_evidence_locatable_again', () => {
  for (const locale of LOCALES) {
    const law = readLaw('role/inspector', locale)
    assert.match(law, /locatable|再次被定位/, `${locale} locatability`)
    assert.match(law, /keep only the evidence that makes the fact locatable again|再只保留足以让该事实再次被定位的证据/, `${locale} locatability funnel step`)
  }
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
