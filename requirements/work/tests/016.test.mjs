import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`
  if (value !== null && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(',')}}`
  }
  return JSON.stringify(value)
}

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).events.find((event) => event.type === 'IncumbencyOpened').incumbencyId

test('WHAT[work-016] 旧生命周期名字不得被公开读取收成新事件', () => {
  const opened = surface.encodeOpening('plan', 'work-old-name', '/repo', 7n)
  const reviewed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Promoted')
  assert.equal(reviewed.decision, 'Write')

  const produced = JSON.parse(reviewed.bytes).events[0]
  assert.equal(produced.type, 'Reviewed')

  const accepted = surface.readProbe(opened + reviewed.bytes)
  assert.equal(accepted.ok, true)
  assert.equal(accepted.names.includes('Reviewed'), true)
  assert.equal(accepted.next, 'Continue')

  for (const oldName of ['RoadOpened', 'AssessmentCommitted']) {
    const frame = JSON.parse(reviewed.bytes)
    frame.events[0].type = oldName
    const renamed = `${canonical(frame)}\n`
    assert.equal(JSON.parse(renamed).events[0].type, oldName)
    const refused = surface.readProbe(opened + renamed)
    assert.equal(refused.ok, false)
    assert.equal(refused.names, '')
    assert.equal(refused.opened, false)
    assert.equal(refused.activeId, '')
    assert.match(refused.reason, /Unknown lifecycle event/)
  }
})
