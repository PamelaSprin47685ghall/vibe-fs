import assert from 'node:assert/strict'
import { decode, encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'
import * as loopSensor from '../../../../dist/OpenCode/Host/LoopSensorSurface.js'

export const repetitiveText = () => ' retry'.repeat(2000)
export const createSensor = options => loopSensor.create({ diagnostic: () => {}, ...options })
export const deferred = () => Promise.withResolvers()
export const rawDelta = (sessionID, field, delta, messageID = 'msg_a') => ({
  type: 'message.part.delta', properties: { sessionID, messageID, partID: 'part_a', field, delta },
})
export const awaitOwned = async (sensor, session, run) => {
  const task = loopSensor.activeTask(sensor, session, run)
  assert.notEqual(task, null, 'expected an owned task for this exact run')
  await task
}
export const chaoticText = () => {
  const pieces = []
  for (let token = 0; token < vocabularySize && pieces.length < 512; token++) {
    let piece
    try { piece = decode([token]) } catch { continue }
    if (!/^ [A-Za-z]{4,}$/.test(piece)) continue
    const roundTrip = encode(piece)
    if (roundTrip.length === 1 && roundTrip[0] === token) pieces.push(piece)
  }
  assert.equal(pieces.length, 512, 'fixture needs distinct, stable single tokens')
  const text = pieces.join('')
  assert.ok(new Set(encode(text).slice(0, 300)).size > 250)
  return text
}
