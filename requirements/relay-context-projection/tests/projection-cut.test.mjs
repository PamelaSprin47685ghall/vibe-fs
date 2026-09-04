import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'

const cutMessages = [
  { id: 'u1', sequence: 1, run: '', text: 'root user request' },
  { id: 'a1', sequence: 2, run: 'old-run', text: 'old manager audit' },
  { id: 't1', sequence: 3, run: 'old-run', text: 'suicide call' },
  { id: 'r1', sequence: 4, run: 'old-run', text: 'suicide result' },
  { id: 'a-late', sequence: 5, run: 'old-run', text: 'late old part' },
  { id: 'system-new', sequence: 6, run: '', text: 'new baton' },
  { id: 'a2', sequence: 7, run: 'new-run', text: 'successor audit' },
]

const cutResult = () => projection.applyCut(cutMessages, 4, ['old-run'], ['u1'])

test('WHAT[PROJ-001] audit projection retains every physical message across the cut', () => {
  assert.equal(cutResult().audit.length, cutMessages.length)
})

test('WHAT[PROJ-002] projection cut covers the suicide request and result parts', () => {
  assert.deepEqual(cutResult().provider.map((message) => message.id), ['u1', 'system-new', 'a2'])
})

test('WHAT[PROJ-005] successor provider view keeps one continuous session narrative', () => {
  const provider = cutResult().provider
  assert.equal(provider[0].id, 'u1')
  assert.equal(provider[provider.length - 1].id, 'a2')
})

test('WHAT[PROJ-008] projection cut preserves only typed authority messages from the predecessor epoch', () => {
  const messages = [
    { id: 'root-authority', sequence: 1, run: '', text: 'root request' },
    { id: 'old-audit', sequence: 2, run: 'old-run', text: 'audit' },
    { id: 'authority-update', sequence: 3, run: '', text: 'also satisfy the new constraint' },
    { id: 'incidental-user-like', sequence: 4, run: '', text: 'not a typed authority revision' },
    { id: 'suicide', sequence: 5, run: 'old-run', text: 'suicide' },
    { id: 'successor', sequence: 6, run: 'new-run', text: 'new audit' },
  ]

  const result = projection.applyCut(
    messages,
    5,
    ['old-run'],
    ['root-authority', 'authority-update'],
  )

  assert.deepEqual(
    result.provider.map((message) => message.id),
    ['root-authority', 'authority-update', 'successor'],
  )
})

test('WHAT[PROJ-003] provider context is rebuilt from root authority current baton and post-cut epoch', () => {
  const context = projection.successorContext('root request', 'authority-2', 'snapshot-9', 'baton-json')
  assert.deepEqual(context, {
    rootRequest: 'root request',
    authorityRevision: 'authority-2',
    snapshotId: 'snapshot-9',
    baton: 'baton-json',
    prompt: '此前已有其他同事负责用户的需求。现在由你接手，先独立评审当前完成情况和质量。',
  })
})

test('WHAT[PROJ-007] rebuilt successor context carries no retired raw history field', () => {
  const context = projection.successorContext('root request', 'authority-2', 'snapshot-9', 'baton-json')
  assert.deepEqual(Object.keys(context).sort(), ['authorityRevision', 'baton', 'prompt', 'rootRequest', 'snapshotId'])
})

