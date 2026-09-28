import assert from 'node:assert/strict'
import test from 'node:test'
import * as presentation from '../../../dist/OpenCode/Host/ProviderFailurePresentation.js'
import { input } from './support/presentation.mjs'

test('WHAT[host-provider-failure-ownership-003] presentation classifier marks licensed recovery as having no final presentation', () => {
  for (const failure of ['ProviderTransient', 'ProviderPermanent']) {
    assert.deepEqual(presentation.classifyPolicyInput(input(failure), 'episode'), {
      mode: 'Recovery', owner: 'Wanxiangshu', episodeId: 'episode', hasFinalPresentation: false,
    })
  }
})

test.todo('WHAT[host-provider-failure-ownership-003] actual recovery emits no plugin final presentation before exhaustion and preserves Host native error visibility (GAP-143)')
