import assert from 'node:assert/strict'
import test from 'node:test'

import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'

const textMessage = (id, role, text) => ({
  info: { id, role },
  parts: [{ type: 'text', text }],
})

test('WHAT[PREFIX-STABILITY-009] prefix replacement removes covered history by stable Host identity', () => {
  const raw = [
    textMessage('covered-u', 'user', 'old user'),
    textMessage('request-local', 'assistant', 'request-local presentation only'),
    textMessage('covered-a', 'assistant', 'old answer'),
    textMessage('live-u', 'user', 'live request'),
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['covered-u', 'covered-a'],
    null,
    'y-prefix',
    'compressed canonical X',
  )

  assert.deepEqual(projected.map(item => item.info.id), ['y-prefix', 'request-local', 'live-u'])
  assert.equal(projected[1], raw[1], 'request-local presentation must survive as the same Host object')
  assert.equal(projected[2], raw[3], 'live history must survive as the same Host object')
})

test('WHAT[PREFIX-STABILITY-009] stable identity replacement preserves survivor order and todowrite round objects', () => {
  const raw = [
    {
      info: { id: 'todo-call-msg', role: 'assistant' },
      parts: [{ type: 'tool-call', tool: 'todowrite', callID: 'todo-call-1', args: { planComplete: false } }],
    },
    textMessage('request-local', 'assistant', 'request-local presentation only'),
    {
      info: { id: 'todo-result-msg', role: 'tool' },
      parts: [{ type: 'tool-result', callID: 'todo-call-1', result: { ok: true } }],
    },
    textMessage('covered-ordinary', 'assistant', 'replace me'),
    textMessage('live-u', 'user', 'live request'),
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['todo-call-msg', 'todo-result-msg', 'covered-ordinary'],
    null,
    'y-prefix',
    'compressed canonical X',
  )

  assert.deepEqual(
    projected.map(item => item.info.id),
    ['y-prefix', 'todo-call-msg', 'request-local', 'todo-result-msg', 'live-u'],
  )
  assert.equal(projected[1], raw[0])
  assert.equal(projected[2], raw[1])
  assert.equal(projected[3], raw[2])
  assert.equal(projected[4], raw[4])
})

test('WHAT[PREFIX-STABILITY-006] same-session memory is inserted after the preserved raw Opening', () => {
  const raw = [
    textMessage('opening-u', 'user', 'raw opening'),
    textMessage('covered-a', 'assistant', 'covered work'),
    textMessage('live-u', 'user', 'live request'),
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['covered-a'],
    'opening-u',
    'y-prefix',
    'compressed post-opening history',
  )

  assert.deepEqual(projected.map(item => item.info.id), ['opening-u', 'y-prefix', 'live-u'])
  assert.equal(projected[0], raw[0], 'Opening must remain the exact raw Host object')
  assert.equal(projected[2], raw[2], 'live history must remain the exact raw Host object')
})

test('WHAT[PREFIX-STABILITY-009] transport suppression removes only exact stale Host ids', () => {
  const retryMessage = (id, text) => ({
    info: { id, role: 'user' },
    parts: [{ type: 'text', text, metadata: { wanxiangshu_origin: 'ProviderRetryAttempt' } }],
  })
  const raw = [
    textMessage('root', 'user', 'root'),
    retryMessage('retry-old', 'retry old'),
    textMessage('business-assistant', 'assistant', 'must survive'),
    retryMessage('retry-current', 'retry current'),
  ]

  const projected = xwire.suppressHostMessagesByIds(raw, ['retry-old'])

  assert.deepEqual(projected.map(item => item.info.id), ['root', 'business-assistant', 'retry-current'])
  assert.equal(projected[0], raw[0])
  assert.equal(projected[1], raw[2], 'unaddressed assistant semantics must survive as the same Host object')
  assert.equal(projected[2], raw[3], 'current retry continuation must survive as the same Host object')
})
