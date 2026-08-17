import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/OpenCode/Host/HostBoundarySurface.js'

const msg = ({ id, role, parentID, completed = false } = {}) => ({
  id, role, parentID,
  time: completed ? { completed: true } : undefined,
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 the bindable run is the unsealed assistant child of the physical user message', () => {
  const physical = 'msg_user_1'
  const result = host.bindableRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'asst_bindable', role: 'assistant', parentID: physical, completed: false }),
  ])
  assert.deepEqual(result, { ok: true, runId: 'asst_bindable', error: null, count: 0 })
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 no bindable run means no ToolContext messageID to treat as the sealed run', () => {
  const physical = 'msg_user_1'
  const result = host.bindableRun(physical, [msg({ id: physical, role: 'user' })])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'NoBindableRun')
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 duplicate bindable runs fail closed', () => {
  const physical = 'msg_user_1'
  const result = host.bindableRun(physical, [
    msg({ id: 'asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'asst_2', role: 'assistant', parentID: physical }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'AmbiguousRun')
  assert.equal(result.count, 2)
})
