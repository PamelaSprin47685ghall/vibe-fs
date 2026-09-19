import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { decode, encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'
import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import * as providerLanguage from '../../../dist/Participant/Provider/LanguageSurface.js'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))

const repetitiveText = () => ' retry'.repeat(2000)

const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

const chaoticText = () => {
  const pieces = []

  for (let token = 0; token < vocabularySize && pieces.length < 512; token += 1) {
    let piece
    try {
      piece = decode([token])
    } catch {
      continue
    }

    if (!/^ [A-Za-z]{4,}$/.test(piece)) continue
    const roundTrip = encode(piece)
    if (roundTrip.length === 1 && roundTrip[0] === token) pieces.push(piece)
  }

  assert.equal(pieces.length, 512, 'fixture needs hundreds of stable, distinct single tokens')
  const text = pieces.join('')
  assert.ok(new Set(encode(text).slice(0, 300)).size > 250, 'fixture prefix must remain highly diverse')
  return text
}

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

const rawDeltaWithoutMessage = (session, field, text) => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    partID: 'prt_1',
    field,
    delta: text,
  },
})

test('WHAT[degeneration-guard-012] LOOP_012_degeneration_guard_is_the_single_closed_recovery_owner', async () => {
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_closed'],
    abort: () => {},
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_closed', repetitiveText(), 'msg_closed_1'))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_closed', 'msg_closed_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_closed', 'TooRepetitive']])

  const ordinarySource = readFileSync(
    join(root, 'src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs'),
    'utf8',
  )
  const fissionSource = readFileSync(join(root, 'src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs'), 'utf8')

  assert.match(ordinarySource, /AbortCause\.DegenerationGuard _ -> AsyncSupport\.completedTask \(\)/)
  assert.match(
    fissionSource,
    /TurnAborted _, AbortCause\.DegenerationGuard _ ->[\s\S]{0,120}DegenerationInterrupted/,
  )
})
