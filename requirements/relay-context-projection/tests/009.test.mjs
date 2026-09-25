import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'

test('WHAT[relay-context-projection-009] provider history retains the predecessor transcript and the loop wake in physical order', () => {
  const messages = [
    { id: 'root-auth', run: '', role: 'user', text: 'root requirement' },
    { id: 'm1-assess', run: 'run-m1', role: 'assistant', text: 'manager 1 private reasoning' },
    { id: 'm1-devops-prompt', run: 'run-m1', role: 'assistant', text: 'devops dispatch prompt' },
    { id: 'devops-result', run: 'run-m1', role: 'tool', text: 'devops execution log' },
    {
      id: 'm1-suicide',
      run: 'run-m1',
      role: 'assistant',
      parts: [{ callID: 'suicide-call-devops', tool: 'suicide' }],
      text: 'suicide call',
    },
    { id: 'wake-continue', run: '', role: 'user', text: 'loop wake' },
    { id: 'm2-assess', run: 'run-m2', role: 'assistant', text: 'manager 2 fresh review' },
  ]

  const result = projection.projectMessages(messages)

  const providerIds = result.provider.map((m) => m.id)
  assert.equal(providerIds.includes('m1-assess'), true)
  assert.equal(providerIds.includes('m1-devops-prompt'), true)
  assert.equal(providerIds.includes('devops-result'), true)
  assert.equal(providerIds.includes('wake-continue'), true)
  assert.deepEqual(providerIds, [
    'root-auth', 'm1-assess', 'm1-devops-prompt', 'devops-result', 'm1-suicide', 'wake-continue', 'm2-assess',
  ])
})
