import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'

const cutMessages = [
  { id: 'u1', run: '', role: 'user', text: 'root user request' },
  { id: 'a1', run: 'old-run', role: 'assistant', text: 'old iteration audit' },
  {
    id: 't1',
    run: 'old-run',
    role: 'assistant',
    parts: [{ callID: 'suicide-call', tool: 'suicide' }],
    text: 'suicide call',
  },
  { id: 'r1', run: 'old-run', role: 'tool', text: 'suicide result' },
  { id: 'a-late', run: 'old-run', role: 'assistant', text: 'late old part' },
  { id: 'wake-1', run: '', role: 'user', text: 'internal loop wake' },
  { id: 'a2', run: 'new-run', role: 'assistant', text: 'next iteration audit' },
]

const cutResult = () => projection.applyCut(cutMessages, 'old-run', 'suicide-call', ['old-run'], ['u1'])

const ids = (result) => result.provider.map((message) => message.id ?? message.info?.id)

test('WHAT[PROJ-003] next iteration context contains exact authority and existing current messages', () => {
  const result = cutResult()
  assert.deepEqual(
    result.provider.filter((message) => ['u1'].includes(message.id)).map((message) => message.id),
    ['u1'],
  )
  for (const message of result.provider) {
    assert.ok(cutMessages.some((origin) => origin.id === message.id), 'provider must not inject synthetic messages')
  }
  assert.deepEqual(ids(result).sort(), [...new Set(ids(result))].sort())
})
