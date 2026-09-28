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

test('WHAT[context-compression-028] the frozen default is 2 and is a legal window', () => {
  assert.equal(phaseWindow.defaultK, 2, 'the owner opens with K = 2 unless something records otherwise')
  assert.equal(phaseWindow.validateK(phaseWindow.defaultK).ok, true)
})

test('WHAT[context-compression-028] the bounded window yields the same boundary as the full history', () => {
  // The projection only ever holds the last K commits, so the formula must agree when
  // it is handed that window instead of A1..AN: the oldest retained phase IS A_(N-K+1).
  for (const n of [1, 2, 3, 7]) {
    for (const k of [1, 2, 3]) {
      const allTurns = turnStarts(n)
      const window = allTurns.slice(-k)
      const decision = phaseWindow.desiredCutoffOfWindow(
        window.map((_, index) => `call-${index}`),
        window,
      )
      assert.equal(decision.kind, 'KeepFrom')
      assert.equal(
        decision.cutoffExclusive,
        cutoffOf(k, n),
        `K=${k} N=${n}: the window-sized list must select the same Bj`,
      )
    }
  }
})

test('WHAT[context-compression-028] the window keeps at most K commits in commit order', () => {
  let window = []
  for (const callId of ['a', 'b', 'c', 'd']) {
    window = phaseWindow.appendPhase(2, callId, window)
  }
  assert.deepEqual(window, ['c', 'd'], 'only the last K commits stay; order is commit order')

  assert.deepEqual(phaseWindow.appendPhase(2, 'a', null), ['a'])
})

test('WHAT[context-compression-028] a phase with no addressable turn proves no boundary', () => {
  // The oldest retained phase is the one the cutoff would land on. If its turn is gone
  // (a voided numbering), folding there would name a boundary the prefix cannot point
  // to, so the honest answer is "no cutoff" rather than the next phase's turn.
  const decision = phaseWindow.desiredCutoffOfWindow(['call-old', 'call-live'], [null, 42])
  assert.equal(decision.kind, 'NoPhases')

  const addressable = phaseWindow.desiredCutoffOfWindow(['call-old', 'call-live'], [7, 42])
  assert.equal(addressable.kind, 'KeepFrom')
  assert.equal(addressable.cutoffExclusive, 7, 'the oldest retained phase decides, not the newest')
})
