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

test('WHAT[host-provider-failure-ownership-004] non-terminal resolution leaves presentation to the Host', () => {
  assert.deepEqual(
    classify('ProviderPermanent', 'episode-settled', { phase: 'Terminal' }),
    { mode: 'Ignore', hasFinalPresentation: false },
  )
})
