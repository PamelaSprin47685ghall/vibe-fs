import assert from 'node:assert/strict'
import test from 'node:test'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'

test('WHAT[work-record-012] record accepts prose without required report fields and preserves voluntarily chosen headings', () => {
  const opening = workRecord.opening('task', [], '')
  for (const claim of [
    'assistant: 已找到原因，尚未修改。',
    'assistant: ### Summary\nFiles Changed: none\nTests: not run',
    'assistant: Closing report is the name of a file, not a required section.',
  ]) {
    assert.equal(workRecord.materialize(opening, [], claim, false), `Recent work\n${claim}`)
  }
})
