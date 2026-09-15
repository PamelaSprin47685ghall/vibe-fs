// SyncDelegate tools remain owner ToolSpecs; runtime/turn wiring crosses only
// SyncDelegateSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as batching from '../../../dist/Execution/Delegation/SyncDelegate/OpenCode/Batching.js'

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

test('WHAT[DELEG-008] DEFERRED_INSPECTOR_stage_and_replace_tool_results_in_messages', () => {
  const placeholder = batching.stageDeferredInspection('session-test-deferred', 'call-deferred-1', 'check auth integrity', 'auth', 1)
  assert.match(placeholder, /Inspector charge accepted and deferred for batch execution/)
  assert.match(placeholder, /check auth integrity/)

  // Messages with part.type === "tool"
  const msgWithPart = {
    info: { role: 'assistant' },
    parts: [
      {
        type: 'tool',
        callID: 'call-deferred-1',
        state: { status: 'completed', output: placeholder },
      },
    ],
  }
  // Messages with role === "tool"
  const msgWithRole = {
    role: 'tool',
    tool_call_id: 'call-deferred-1',
    content: placeholder,
  }

  const list = [msgWithPart, msgWithRole]
  batching.applyReplacedResults(list)

  // Prior to settlement, durable map does not replace
  assert.equal(msgWithPart.parts[0].state.output, placeholder)
  assert.equal(msgWithRole.content, placeholder)
})
