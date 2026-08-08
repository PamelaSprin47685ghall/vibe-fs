// PPT: PairProgrammingThoughtTransform — HOST-013 marker injection over raw messages.

import assert from 'node:assert/strict'
import test from 'node:test'

import { toList, listItems } from '../support/domain.mjs'

const {
  tryInject,
  isPairProgrammingThought,
  source,
  text,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

const inject = (session, raw) => {
  const out = tryInject(session, text, toList(raw))
  return out === undefined ? undefined : listItems(out)
}

const userMsg = (id, body = 'hello') => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: body }],
})

const assistantToolResult = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'tool-result', callID: 'call_1', result: 'done' }],
})

const assistantText = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'text', text: 'ok' }],
})

const markerIds = (messages) =>
  messages.filter((m) => isPairProgrammingThought(m)).map((m) => m.info.id)

test('PPT_source_is_the_frozen_side_channel_identity', () => {
  assert.equal(source, 'pair-programming-guideline')
  assert.ok(text.length > 0, 'frozen thought text must be non-empty')
  assert.equal(isPairProgrammingThought(null), false)
  assert.equal(isPairProgrammingThought({}), false)
  assert.equal(isPairProgrammingThought({ info: { source: 'other' } }), false)
  assert.equal(isPairProgrammingThought({ info: { source } }), true)
  assert.equal(isPairProgrammingThought({ parts: [] }), false, 'no info.source means not a marker')
})

test('PPT_tryInject_returns_none_without_anchor', () => {
  assert.equal(inject('ses_1', []), undefined)
  assert.equal(inject('ses_1', [assistantText('m1')]), undefined, 'assistant text alone is no anchor')
})

test('PPT_tryInject_injects_one_marker_after_user_anchor', () => {
  const raw = [userMsg('msg_1')]
  const out = inject('ses_1', raw)
  assert.ok(out, 'user anchor must produce a rewrite')
  assert.equal(out.length, 2)
  assert.deepEqual(out[0], raw[0], 'original user message must pass through verbatim')
  const marker = out[1]
  assert.equal(marker.info.role, 'assistant')
  assert.equal(marker.info.source, source)
  assert.equal(marker.info.synthetic, true)
  assert.match(marker.info.id, /^pair-programming-guideline-[0-9a-f]{24}$/)
  assert.equal(marker.parts.length, 1)
  assert.equal(marker.parts[0].type, 'tool')
  assert.equal(marker.parts[0].tool, 'guideline')
  assert.equal(marker.parts[0].callID, marker.info.id)
  assert.equal(marker.parts[0].state.status, 'completed')
  assert.equal(marker.parts[0].state.output, text, 'the marker carries the frozen guideline text')
})

test('PPT_tryInject_appends_one_final_marker_after_every_anchor', () => {
  const raw = [userMsg('msg_1'), assistantToolResult('msg_2')]
  const out = inject('ses_1', raw)
  assert.equal(out.length, 3, 'HOST-013 writes ONE final marker, not one per anchor')
  assert.deepEqual(out[0], raw[0])
  assert.deepEqual(out[1], raw[1], 'tool-result message stays verbatim')
  assert.equal(isPairProgrammingThought(out[2]), true)
})

test('PPT_tryInject_marker_id_is_stable_per_session_and_anchor', () => {
  const a = inject('ses_1', [userMsg('msg_1')])
  const b = inject('ses_1', [userMsg('msg_1')])
  assert.equal(a[1].info.id, b[1].info.id, 'same anchor + session yields the same id')

  const c = inject('ses_2', [userMsg('msg_1')])
  assert.notEqual(a[1].info.id, c[1].info.id, 'different session yields a different id')
})

test('PPT_tryInject_second_pass_is_a_noop_when_marker_already_present', () => {
  const once = inject('ses_1', [userMsg('msg_1')])
  assert.ok(once)
  const again = inject('ses_1', once)
  assert.ok(again, 're-transform returns a value')
  assert.deepEqual(again, once, 'the marker is stripped and re-added to the same shape — idempotent bytes')
})

test('PPT_tryInject_without_session_id_still_injects_stable_marker', () => {
  const out = inject(undefined, [userMsg('msg_1')])
  assert.ok(out)
  const again = inject(undefined, [userMsg('msg_1')])
  assert.equal(out[1].info.id, again[1].info.id, 'missing session id participates as empty string')
})

test('PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker', () => {
  const raw = [userMsg('msg_1', text)]
  const out = inject('ses_1', raw)
  assert.equal(isPairProgrammingThought(out[0]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 2, 'anchor still triggers a real marker after the user message')
})

test('PPT_tryInject_legacy_markers_are_cleaned_before_reinjection', () => {
  const legacy = {
    info: { id: 'pair-programming-thought-old', role: 'assistant', source: 'pair-programming-thought' },
    parts: [{ type: 'tool', tool: 'guideline', callID: 'old', state: { status: 'completed' } }],
  }
  const out = inject('ses_1', [legacy, userMsg('msg_1')])
  assert.ok(out)
  assert.equal(out.length, 2, 'the legacy marker is dropped, the user anchor gets one fresh marker')
  assert.deepEqual(markerIds(out), [out[1].info.id], 'only the fresh marker remains')
})
