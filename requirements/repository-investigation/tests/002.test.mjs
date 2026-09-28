import assert from 'node:assert/strict'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as semble from '../../../dist/Repository/Investigation/SembleSurface.js'

const fixturePath = fileURLToPath(new URL('../../verification-system/tests/support/semble-mcp-fixture.js', import.meta.url))

test('WHAT[repository-investigation-002] search codec preserves candidate location and content but does not certify the file exists', () => {
  const hits = semble.parseText(JSON.stringify({ results: [
    { file_path: 'src/A.fs', start_line: 2, end_line: 8, content: 'let a = 1\nlet b = 2', score: 0.42, total_lines: 30 },
    { start_line: 1, content: 'orphan' },
    { file_path: 'src/B.fs', start_line: 4, end_line: 5, content: 'line', score: 0.1 },
  ] }))
  assert.deepEqual(hits, [
    { filePath: 'src/A.fs', startLine: 2, endLine: 8, content: 'let a = 1\nlet b = 2', score: 0.42, totalLines: 30 },
    { filePath: 'src/B.fs', startLine: 4, endLine: 5, content: 'line', score: 0.1, totalLines: 5 },
  ])
  for (const value of ['', '{', '{"results":[]}']) assert.deepEqual(semble.parseText(value), [])
  for (const value of [null, {}]) assert.deepEqual(semble.parseToolResult(value), [])
  const fromTool = semble.parseToolResult({ content: [{ type: 'text', text: JSON.stringify({ results: [{ file_path: 'src/C.fs', content: 'x', score: 1 }] }) }] })
  assert.deepEqual(fromTool, [{ filePath: 'src/C.fs', content: 'x', score: 1, startLine: 1, endLine: 1, totalLines: 1 }])
})

test('WHAT[repository-investigation-002] actual fixture transport preserves requested query, workspace and returned candidate coordinates', async () => {
  const hits = await semble.search(semble.launchFromVars({ SEMBLE_MCP_FIXTURE: fixturePath, WANXIANGSHU_TEST: 'true' }), 'auth handler', '/tmp/repo', 3)
  assert.deepEqual(hits, [{
    filePath: 'src/Example.fs', startLine: 10, endLine: 20, score: 0.91, totalLines: 40,
    content: 'query=auth handler;repo=/tmp/repo;top_k=3;max_snippet_lines=20',
  }])
})

test.todo('WHAT[repository-investigation-002] actual observations retain sufficient provenance for reinspection; a search candidate codec does not prove current repository evidence (GAP-083)')
