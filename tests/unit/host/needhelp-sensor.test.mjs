import assert from 'node:assert/strict'
import test from 'node:test'

import { idValue, okResult, providerRun, sessionId } from '../support/domain.mjs'
import {
  isNeedHelpDelta,
  tryDecodeReasoningDelta,
} from '../../../dist/Infrastructure/OpenCode/Codec/NeedHelpEventCodec.js'
import * as NeedHelpSensorModule from '../../../dist/Infrastructure/OpenCode/Host/NeedHelpSensor.js'
import { AssistancePrompt_stripSentinel as stripSentinel } from '../../../dist/Domain/AssistancePrompt.js'

const { NeedHelpSensor } = NeedHelpSensorModule

const method = (name) => {
  const prefix = `NeedHelpSensor__${name}`
  const key = Object.keys(NeedHelpSensorModule).find((entry) => entry === prefix || entry.startsWith(`${prefix}_`))
  if (!key) throw new Error(`NeedHelpSensor method ${name} not found`)
  return NeedHelpSensorModule[key]
}

const observe = method('Observe')
const isArmed = method('IsArmed')
const tryTake = method('TryTake')
const dropAttempt = method('DropAttempt')
const sentinelOf = method('get_Sentinel')

const delta = (session, message, field, text) => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    messageID: message,
    partID: 'prt_reasoning',
    field,
    delta: text,
  },
})

const waitFor = async (predicate, message, ms = 1000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const createSensor = (owned) => {
  const aborts = []
  const sensor = new NeedHelpSensor(
    (sid) => owned.has(idValue.session(sid)),
    async (sid) => {
      aborts.push(idValue.session(sid))
      return okResult(undefined)
    },
  )
  return { sensor, aborts }
}

test('HOST_027_control_sentinel_strips_from_persisted_reasoning_without_touching_surrounding_bytes', () => {
  const { sensor } = createSensor(new Set(['ses_a']))
  assert.equal(sentinelOf(sensor), '[NEEDHELP]')
  assert.equal(stripSentinel('before [NEEDHELP] after'), 'before  after')
  assert.equal(stripSentinel('[needhelp]'), '[needhelp]', 'case variants are ordinary reasoning bytes')
})

test('HOST_027_codec_accepts_reasoning_family_only_and_binds_provider_run_to_message_id', () => {
  const raw = delta('ses_a', 'asst_42', 'reasoning', '[NEEDHELP]')
  assert.equal(isNeedHelpDelta(raw), true)
  const decoded = tryDecodeReasoningDelta(raw)
  assert.ok(decoded)
  assert.equal(idValue.session(decoded.SessionId), 'ses_a')
  assert.equal(idValue.providerRun(decoded.ProviderRun), 'asst_42')
  assert.equal(decoded.Delta, '[NEEDHELP]')

  for (const field of ['text', 'tool', 'output']) {
    const other = delta('ses_a', 'asst_42', field, '[NEEDHELP]')
    assert.equal(isNeedHelpDelta(other), false)
    assert.equal(tryDecodeReasoningDelta(other), undefined)
  }
})

test('HOST_027_exact_sentinel_triggers_across_fragmented_reasoning_deltas', async () => {
  const { sensor, aborts } = createSensor(new Set(['ses_a']))
  assert.equal(sentinelOf(sensor), '[NEEDHELP]')

  observe(sensor, delta('ses_a', 'asst_1', 'thinking', 'prefix [NEED'))
  await new Promise((resolve) => setImmediate(resolve))
  assert.deepEqual(aborts, [])

  observe(sensor, delta('ses_a', 'asst_1', 'thinking', 'HELP] suffix'))
  await waitFor(() => aborts.length === 1, 'fragmented sentinel did not abort')
  assert.deepEqual(aborts, ['ses_a'])
  assert.equal(isArmed(sensor, sessionId('ses_a'), providerRun('asst_1')), true)
})

test('HOST_027_case_variants_unowned_and_visible_text_do_not_trigger', async () => {
  const { sensor, aborts } = createSensor(new Set(['ses_owned']))

  observe(sensor, delta('ses_owned', 'asst_1', 'reasoning', '[needhelp]'))
  observe(sensor, delta('ses_other', 'asst_2', 'reasoning', '[NEEDHELP]'))
  observe(sensor, delta('ses_owned', 'asst_3', 'text', '[NEEDHELP]'))
  await new Promise((resolve) => setImmediate(resolve))

  assert.deepEqual(aborts, [])
})

test('HOST_027_same_provider_run_aborts_once_but_different_run_is_independent', async () => {
  const { sensor, aborts } = createSensor(new Set(['ses_a']))

  observe(sensor, delta('ses_a', 'asst_1', 'reasoning', '[NEEDHELP]'))
  observe(sensor, delta('ses_a', 'asst_1', 'reasoning', '[NEEDHELP]'))
  await waitFor(() => aborts.length === 1, 'first run did not abort')
  assert.deepEqual(aborts, ['ses_a'])

  observe(sensor, delta('ses_a', 'asst_2', 'reasoning_content', '[NEEDHELP]'))
  await waitFor(() => aborts.length === 2, 'second run did not abort independently')
  assert.deepEqual(aborts, ['ses_a', 'ses_a'])

  assert.equal(tryTake(sensor, sessionId('ses_a'), providerRun('asst_1')), true)
  assert.equal(tryTake(sensor, sessionId('ses_a'), providerRun('asst_1')), false)
  assert.equal(isArmed(sensor, sessionId('ses_a'), providerRun('asst_1')), false)

  dropAttempt(sensor, sessionId('ses_a'), providerRun('asst_2'))
  assert.equal(isArmed(sensor, sessionId('ses_a'), providerRun('asst_2')), false)
})
