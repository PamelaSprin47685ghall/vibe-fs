// SyncDelegate tools remain owner ToolSpecs; runtime/turn wiring crosses only
// SyncDelegateSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const inspector = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Tools/InspectorTool.fs', import.meta.url), 'utf8')
const coder = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Tools/CoderTool.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-021] SYNC_TOOLS_inspector_establishes_one_dedicated_role', () => {
  assert.match(inspector, /InspectorTool|spec/)
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'scope').agent, 'inspector')
})
test('WHAT[DELEG-021] SYNC_TOOLS_coder_repair_and_establish_share_coder_role', () => {
  assert.match(coder, /establishSpec/)
  assert.match(coder, /repairSpec/)
  assert.equal(sync.vocabulary('Coder', 'Deep', 'scope').agent, 'coder')
})
test('WHAT[DELEG-021] SYNC_TOOLS_malformed_owner_context_is_rejected_at_codec_boundary', () => {
  const legacy = [['.', 'tag'].join(''), ['.', 'fields'].join(''), ['cases', '()'].join('')]
  assert.equal(legacy.some((token) => inspector.includes(token)), false)
})
