// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: delegation. EXEC-008 child background 使用最新 durable 工作记录：
// fork child payload 的 commissioner record 是 LWR snapshot，逐字进入渲染；
// 无 parent_work_record 信封（DELEG-019 fork child payload 契约面）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { forkChildPayload } from '../../verification-system/tests/support/domain.mjs'

test('EXEC_008_child_background_uses_latest_durable_snapshot', () => {
  const lwrSnapshot = 'LWR snapshot at turn 9'
  const rendered = forkChildPayload.render({
    assignment: 'Summarize the output',
    commissionerRecord: lwrSnapshot,
    rootRequirements: [],
    payload: undefined,
  })

  assert.equal(rendered.includes(lwrSnapshot), true)
  assert.equal(rendered.includes(forkChildPayload.commissionerRecordInstruction), true)
  assert.equal(rendered.includes('parent_work_record'), false)
})
