import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const pair = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");

const inject = async (session, raw, markerText = pair.text) => {
  const result = await pair.tryInject(session, markerText, raw)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}
const userMsg = (id, body = 'hello') => ({ info: { id, role: 'user' }, parts: [{ type: 'text', text: body }] })
const assistantText = (id, body = 'ack') => ({ info: { id, role: 'assistant' }, parts: [{ type: 'text', text: body }] })
const pairMessages = (messages) => messages.filter((message) => pair.isPairProgrammingThought(message))

test('WHAT[PROVIDER-PROJECTION-010] C_PH_cursor_keeps_durable_occurrence_without_synthetic_message', async () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    assert.equal(pair.skipAutoInjectedRequested(null), false)
    assert.equal(pair.skipAutoInjectedRequested('cursor'), false)
    assert.equal(pair.skipAutoInjectedRequested('anthropic'), false)
    const session = 'ses_cursor_no_synthetic'
    const raw = [{ info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } }, parts: [{ type: 'text', text: 'steer' }] }]
    const cursor = await inject(session, raw)
    assert.equal(pairMessages(cursor).length, 0)
    assert.equal(cursor.length, 1)
    assert.equal(cursor[0].parts[0].text, `steer\0\uFEFF<skill_content>\n${pair.text.trim()}\n</skill_content>`)
    assert.doesNotMatch(cursor[0].parts[0].text, /name=/)
    const ordinary = await inject('ses_ordinary', [userMsg('u0'), assistantText('a0'), userMsg('u1', 'steer')])
    assert.equal(pairMessages(ordinary).length, 0)
    assert.deepEqual(ordinary, [userMsg('u0'), assistantText('a0'), userMsg('u1', 'steer')])
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})
test('WHAT[PROVIDER-PROJECTION-010] C_PH_cursor_appends_NUL_BOM_guidance_inside_real_completed_tool_result', async () => {
  const raw = [
    { info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'default' } }, parts: [{ type: 'text', text: 'read it' }] },
    { info: { id: 'c1', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'call_read', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
    { info: { id: 'r1', role: 'assistant', providerID: 'cursor', modelID: 'default' }, parts: [{ type: 'tool', tool: 'read', callID: 'call_read', state: { status: 'completed', input: { filePath: 'AGENTS.md' }, output: 'success' } }] },
  ]
  const out = await inject('ses_cursor_completed_tool', raw)
  assert.equal(pairMessages(out).length, 0)
  assert.equal(out.length, raw.length)
  assert.equal(out.at(-1).info.id, 'r1')
  assert.equal(out.at(-1).parts[0].state.status, 'completed')
  assert.equal(out.at(-1).parts[0].state.output, `success\0\uFEFF${pair.text}`)
  assert.equal(raw.at(-1).parts[0].state.output, 'success')
  assert.deepEqual(await inject('ses_cursor_completed_tool', raw), out)
})
test('WHAT[PROVIDER-PROJECTION-010] C_PH_cursor_appends_NUL_BOM_guidance_inside_real_error_tool_result', async () => {
  const raw = [
    { info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'default' } }, parts: [{ type: 'text', text: 'read it' }] },
    { info: { id: 'c1', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'call_read', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
    { info: { id: 'r1', role: 'assistant', providerID: 'cursor', modelID: 'default' }, parts: [{ type: 'tool', tool: 'read', callID: 'call_read', state: { status: 'error', input: { filePath: 'missing' }, error: 'not found' } }] },
  ]
  const out = await inject('ses_cursor_error_tool', raw)
  assert.equal(pairMessages(out).length, 0)
  assert.equal(out.at(-1).parts[0].state.status, 'error')
  assert.equal(out.at(-1).parts[0].state.error, `not found\0\uFEFF${pair.text}`)
  assert.equal(raw.at(-1).parts[0].state.error, 'not found')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const { assertJsData } = await import("../../verification-system/tests/support/js-contract.mjs");

const toml = await import('../../../dist/Foundation/SyntheticTomlSurface.js')
const valueOf = (rendered) => parseToml(`x = ${rendered}`).x

test('WHAT[PROVIDER-PROJECTION-010] P6_TOML_SURFACE_renderer_returns_data_only_values', () => {
  assert.equal(typeof toml.renderString, 'function')
  assert.equal(typeof toml.renderDocument, 'function')
  assert.equal(toml.renderDocument([], [toml.field('status', toml.renderString('ok'))]), 'status = "ok"\n')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");

const toml = await import('../../../dist/Foundation/SyntheticTomlSurface.js')
const valueOf = (rendered) => parseToml(`x = ${rendered}`).x
const syntaxLines = (document) => {
  const lines = []
  let inLiteral = false

  for (const line of document.split('\n')) {
    if (inLiteral) {
      if (line === "'''") inLiteral = false
      continue
    }
    if (/=\s*'''$/.test(line)) {
      inLiteral = true
      continue
    }
    lines.push(line)
  }

  return lines
}

test('WHAT[PROVIDER-PROJECTION-010] ARCH_011_renderer_stays_data_only', () => {
  assert.equal(typeof toml.renderString, 'function')
  assert.equal(typeof toml.renderDocument, 'function')
  assert.equal(toml.renderDocument([], [toml.field('status', toml.renderString('ok'))]), 'status = "ok"\n')
})
}
