// tests/unit/Execution/executor-summarize.test.mjs — EXECUTOR-001 the map/reduce prompt composers.
//
// The prompt is the plain intent only; the chunk/combined content is carried
// by the fork envelope's `content` field (FORK_CHILD_PAYLOAD_payload_renders_as_content).

import assert from 'node:assert/strict'
import test from 'node:test'
import { executorSummarize } from '../support/domain.mjs'

test('EXECUTOR_SUMMARIZE_summarize_chunk_prompt_is_plain_intent', () => {
  assert.equal(
    executorSummarize.summarizeChunkPrompt(1),
    'Summarize command output chunk 1. Preserve errors, decisions, paths, and exact numbers; omit raw code.',
  )
})

test('EXECUTOR_SUMMARIZE_reduce_batch_prompt_is_plain_intent', () => {
  assert.equal(
    executorSummarize.reduceBatchPrompt(2),
    'Reduce level-2 command-output summaries into one dense report. Preserve failures and exact facts; do not include raw code.',
  )
})

test('EXECUTOR_SUMMARIZE_index_varies_in_chunk_prompt', () => {
  assert.ok(executorSummarize.summarizeChunkPrompt(7).startsWith('Summarize command output chunk 7.'))
})

test('EXECUTOR_SUMMARIZE_level_varies_in_reduce_prompt', () => {
  assert.ok(executorSummarize.reduceBatchPrompt(5).startsWith('Reduce level-5 command-output summaries into one dense report.'))
})
