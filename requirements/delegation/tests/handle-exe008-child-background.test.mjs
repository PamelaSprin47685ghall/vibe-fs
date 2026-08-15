// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: delegation. EXEC-008 / DELEG-019 child background：
// commissioner record is the durable LWR snapshot as `commissioner_record` TOML data;
// instruction header only names it. Never `# Opening` / `# Chronicle`.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
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
  const parsed = parseToml(rendered)

  assert.equal(rendered.includes(forkChildPayload.commissionerRecordInstruction), true)
  assert.ok(rendered.includes('commissioner_record ='))
  assert.equal(parsed.commissioner_record, `${lwrSnapshot}\n`)
  // DELEG-019: durable LWR is a TOML field value, not `# Opening` instruction lines.
  assert.equal(rendered.includes('# Opening'), false)
  assert.equal(rendered.includes('# Chronicle'), false)
})
