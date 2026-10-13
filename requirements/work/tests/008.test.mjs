import assert from 'node:assert/strict'
import test from 'node:test'
import { projectSuccessor } from './support/contract.mjs'

test('WHAT[work-008] a successor strips prior assistant and tool and injects LWR_prev once', () => {
  const messages = [
    { role: 'user', firstSeen: 1 },
    { role: 'assistant', firstSeen: 2 },
    { role: 'tool', firstSeen: 3 },
    { role: 'assistant', firstSeen: 9 },
  ]
  const successor = projectSuccessor(messages, 4, false)
  assert.equal(successor.ok, true)
  assert.equal(successor.projected.filter((message) => message.role === 'lwr_prev').length, 1)
  assert.equal(successor.projected.some((message) => message.role === 'assistant' && message.firstSeen <= 4), false)
  assert.equal(successor.projected.some((message) => message.role === 'tool'), false)
  const first = projectSuccessor(messages, 0, true)
  assert.equal(first.projected.some((message) => message.role === 'lwr_prev'), false)
})
