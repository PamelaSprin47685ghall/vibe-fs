// TIME-007 — bind SessionStartedAt once; elapsed is sampled per occurrence.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')

const first = '2026-08-14T08:00:00.000Z'
const later = '2026-08-14T08:05:00.000Z'

test('WHAT[TIME-007] TIME_007_session_started_at_is_bind_once_to_first_prompt_sample', () => {
  const initial = process.sessionStartBind(first, null)
  const rebound = process.sessionStartBind(later, initial)

  assert.equal(Date.parse(process.sessionStartAt(initial)), Date.parse(first))
  assert.equal(Date.parse(process.sessionStartAt(rebound)), Date.parse(first), 'later prompts cannot move SessionStartedAt')
})

test('WHAT[TIME-007] TIME_007_durable_session_start_fact_keeps_the_first_prompt_sample', () => {
  const ledger = process.createSessionStartLedger()
  process.appendSessionStart(ledger, 'ses_elapsed', first)
  process.appendSessionStart(ledger, 'ses_elapsed', later)

  const state = process.readSessionStart(ledger, 'ses_elapsed')
  assert.equal(Date.parse(process.sessionStartAt(state)), Date.parse(first))
})

test('WHAT[TIME-007] TIME_007_session_start_uses_bounded_projection_not_history_scan_or_mutable_counter', () => {
  for (const relative of [
    '../../../src/Wanxiangshu/Execution/Session/SessionStartedAtProjection.fs',
    '../../../src/Wanxiangshu/Execution/Session/SessionStartedAtLedger.fs',
  ]) {
    const source = readFileSync(new URL(relative, import.meta.url), 'utf8')
    for (const forbidden of ['XTrace', 'transcript', 'messages', 'Dictionary<', 'mutable ']) {
      assert.ok(!source.includes(forbidden), `${relative} must not depend on ${forbidden}`)
    }
  }
})

test('WHAT[TIME-007] TIME_007_elapsed_is_clamped_and_human_readable_in_both_languages', () => {
  const positive = 125000
  const negative = -5000

  const en = process.renderElapsed('en', positive)
  assert.match(en, /2 minutes 5 seconds/i)
  assert.match(en, /wall-clock|session/i)

  const zh = process.renderElapsed('zh', positive)
  assert.match(zh, /2 分钟 5 秒/)
  assert.match(zh, /会话|墙钟|实际时间|wall-clock|session/i)

  assert.match(process.renderElapsed('en', negative), /0 minutes 0 seconds/i)
  assert.match(process.renderElapsed('zh', negative), /0 分钟 0 秒/)
})

test('WHAT[TIME-007] GD_012_elapsed_is_fresh_per_occurrence_but_old_marker_bytes_stay_frozen', () => {
  const guideline = 'canonical pair guideline'
  const oldElapsed = process.renderElapsed('en', 30000)
  const newElapsed = process.renderElapsed('en', 90000)

  const oldMarker = process.composeWithElapsed(null, oldElapsed, null, guideline)
  const newMarker = process.composeWithElapsed(null, newElapsed, null, guideline)

  assert.match(oldMarker, /30 seconds/i)
  assert.match(newMarker, /1 minute 30 seconds/i)
  assert.notEqual(oldMarker, newMarker)
  assert.match(oldMarker, /30 seconds/i, 'historical MarkerText is an immutable occurrence value')
})

test('WHAT[TIME-007] GD_012_composition_order_is_tip_elapsed_estimate_guideline', () => {
  const marker = process.composeWithElapsed('tip', 'elapsed', 'estimate', 'guideline')
  assert.equal(marker, 'tip\n\nelapsed\n\nestimate\n\nguideline')
})
