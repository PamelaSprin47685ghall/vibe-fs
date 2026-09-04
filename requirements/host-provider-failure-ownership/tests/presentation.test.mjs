import assert from 'node:assert/strict'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as failure from '../../../dist/OpenCode/Host/ProviderFailurePresentation.js'
import { scanRetryOwnership } from '../../../scripts/checks/retry-owner.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[HOSTFAIL-003] recoverable provider failures are claimed with stable episode identity', () => {
  assert.deepEqual(failure.classify('UpstreamCapacity', 'episode-1'), {
    mode: 'Claimed',
    owner: 'Wanxiangshu',
    episodeId: 'episode-1',
  })
})

test('WHAT[HOSTFAIL-004] unknown and non-provider failures keep default Host presentation', () => {
  for (const kind of ['PluginBug', 'GitFailure', 'UserValidation', 'Unknown']) {
    assert.deepEqual(failure.classify(kind, 'episode-1'), { mode: 'Default' })
  }
})

test('WHAT[HOSTFAIL-006] exhaustion uses one final Wanxiangshu presentation', () => {
  assert.deepEqual(failure.classify('ProviderCapacityExhausted', 'episode-final'), {
    mode: 'Final',
    owner: 'Wanxiangshu',
    episodeId: 'episode-final',
  })
})

test('WHAT[HOSTFAIL-005] claimed failure recovers through the policy owner with zero Host retry', () => {
  assert.deepEqual(failure.classify('UpstreamCapacity', 'episode-5'), {
    mode: 'Claimed',
    owner: 'Wanxiangshu',
    episodeId: 'episode-5',
  })
  assert.deepEqual(scanRetryOwnership(ROOT), [])
})
