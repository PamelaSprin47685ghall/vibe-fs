import assert from 'node:assert/strict'
import test from 'node:test'
import * as failure from '../../../dist/OpenCode/Host/ProviderFailurePresentation.js'

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

