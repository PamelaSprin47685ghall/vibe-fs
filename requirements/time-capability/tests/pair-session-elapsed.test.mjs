import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  providerLanguage,
  sessionId,
  stream,
  utcOffset,
} from '../../verification-system/tests/support/domain.mjs'

const {
  SessionStartedAtProjection_bind: bindStartedAt,
  SessionStartedAtProjection_startedAt: startedAt,
} = await import('../../../dist/Execution/Session/SessionStartedAtProjection.js')
const { composeWithElapsed, renderElapsed } = await import(
  '../../../dist/OpenCode/Host/PairProgrammingCalibration.js'
)

test('TIME_007_session_started_at_is_bind_once_to_first_prompt_sample', () => {
  const first = utcOffset('2026-08-14T08:00:00.000Z')
  const later = utcOffset('2026-08-14T08:05:00.000Z')

  const initial = bindStartedAt(first, undefined)
  const rebound = bindStartedAt(later, initial)

  assert.equal(startedAt(initial).getTime(), first.getTime())
  assert.equal(startedAt(rebound).getTime(), first.getTime(), 'later prompts cannot move SessionStartedAt')
})

test('TIME_007_durable_session_start_fact_keeps_the_first_prompt_sample', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-session-start-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)

  try {
    const sid = sessionId('ses_elapsed')
    const first = utcOffset('2026-08-14T08:00:00.000Z')
    const later = utcOffset('2026-08-14T08:05:00.000Z')
    const append = (started) =>
      agentJournal.appendAgent(
        stream.session(sid),
        undefined,
        agentFact('SessionStartedAtBound', { SessionId: sid, StartedAt: started }),
        opened.journal,
      )

    assert.equal((await append(first)).ok, true)
    assert.equal((await append(later)).ok, true)

    const state = agentJournal.snapshot(opened.journal).AgentProjections.Sessions.get(sid).SessionStartedAt
    assert.equal(startedAt(state).getTime(), first.getTime())
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('TIME_007_session_start_uses_bounded_projection_not_history_scan_or_mutable_counter', () => {
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

test('TIME_007_elapsed_is_clamped_and_human_readable_in_both_languages', () => {
  const positive = 125000
  const negative = -5000

  const en = renderElapsed(providerLanguage.english, positive)
  assert.match(en, /2 minutes 5 seconds/i)
  assert.match(en, /wall-clock|session/i)

  const zh = renderElapsed(providerLanguage.simplifiedChinese, positive)
  assert.match(zh, /2 分钟 5 秒/)
  assert.match(zh, /会话|墙钟|实际时间|wall-clock|session/i)

  assert.match(renderElapsed(providerLanguage.english, negative), /0 minutes 0 seconds/i)
  assert.match(renderElapsed(providerLanguage.simplifiedChinese, negative), /0 分钟 0 秒/)
})

test('GD_012_elapsed_is_fresh_per_occurrence_but_old_marker_bytes_stay_frozen', () => {
  const guideline = 'canonical pair guideline'
  const oldElapsed = renderElapsed(providerLanguage.english, 30000)
  const newElapsed = renderElapsed(providerLanguage.english, 90000)

  const oldMarker = composeWithElapsed(undefined, oldElapsed, undefined, guideline)
  const newMarker = composeWithElapsed(undefined, newElapsed, undefined, guideline)

  assert.match(oldMarker, /30 seconds/i)
  assert.match(newMarker, /1 minute 30 seconds/i)
  assert.notEqual(oldMarker, newMarker)
  assert.match(oldMarker, /30 seconds/i, 'historical MarkerText is an immutable occurrence value')
})

test('GD_012_composition_order_is_tip_elapsed_estimate_guideline', () => {
  const marker = composeWithElapsed('tip', 'elapsed', 'estimate', 'guideline')
  assert.equal(marker, 'tip\n\nelapsed\n\nestimate\n\nguideline')
})
