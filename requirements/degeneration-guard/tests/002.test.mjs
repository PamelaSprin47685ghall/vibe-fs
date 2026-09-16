import assert from 'node:assert/strict'
import test from 'node:test'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
const repetitiveText = () => ' retry'.repeat(2000)
const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

const rawDelta = (session, field, text, messageId = 'msg_a') => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    messageID: messageId,
    partID: 'prt_1',
    field,
    delta: text,
  },
})

test('WHAT[DG-002] LOOP_009_text_and_reasoning_delta_decode_fail_closed', () => {
  assert.equal(loopDetector.tryDecodeTextDelta({ type: 'session.status' }), null)

  for (const field of ['text', 'reasoning', 'model_thought', 'thinking', 'reasoning_content']) {
    assert.deepEqual(loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        messageID: 'msg_a',
        partID: 'prt_1',
        field,
        delta: 'zzzz',
      },
    }), {
      sessionId: 'ses_loop',
      messageId: 'msg_a',
      partId: 'prt_1',
      field,
      delta: 'zzzz',
    })
  }

  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    assert.equal(loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        messageID: 'msg_a',
        partID: 'prt_1',
        field,
        delta: 'zzzz',
      },
    }), null)
  }
})

test('WHAT[DG-002] LOOP_002_sensor_observes_text_and_reasoning_only', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_text'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    loopSensor.observe(sensor, rawDelta('ses_text', field, repetitiveText()))
  }
  await wait()
  assert.deepEqual(aborts, [])

  loopSensor.observe(sensor, rawDelta('ses_text', 'reasoning', repetitiveText()))
  await wait()
  assert.deepEqual(aborts, ['ses_text'])
})
