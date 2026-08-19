// tests/unit/Domain/loop-sensor.test.mjs — LOOP-001/002/006/007/008/011.
//
// Sensor edge contracts the pure detector cannot prove:
//
//   1. LoopKillArmed is process-local, not journal-persisted (LOOP-001)
//   2. Observe consumes field=text only; never writes domain facts (LOOP-002)
//   3. Owned stream aborts exactly once per attempt (LOOP-006)
//   4. Unowned / armed / reasoning deltas are ignored (LOOP-007)
//   5. Loop-kill advances Fallback only via FallbackController (LOOP-008)

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as fallback from '../../../dist/Participant/Provider/Attempt/Fallback/HandleSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import * as providerLanguage from '../../../dist/Participant/Provider/LanguageSurface.js'

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
const resourceLines = (semanticPath) =>
  providerLanguage.readText('English', semanticPath)
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .trimEnd()
    .split('\n')
const runtimeNudge = {
  loopContinueInstructions: resourceLines('runtime/loop-continue'),
  loopContinue: providerLanguage.readText('English', 'runtime/loop-continue'),
  providerRetry: providerLanguage.readText('English', 'runtime/provider-retry'),
}
// Enough repeated text to drive weighted-distinct tokens below the calibrated midpoint.
const loopText = (character = 'x') => character.repeat(4000)

const rawDelta = (session, field, text) => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    messageID: 'msg_a',
    partID: 'prt_1',
    field,
    delta: text,
  },
})

test('WHAT[DG-008] LOOP_001_kill_arm_is_process_local_not_persisted', async () => {
  // Owner construct takes only {owned, abort} — no journal / store / path.
  // Two independent sensors share nothing; the one-shot typed cause is local state.
  const first = loopSensor.create({ owned: ['ses_a'], abort: () => {} })
  const second = loopSensor.create({ owned: ['ses_a'], abort: () => {} })

  loopSensor.observe(first, loopSensor.textDelta('ses_a', loopText('p')))
  await wait(15)
  assert.equal(loopSensor.consumeAbortCause(first, 'ses_a'), 'LoopKill')
  assert.equal(loopSensor.consumeAbortCause(second, 'ses_a'), 'External')

  loopSensor.observe(second, loopSensor.textDelta('ses_a', loopText('p')))
  await wait(15)
  assert.equal(loopSensor.consumeAbortCause(second, 'ses_a'), 'LoopKill')
  assert.equal(loopSensor.consumeAbortCause(first, 'ses_a'), 'External')
})

test('WHAT[DG-002] LOOP_002_sensor_observes_text_delta_only', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_text'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  // Non-textual fields never decode → Observe is a pure no-op (no arm, no abort).
  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    assert.equal(loopDetector.tryDecodeTextDelta(rawDelta('ses_text', field, loopText('r'))), null)
    loopSensor.observe(sensor, rawDelta('ses_text', field, loopText('r')))
  }
  await wait(8)
  assert.deepEqual(aborts, [])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_text'), 'External')

  // field=text is accepted; low diversity arms + aborts.
  assert.deepEqual(loopDetector.tryDecodeTextDelta(rawDelta('ses_text', 'text', 'abcd')), {
    sessionId: 'ses_text',
    messageId: 'msg_a',
    partId: 'prt_1',
    field: 'text',
    delta: 'abcd',
  })
  loopSensor.observe(sensor, loopSensor.textDelta('ses_text', loopText('t')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_text'])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_text'), 'LoopKill')
  // Sensor construct has no journal port; arm mark is the only side effect.
})

test('WHAT[DG-010] LOOP_007_unowned_and_armed_deltas_are_ignored', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_owned'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  // Non-owned session: Observe returns without arming or aborting.
  loopSensor.observe(sensor, loopSensor.textDelta('ses_stranger', loopText('u')))
  await wait(8)
  assert.deepEqual(aborts, [])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_stranger'), 'External')
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_owned'), 'External')

  // Owned text loop arms once; subsequent deltas on the same attempt are ignored.
  loopSensor.observe(sensor, loopSensor.textDelta('ses_owned', loopText('v')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_owned'])

  loopSensor.observe(sensor, loopSensor.textDelta('ses_owned', loopText('v')))
  await wait(10)
  assert.deepEqual(aborts, ['ses_owned'])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_owned'), 'LoopKill')
})

test('WHAT[DG-002] LOOP_007_reasoning_deltas_trigger_loop_kill', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_owned'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  // Reasoning stream decodes and triggers loop kill when low diversity.
  assert.deepEqual(
    loopDetector.tryDecodeTextDelta(rawDelta('ses_owned', 'reasoning', loopText('q'))),
    {
      sessionId: 'ses_owned',
      messageId: 'msg_a',
      partId: 'prt_1',
      field: 'reasoning',
      delta: loopText('q'),
    },
  )
  loopSensor.observe(sensor, rawDelta('ses_owned', 'reasoning', loopText('q')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_owned'])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_owned'), 'LoopKill')
})

test('WHAT[DG-007] LOOP_006_owned_low_diversity_stream_aborts_exactly_once', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_loop'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('a')))
  await wait(15)

  assert.deepEqual(aborts, ['ses_loop'])

  // Same attempt: more loop text must not re-abort (LOOP-006 idempotent).
  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('a')))
  await wait(10)
  assert.deepEqual(aborts, ['ses_loop'])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_loop'), 'LoopKill')
})

test('WHAT[DG-007] LOOP_006_unowned_session_never_aborts', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_owned'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_stranger', loopText('b')))
  await wait(8)

  assert.deepEqual(aborts, [])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_stranger'), 'External')
})

test('WHAT[DG-006] LOOP_006_reset_detector_preserves_loop_kill_armed', async () => {
  // Production SessionIdle calls ResetDetector BEFORE reconcile/TurnAborted.
  // If ResetDetector cleared LoopKillArmed, the AABB bridge would always miss.
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_idle'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', loopText('i')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_idle'])

  loopSensor.resetDetector(sensor, 'ses_idle')

  // Still armed → further deltas must not re-abort.
  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', loopText('i')))
  await wait(10)
  assert.deepEqual(aborts, ['ses_idle'])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_idle'), 'LoopKill', 'typed cause must survive idle reset')
})

test('WHAT[DG-007] LOOP_006_clear_armed_allows_next_attempt_to_arm_again', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_loop'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('c')))
  await wait(15)
  assert.equal(aborts.length, 1)

  // LOOP-006: reconcile consumes the one-shot cause before the next attempt streams.
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_loop'), 'LoopKill')
  loopSensor.resetDetector(sensor, 'ses_loop')

  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('c')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_loop', 'ses_loop'])
  assert.equal(loopSensor.consumeAbortCause(sensor, 'ses_loop'), 'LoopKill')
})

test('WHAT[DG-011] LOOP_006_continuation_text_is_the_english_loop_nudge', () => {
  assert.deepEqual(runtimeNudge.loopContinueInstructions, [
    'Continue from the interruption.',
    '',
    'Do not repeat content already produced unless correcting it is necessary.',
    'The interruption does not change the charge.',
  ])
  assert.notEqual(runtimeNudge.loopContinue, runtimeNudge.providerRetry)
  assert.match(runtimeNudge.loopContinue, /Continue from the interruption/)
  assert.match(runtimeNudge.providerRetry, /The previous physical attempt did not complete/)
})

test('WHAT[DG-009] LOOP_006_armed_abort_bridges_to_fallback_advance_once', async () => {
  // Layer-2 half of TurnCompletionProgram on TurnAborted:
  //   isArmed → ClearArmed → FallbackController.recordConfirmedFailure("loop-kill")
  // The sensor only arms; the controller is the single cursor writer.
  const directory = mkdtempSync(join(tmpdir(), 'wxs-loop-bridge-'))
  const created = await journal.JournalSurface_bootWithWriterId(
    directory,
    'writer-loop-bridge',
    'rt_loop',
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  const sensor = loopSensor.create({
    owned: ['ses_bridge'],
    abort: () => {},
  })

  try {
    const handle = created.journal
    const SESSION = 'ses_bridge'
    const accepted = await dispatch.acceptHumanRoot(handle, SESSION, 'msg_u1', 'fast-coder')
    assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)

    // Arm through the real streaming path, then consume the typed Host outcome.
    loopSensor.observe(sensor, loopSensor.textDelta(SESSION, loopText('g')))
    await wait(15)
    assert.equal(loopSensor.consumeAbortCause(sensor, SESSION), 'LoopKill')

    const first = await fallback.recordConfirmedFailure(
      handle,
      fallback.defaultAutoRecoveryBudget,
      SESSION,
      'msg_asst_1',
      'loop-kill',
    )
    assert.deepEqual(first, { ok: true, outcome: 'Advanced' })

    const state = fallback.snapshot(handle, SESSION)
    assert.deepEqual(
      { offset: state.offset, failures: state.failures, exhausted: state.exhausted },
      { offset: 1, failures: 1, exhausted: false },
    )

    // Same provider run observed twice (idle + retry race) advances once.
    const second = await fallback.recordConfirmedFailure(
      handle,
      fallback.defaultAutoRecoveryBudget,
      SESSION,
      'msg_asst_1',
      'loop-kill',
    )
    assert.deepEqual(second, { ok: true, outcome: 'AlreadyRecorded' })

    const again = fallback.snapshot(handle, SESSION)
    assert.deepEqual(
      { offset: again.offset, failures: again.failures },
      { offset: 1, failures: 1 },
    )

    // The cause is one-shot; a later unarmed abort is External and cannot advance fallback.
    assert.equal(loopSensor.consumeAbortCause(sensor, SESSION), 'External')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[DG-012] LOOP_008_loop_kill_advances_cursor_only_via_fallback_controller', async () => {
  // LOOP-008: sensor never mutates Offset. Cursor arithmetic stays in
  // FallbackController (FALLBACK-003). Same ProviderRun is deduped once.
  // (Reuses the bridge shape already proven by LOOP_006_armed_abort_bridges_….)
  const directory = mkdtempSync(join(tmpdir(), 'wxs-loop-008-'))
  const created = await journal.JournalSurface_bootWithWriterId(
    directory,
    'writer-loop-008',
    'rt_loop_008',
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  // Sensor has no journal handle — it cannot be a second Offset writer.
  const sensor = loopSensor.create({ owned: ['ses_008'], abort: () => {} })

  try {
    const handle = created.journal
    const SESSION = 'ses_008'
    const accepted = await dispatch.acceptHumanRoot(handle, SESSION, 'msg_u008', 'fast-coder')
    assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)

    loopSensor.observe(sensor, loopSensor.textDelta(SESSION, loopText('h')))
    await wait(15)
    assert.equal(loopSensor.consumeAbortCause(sensor, SESSION), 'LoopKill')

    const before = fallback.snapshot(handle, SESSION)
    assert.equal(before.offset, 0)

    const first = await fallback.recordConfirmedFailure(
      handle,
      fallback.defaultAutoRecoveryBudget,
      SESSION,
      'msg_asst_008',
      'loop-kill',
    )
    assert.deepEqual(first, { ok: true, outcome: 'Advanced' })

    const mid = fallback.snapshot(handle, SESSION)
    assert.deepEqual(
      { offset: mid.offset, failures: mid.failures },
      { offset: 1, failures: 1 },
    )

    const second = await fallback.recordConfirmedFailure(
      handle,
      fallback.defaultAutoRecoveryBudget,
      SESSION,
      'msg_asst_008',
      'loop-kill',
    )
    assert.deepEqual(second, { ok: true, outcome: 'AlreadyRecorded' })

    const after = fallback.snapshot(handle, SESSION)
    assert.deepEqual(
      { offset: after.offset, failures: after.failures },
      { offset: 1, failures: 1 },
    )
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[DG-009] LOOP_008_budget_exhaustion_is_final_and_writes_the_exhausted_fact', async () => {
  // FALLBACK-005: the 12th consecutive failure is immediately final — the
  // controller returns Exhausted and writes FallbackExhausted; a 13th
  // confirmed failure on the same run stays AlreadyRecorded (nothing written).
  const directory = mkdtempSync(join(tmpdir(), 'wxs-loop-exhaust-'))
  const created = await journal.JournalSurface_bootWithWriterId(
    directory,
    'writer-loop-exhaust',
    'rt_loop_ex',
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  const handle = created.journal
  const SESSION = 'ses_exhaust'

  try {
    const accepted = await dispatch.acceptHumanRoot(handle, SESSION, 'msg_u1', 'fast-coder')
    assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)

    for (let i = 1; i <= 11; i += 1) {
      const advanced = await fallback.recordConfirmedFailure(
        handle,
        fallback.defaultAutoRecoveryBudget,
        SESSION,
        `run-${i}`,
        'loop-kill',
      )
      assert.deepEqual(advanced, { ok: true, outcome: 'Advanced' }, `attempt ${i} must advance`)
    }

    const twelfth = await fallback.recordConfirmedFailure(
      handle,
      fallback.defaultAutoRecoveryBudget,
      SESSION,
      'run-12',
      'loop-kill',
    )
    assert.deepEqual(twelfth, { ok: true, outcome: 'Exhausted' })

    const exhaustedState = fallback.snapshot(handle, SESSION)
    assert.equal(exhaustedState.failures, 12, 'twelve confirmed failures recorded')
    assert.equal(exhaustedState.exhausted, true, 'FallbackExhausted folded')

    const thirteenth = await fallback.recordConfirmedFailure(
      handle,
      fallback.defaultAutoRecoveryBudget,
      SESSION,
      'run-13',
      'loop-kill',
    )
    assert.deepEqual(thirteenth, { ok: true, outcome: 'AlreadyRecorded' }, 'post-exhaustion is a no-op')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
