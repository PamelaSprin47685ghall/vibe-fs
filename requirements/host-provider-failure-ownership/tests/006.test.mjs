import assert from 'node:assert/strict'
import test from 'node:test'
import { input, classify } from './support/presentation.mjs'

test('WHAT[host-provider-failure-ownership-006] presentation classifier distinguishes exhausted policy from recovery and settled execution', () => {
  for (const failure of ['ProviderTransient', 'ProviderPermanent']) {
    assert.deepEqual(classify(failure, 'episode-final', {
      provider: { ...input(failure).provider, retryBudget: 'Exhausted' },
    }), { mode: 'Final', owner: 'Wanxiangshu', episodeId: 'episode-final', hasFinalPresentation: true })
    assert.deepEqual(classify(failure, 'episode-final', { phase: 'Terminal' }), { mode: 'Ignore', hasFinalPresentation: false })
  }
})

test.todo('WHAT[host-provider-failure-ownership-006] real budget and capacity exhaustion durably terminalize, stop admission and emit exactly one final through duplicate callbacks and restart (GAP-143)')
