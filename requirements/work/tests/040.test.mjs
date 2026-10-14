import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

test('WHAT[work-040] one open Work has at most one active tenure', () => {
  const first = surface.encodeOpening('plan', 'work-single-tenure', '/repo', 4n)
  const opened = surface.readProbe(first)
  assert.equal(opened.ok, true)
  assert.equal(opened.next, 'Review')
  assert.equal(opened.activeId.length > 0, true)
  assert.equal(opened.names.includes('IncumbencyOpened'), true)

  const again = surface.readProbe(first + first)
  assert.equal(again.ok, false)
  assert.equal(again.opened, false)
  assert.equal(again.names, '')
  assert.equal(again.activeId, '')
  assert.match(again.reason, /Replayed/)
  assert.notEqual(again.next, 'Review')
})
