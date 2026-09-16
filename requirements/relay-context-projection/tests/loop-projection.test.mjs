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

const cutResult = () => projection.applyCut(cutMessages, 'old-run', 'suicide-call', ['old-run'], ['u1'])

const ids = (result) => result.provider.map((message) => message.id ?? message.info?.id)

test('WHAT[PROJ-001] audit projection retains every physical message across the cut', () => {
  assert.equal(cutResult().audit.length, cutMessages.length)
})

test('WHAT[PROJ-002] projection cut covers the suicide request and result parts', () => {
  assert.deepEqual(ids(cutResult()), ['u1', 'a2'])
})

test('WHAT[RETIRE-008] wire cut drops the retired tail and the internal loop wake until the next real user turn', () => {
  const providerIds = ids(cutResult())
  assert.equal(providerIds.includes('a-late'), false)
  assert.equal(providerIds.includes('wake-1'), false)
  assert.equal(providerIds.includes('t1'), false)
  assert.equal(providerIds.includes('r1'), false)
})

test('WHAT[PROJ-005] next iteration shows the current-iteration tail after a clean authority start', () => {
  const provider = ids(cutResult())
  assert.equal(provider[0], 'u1')
  assert.equal(provider[provider.length - 1], 'a2')
})

test('WHAT[PROJ-008] projection cut preserves only typed authority from the retired iteration', () => {
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

  const result = projection.applyCut(
    messages,
    'old-run',
    'suicide-call-2',
    ['old-run'],
    ['root-authority', 'authority-update'],
  )

  assert.deepEqual(
    result.provider.map((message) => message.id),
    ['root-authority', 'authority-update', 'current'],
  )
})

test('WHAT[PROJ-003] next iteration context contains exact authority and existing current messages', () => {
  const result = cutResult()
  assert.deepEqual(
    result.provider.filter((message) => ['u1'].includes(message.id)).map((message) => message.id),
    ['u1'],
  )
  for (const message of result.provider) {
    assert.ok(cutMessages.some((origin) => origin.id === message.id), 'provider must not inject synthetic messages')
  }
  assert.deepEqual(ids(result).sort(), [...new Set(ids(result))].sort())
})

test('WHAT[PROJ-004] retired finish and internal wake project to a clean authority start', () => {
  const initial = [{ id: 'root', run: '', role: 'user', text: 'root user request' }]
  const initialProvider = projection.applyCut(initial, '', '', [], ['root'])
  assert.deepEqual(ids(initialProvider), ['root'])

  const retired = [
    { id: 'root', run: '', role: 'user', text: 'root user request' },
    { id: 'old-audit', run: 'old-run', role: 'assistant', text: 'old iteration audit' },
    {
      id: 'suicide',
      run: 'old-run',
      role: 'assistant',
      parts: [{ callID: 'suicide-call', tool: 'suicide' }],
      text: 'suicide call',
    },
    { id: 'wake', run: '', role: 'user', text: 'internal loop wake' },
  ]
  const nextStart = projection.applyCut(retired, 'old-run', 'suicide-call', ['old-run'], ['root'])
  assert.deepEqual(ids(nextStart), ['root'])
})

test('WHAT[PROJ-006] projection is deterministic and bounded', () => {
  const first = projection.applyCut(cutMessages, 'old-run', 'suicide-call', ['old-run'], ['u1'])
  const second = projection.applyCut(cutMessages, 'old-run', 'suicide-call', ['old-run'], ['u1'])
  assert.deepEqual(first.provider, second.provider)
  assert.ok(first.provider.length <= first.audit.length)
})

test('WHAT[PROJ-007] Accepted retirement reopened after invalidation cuts to authority plus current tail', () => {
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
  const result = projection.applyCut(messages, 'old-run', 'suicide-accepted', ['old-run'], ['root'])
  assert.deepEqual(ids(result), ['root', 'current'])
})
