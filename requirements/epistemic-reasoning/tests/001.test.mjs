import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync, readdirSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { close, createStore, start, resume, state, assessWhy, relativeServerEntry } = await import("./support.mjs");

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '../../..')

test('WHAT[EPI-001] start_yields_semantic_assessment_request', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assert.equal(started.status, 'yield')
  assert.equal(started.request.type, 'SemanticAssessmentRequest')
})
}

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { createStore, start, resume, state, mcpServer } = await import("../../../dist/Sphinx/Surface.js");


test('WHAT[EPI-001] start_yield_returns_structured_content_with_next_tool', async () => {
  const server = mcpServer(createStore())
  const result = await server._registeredTools.start.handler({ question: '花青素合成是否解释红色？' })

  assert.equal(result.isError, undefined)
  assert.equal(result.content[0].type, 'text')
  assert.match(result.content[0].text, /Next tool: assess/)

  const structured = result.structuredContent
  assert.equal(structured.status, 'yield')
  assert.equal(typeof structured.handle, 'string')
  assert.ok(structured.handle.length > 0)
  assert.equal(structured.revision, 0)
  assert.equal(structured.nextTool, 'assess')
  assert.equal(structured.request.type, 'SemanticAssessmentRequest')
  assert.equal(structured.answer, null)
})
}
