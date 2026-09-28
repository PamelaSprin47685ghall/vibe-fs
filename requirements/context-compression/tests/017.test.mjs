import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Context/Companion/ProjectionSurface.js'

test('WHAT[context-compression-017] same-session memory rendering does not introduce delegation fields', () => {
  const block = projection.memoryBlock('Chronicle\nself history')
  assert.match(block, /^# Chronicle$/m)
  assert.match(block, /^# self history$/m)
  assert.doesNotMatch(block, /(?:^|\n)(?:commissioner_record|attached_work_record)\s*=/)
})

test.todo('WHAT[context-compression-017] real compaction and recovery preserve exact Opening messages while same-session FrozenRecordPrefix excludes them; 028 covers one live XWire path only (GAP-104)')
test.todo('WHAT[context-compression-017] the actual Blogger start uses max of coverage and true Opening floor independently of phase commits; a test-defined cursor-plus-one function proves nothing (GAP-104)')
