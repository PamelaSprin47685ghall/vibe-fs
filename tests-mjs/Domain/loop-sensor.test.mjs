// tests-mjs/Domain/loop-sensor.test.mjs — LOOP-006/011 layer 2.
//
// Two contracts the pure detector cannot prove:
//
//   1. LOOP text deltas abort owned sessions exactly once per attempt
//   2. LoopKillArmed + confirmed failure advances Fallback exactly once
//      (the bridge half of TurnCompletionProgram on TurnAborted)

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
  loopDetector,
  loopSensor,
  physicalUser,
  promptDispatcher,
  runtimeNudge,
  sessionId,
} from '../domain.mjs'
import * as PromptDispatcher from '../../build/next/Application/Prompting/PromptDispatcher.js'

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
// Slow prior: need enough 4-grams of a single character to climb past HHI=0.03.
const loopText = (character = 'x') => character.repeat(4000)

test('LOOP_006_owned_low_diversity_stream_aborts_exactly_once', async () => {
  const aborts = []
  const sensor = loopSensor.create({
    owned: ['ses_loop'],
    abort: (sid) => {
      aborts.push(sid)
    },
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('a')))
  await wait(50)

  assert.deepEqual(aborts, ['ses_loop'])
  assert.equal(loopSensor.isArmed(sensor, 'ses_loop'), true)

  // Same attempt: more loop text must not re-abort (LOOP-006 idempotent).
  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('a')))
  await wait(30)
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
  await wait(20)

  assert.deepEqual(aborts, [])
  assert.equal(loopSensor.isArmed(sensor, 'ses_stranger'), false)
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
  await wait(50)
  assert.equal(aborts.length, 1)

  // LOOP-006: completion path clears the mark before the next attempt streams.
  loopSensor.clearArmed(sensor, 'ses_loop')
  loopSensor.resetDetector(sensor, 'ses_loop')
  assert.equal(loopSensor.isArmed(sensor, 'ses_loop'), false)

  loopSensor.observe(sensor, loopSensor.textDelta('ses_loop', loopText('c')))
  await wait(50)
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
