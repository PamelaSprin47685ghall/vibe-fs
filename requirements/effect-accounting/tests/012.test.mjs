import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[effect-accounting-012] actual relay records confirmed rebase and a new review before its publish claim and physical update', async () => {
  const result = await change.observeRelayProgram('fresh')
  assert.equal(result.verdict.kind, 'Published')
  const rebase = result.timeline.indexOf('fact:RebasedCandidateReady')
  const review = result.timeline.indexOf('await:Candidate', rebase + 1)
  const claim = result.timeline.indexOf('fact:PublishClaimed')
  const update = result.timeline.indexOf('git:ff:rebased-1')
  assert.ok(rebase >= 0 && review > rebase && claim > review && update > claim)
  assert.equal(result.ffCalls, 1)
})

for (const scenario of ['reentry-missing-rebased', 'rebase-reuse-old-cert']) {
  test(`WHAT[effect-accounting-012] actual relay rejects ${scenario} without a publish claim or physical update`, async () => {
    const result = await change.observeRelayProgram(scenario)
    assert.equal(result.verdict.kind, 'IntegrationFailed')
    assert.equal(result.facts.includes('PublishClaimed'), false)
    assert.equal(result.ffCalls, 0)
  })
}
