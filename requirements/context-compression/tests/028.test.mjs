import assert from 'node:assert/strict'
import test from 'node:test'
import * as phaseWindow from '../../../dist/Context/Prefix/Surface.js'

// context-compression-028 pins the K-window formula. The table below is the clause's
// own worked example: with committed phases A1..AN and Bi the start of Ai's turn, the
// desired cutoff is Bj for j = max(1, N − K + 1).
const turnStarts = (n) => Array.from({ length: n }, (_, index) => index + 1)

const cutoffOf = (k, n) => {
  const decision = phaseWindow.desiredCutoff(k, turnStarts(n))
  return decision.kind === 'KeepFrom' ? decision.cutoffExclusive : null
}

test('WHAT[context-compression-028] N=0 yields no cutoff and no synthetic phase', () => {
  for (const k of [1, 2, 5]) {
    assert.equal(cutoffOf(k, 0), null, `K=${k} must not invent a zero-th assume call`)
  }
})

test('WHAT[context-compression-028] K=1 keeps only the current phase turn', () => {
  assert.equal(cutoffOf(1, 1), 1)
  assert.equal(cutoffOf(1, 2), 2)
  assert.equal(cutoffOf(1, 3), 3)
  assert.equal(cutoffOf(1, 4), 4)
})

test('WHAT[context-compression-028] K=2 keeps the previous phase as well', () => {
  assert.equal(cutoffOf(2, 1), 1)
  assert.equal(cutoffOf(2, 2), 1)
  assert.equal(cutoffOf(2, 3), 2)
  assert.equal(cutoffOf(2, 4), 3)
})

test('WHAT[context-compression-028] two commits inside one turn share their boundary', () => {
  // A2 and A3 land in the same semantic turn, so their Bi is equal: the window keeps
  // the whole turn instead of cutting a tool call away from its result.
  const decision = phaseWindow.desiredCutoff(2, [1, 4, 4, 7])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 4)
})

test('WHAT[context-compression-028] a non-positive K is refused', () => {
  assert.equal(phaseWindow.validateK(0).ok, false)
  assert.equal(phaseWindow.validateK(-1).ok, false)
  assert.match(phaseWindow.validateK(0).error, /positive integer/)
  assert.equal(phaseWindow.validateK(1).ok, true)
})

test('WHAT[context-compression-028] the actual cutoff never exceeds proven coverage', () => {
  const desired = phaseWindow.desiredCutoff(2, turnStarts(4))

  // Coverage proves only up to turn 2, so the desire is clamped down to it.
  const clamped = phaseWindow.actualCutoff(desired, 2, 2)
  assert.equal(clamped.kind, 'KeepFrom')
  assert.equal(clamped.cutoffExclusive, 2)
})

test('WHAT[context-compression-028] no coverage means no compression', () => {
  const desired = phaseWindow.desiredCutoff(2, turnStarts(4))
  const decision = phaseWindow.actualCutoff(desired, 1, null)
  assert.equal(decision.kind, 'NoPhases', 'uncovered raw must be kept, never dropped')
})

test('WHAT[context-compression-028] a committed cutoff is history, not a preference', () => {
  const desired = phaseWindow.desiredCutoff(2, turnStarts(4))

  // Coverage has moved past the committed cutoff, so the new cutoff advances.
  const advanced = phaseWindow.actualCutoff(desired, 2, 9)
  assert.equal(advanced.cutoffExclusive, 3)

  // Coverage that has fallen behind the committed cutoff cannot retreat it.
  const behind = phaseWindow.actualCutoff(desired, 5, 3)
  assert.equal(behind.cutoffExclusive, 5)
})
