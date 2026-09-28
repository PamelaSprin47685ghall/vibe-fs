import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'

test('WHAT[intra-participant-parallelism-002] production prompt parser preserves each complete input and assigns its array index', () => {
  const prompts = ['  A  \r\nstill A', 'B\r\n', '\t中文 😀\n\nlast  ']
  const parsed = fission.parsePrompt(prompts)
  assert.equal(parsed.ok, true)
  assert.equal(parsed.count, prompts.length)
  assert.deepEqual(parsed.lanes, prompts.map((prompt, index) => ({ index, prompt })))
})

test('WHAT[intra-participant-parallelism-002] parser rejects too few lanes and every empty position without dropping it', () => {
  for (const prompts of [[], ['A']]) assert.equal(fission.parsePrompt(prompts).reason, 'TooFewLanes')
  for (const blank of ['', ' \t\r\n']) {
    for (const index of [0, 1, 2]) {
      const prompts = ['A', 'B', 'C']
      prompts[index] = blank
      assert.deepEqual(fission.parsePrompt(prompts), { ok: false, reason: 'EmptyLanePrompt', laneIndex: index })
    }
  }
})

test.todo('WHAT[intra-participant-parallelism-002] actual Host tool schema and execute path preserve the canonical prompt array through lane dispatch (GAP-158)')
