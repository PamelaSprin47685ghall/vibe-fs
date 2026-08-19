// Chronicle owner contract (ENFORCER-010/020/022/023/040/041/061).
// The semantic surface owns Host-facing translation; this test never constructs
// ToolSpec, HostToolContext or Fable result cases.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

test('WHAT[BD-006] CHRONICLE_canonical_text_trims_and_rejects_empty', () => {
  const ok = blog.canonicalText('  work entry  ')
  assert.equal(ok.ok, true)
  assert.equal(ok.value, 'work entry')

  const empty = blog.canonicalText('   ')
  assert.equal(empty.ok, false)
  assert.equal(empty.error, blog.emptyTextError)

  const nil = blog.canonicalText(undefined)
  assert.equal(nil.ok, false)
  assert.equal(nil.error, blog.emptyTextError)
})

test('WHAT[BD-010] CHRONICLE_live_cycle_requires_a_host_with_a_flight', () => {
  assert.equal(blog.hasLiveCycle(false, 'ses-blog'), false)
  assert.equal(blog.hasLiveCycle(false, 'ses-blog'), false)
  assert.equal(blog.hasLiveCycle(true, 'ses-blog'), true)
  assert.equal(blog.hasLiveCycle(true, 'ses-other'), true, 'flight is per host query, session passed through')
})

test('WHAT[BD-001] CHRONICLE_tip_enum_equals_catalog_field_names', () => {
  const fields = blog.tipFieldNames()
  assert.equal(fields.length, 120)
  assert.ok(fields.includes('primitive-obsession'))
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

test('WHAT[BD-017] CHRONICLE_empty_canonical_text_returns_public_consequence', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: '   ', tip: 'primitive-obsession' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'nothing-to-remember')
  assert.equal(result.error, blog.emptyTextError)
})

test('WHAT[BD-006] CHRONICLE_missing_tip_returns_rulebook_consequence', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: 'entry' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'missing-tip')
  assert.equal(result.error, enforcer.missingTipError)
})

test('WHAT[BD-007] CHRONICLE_unknown_tip_is_repaired_at_runtime', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: 'entry', tip: 'not-a-field' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'remembered')
  assert.equal(result.error, null)
})

test('WHAT[BD-009] CHRONICLE_valid_entry_with_identity_returns_fixed_ok', () => {
  const result = blog.execute({
    hasFlight: true,
    sessionId: 'ses-blog',
    providerRun: 'run-1',
    toolCallId: 'call-1',
    entry: '  entry  ',
    tip: 'primitive-obsession',
  })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'remembered')
})

test('WHAT[BD-009] CHRONICLE_valid_entry_without_tool_identity_still_returns_ok', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: 'entry', tip: 'primitive-obsession' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'remembered')
})
