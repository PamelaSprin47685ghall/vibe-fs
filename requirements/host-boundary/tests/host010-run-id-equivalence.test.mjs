// HOST-BOUNDARY-008 / HOST-010: transform bindableRun id ≡ ToolContext.messageID
// encoding. Same snapshot, same turn: the assistant id bindableRun selects is the
// only string decodeContext may lift to ProviderRunIdentity. 0/≥2 bind produces
// no legal run id. Host threading of that id through a live OpenCode turn remains
// the Long Stroke physical contract.

import assert from 'node:assert/strict'
import test from 'node:test'
import { toolHostCodec } from '../../verification-system/tests/support/domain/context.mjs'
import { reviewSeal } from '../../verification-system/tests/support/domain/enforcer.mjs'

const msg = ({ id, role, parentID, completed, agent } = {}) => {
  const info = { id, role }
  if (parentID !== undefined) info.parentID = parentID
  if (agent !== undefined) info.agent = agent
  if (completed) info.time = { completed: Date.now() }
  return { info, parts: [] }
}

const contextOf = (messageID) =>
  toolHostCodec.decodeContext({
    sessionID: 'ses_host010',
    callID: 'call_host010',
    messageID,
  })

test('HOST-BOUNDARY-008 bindableRun id equals ToolContext.messageID encoding', () => {
  const physical = 'msg_user_1'
  const bound = reviewSeal.bindableRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical }),
  ])
  assert.equal(bound.ok, true)
  assert.equal(bound.id, 'msg_asst_1')
  assert.equal(contextOf(bound.id).providerRunId, bound.id)
  assert.notEqual(contextOf('msg_other_run').providerRunId, bound.id)
})

test('HOST-BOUNDARY-008 no bindable run means no ToolContext messageID to treat as the sealed run', () => {
  const physical = 'msg_user_1'
  const none = reviewSeal.bindableRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical, completed: true }),
  ])
  assert.equal(none.ok, false)
  const ambiguous = reviewSeal.bindableRun(physical, [
    msg({ id: physical, role: 'user' }),
    msg({ id: 'msg_asst_1', role: 'assistant', parentID: physical }),
    msg({ id: 'msg_asst_2', role: 'assistant', parentID: physical }),
  ])
  assert.equal(ambiguous.ok, false)
  assert.equal(ambiguous.rejection.case, 'AmbiguousRun')
})
