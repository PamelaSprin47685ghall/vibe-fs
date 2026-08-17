// HOST-013 pair-programming projection through its owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const inject = async (session, raw, markerText = pair.text) => {
  const result = await pair.tryInject(session, markerText, raw)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}

const userMsg = (id, body = 'hello') => ({ info: { id, role: 'user' }, parts: [{ type: 'text', text: body }] })
const pairMessages = (messages) => messages.filter((message) => pair.isPairProgrammingThought(message))
const skillContent = (markerText) => `<skill_content name="">\n${markerText.trim()}\n</skill_content>`
const assertPairShape = (msg, callId, markerText) => {
  assert.equal(msg.info.role, 'assistant')
  assert.equal(msg.info.source, pair.source)
  assert.equal(msg.info.synthetic, true)
  assert.equal(msg.parts.length, 1)
  assert.equal(msg.parts[0].type, 'tool')
  assert.equal(msg.parts[0].tool, 'skill')
  assert.equal(msg.parts[0].callID, callId)
  assert.equal(msg.parts[0].state.status, 'completed')
  assert.notEqual(msg.parts[0].state.status, 'pending')
  assert.notEqual(msg.parts[0].state.status, 'running')
  assert.deepEqual(msg.parts[0].state.input, { name: '' })
  assert.equal(msg.parts[0].state.output, skillContent(markerText))
}

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
    assert.deepEqual(cursor, raw)
    const ordinary = await inject(session, [userMsg('u1', 'steer')])
    assert.equal(pairMessages(ordinary).length, 1)
    assertPairShape(ordinary[0], pair.stableCallId(session, 1n), pair.text)
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
  assert.equal(out.at(-1).parts[0].state.output, `success\0\uFEFF${skillContent(pair.text)}`)
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
  assert.equal(out.at(-1).parts[0].state.error, `not found\0\uFEFF${skillContent(pair.text)}`)
  assert.equal(raw.at(-1).parts[0].state.error, 'not found')
})
