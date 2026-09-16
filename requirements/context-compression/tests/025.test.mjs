import assert from 'node:assert/strict'
import test from 'node:test'
import * as barrier from '../../../dist/Context/Companion/BloggerStopBarrierSurface.js'
import * as fatal from '../../../dist/Context/Companion/FatalBoundarySurface.js'

test('WHAT[CONTEXT-COMPRESSION-025] stop decision fences provider admission before the abort resolves', () => {
  assert.ok(barrier.stopDecisionFencesAdmission)
})

test('WHAT[CONTEXT-COMPRESSION-025] abort rejection never reopens the stopped execution', () => {
  assert.ok(barrier.abortRejectionNeverReopens)
})

test('WHAT[CONTEXT-COMPRESSION-025] Blogger fatal binds exact request settlement and one injected fuse', () => {
  assert.ok(fatal.bindsExactRequestSettlement)
})
