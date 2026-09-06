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
    retryBudget: 'Exhausted',
    fallbackBudget: 'Available',
    breaker: 'Closed',
  },
  ...change,
})

const classify = (failure, episodeId, change) =>
  presentation.classifyPolicyInput(input(failure, change), episodeId)

test('WHAT[HOSTFAIL-003] recovery resolution produces zero Wanxiangshu final presentation', () => {
  for (const failure of ['ProviderTransient', 'ProviderPermanent']) {
    assert.deepEqual(classify(failure, 'episode-1'), {
      mode: 'Recovery',
      owner: 'Wanxiangshu',
      episodeId: 'episode-1',
      hasFinalPresentation: false,
    })
  }
})

test('WHAT[HOSTFAIL-003] recovery resolution has no suppression facade', () => {
  const decision = classify('ProviderTransient', 'episode-1')
  assert.equal(decision.mode, 'Recovery')
  assert.equal(decision.hasFinalPresentation, false)
  assert.equal(decision.suppressHostNotification, undefined)
})

test('WHAT[HOSTFAIL-004] non-terminal resolution leaves presentation to the Host', () => {
  assert.deepEqual(
    classify('ProviderPermanent', 'episode-settled', { phase: 'Terminal' }),
    { mode: 'Ignore', hasFinalPresentation: false },
  )
})

test('WHAT[HOSTFAIL-006] exhaustion uses one typed final Wanxiangshu presentation', () => {
  assert.deepEqual(
    classify('ProviderPermanent', 'episode-final', {
      provider: { ...input('ProviderPermanent').provider, fallbackBudget: 'Exhausted' },
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
    provider: { ...input('ProviderPermanent').provider, fallbackBudget: 'Exhausted' },
  })
  assert.equal(decision.mode, 'Final')
  assert.equal(decision.hasFinalPresentation, true)
  assert.notEqual(decision.mode, 'Recovery')
})

test('WHAT[HOSTFAIL-005] policy owner recovers with zero Host retry', () => {
  assert.equal(classify('ProviderPermanent', 'episode-5').hasFinalPresentation, false)
  assert.deepEqual(scanRetryOwnership(ROOT), [])
})
