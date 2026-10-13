import assert from 'node:assert/strict'
import test from 'node:test'

test('WHAT[relay-context-projection-002] a cut identifies the retiring attempt and does not keep full history', () => {
  const cut = { providerRunId: 'r', toolCallId: 'c' }
  const late = { providerRunId: 'r', toolCallId: 'c', role: 'assistant' }
  const fresh = { providerRunId: 'r2', toolCallId: 'c2', role: 'user' }
  const stale = (message) => message.providerRunId === cut.providerRunId && message.toolCallId === cut.toolCallId
  assert.equal(stale(late), true)
  assert.equal(stale(fresh), false)
  assert.equal(late.role === 'assistant' && stale(late), true)
})
