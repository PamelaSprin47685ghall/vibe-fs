import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotSurface from '../../../dist/OpenCode/Host/SessionSnapshotSurface.js'
import * as ProviderRunBindingSurface from '../../../dist/OpenCode/Host/ProviderRunBindingSurface.js'

const projectMessages = SessionSnapshotSurface.projectMessages
const bindableRun = ProviderRunBindingSurface.bindableRun
const observeSequence = ProviderRunBindingSurface.observeSequence

const msg = ({ id, role, parentID, completed = false, summary = false } = {}) => ({
  id, role, parentID,
  time: completed ? { completed: true } : undefined,
  summary,
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

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 projection lag may catch up to the unique bindable run', () => {
  const physical = 'msg_user_1'
  const result = observeSequence(physical, [
    projectMessages([msg({ id: physical, role: 'user' })]),
    projectMessages([
      msg({ id: physical, role: 'user' }),
      msg({ id: 'asst_after_projection', role: 'assistant', parentID: physical }),
    ]),
  ])

  assert.deepEqual(result, {
    ok: true,
    id: 'asst_after_projection',
    reads: 2,
  })
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 ambiguity is not retried as projector lag', () => {
  const physical = 'msg_user_1'
  const result = observeSequence(physical, [
    projectMessages([
      msg({ id: 'asst_1', role: 'assistant', parentID: physical }),
      msg({ id: 'asst_2', role: 'assistant', parentID: physical }),
    ]),
    projectMessages([
      msg({ id: physical, role: 'user' }),
      msg({ id: 'asst_later', role: 'assistant', parentID: physical }),
    ]),
  ])

  assert.deepEqual(result, {
    ok: false,
    error: 'AmbiguousRun',
    count: 2,
    reads: 1,
  })
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 not-latest rejection is not retried as projector lag', () => {
  const physical = 'msg_user_1'
  const result = observeSequence(physical, [
    projectMessages([
      msg({ id: 'asst_1', role: 'assistant', parentID: physical }),
      msg({ id: 'asst_9', role: 'assistant', parentID: 'msg_other', completed: true }),
    ]),
    projectMessages([
      msg({ id: physical, role: 'user' }),
      msg({ id: 'asst_later', role: 'assistant', parentID: physical }),
    ]),
  ])

  assert.deepEqual(result, {
    ok: false,
    error: 'NotLatestRun',
    reads: 1,
  })
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 compaction is not retried as projector lag', () => {
  const physical = 'msg_user_1'
  const result = observeSequence(physical, [
    projectMessages([
      msg({ id: 'asst_compact', role: 'assistant', parentID: physical, summary: true }),
    ]),
    projectMessages([
      msg({ id: physical, role: 'user' }),
      msg({ id: 'asst_later', role: 'assistant', parentID: physical }),
    ]),
  ])

  assert.deepEqual(result, {
    ok: false,
    error: 'NoBindableRun',
    reads: 1,
  })
})

test('WHAT[HOST-BOUNDARY-008] HOST-BOUNDARY-008 projection catch-up is bounded by the production read budget', () => {
  const physical = 'msg_user_1'
  const missing = projectMessages([msg({ id: physical, role: 'user' })])
  const result = observeSequence(physical, Array.from({ length: 8 }, () => missing))

  assert.deepEqual(result, {
    ok: false,
    error: 'NoBindableRun',
    reads: 6,
  })
})
