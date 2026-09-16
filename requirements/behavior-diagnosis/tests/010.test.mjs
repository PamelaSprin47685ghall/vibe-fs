// BD-010: Cycle provider-run identity fail-closed gate
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const tip = () => enforcer.fieldNames()[0]
const call = (text, evidence) => ({
  text,
  tipField: tip(),
  ...(evidence === undefined ? {} : { evidence }),
})
const valid = (messageId, overrides = {}) => ({
  messageId,
  parts: [{ tool: 'chronicle', callID: 'c1', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }],
  ...overrides,
})
const classify = (messageId, parts) => enforcer.classifyAssistantStep({ messageId, parts })

test('WHAT[BD-010] CHRONICLE_live_cycle_requires_a_host_with_a_flight', () => {
  assert.equal(blog.hasLiveCycle(false, 'ses-blog'), false)
  assert.equal(blog.hasLiveCycle(false, 'ses-blog'), false)
  assert.equal(blog.hasLiveCycle(true, 'ses-blog'), true)
  assert.equal(blog.hasLiveCycle(true, 'ses-other'), true, 'flight is per host query, session passed through')
})

test('WHAT[BD-010] CHRONICLE_no_live_cycle_rejects_and_aborts_the_session', () => {
  const result = blog.execute({ hasFlight: false, sessionId: 'ses-blog', entry: 'x', tip: 'primitive-obsession' })
  assert.equal(result.ok, false)
  assert.equal(result.error, blog.noLiveCycleError)
  assert.equal(result.abortedSession, 'ses-blog')
})

test('WHAT[BD-010] CHRONICLE_no_live_cycle_does_not_abort_a_blank_session', () => {
  const result = blog.execute({ hasFlight: false, sessionId: '', entry: 'x', tip: 'primitive-obsession' })
  assert.equal(result.ok, false)
  assert.equal(result.error, blog.noLiveCycleError)
  assert.equal(result.abortedSession, null)
})

test('WHAT[BD-010] ENFORCER_043_valid_cycle_requires_nonempty_text', () => {
  assert.equal(enforcer.isValidCycle(enforcer.canonicalCycle(call('content'))), true)
  assert.equal(enforcer.isValidCycle(enforcer.canonicalCycle(call('   '))), false)
})

test('WHAT[BD-010] ENFORCER_061_whitespace_provider_run_is_fail_closed', () => {
  const out = blog.protocol(valid('   '))
  assert.equal(out.state, 'ProjectMessages')
  assert.match(out.fatal, /no provable provider run/)
})

test('WHAT[BD-010] ENFORCER_whitespace_message_id_fails_cycle_validation', () => {
  const out = classify('   ', [{ tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }])
  assert.equal(out.providerRun, null)
})

test('WHAT[BD-010] ENFORCER_blog_call_with_name_field_and_lowercase_id_commits', () => {
  const out = classify('asst-name', [{ name: 'chronicle', callId: 'c-low', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'entry' } } }])
  assert.equal(out.acceptedCalls, 1)
  assert.equal(out.protocol, 'CommitCandidate')
})

test('WHAT[BD-010] ENFORCER_043_no_provable_provider_run_fails_closed', () => {
  const result = enforcer.validateProviderRun('')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'no provable provider run')
})

test('WHAT[BD-010] ENFORCER_043_whitespace_provider_run_fails_closed', () => {
  const result = enforcer.validateProviderRun('   ')
  assert.equal(result.ok, false)
  assert.match(result.error, /no provable provider run/)
})

test('WHAT[BD-010] ENFORCER_043_provider_run_identity_is_preserved', () => {
  const result = enforcer.validateProviderRun('asst-identity')
  assert.equal(result.ok, true)
  assert.equal(result.providerRun, 'asst-identity')
})
