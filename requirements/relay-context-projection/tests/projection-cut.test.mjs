import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'

test('WHAT[PROJ-001] WHAT[PROJ-002] WHAT[PROJ-005] successor provider view cuts predecessor raw messages including suicide while audit stays complete', () => {
  const messages = [
    { id: 'u1', sequence: 1, run: '', text: 'root user request' },
    { id: 'a1', sequence: 2, run: 'old-run', text: 'old manager audit' },
    { id: 't1', sequence: 3, run: 'old-run', text: 'suicide call' },
    { id: 'r1', sequence: 4, run: 'old-run', text: 'suicide result' },
    { id: 'a-late', sequence: 5, run: 'old-run', text: 'late old part' },
    { id: 'system-new', sequence: 6, run: '', text: 'new baton' },
    { id: 'a2', sequence: 7, run: 'new-run', text: 'successor audit' },
  ]
  const result = projection.applyCut(messages, 4, ['old-run'])
  assert.equal(result.audit.length, messages.length)
  assert.deepEqual(result.provider.map((message) => message.id), ['u1', 'system-new', 'a2'])
})

test('WHAT[PROJ-003] WHAT[PROJ-007] provider context is rebuilt from root authority current baton and post-cut epoch', () => {
  const context = projection.successorContext('root request', 'authority-2', 'snapshot-9', 'baton-json')
  assert.deepEqual(context, {
    rootRequest: 'root request',
    authorityRevision: 'authority-2',
    snapshotId: 'snapshot-9',
    baton: 'baton-json',
    prompt: '此前已有其他同事负责用户的需求。现在由你接手，先独立评审当前完成情况和质量。',
  })
})

