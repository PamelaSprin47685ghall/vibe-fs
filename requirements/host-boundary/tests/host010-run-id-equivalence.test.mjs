import assert from 'node:assert/strict'
import test from 'node:test'
import { runIdentity } from './support/host-surface.mjs'

const msg = ({ id, role, parentID, completed = false } = {}) => ({ id, role, parentID, completed })

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 bindableRun id equals ToolContext.messageID encoding', () => {
  const physical = 'msg_user_1'
  const bound = runIdentity.bindableRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'asst_bindable', role: 'assistant', parentID: physical, completed: false }),
  ])
  assert.equal(bound, 'asst_bindable')
  assert.equal(runIdentity.contextMessageId(bound), bound)
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 no bindable run means no ToolContext messageID to treat as the sealed run', () => {
  const physical = 'msg_user_1'
  assert.equal(runIdentity.bindableRun(physical, [msg({ id: physical, role: 'user' })]), undefined)
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 duplicate bindable runs fail closed', () => {
  const physical = 'msg_user_1'
  assert.equal(runIdentity.bindableRun(physical, [
    msg({ id: 'asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'asst_2', role: 'assistant', parentID: physical }),
  ]), undefined)
})
