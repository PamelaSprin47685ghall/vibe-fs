import assert from 'node:assert/strict'
import test from 'node:test'
import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'

test('WHAT[context-compression-030] every covered assume call and matching result survives LWR replacement unchanged', () => {
  const raw = [
    {
      info: { id: 'assume-call', role: 'assistant' },
      parts: [{ type: 'tool-call', tool: 'assume', callID: 'assume-1', args: { assumption: 'Use the monotone cutoff.' } }],
    },
    {
      info: { id: 'assume-result', role: 'tool' },
      parts: [{ type: 'tool-result', callID: 'assume-1', result: 'Committed.' }],
    },
    { info: { id: 'ordinary-covered', role: 'assistant' }, parts: [{ type: 'text', text: 'old prose' }] },
    { info: { id: 'live-user', role: 'user' }, parts: [{ type: 'text', text: 'continue' }] },
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['assume-call', 'assume-result', 'ordinary-covered'],
    null,
    'lwr-prefix',
    'compressed history',
  )

  assert.deepEqual(projected.map((message) => message.info.id), ['lwr-prefix', 'assume-call', 'assume-result', 'live-user'])
  assert.equal(projected[1], raw[0], 'assume call must remain the exact original Host object')
  assert.equal(projected[2], raw[1], 'assume result must remain the exact original Host object')
})

test('WHAT[context-compression-030] modern single-message assume tool parts survive while unrelated covered tools do not', () => {
  const raw = [
    {
      info: { id: 'assume-modern', role: 'assistant' },
      parts: [{
        type: 'tool',
        tool: 'assume',
        callID: 'assume-2',
        state: { status: 'completed', input: { assumption: 'Keep this exact call.' }, output: 'Committed.' },
      }],
    },
    {
      info: { id: 'read-modern', role: 'assistant' },
      parts: [{
        type: 'tool',
        tool: 'read',
        callID: 'read-1',
        state: { status: 'completed', input: { path: 'x' }, output: 'old' },
      }],
    },
    { info: { id: 'tail', role: 'user' }, parts: [{ type: 'text', text: 'tail' }] },
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['assume-modern', 'read-modern'],
    null,
    'lwr-prefix-2',
    'summary',
  )

  assert.deepEqual(projected.map((message) => message.info.id), ['lwr-prefix-2', 'assume-modern', 'tail'])
  assert.equal(projected[1], raw[0])
})
