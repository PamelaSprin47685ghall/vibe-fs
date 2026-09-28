import assert from 'node:assert/strict'
import test from 'node:test'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const raw = () => [
  { info: { id: 'user', role: 'user' }, parts: [{ type: 'text', text: 'Original user instruction' }] },
  { info: { id: 'call', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'read-1', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
  { info: { id: 'result', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'read-1', state: { status: 'completed', input: {}, output: 'original result\r\n', time: { start: 0, end: 1 } } }] },
]

test('WHAT[guidance-delivery-009] actual injection changes only the terminal tool output suffix', async () => {
  const input = raw()
  const before = structuredClone(input)
  const marker = '# guidance\n# quoted user text is still a reminder'
  const result = await pair.tryInject('guidance-suffix', marker, input)
  assert.equal(result.ok, true, result.error)
  const expected = structuredClone(before)
  expected[2].parts[0].state.output += `\0\uFEFF${marker}`
  assert.deepEqual(result.value, expected)
  assert.deepEqual(input, before)
})

test.todo('WHAT[guidance-delivery-009] GAP-116 actual durable delivery leaves authority roots and execution permissions unchanged')
