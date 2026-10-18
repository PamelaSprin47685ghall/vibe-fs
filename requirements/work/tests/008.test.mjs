import assert from 'node:assert/strict'
import test from 'node:test'
import { projectSuccessor } from './support/contract.mjs'
import { withSuccessor } from './support/cut.mjs'

test('WHAT[work-008] a successor strips prior assistant and tool and never injects a synthetic LWR_prev record', () => {
  const messages = [
    { role: 'user', firstSeen: 1 },
    { role: 'assistant', firstSeen: 2 },
    { role: 'tool', firstSeen: 3 },
    { role: 'assistant', firstSeen: 9 },
  ]
  const successor = projectSuccessor(messages, 4, false)
  assert.equal(successor.ok, true)
  assert.equal(successor.projected.filter((message) => message.role === 'lwr_prev').length, 0)
  assert.equal(successor.projected.some((message) => message.role === 'assistant' && message.firstSeen <= 4), false)
  assert.equal(successor.projected.some((message) => message.role === 'assistant' && message.firstSeen > 4), true)
  assert.equal(successor.projected.some((message) => message.role === 'tool'), false)
  const first = projectSuccessor(messages, 0, true)
  assert.equal(first.projected.some((message) => message.role === 'lwr_prev'), false)
})

// The Manager successor projection applies the same stable-identity criterion as
// the Plan tenure assembly (work-034): a Host id that has not been captured yet
// stays, and a semantic interaction with no Host id at all refuses the assembly
// instead of guessing a cursor.
test('WHAT[work-008] a semantic successor row with no stable Host identity refuses the assembly', async () => {
  await withSuccessor(async ({hooks, session, history, gate}) => {
    const unidentified = {
      role: 'assistant',
      parts: [{type: 'text', text: 'predecessor row without a stable identity'}],
    }
    const output = {messages: [...structuredClone(history), gate, unidentified]}
    await assert.rejects(
      () => Promise.resolve(hooks['experimental.chat.messages.transform']({sessionID: session}, output)),
      /no stable XTrace message identity/i,
    )
  }, {session: 'review-manager-unstable-successor-row'})
})
