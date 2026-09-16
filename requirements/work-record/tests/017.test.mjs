// requirements/work-record/tests/017.test.mjs
//
// Law: WORK-RECORD-017
// Scenario T22: Fission convergence materializes a single canonical invocation work record.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as workRecord from '../../../dist/Work/Record/Surface.js'

test('WHAT[WORK-RECORD-017] T22_fission_convergence_materializes_single_canonical_invocation_work_record', () => {
  assert.equal(typeof workRecord.materializeFissionInvocationRecord, 'function', 'must export materializeFissionInvocationRecord')

  const convergedTrace = {
    invocationId: 'inv-fission-1',
    lanes: [
      { key: 'lane-1', statements: ['investigated module A'] },
      { key: 'lane-2', statements: ['investigated module B'] }
    ],
    takeoverStatement: 'Integrated findings from both modules.'
  }

  const record = workRecord.materializeFissionInvocationRecord(convergedTrace)
  assert.match(record, /Recent work/)
  assert.match(record, /Integrated findings from both modules./)
  assert.doesNotMatch(record, /### Fragment/, 'must not expose un-converged fragmented sub-records')
})
