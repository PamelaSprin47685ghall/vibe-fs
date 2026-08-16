// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: delegation. EXEC-008 / DELEG-019 child background：
// commissioner record is the durable LWR snapshot as `commissioner_record` TOML data;
// instruction header only names it. Never `# Opening` / `# Chronicle`.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

const { render, instructions } = await import('../../../dist/Execution/Delegation/Fork/Surface.js')

const en = instructions('en')

test('WHAT[DELEG-019] EXEC_008_child_background_uses_latest_durable_snapshot', () => {
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
  const rendered = render('en', {
    Assignment: 'Summarize the output',
    CommissionerRecord: lwrSnapshot,
    Attachment: undefined,
    RootRequirements: [],
    Payload: undefined,
  })
  const parsed = parseToml(rendered)

  assert.equal(rendered.includes(en.CommissionerRecord), true)
  assert.ok(rendered.includes('commissioner_record ='))
  assert.equal(parsed.commissioner_record, `${lwrSnapshot}\n`)
  // DELEG-019: durable LWR is a TOML field value, not `# Opening` instruction lines.
  assert.equal(rendered.includes('# Opening'), false)
  assert.equal(rendered.includes('# Chronicle'), false)
})
