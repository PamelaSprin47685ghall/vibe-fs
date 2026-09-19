// requirements/semantic-trace/tests/012.test.mjs
//
// Law: semantic-trace-012
// Scenario T21: Multiple resumes in the same session produce strictly isolated invocation ranges.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as semanticTrace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

test('WHAT[semantic-trace-012] T21_multiple_session_resumes_produce_strictly_isolated_invocation_ranges', () => {
  assert.equal(typeof semanticTrace.createInvocationBoundary, 'function', 'must export createInvocationBoundary')

  const trace = semanticTrace.emptyTrace()
  const inv1 = semanticTrace.createInvocationBoundary(trace, 'inv-1')
  const inv2 = semanticTrace.createInvocationBoundary(trace, 'inv-2')

  assert.notEqual(inv1.startCursor, inv2.startCursor)
  assert.equal(semanticTrace.isDisjointRange(inv1.range, inv2.range), true)
})
