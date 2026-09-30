import assert from 'node:assert/strict'
import test from 'node:test'
import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'

const textMessage = (id, role, text) => ({
  info: { id, role },
  parts: [{ type: 'text', text }],
})

test('WHAT[context-compression-020] todowrite has no permanent raw-history exemption after the committed cutoff crosses it', () => {
  const raw = [
    {
      info: { id: 'todo-call', role: 'assistant' },
      parts: [{ type: 'tool-call', tool: 'todowrite', callID: 'todo-1', args: { obligations: [], retainCheckpoints: 1 } }],
    },
    {
      info: { id: 'todo-result', role: 'tool' },
      parts: [{ type: 'tool-result', callID: 'todo-1', result: 'Todos updated' }],
    },
    textMessage('covered-work', 'assistant', 'old work'),
    textMessage('live-user', 'user', 'continue'),
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['todo-call', 'todo-result', 'covered-work'],
    null,
    'lwr-prefix',
    'compressed history',
  )

  assert.deepEqual(projected.map((item) => item.info.id), ['lwr-prefix', 'live-user'])
})
