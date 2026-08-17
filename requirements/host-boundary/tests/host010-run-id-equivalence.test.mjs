import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotSurface from '../../../dist/OpenCode/Host/SessionSnapshotSurface.js'
import * as ProviderRunBindingSurface from '../../../dist/OpenCode/Host/ProviderRunBindingSurface.js'

const projectMessages = SessionSnapshotSurface.projectMessages
const bindableRun = ProviderRunBindingSurface.bindableRun

const msg = ({ id, role, parentID, completed = false } = {}) => ({
  id, role, parentID,
  time: completed ? { completed: true } : undefined,
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 the bindable run is the unsealed assistant child of the physical user message', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([
    msg({ id: physical, role: 'user' }),
    msg({ id: 'asst_bindable', role: 'assistant', parentID: physical, completed: false }),
  ])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, true)
  assert.equal(result.id, 'asst_bindable')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 no bindable run means no ToolContext messageID to treat as the sealed run', () => {
  const physical = 'msg_user_1'
  const messages = projectMessages([msg({ id: physical, role: 'user' })])
  const result = bindableRun(physical, messages)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoBindableRun')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 duplicate bindable runs fail closed', () => {
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
