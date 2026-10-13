import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

test('旧生命周期名字不得被统一解码收成新事件', () => {
  const opened = surface.encodeOpening('plan', 'work-old-name', '/repo', 7n)
  const reviewed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Promoted')
  assert.equal(reviewed.decision, 'Write')

  const bytes = reviewed.bytes
  const produced = JSON.parse(bytes)
  assert.equal(produced.event_type, 'Reviewed')

  const decoded = surface.decodeDocument(bytes)
  assert.equal(decoded.ok, true)
  assert.equal(decoded.names, 'Reviewed')
  assert.equal(decoded.names.includes('WorkOpened'), false)

  for (const oldName of ['RoadOpened', 'AssessmentCommitted']) {
    const renamed = bytes.replace('Reviewed', oldName)
    assert.equal(JSON.parse(renamed).event_type, oldName)
    assert.equal(renamed.endsWith('\n'), true)
    const refused = surface.decodeDocument(renamed)
    assert.equal(refused.ok, false)
    assert.equal(refused.names, '')
    assert.equal(refused.fault.includes(oldName), true)
  }
})
