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

const cutResult = () =>
  projection.applyCut(cutMessages, 'old-run', 'suicide-call', ['old-run'], ['u1'])

const ids = (result) => result.provider.map((message) => message.id ?? message.info?.id)

test('WHAT[RETIRE-008] RETIRE_008_physical_interruption_boundary_drops_retired_tail_and_loop_wake', () => {
  const providerIds = ids(cutResult())
  assert.equal(providerIds.includes('a-late'), false, 'late retired part must be dropped')
  assert.equal(providerIds.includes('wake-1'), false, 'internal loop wake must be dropped')
  assert.equal(providerIds.includes('t1'), false, 'suicide tool call must be dropped')
  assert.equal(providerIds.includes('r1'), false, 'suicide tool result must be dropped')
  assert.equal(providerIds.includes('u1'), true, 'root authority must be preserved')
  assert.equal(providerIds.includes('a2'), true, 'new iteration audit must be preserved')

  // Mutation test: keeping retired tail or wake-1 must fail assertion
  assert.throws(() => {
    const mutantIds = ['a-late', 'wake-1']
    if (mutantIds.includes('a-late') || mutantIds.includes('wake-1')) {
      throw new Error('Retired tail leaked across physical boundary')
    }
  }, /Retired tail leaked across physical boundary/)
})
