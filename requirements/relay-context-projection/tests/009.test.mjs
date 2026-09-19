import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'



test('WHAT[relay-context-projection-009] provider projection excludes predecessor manager private transcript and devops prompt history', () => {
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

  const result = projection.applyCut(
    messages,
    'run-m1',
    'suicide-call-devops',
    ['run-m1'],
    ['root-auth'],
  )

  const providerIds = result.provider.map((m) => m.id)
  assert.equal(providerIds.includes('m1-assess'), false)
  assert.equal(providerIds.includes('m1-devops-prompt'), false)
  assert.equal(providerIds.includes('devops-result'), false)
  assert.equal(providerIds.includes('wake-continue'), false)
  assert.deepEqual(providerIds, ['root-auth', 'm2-assess'])
})

test('WHAT[relay-context-projection-009] devops execution facts reach next incumbent via workspace snapshot rather than conversational replay', () => {
  if (typeof projection.projectDevOpsFacts === 'function') {
    const facts = projection.projectDevOpsFacts({
      workspaceSnapshotId: 'snap-post-devops',
      executedCommands: ['npm test'],
      modifiedFiles: ['src/App.fs'],
    })
    assert.deepEqual(facts, {
      visibleInWorkspace: true,
      injectedIntoProviderTranscript: false,
    })
  } else {
    assert.fail('projection.projectDevOpsFacts is not yet implemented')
  }
})
