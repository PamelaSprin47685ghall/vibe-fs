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

test('WHAT[relay-context-projection-008] the retired tail and the internal loop wake remain in the retained history', () => {
  const providerIds = ids(cutResult())
  assert.equal(providerIds.includes('a-late'), true)
  assert.equal(providerIds.includes('wake-1'), true)
  assert.equal(providerIds.includes('t1'), true)
  assert.equal(providerIds.includes('r1'), true)
})

test('WHAT[relay-context-projection-008] projection keeps typed authority messages inside the retained physical history', () => {
  const messages = [
    { id: 'root-authority', run: '', role: 'user', text: 'root request' },
    { id: 'old-audit', run: 'old-run', role: 'assistant', text: 'audit' },
    { id: 'authority-update', run: '', role: 'user', text: 'also satisfy the new constraint' },
    { id: 'incidental-user-like', run: '', role: 'user', text: 'not a typed authority revision' },
    {
      id: 'suicide',
      run: 'old-run',
      role: 'assistant',
      parts: [{ callID: 'suicide-call-2', tool: 'suicide' }],
      text: 'suicide',
    },
    { id: 'wake-2', run: '', role: 'user', text: 'internal wake' },
    { id: 'current', run: 'new-run', role: 'assistant', text: 'new audit' },
  ]

  const result = projection.projectMessages(messages)

  assert.deepEqual(
    result.provider.map((message) => message.id),
    ['root-authority', 'old-audit', 'authority-update', 'incidental-user-like', 'suicide', 'wake-2', 'current'],
  )
})
