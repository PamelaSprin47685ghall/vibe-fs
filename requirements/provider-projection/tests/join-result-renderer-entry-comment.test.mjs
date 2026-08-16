// Split from tests/unit/codec/join-result-renderer.test.mjs (cutover Wave 2a); owner: provider-projection.
// PROVIDER-PROJECTION-009 (instruction/data plane 由消费语义决定): the join
// entry's work-record rendering is classified into an entry-local `# LWR`
// comment (child→parent). An empty work record yields NO comment.
// Complementary hard lock (realistic LWR sections + forbid TOML field wrap):
// `requirements/delegation/tests/join-v2-wire.test.mjs`
// `EXEC_004_child_to_parent_lwr_is_hashed_comment_not_toml_field`.

import assert from 'node:assert/strict'
import test from 'node:test'
import { providerLanguage, toList } from '../../verification-system/tests/support/domain.mjs'

const {
  renderJoinItemBatch,
} = await import('../../../dist/Execution/Delegation/Fork/OpenCode/JoinResultRenderer.js')

const {
  JoinItem,
  AgentJoinItem,
  AgentCompletionPayload,
} = await import('../../../dist/Execution/Session/AgentCompletion.js')

const { NonEmptyBatch_ofHeadTail: batchOf } = await import('../../../dist/Execution/Session/Wait/CompletionMailbox.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')

const lang = providerLanguage.english

const completedPayload = (over = {}) =>
  new AgentCompletionPayload('a1', undefined, 'run-1', Role.Coder, undefined, undefined, over.workRecord ?? '', undefined)

const agentItem = (item) => new JoinItem(0, [item])

test('WHAT[PROVIDER-PROJECTION-009] MISC_join_render_batch_empty_work_record_no_comment', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(0, [completedPayload({ workRecord: '' })])), toList([]))
  const wire = renderJoinItemBatch(lang, () => 'x', batch)
  assert.match(wire, /# x has returned\./)
  assert.equal(wire.trim().split('\n').length, 1)
})

test('WHAT[PROVIDER-PROJECTION-009] MISC_join_render_batch_child_to_parent_lwr_stays_entry_local_comment', () => {
  const lwr = 'Chronicle\ndid the thing\n\nRecent work\nok'
  const batch = batchOf(agentItem(new AgentJoinItem(0, [completedPayload({ workRecord: lwr })])), toList([]))
  const wire = renderJoinItemBatch(lang, () => 'fast-coder', batch)
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /^# Chronicle$/m)
  assert.match(wire, /^# did the thing$/m)
  assert.ok(!wire.includes('work_record ='))
  assert.ok(!wire.includes("= '''"))
})
