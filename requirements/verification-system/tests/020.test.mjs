import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

test('WHAT[verification-system-020] the property facility reproduces a bounded seeded input sequence', () => {
  const observe = () => {
    const inputs = []
    const result = fc.check(fc.property(fc.integer(), (input) => {
      inputs.push(input)
      return true
    }), { seed: 20260926, numRuns: 25 })
    assert.equal(result.failed, false)
    assert.equal(result.numRuns, 25)
    return inputs
  }
  const first = observe()
  assert.equal(first.length, 25)
  assert.deepEqual(observe(), first)
})

test('WHAT[verification-system-020] a controlled false law reports a shrink path that replays its counterexample', () => {
  // Deliberately false canary: the runner must refute it, not accept random activity as proof.
  const property = fc.property(fc.integer({ min: 1, max: 100000 }), (input) => input < 1)
  const result = fc.check(property, { seed: 20260926, numRuns: 25 })
  assert.equal(result.failed, true)
  assert.deepEqual(result.counterexample, [1])
  assert.ok(result.numRuns <= 25)
  assert.ok(result.counterexamplePath.length > 0)
  const replay = fc.check(property, {
    seed: result.seed, path: result.counterexamplePath, numRuns: 25,
  })
  assert.equal(replay.failed, true)
  assert.deepEqual(replay.counterexample, result.counterexample)
  assert.throws(() => fc.assert(property, { seed: result.seed, numRuns: 25 }), (error) => {
    assert.match(error.message, /seed:/)
    assert.match(error.message, /path:/)
    assert.match(error.message, /Counterexample:/)
    return true
  })
})
