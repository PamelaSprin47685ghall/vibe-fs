// Integration repros at the Blog owner boundary. The fixture drives semantic
// terminal evidence instead of importing PluginScope/Journal internals.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../../dist/Enforcer/BlogSurface.js'

test('WHAT[BD-017] REPRO_blogger_pure_prose_protocol_records_the_idle_owned_nudge_occasion', () => {
  const first = blog.repairProtocol({ priorState: 'NoRecovery', terminalRun: 'asst-blogger-prose-only', nudgeSucceeded: true })
  assert.equal(first.state, 'InteractionNudgeIssued')
  assert.equal(first.run, 'asst-blogger-prose-only')
})

test('WHAT[BD-017] REPRO_blogger_pure_prose_terminal_idle_should_nudge_without_another_transform', () => {
  const first = blog.repairProtocol({ priorState: 'NoRecovery', terminalRun: 'asst-blogger-idle-prose-only', nudgeSucceeded: true })
  assert.equal(first.state, 'InteractionNudgeIssued')
  assert.equal(first.run, 'asst-blogger-idle-prose-only')
})

test('WHAT[BD-017] REPRO_blogger_aabb_is_sent_even_when_generic_fallback_reaches_exhaustion_on_that_failure', () => {
  const nudge = blog.repairProtocol({ priorState: 'NoRecovery', terminalRun: 'asst-generic-exhaust-p1', nudgeSucceeded: true })
  assert.equal(nudge.state, 'InteractionNudgeIssued')
  const aabb = blog.repairProtocol({
    priorState: nudge.state,
    repairTerminalRun: nudge.run,
    terminalRun: 'asst-generic-exhaust-p2',
    nudgeSucceeded: true,
    fallbackExhausted: true,
  })
  assert.equal(aabb.state, 'AabbRepairIssued')
  const exhausted = blog.repairProtocol({
    priorState: aabb.state,
    repairTerminalRun: aabb.run,
    terminalRun: 'asst-generic-exhaust-p3',
    nudgeSucceeded: true,
    fallbackExhausted: true,
  })
  assert.equal(exhausted.state, 'ProtocolExhausted')
})

test('WHAT[BD-017] REPRO_blogger_second_prose_terminal_idle_spends_aabb_not_second_nudge', () => {
  const first = blog.repairProtocol({ priorState: 'NoRecovery', terminalRun: 'asst-aabb-p1', nudgeSucceeded: true })
  const second = blog.repairProtocol({
    priorState: first.state,
    repairTerminalRun: first.run,
    terminalRun: 'asst-aabb-p2',
    nudgeSucceeded: true,
  })
  assert.equal(second.state, 'AabbRepairIssued')
  assert.equal(second.run, 'asst-aabb-p2')
  const duplicate = blog.repairProtocol({
    priorState: second.state,
    repairTerminalRun: second.run,
    terminalRun: second.run,
    nudgeSucceeded: true,
  })
  assert.equal(duplicate.state, 'AabbRepairIssued')
  assert.equal(duplicate.run, second.run)
})
