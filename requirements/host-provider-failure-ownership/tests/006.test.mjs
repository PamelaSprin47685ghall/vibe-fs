import assert from 'node:assert/strict'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as presentation from '../../../dist/OpenCode/Host/ProviderFailurePresentation.js'
import { scanRetryOwnership } from '../../../scripts/checks/retry-owner.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const input = (failure, change = {}) => ({
  failure,
  phase: 'ProviderStarted',
  executionKey: {
    sessionId: 'ses-provider-presentation',
    physicalUserMessageId: 'msg-provider-presentation',
  },
  capacityFence: null,
  provider: {
    logicalRun: 'logical-provider-presentation',
    providerRun: 'run-provider-presentation',
    requestKind: 'WorkMain',
    retryBudget: 'Available',
    breaker: 'Closed',
  },
  ...change,
})

const classify = (failure, episodeId, change) =>
  presentation.classifyPolicyInput(input(failure, change), episodeId)

test('WHAT[HOSTFAIL-006] exhaustion uses one typed final Wanxiangshu presentation', () => {
  assert.deepEqual(
    classify('ProviderPermanent', 'episode-final', {
      provider: { ...input('ProviderPermanent').provider, retryBudget: 'Exhausted' },
    }),
    {
      mode: 'Final',
      owner: 'Wanxiangshu',
      episodeId: 'episode-final',
      hasFinalPresentation: true,
    },
  )
})

test('WHAT[HOSTFAIL-006] terminal resolution is never retrying', () => {
  const decision = classify('ProviderPermanent', 'episode-final', {
    provider: { ...input('ProviderPermanent').provider, retryBudget: 'Exhausted' },
  })
  assert.equal(decision.mode, 'Final')
  assert.equal(decision.hasFinalPresentation, true)
  assert.notEqual(decision.mode, 'Recovery')
})
