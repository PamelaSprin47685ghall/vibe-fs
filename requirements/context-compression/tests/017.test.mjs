import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'

test('WHAT[context-compression-017] same-session memory rendering does not introduce delegation fields', () => {
  const block = projection.memoryBlock('Chronicle\nself history')
  assert.match(block, /^# Chronicle$/m)
  assert.match(block, /^# self history$/m)
  assert.doesNotMatch(block, /(?:^|\n)(?:commissioner_record|attached_work_record)\s*=/)
})

test('WHAT[context-compression-017] every covered physical user message survives LWR replacement as the exact Host object', () => {
  const raw = [
    { info: { id: 'user-1', role: 'user' }, parts: [{ type: 'text', text: 'first request' }] },
    { info: { id: 'assistant-1', role: 'assistant' }, parts: [{ type: 'text', text: 'old answer 1' }] },
    { info: { id: 'user-2', role: 'user' }, parts: [{ type: 'text', text: 'second request' }] },
    { info: { id: 'assistant-2', role: 'assistant' }, parts: [{ type: 'text', text: 'old answer 2' }] },
    { info: { id: 'user-3', role: 'user' }, parts: [{ type: 'text', text: 'third request' }] },
    { info: { id: 'tail', role: 'assistant' }, parts: [{ type: 'text', text: 'live tail' }] },
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['user-1', 'assistant-1', 'user-2', 'assistant-2', 'user-3'],
    'user-1',
    'lwr-users',
    'compressed assistant history',
  )

  assert.deepEqual(
    projected.map((message) => message.info.id),
    ['user-1', 'lwr-users', 'user-2', 'user-3', 'tail'],
  )
  assert.equal(projected[0], raw[0])
  assert.equal(projected[2], raw[2])
  assert.equal(projected[3], raw[4])
})

test('WHAT[context-compression-017] synthetic companion-memory user-role messages do not acquire the physical-user exemption', () => {
  const raw = [
    { info: { id: 'user-1', role: 'user' }, parts: [{ type: 'text', text: 'request' }] },
    {
      info: {
        id: 'old-lwr',
        role: 'user',
        synthetic: true,
        source: 'companion-memory',
      },
      parts: [{ type: 'text', text: 'old synthetic memory' }],
    },
    { info: { id: 'user-2', role: 'user' }, parts: [{ type: 'text', text: 'follow-up' }] },
    { info: { id: 'tail', role: 'assistant' }, parts: [{ type: 'text', text: 'live tail' }] },
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['user-1', 'old-lwr', 'user-2'],
    'user-1',
    'new-lwr',
    'new synthetic memory',
  )

  assert.deepEqual(projected.map((message) => message.info.id), ['user-1', 'new-lwr', 'user-2', 'tail'])
  assert.equal(projected[0], raw[0])
  assert.equal(projected[2], raw[2])
  assert.equal(projected.includes(raw[1]), false)
})

test.todo('WHAT[context-compression-017] real compaction and recovery preserve every exact physical user message while same-session FrozenRecordPrefix excludes them; pure XWire replacement does not prove the full lifecycle (GAP-104)')
test.todo('WHAT[context-compression-017] the actual Blogger start uses max of coverage and true Opening floor independently of phase commits; a test-defined cursor-plus-one function proves nothing (GAP-104)')
