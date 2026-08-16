// One-shot ownership is a Handle/Tool owner concern; semantic tests observe
// owner source and the opaque SyncDelegate vocabulary only.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const tool = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-021] ONESHOT_TOOL_requires_nonempty_charge', () => {
  assert.match(tool, /charge/i)
  assert.match(tool, /String\.IsNullOrWhiteSpace|nonEmpty/i)
})
test('WHAT[DELEG-021] ONESHOT_TOOL_role_is_coder_or_inspector_not_generic_agent', () => {
  assert.equal(sync.vocabulary('Coder', 'Fast', 's').role, 'coder')
  assert.equal(sync.vocabulary('Inspector', 'Fast', 's').role, 'inspector')
})
test('WHAT[DELEG-021] ONESHOT_TOOL_pending_completion_is_not_fabricated', () => {
  assert.doesNotMatch(tool, /return.*completed.*without|fake|placeholder/i)
})
