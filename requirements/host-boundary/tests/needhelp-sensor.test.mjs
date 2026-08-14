import assert from 'node:assert/strict'
import test from 'node:test'

import { idValue, okResult, providerRun, sessionId } from '../../verification-system/tests/support/domain.mjs'
import {
  isNeedHelpDelta,
  isNeedHelpRelevantEvent,
  tryDecodeDelta,
  tryDecodePartUpdated,
  tryDecodeReasoningDelta,
} from '../../../dist/Interaction/Dispatch/OpenCode/NeedHelpEventCodec.js'
import * as NeedHelpSensorModule from '../../../dist/Interaction/Dispatch/OpenCode/NeedHelpSensor.js'
import { AssistancePrompt_stripSentinel as stripSentinel } from '../../../dist/Interaction/Dispatch/AssistancePrompt.js'

const { NeedHelpSensor } = NeedHelpSensorModule

const method = (name) => {
  const prefix = `NeedHelpSensor__${name}`
  const key = Object.keys(NeedHelpSensorModule).find((entry) => entry === prefix || entry.startsWith(`${prefix}_`))
  if (!key) throw new Error(`NeedHelpSensor method ${name} not found`)
  return NeedHelpSensorModule[key]
}

const observe = method('Observe')
const isArmed = method('IsArmed')
const hasArmedSession = method('HasArmedSession')
const tryTake = method('TryTake')
const dropAttempt = method('DropAttempt')
const sentinelOf = method('get_Sentinel')
const isReasoningDelta = method('IsReasoningDelta')

const partUpdated = (session, message, type = 'reasoning', partID = 'prt_reasoning') => ({
  type: 'message.part.updated',
  properties: {
    sessionID: session,
    part: { id: partID, sessionID: session, messageID: message, type, text: '' },
    time: 1,
  },
})

const delta = (session, message, field, text, partID = 'prt_reasoning') => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    messageID: message,
    partID,
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

test('HOST_027_codec_correlates_real_host_part_kind_with_text_delta_and_keeps_legacy_direct_field_compat', () => {
  const updated = partUpdated('ses_a', 'asst_42', 'reasoning')
  assert.equal(isNeedHelpRelevantEvent(updated), true)
  const part = tryDecodePartUpdated(updated)
  assert.ok(part)
  assert.equal(idValue.session(part.SessionId), 'ses_a')
  assert.equal(idValue.providerRun(part.ProviderRun), 'asst_42')
  assert.equal(part.PartId, 'prt_reasoning')
  assert.equal(part.PartType, 'reasoning')

  const realDelta = delta('ses_a', 'asst_42', 'text', '[NEEDHELP]')
  assert.equal(isNeedHelpRelevantEvent(realDelta), true)
  assert.equal(isNeedHelpDelta(realDelta), false, 'field=text is not itself proof of reasoning')
  const decodedReal = tryDecodeDelta(realDelta)
  assert.ok(decodedReal)
  assert.equal(decodedReal.Field, 'text')
  assert.equal(decodedReal.Delta, '[NEEDHELP]')

  const legacy = delta('ses_a', 'asst_42', 'reasoning', '[NEEDHELP]')
  assert.equal(isNeedHelpDelta(legacy), true)
  const decodedLegacy = tryDecodeReasoningDelta(legacy)
  assert.ok(decodedLegacy)
  assert.equal(decodedLegacy.Delta, '[NEEDHELP]')
})

test('HOST_027_exact_sentinel_triggers_across_fragmented_real_host_reasoning_text_deltas', async () => {
  const { sensor, aborts } = createSensor(new Set(['ses_a']))
  assert.equal(sentinelOf(sensor), '[NEEDHELP]')

  observe(sensor, partUpdated('ses_a', 'asst_1', 'reasoning'))
  const first = delta('ses_a', 'asst_1', 'text', 'prefix [NEED')
  assert.equal(isReasoningDelta(sensor, first), true)
  observe(sensor, first)
  await new Promise((resolve) => setImmediate(resolve))
  assert.deepEqual(aborts, [])

  observe(sensor, delta('ses_a', 'asst_1', 'text', 'HELP] suffix'))
  await waitFor(() => aborts.length === 1, 'fragmented sentinel did not abort')
  assert.deepEqual(aborts, ['ses_a'])
  assert.equal(isArmed(sensor, sessionId('ses_a'), providerRun('asst_1')), true)
  assert.equal(hasArmedSession(sensor, sessionId('ses_a')), true, 'coarse abort routing sees typed NEEDHELP ownership')
  assert.equal(hasArmedSession(sensor, sessionId('ses_other')), false)
})

test('HOST_027_case_variants_unowned_and_visible_text_do_not_trigger', async () => {
  const { sensor, aborts } = createSensor(new Set(['ses_owned']))

  observe(sensor, partUpdated('ses_owned', 'asst_1', 'reasoning'))
  observe(sensor, delta('ses_owned', 'asst_1', 'text', '[needhelp]'))

  observe(sensor, partUpdated('ses_other', 'asst_2', 'reasoning'))
  observe(sensor, delta('ses_other', 'asst_2', 'text', '[NEEDHELP]'))

  observe(sensor, partUpdated('ses_owned', 'asst_3', 'text', 'prt_visible'))
  const visible = delta('ses_owned', 'asst_3', 'text', '[NEEDHELP]', 'prt_visible')
  assert.equal(isReasoningDelta(sensor, visible), false)
  observe(sensor, visible)
  await new Promise((resolve) => setImmediate(resolve))

  assert.deepEqual(aborts, [])
})

test('HOST_027_same_provider_run_aborts_once_but_different_run_is_independent', async () => {
  const { sensor, aborts } = createSensor(new Set(['ses_a']))

  observe(sensor, partUpdated('ses_a', 'asst_1', 'reasoning'))
  observe(sensor, delta('ses_a', 'asst_1', 'text', '[NEEDHELP]'))
  observe(sensor, delta('ses_a', 'asst_1', 'text', '[NEEDHELP]'))
  await waitFor(() => aborts.length === 1, 'first run did not abort')
  assert.deepEqual(aborts, ['ses_a'])

  observe(sensor, partUpdated('ses_a', 'asst_2', 'reasoning'))
  observe(sensor, delta('ses_a', 'asst_2', 'text', '[NEEDHELP]'))
  await waitFor(() => aborts.length === 2, 'second run did not abort independently')
  assert.deepEqual(aborts, ['ses_a', 'ses_a'])

  assert.equal(tryTake(sensor, sessionId('ses_a'), providerRun('asst_1')), true)
  assert.equal(tryTake(sensor, sessionId('ses_a'), providerRun('asst_1')), false)
  assert.equal(isArmed(sensor, sessionId('ses_a'), providerRun('asst_1')), false)
  assert.equal(hasArmedSession(sensor, sessionId('ses_a')), true, 'second run still owns the session abort')

  dropAttempt(sensor, sessionId('ses_a'), providerRun('asst_2'))
  assert.equal(isArmed(sensor, sessionId('ses_a'), providerRun('asst_2')), false)
  assert.equal(hasArmedSession(sensor, sessionId('ses_a')), false)
})
