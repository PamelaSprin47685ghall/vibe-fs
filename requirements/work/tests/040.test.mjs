import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

test('WHAT[work-040] one open Work has at most one active tenure', () => {
  const first = surface.encodeOpening('plan', 'work-single-tenure', '/repo', 4n)
  const opened = surface.readProbe(first)
  assert.equal(opened.position, 'ActiveRebound')
  assert.equal(opened.names.includes('IncumbencyOpened'), true)

  // A second WorkOpened for the same workId is refused by the fold, not folded
  // into "already open".
  const again = surface.readProbe(first + first)
  assert.notEqual(again.position, 'ActiveRebound')
  assert.equal(again.detail.includes('second WorkOpened'), true)
})
