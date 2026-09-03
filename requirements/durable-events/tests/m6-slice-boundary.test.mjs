import test from 'node:test'
import { assertEffectIsInjected, assertFatalBoundary, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[DURABLE-EVENTS-023] canonical codec and owner folds reject physical store and outer-union authority', () => {
  assertPureContract()
  assertEffectIsInjected('file-system')
})

test('WHAT[DURABLE-EVENTS-024] semantic cut fatal requires settlement and one injected physical fuse', () => {
  assertFatalBoundary('durable-events')
})
