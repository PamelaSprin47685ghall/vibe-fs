import assert from 'node:assert/strict'
import test from 'node:test'
import * as temporal from '../../../dist/Process/Surface.js'

const first = '2026-08-14T08:00:00.000Z'
const later = '2026-08-14T08:05:00.000Z'

test('WHAT[time-capability-007] the actual session-start projection retains its first bound instant', () => {
  const initial = temporal.sessionStartBind(first, null)
  const rebound = temporal.sessionStartBind(later, initial)
  assert.equal(Date.parse(temporal.sessionStartAt(initial)), Date.parse(first))
  assert.equal(Date.parse(temporal.sessionStartAt(rebound)), Date.parse(first))
})

test('WHAT[time-capability-007] elapsed rendering is human-readable in both languages and clamps negative values', () => {
  assert.match(temporal.renderElapsed('en', 125000), /2 minutes 5 seconds/i)
  assert.match(temporal.renderElapsed('zh', 125000), /2 分钟 5 秒/)
  assert.match(temporal.renderElapsed('en', -5000), /0 minutes 0 seconds/i)
  assert.match(temporal.renderElapsed('zh', -5000), /0 分钟 0 秒/)
})

test.todo('WHAT[time-capability-007] first actual prompt samples and durably binds the injected clock before later prompts')
test.todo('WHAT[time-capability-007] a restarted session reuses its origin and replays stored marker bytes without resampling')
