// ENFORCER-060/061/064..068 — bounded interaction-repair protocol.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const valid = (messageId, overrides = {}) => ({
  messageId,
  parts: [{ tool: 'chronicle', callID: 'c1', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }],
  ...overrides,
})
const prose = (messageId) => ({ messageId, parts: [{ type: 'text', text: 'plain response' }] })
const invalid = (messageId) => ({ messageId, parts: [{ tool: 'chronicle', state: { status: 'completed', input: { text: 'no tip' } } }] })

test('WHAT[BD-017] ENFORCER_061_empty_calls_rebuilds_without_fatal', () => {
  const out = blog.protocol(prose('asst-prose'))
  assert.equal(out.state, 'ProjectMessages')
  assert.equal(out.fatal, null)
})

test('WHAT[BD-017] ENFORCER_061_invalid_tip_is_protocol_skip', () => {
  const out = blog.protocol(invalid('asst-skip'))
  assert.equal(out.state, 'ProjectMessages')
  assert.equal(out.fatal, null)
  assert.equal(enforcer.classifyAssistantStep(invalid('asst-skip')).acceptedCalls, 0)
})

test('WHAT[BD-010] ENFORCER_061_whitespace_provider_run_is_fail_closed', () => {
  const out = blog.protocol(valid('   '))
  assert.equal(out.state, 'ProjectMessages')
  assert.match(out.fatal, /no provable provider run/)
})

test('WHAT[BD-009] ENFORCER_061_exactly_one_valid_call_stops_physical_run', () => {
  const out = blog.protocol(valid('asst-valid'))
  assert.equal(out.state, 'StopPhysicalRun')
  assert.equal(out.fatal, null)
})

test('WHAT[BD-009] ENFORCER_064_two_valid_calls_require_protocol_repair', () => {
  const out = blog.protocol({
    messageId: 'asst-two',
    parts: [valid('asst-two').parts[0], valid('asst-two').parts[0]],
  })
  assert.equal(out.state, 'ProjectMessages')
  assert.match(out.fatal, /exactly one chronicle call/)
})

test('WHAT[BD-017] ENFORCER_066_first_pure_terminal_issues_interaction_nudge', () => {
  const out = blog.repairProtocol({ priorState: 'NoRecovery', terminalRun: 'asst-p1', nudgeSucceeded: true })
  assert.equal(out.state, 'InteractionNudgeIssued')
  assert.equal(out.run, 'asst-p1')
})

test('WHAT[BD-017] ENFORCER_067_second_different_pure_terminal_issues_aabb', () => {
  const out = blog.repairProtocol({
    priorState: 'InteractionNudgeIssued',
    terminalRun: 'asst-p2',
    repairTerminalRun: 'asst-p1',
    nudgeSucceeded: true,
  })
  assert.equal(out.state, 'AabbRepairIssued')
  assert.equal(out.run, 'asst-p2')
})

test('WHAT[BD-017] ENFORCER_067_reentry_of_same_terminal_is_idempotent', () => {
  const out = blog.repairProtocol({
    priorState: 'InteractionNudgeIssued',
    terminalRun: 'asst-p1',
    repairTerminalRun: 'asst-p1',
    nudgeSucceeded: true,
  })
  assert.equal(out.state, 'InteractionNudgeIssued')
  assert.equal(out.run, 'asst-p1')
})

test('WHAT[BD-017] ENFORCER_068_new_invalid_terminal_after_aabb_continues_while_fallback_budget_remains', () => {
  const out = blog.repairProtocol({
    priorState: 'AabbRepairIssued',
    terminalRun: 'asst-p3',
    repairTerminalRun: 'asst-p2',
    nudgeSucceeded: true,
  })
  assert.equal(out.state, 'AabbRepairIssued')
  assert.equal(out.run, 'asst-p3')
})

test('WHAT[BD-017] ENFORCER_068_new_invalid_terminal_after_aabb_exhausts_only_with_fallback_exhaustion', () => {
  const out = blog.repairProtocol({
    priorState: 'AabbRepairIssued',
    terminalRun: 'asst-p3',
    repairTerminalRun: 'asst-p2',
    nudgeSucceeded: true,
    fallbackExhausted: true,
  })
  assert.equal(out.state, 'ProtocolExhausted')
  assert.equal(out.run, null)
})

test('WHAT[BD-006] ENFORCER_064_empty_text_returns_public_tool_error', () => {
  const out = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: ' ', tip: 'primitive-obsession' })
  assert.equal(out.text, 'nothing-to-remember')
  assert.equal(out.error, blog.emptyTextError)
})

test('WHAT[BD-017] ENFORCER_066_nudge_hard_failure_falls_back_to_aabb', () => {
  const out = blog.repairProtocol({ priorState: 'NoRecovery', terminalRun: 'asst-p1', nudgeSucceeded: false })
  assert.equal(out.state, 'AabbRepairIssued')
})
