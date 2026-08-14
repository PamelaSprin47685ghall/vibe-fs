// Split from tests/unit/codec/join-result-renderer.test.mjs (cutover Wave 2a); owner: provider-projection.
// PROVIDER-PROJECTION-009 (instruction/data plane 由消费语义决定): the join
// entry's work-record rendering is classified into an entry-local comment —
// an empty work record yields NO comment.

import assert from 'node:assert/strict'
import test from 'node:test'
import { providerLanguage, toList } from '../../verification-system/tests/support/domain.mjs'

const {
  renderJoinItemBatch,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/JoinResultRenderer.js')

const {
  JoinItem,
  AgentJoinItem,
  AgentCompletionPayload,
} = await import('../../../dist/Session/AgentCompletion.js')

const { NonEmptyBatch_ofHeadTail: batchOf } = await import('../../../dist/Session/CompletionMailbox.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

const lang = providerLanguage.english

const completedPayload = (over = {}) =>
  new AgentCompletionPayload('a1', undefined, 'run-1', Role.Coder, undefined, undefined, over.workRecord ?? '', undefined)

const agentItem = (item) => new JoinItem(0, [item])

test('MISC_join_render_batch_empty_work_record_no_comment', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(0, [completedPayload({ workRecord: '' })])), toList([]))
  const wire = renderJoinItemBatch(lang, () => 'x', batch)
  assert.match(wire, /# x has returned\./)
  assert.equal(wire.trim().split('\n').length, 1)
})
