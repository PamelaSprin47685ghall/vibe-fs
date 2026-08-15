// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: delegation. EXEC-008 / DELEG-019 child background：
// commissioner record is the durable LWR snapshot as ordinary WorkRecord prose in the body;
// instruction header only names it. Never `# Opening` / `# Chronicle`, never parent_work_record.

import assert from 'node:assert/strict'
import test from 'node:test'
import { forkChildPayload } from '../../verification-system/tests/support/domain.mjs'

test('EXEC_008_child_background_uses_latest_durable_snapshot', () => {
  const lwrSnapshot = [
    'Opening',
    'LWR snapshot at turn 9',
    '',
    'Chronicle',
    'durable frame',
    '',
    'Recent work',
    'tail',
  ].join('\n')
  const rendered = forkChildPayload.render({
    assignment: 'Summarize the output',
    commissionerRecord: lwrSnapshot,
    rootRequirements: [],
    payload: undefined,
  })

  assert.equal(rendered.includes(lwrSnapshot), true)
  assert.equal(rendered.includes(forkChildPayload.commissionerRecordInstruction), true)
  assert.equal(rendered.includes('parent_work_record'), false)
  // DELEG-019: durable LWR enters as body prose, not `# Opening` instruction lines.
  assert.equal(rendered.includes('# Opening'), false)
  assert.equal(rendered.includes('# Chronicle'), false)
  assert.ok(rendered.includes('\n\nOpening\n'))
})