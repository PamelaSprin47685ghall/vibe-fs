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

test('WHAT[host-provider-failure-ownership-003] recovery resolution produces zero Wanxiangshu final presentation', () => {
  for (const failure of ['ProviderTransient', 'ProviderPermanent']) {
    assert.deepEqual(classify(failure, 'episode-1'), {
      mode: 'Recovery',
      owner: 'Wanxiangshu',
      episodeId: 'episode-1',
      hasFinalPresentation: false,
    })
  }
})

test('WHAT[host-provider-failure-ownership-003] recovery resolution has no suppression facade', () => {
  const decision = classify('ProviderTransient', 'episode-1')
  assert.equal(decision.mode, 'Recovery')
  assert.equal(decision.hasFinalPresentation, false)
  assert.equal(decision.suppressHostNotification, undefined)
})
