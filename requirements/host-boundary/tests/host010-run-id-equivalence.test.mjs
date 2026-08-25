import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotSurface from '../../../dist/OpenCode/Host/SessionSnapshotSurface.js'
import * as ProviderRunBindingSurface from '../../../dist/OpenCode/Host/ProviderRunBindingSurface.js'

const projectMessages = SessionSnapshotSurface.projectMessages
const bindableRun = ProviderRunBindingSurface.bindableRun

const msg = ({ id, role, parentID, completed = false, summary = false } = {}) => ({
  id, role, parentID,
  time: completed ? { completed: true } : undefined,
  summary,
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 the bindable run is the unsealed assistant child of the physical user message', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: 'msg_user_1', role: 'user' }),
    msg({ id: 'asst_bindable', role: 'assistant', parentID: physical }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, true)
  assert.equal(result.id, 'asst_bindable')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 zero candidates fail closed as typed NoBindableRun — never a wait', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([msg({ id: physical, role: 'user' })])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoBindableRun')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 multiple candidates fail closed as typed AmbiguousRun', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: 'asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'asst_2', role: 'assistant', parentID: physical }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'AmbiguousRun')
  assert.equal(result.count, 2)
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 a sealed assistant is not a bindable run — typed rejection, no terminal masking', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: physical, role: 'user' }),
    msg({ id: 'asst_sealed', role: 'assistant', parentID: physical, completed: true }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoBindableRun')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 wrong-parent assistants are never candidates', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: 'asst_other', role: 'assistant', parentID: 'msg_other' }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoBindableRun')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 not-latest candidate fails closed as NotLatestRun', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: 'asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'asst_9', role: 'assistant', parentID: 'msg_other' }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NotLatestRun')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 compaction children are excluded from binding and never retried', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: 'asst_compact', role: 'assistant', parentID: physical, summary: true }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoBindableRun')
})
