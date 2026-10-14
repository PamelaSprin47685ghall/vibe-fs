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

test('WHAT[work-040] one open Work has at most one active tenure', () => {
  const first = surface.encodeOpening('plan', 'work-single-tenure', '/repo', 4n)
  const opened = surface.readProbe(first)
  assert.equal(opened.ok, true)
  assert.equal(opened.next, 'Review')
  assert.equal(opened.activeId.length > 0, true)
  assert.equal(opened.names.includes('IncumbencyOpened'), true)

  // A second opening of the same work under a different request id is refused:
  // one open work has at most one active tenure.
  const secondFrame = JSON.parse(first)
  secondFrame.requestId = 'open:work-single-tenure:second'
  const second = `${canonical(secondFrame)}\n`
  const again = surface.readProbe(`${first}${second}`)
  assert.equal(again.ok, false)
  assert.equal(again.opened, false)
  assert.equal(again.names, '')
  assert.equal(again.activeId, '')
  assert.equal(again.reason, 'Work is already open or identity differs')
  assert.notEqual(again.next, 'Review')
})
