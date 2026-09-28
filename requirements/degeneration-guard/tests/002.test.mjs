import assert from 'node:assert/strict'
import test from 'node:test'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, createSensor, rawDelta, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-002] delta codec accepts textual fields and rejects unrelated events and fields', () => {
  assert.equal(detector.tryDecodeTextDelta({ type: 'session.status' }), null)
  for (const field of ['text', 'reasoning', 'model_thought', 'thinking', 'reasoning_content']) {
    assert.deepEqual(detector.tryDecodeTextDelta(rawDelta('session', field, 'words', 'run')), {
      sessionId: 'session', messageId: 'run', partId: 'part_a', field, delta: 'words',
    })
  }
  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    assert.equal(detector.tryDecodeTextDelta(rawDelta('session', field, 'words')), null)
  }
})

test('WHAT[degeneration-guard-002] non-text deltas cause no interruption before a reasoning control sample', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: () => {} })
  for (const field of ['tool', 'tool_call', 'custom_metadata']) sensor.observe(handle, rawDelta('session', field, repetitiveText(), 'run'))
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
  assert.deepEqual(aborts, [])
  sensor.observe(handle, rawDelta('session', 'reasoning', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  assert.deepEqual(aborts, ['session'])
})

test.todo('WHAT[degeneration-guard-002] actual Host only supplies assistant text streams and cannot turn deltas into terminal authority or journal facts (GAP-145)')
