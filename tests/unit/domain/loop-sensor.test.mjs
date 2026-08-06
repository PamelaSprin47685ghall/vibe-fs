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
import {
  agentJournal,
  cursor,
  fallbackController,
  fallbackProjection,
  fold,
  loopEventCodec,
  loopSensor,
  physicalUser,
  promptDispatcher,
  runtimeNudge,
  sessionId,
} from '../support/domain.mjs'
import * as PromptDispatcher from '../../../dist/Application/Prompting/PromptDispatcher.js'

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
// Slow prior: enough single-character 4-grams to pull N_eff under 140.
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

test('LOOP_001_kill_arm_is_process_local_not_persisted', () => {
  // Facade construct takes only {owned, abort} — no journal / store / path.
  // Two independent sensors share nothing; TryArm is local HashSet state.
  const first = loopSensor.create({ owned: ['ses_a'], abort: () => {} })
  const second = loopSensor.create({ owned: ['ses_a'], abort: () => {} })

  assert.equal(loopSensor.tryArm(first, 'ses_a'), true)
  assert.equal(loopSensor.isArmed(first, 'ses_a'), true)
  assert.equal(loopSensor.isArmed(second, 'ses_a'), false)
  assert.equal(loopSensor.tryArm(second, 'ses_a'), true)
  assert.equal(loopSensor.isArmed(second, 'ses_a'), true)

  loopSensor.clearArmed(first, 'ses_a')
  assert.equal(loopSensor.isArmed(first, 'ses_a'), false)
  assert.equal(loopSensor.isArmed(second, 'ses_a'), true)
})

test('LOOP_002_sensor_observes_text_delta_only', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_text'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  // Non-text fields never decode → Observe is a pure no-op (no arm, no abort).
  for (const field of ['reasoning', 'model_thought', 'thinking']) {
    assert.equal(loopEventCodec.tryDecodeTextDelta(rawDelta('ses_text', field, loopText('r'))), undefined)
    loopSensor.observe(sensor, rawDelta('ses_text', field, loopText('r')))
  }
  await wait(8)
  assert.deepEqual(aborts, [])
  assert.equal(loopSensor.isArmed(sensor, 'ses_text'), false)

  // field=text is the only accepted stream; low diversity arms + aborts.
  assert.deepEqual(loopEventCodec.tryDecodeTextDelta(rawDelta('ses_text', 'text', 'abcd')), {
    sessionId: 'ses_text',
    messageId: 'msg_a',
    partId: 'prt_1',
    field: 'text',
    delta: 'abcd',
  })
  loopSensor.observe(sensor, loopSensor.textDelta('ses_text', loopText('t')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_text'])
  assert.equal(loopSensor.isArmed(sensor, 'ses_text'), true)
  // Sensor construct has no journal port; arm mark is the only side effect.
})

test('LOOP_007_unowned_and_reasoning_deltas_are_ignored', async () => {
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
  assert.equal(loopSensor.isArmed(sensor, 'ses_stranger'), false)
  assert.equal(loopSensor.isArmed(sensor, 'ses_owned'), false)

  // Reasoning never reaches the detector (codec fail-closed).
  assert.equal(
    loopEventCodec.tryDecodeTextDelta(rawDelta('ses_owned', 'reasoning', loopText('q'))),
    undefined,
  )
  loopSensor.observe(sensor, rawDelta('ses_owned', 'reasoning', loopText('q')))
  await wait(8)
  assert.deepEqual(aborts, [])

  // Owned text loop arms once; subsequent deltas on the same attempt are ignored.
  loopSensor.observe(sensor, loopSensor.textDelta('ses_owned', loopText('v')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_owned'])
  assert.equal(loopSensor.isArmed(sensor, 'ses_owned'), true)

  loopSensor.observe(sensor, loopSensor.textDelta('ses_owned', loopText('v')))
  await wait(10)
  assert.deepEqual(aborts, ['ses_owned'])
})

test('LOOP_006_owned_low_diversity_stream_aborts_exactly_once', async () => {
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
  assert.equal(loopSensor.isArmed(sensor, 'ses_loop'), true)

  // Same attempt: more loop text must not re-abort (LOOP-006 idempotent).
  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('a')))
  await wait(10)
  assert.deepEqual(aborts, ['ses_loop'])
})

test('LOOP_006_unowned_session_never_aborts', async () => {
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
  assert.equal(loopSensor.isArmed(sensor, 'ses_stranger'), false)
})

test('LOOP_006_reset_detector_preserves_loop_kill_armed', async () => {
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
  assert.equal(loopSensor.isArmed(sensor, 'ses_idle'), true)

  loopSensor.resetDetector(sensor, 'ses_idle')
  assert.equal(loopSensor.isArmed(sensor, 'ses_idle'), true, 'armed must survive idle reset')

  // Still armed → further deltas must not re-abort.
  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', loopText('i')))
  await wait(10)
  assert.deepEqual(aborts, ['ses_idle'])
})

test('LOOP_006_clear_armed_allows_next_attempt_to_arm_again', async () => {
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

  // LOOP-006: completion path clears the mark before the next attempt streams.
  loopSensor.clearArmed(sensor, 'ses_loop')
  loopSensor.resetDetector(sensor, 'ses_loop')
  assert.equal(loopSensor.isArmed(sensor, 'ses_loop'), false)

  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('c')))
  await wait(15)
  assert.deepEqual(aborts, ['ses_loop', 'ses_loop'])
})

test('LOOP_006_continuation_text_is_the_english_loop_nudge', () => {
  assert.deepEqual(runtimeNudge.loopContinueInstructions, [
    'Continue from the interruption without repeating already produced content.',
  ])
  assert.notEqual(runtimeNudge.loopContinue, runtimeNudge.providerRetry)
  assert.match(runtimeNudge.loopContinue, /Continue from the interruption/)
  assert.match(runtimeNudge.providerRetry, /Continue after provider failure/)
})

test('LOOP_006_armed_abort_bridges_to_fallback_advance_once', () => {
  // Layer-2 half of TurnCompletionProgram on TurnAborted:
  //   isArmed → ClearArmed → FallbackController.recordConfirmedFailure("loop-kill")
  // The sensor only arms; the controller is the single cursor writer.
  const directory = mkdtempSync(join(tmpdir(), 'wxs-loop-bridge-'))
  const created = agentJournal.create({ directory, runtime: 'rt_loop' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  const sensor = loopSensor.create({
    owned: ['ses_bridge'],
    abort: () => {},
  })

  try {
    const journal = created.journal
    const SESSION = 'ses_bridge'
    const runtime = PromptDispatcher.forJournal(journal)
    const accepted = PromptDispatcher.Runtime__AcceptHumanRoot(
      runtime,
      sessionId(SESSION),
      physicalUser('msg_u1'),
      'fast-coder',
    )
    assert.equal(accepted.tag, 0, `AcceptHumanRoot failed: ${accepted.fields?.[0]}`)

    // Arm as the sensor would after detecting LOOP.
    assert.equal(loopSensor.tryArm(sensor, SESSION), true)
    assert.equal(loopSensor.isArmed(sensor, SESSION), true)

    // Bridge: completion path clears the mark, then records the failure.
    assert.equal(loopSensor.isArmed(sensor, SESSION), true)
    loopSensor.clearArmed(sensor, SESSION)
    assert.equal(loopSensor.isArmed(sensor, SESSION), false)

    const first = fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_1',
      'loop-kill',
    )
    assert.deepEqual(first, { ok: true, outcome: 'Advanced' })

    const snapshot = promptDispatcher.journalSnapshot(journal)
    const state = fallbackProjection.read(fold.session(snapshot, SESSION).Fallback)
    assert.deepEqual(
      { offset: state.offset, failures: state.failures, exhausted: state.exhausted },
      { offset: 1, failures: 1, exhausted: false },
    )

    // Same provider run observed twice (idle + retry race) advances once.
    const second = fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_1',
      'loop-kill',
    )
    assert.deepEqual(second, { ok: true, outcome: 'AlreadyRecorded' })

    const again = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    assert.deepEqual(
      { offset: again.offset, failures: again.failures },
      { offset: 1, failures: 1 },
    )

    // Without LoopKillArmed, a plain user abort would not reach recordConfirmedFailure
    // (TurnCompletionProgram keeps TerminalOutcome.Aborted). That branch is structural
    // in production; the sensor's clear above is what makes a later unarmed abort inert.
    assert.equal(loopSensor.isArmed(sensor, SESSION), false)
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('LOOP_008_loop_kill_advances_cursor_only_via_fallback_controller', () => {
  // LOOP-008: sensor never mutates Offset. Cursor arithmetic stays in
  // FallbackController (FALLBACK-003). Same ProviderRun is deduped once.
  // (Reuses the bridge shape already proven by LOOP_006_armed_abort_bridges_….)
  const directory = mkdtempSync(join(tmpdir(), 'wxs-loop-008-'))
  const created = agentJournal.create({ directory, runtime: 'rt_loop_008' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  // Sensor has no journal handle — it cannot be a second Offset writer.
  const sensor = loopSensor.create({ owned: ['ses_008'], abort: () => {} })

  try {
    const journal = created.journal
    const SESSION = 'ses_008'
    const runtime = PromptDispatcher.forJournal(journal)
    const accepted = PromptDispatcher.Runtime__AcceptHumanRoot(
      runtime,
      sessionId(SESSION),
      physicalUser('msg_u008'),
      'fast-coder',
    )
    assert.equal(accepted.tag, 0, `AcceptHumanRoot failed: ${accepted.fields?.[0]}`)

    assert.equal(loopSensor.tryArm(sensor, SESSION), true)
    loopSensor.clearArmed(sensor, SESSION)

    const before = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    assert.equal(before.offset, 0)

    const first = fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_008',
      'loop-kill',
    )
    assert.deepEqual(first, { ok: true, outcome: 'Advanced' })

    const mid = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    assert.deepEqual(
      { offset: mid.offset, failures: mid.failures },
      { offset: 1, failures: 1 },
    )

    const second = fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_008',
      'loop-kill',
    )
    assert.deepEqual(second, { ok: true, outcome: 'AlreadyRecorded' })

    const after = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    assert.deepEqual(
      { offset: after.offset, failures: after.failures },
      { offset: 1, failures: 1 },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})
