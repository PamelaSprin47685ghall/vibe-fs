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

test('WHAT[relay-context-projection-007] Accepted retirement reopened after invalidation retains the full physical history', () => {
  const messages = [
    { id: 'root', run: '', role: 'user', text: 'root request' },
    { id: 'old-audit', run: 'old-run', role: 'assistant', text: 'perfect assessment narrative' },
    {
      id: 'suicide',
      run: 'old-run',
      role: 'assistant',
      parts: [{ callID: 'suicide-accepted', tool: 'suicide' }],
      text: 'suicide call',
    },
    { id: 'late-old', run: 'old-run', role: 'assistant', text: 'late old part' },
    { id: 'wake', run: '', role: 'user', text: 'internal loop wake' },
    { id: 'current', run: 'new-run', role: 'assistant', text: 'reopened iteration audit' },
  ]
  const result = projection.projectMessages(messages)
  assert.deepEqual(ids(result), ['root', 'old-audit', 'suicide', 'late-old', 'wake', 'current'])
})
