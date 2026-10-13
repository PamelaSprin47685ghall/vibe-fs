import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

test('统一事件文件最后一行没有换行，读者整份 StorageInvalid', () => {
  const opened = surface.encodeOpening('plan', 'work-truncated-line', '/repo', 7n)
  assert.equal(opened.endsWith('\n'), true)
  const prefix = surface.readProbe(opened)
  assert.equal(prefix.position, 'ActiveRebound')

  const truncated = `${opened}{"event_type":"IncumbencyOpened"`
  assert.equal(truncated.endsWith('\n'), false)

  const read = surface.readProbe(truncated)
  assert.equal(read.position, 'StorageInvalid')
  assert.equal(read.names, '')
  assert.equal(read.sequence, '')
  assert.notEqual(read.next, 'EnterDispatch')
  assert.notEqual(read.next, 'EnterSuccessorPrompt')
  assert.notEqual(read.next, 'WaitForUserPrompt')
  assert.notEqual(read.position, 'ActiveRebound')
  assert.notEqual(read.position, 'DeliveredIdempotent')
  assert.notEqual(read.position, 'PairIncomplete')
  assert.notEqual(read.position, 'Conflict')

  const decoded = surface.decodeDocument(truncated)
  assert.equal(decoded.ok, false)
  assert.equal(decoded.names, '')
  assert.equal(decoded.names.includes('IncumbencyOpened'), false)
  assert.equal(decoded.names.includes('WorkOpened'), false)
})
