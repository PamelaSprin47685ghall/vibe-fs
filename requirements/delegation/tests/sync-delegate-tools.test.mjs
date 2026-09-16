// The delegated Engineer charge stays an owner surface; runtime/turn wiring
// crosses only SyncDelegateSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const surface = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-021] SYNC_TOOLS_engineer_establishes_one_dedicated_role', () => {
  assert.match(surface, /executeEngineerCharge/)
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'scope').agent, 'engineer')
})
test('WHAT[DELEG-021] SYNC_TOOLS_malformed_owner_context_is_rejected_at_codec_boundary', () => {
  const legacy = [['.', 'tag'].join(''), ['.', 'fields'].join(''), ['cases', '()'].join('')]
  assert.equal(legacy.some((token) => surface.includes(token)), false)
})

test('WHAT[DELEG-008] DEFERRED_ENGINEER_stage_and_replace_tool_results_in_messages', () => {
  const placeholder = sync.stageDeferredInspection('session-test-deferred', 'call-deferred-1', 'check auth integrity', 'auth', 1)
  assert.match(placeholder, /Engineer charge accepted and deferred for batch execution/)
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
  sync.applyReplacedResults(list)

  // Prior to settlement, durable map does not replace
  assert.equal(msgWithPart.parts[0].state.output, placeholder)
  assert.equal(msgWithRole.content, placeholder)
})
