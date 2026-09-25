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

const cutResult = () => projection.projectMessages(cutMessages)

const ids = (result) => result.provider.map((message) => message.id ?? message.info?.id)

test('WHAT[relay-context-projection-004] retired finish and internal wake remain in the provider history', () => {
  const initial = [{ id: 'root', run: '', role: 'user', text: 'root user request' }]
  assert.deepEqual(ids(projection.projectMessages(initial)), ['root'])

  const retired = [
    { id: 'root', run: '', role: 'user', text: 'root user request' },
    { id: 'old-audit', run: 'old-run', role: 'assistant', text: 'old iteration audit' },
    {
      id: 'suicide',
      run: 'old-run',
      role: 'assistant',
      parts: [{ callID: 'suicide-call', tool: 'suicide' }],
      text: 'suicide call',
    },
    { id: 'wake', run: '', role: 'user', text: 'internal loop wake' },
  ]
  assert.deepEqual(ids(projection.projectMessages(retired)), ['root', 'old-audit', 'suicide', 'wake'])
})
