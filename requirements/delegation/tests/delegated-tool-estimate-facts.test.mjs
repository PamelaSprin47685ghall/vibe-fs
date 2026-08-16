import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'

const {
  DelegatedToolEstimateProjection_remaining: remaining,
  DelegatedToolEstimateProjection_countedCallCount: countedCallCount,
} = await import('../../../dist/Execution/Delegation/DelegatedToolEstimateProjection.js')

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegated-estimate-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)
  try {
    await fn(opened.journal)
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const stateOf = (journal, sid) =>
  agentJournal.snapshot(journal).AgentProjections.Sessions.get(sid).DelegatedToolEstimate

const append = (journal, sid, caseName, payload) =>
  agentJournal.appendAgent(stream.session(sid), undefined, agentFact(caseName, payload), journal)

test('WHAT[DELEG-022] DELEG_022_durable_replace_and_tool_observation_fold_incrementally', async () => {
  await withJournal(async (journal) => {
    const sid = sessionId('ses_delegate')

    assert.equal(
      (await append(journal, sid, 'DelegatedToolEstimateReplaced', {
        SessionId: sid,
        ExpectedToolCalls: 3,
      })).ok,
      true,
    )
    assert.equal(remaining(stateOf(journal, sid)), 3)
    assert.equal(countedCallCount(stateOf(journal, sid)), 0)

    const call = toolCallId('tool-1')
    assert.equal(
      (await append(journal, sid, 'DelegatedToolCallObserved', {
        SessionId: sid,
        ToolCallId: call,
      })).ok,
      true,
    )
    assert.equal(remaining(stateOf(journal, sid)), 2)
    assert.equal(countedCallCount(stateOf(journal, sid)), 1)

    assert.equal(
      (await append(journal, sid, 'DelegatedToolCallObserved', {
        SessionId: sid,
        ToolCallId: call,
      })).ok,
      true,
    )
    assert.equal(remaining(stateOf(journal, sid)), 2, 'replayed observation must not double decrement')
    assert.equal(countedCallCount(stateOf(journal, sid)), 1)
  })
})

test('WHAT[DELEG-022] DELEG_022_durable_replace_resets_the_measurement_without_a_program_stage', async () => {
  await withJournal(async (journal) => {
    const sid = sessionId('ses_delegate_replace')
    await append(journal, sid, 'DelegatedToolEstimateReplaced', { SessionId: sid, ExpectedToolCalls: 1 })
    await append(journal, sid, 'DelegatedToolCallObserved', { SessionId: sid, ToolCallId: toolCallId('tool-1') })
    assert.equal(remaining(stateOf(journal, sid)), 0)

    await append(journal, sid, 'DelegatedToolEstimateReplaced', { SessionId: sid, ExpectedToolCalls: 5 })
    assert.equal(remaining(stateOf(journal, sid)), 5)
    assert.equal(countedCallCount(stateOf(journal, sid)), 0)
  })
})
