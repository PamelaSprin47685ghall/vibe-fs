// ENFORCER small-branch coverage: assistant-step and Blog-part predicates.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const classify = (messageId, parts) => enforcer.classifyAssistantStep({ messageId, parts })

test('WHAT[BD-017] ENFORCER_last_assistant_step_ignores_malformed_messages', () => {
  assert.equal(classify('', [null]).providerRun, null)
  assert.equal(classify('', [{ info: { id: 'x', role: 'user' } }]).acceptedCalls, 0)
  assert.equal(classify('', [{ info: { id: 'x' } }]).acceptedCalls, 0)
  assert.equal(classify('', [{ info: { role: 'assistant' } }]).acceptedCalls, 0)

  const bare = classify('a-1', [])
  assert.equal(bare.providerRun, 'a-1')
  assert.equal(bare.acceptedCalls, 0)

  const full = classify('a-2', [{ tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 't' } } }])
  assert.equal(full.providerRun, 'a-2')
  assert.equal(full.acceptedCalls, 1)
})

test('WHAT[BD-017] ENFORCER_bad_tip_decode_is_protocol_skip_and_rebuilds', () => {
  const out = classify('asst-skip', [{ tool: 'chronicle', state: { status: 'completed', input: { text: 'no tip' } } }])
  assert.equal(out.protocol, 'ProjectMessages')
  assert.equal(out.acceptedCalls, 0)
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

test('WHAT[BD-017] ENFORCER_completed_blog_part_in_empty_arm_rebuilds', () => {
  const out = classify('asst-completed-skip', [
    { tool: 'chronicle', state: { status: 'completed', input: {} } },
    { type: 'text', text: 'plain' },
  ])
  assert.equal(out.hasBlogToolPart, true)
  assert.equal(out.acceptedCalls, 0)
})

test('WHAT[BD-017] ENFORCER_interrupted_statusless_blog_part_aabbs', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { metadata: { interrupted: true } } })
  assert.equal(out.blogPartInterrupted, true)
  assert.equal(out.hasFailedBlogAttempt, true)
})

test('WHAT[BD-017] ENFORCER_uninterrupted_statusless_blog_part_rebuilds', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { metadata: { interrupted: false } } })
  assert.equal(out.blogPartInterrupted, false)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_running_blog_part_projects_raw', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { status: 'running' } })
  assert.equal(out.hasIncompleteBlogTool, true)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_unknown_status_blog_part_is_not_a_failed_attempt', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: { status: 'weird', metadata: { interrupted: false } } })
  assert.equal(out.hasIncompleteBlogTool, false)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_stateless_blog_part_has_no_status', () => {
  const out = blog.classifyPart({ tool: 'chronicle' })
  assert.equal(out.status, null)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_statusless_blog_part_is_not_incomplete', () => {
  const out = blog.classifyPart({ tool: 'chronicle', state: {} })
  assert.equal(out.hasIncompleteBlogTool, false)
  assert.equal(out.hasFailedBlogAttempt, false)
})

test('WHAT[BD-017] ENFORCER_null_part_in_transcript_is_ignored', () => {
  const out = classify('asst-nullpart', [null])
  assert.equal(out.acceptedCalls, 0)
  assert.equal(out.hasBlogToolPart, false)
})
