import assert from 'node:assert/strict'
import test from 'node:test'
import * as satellite from '../../../dist/Execution/Session/SatelliteRuntimeSurface.js'

test('WHAT[MANAGED-SESSION-011] HOST_015_missing_restored_child_closes_then_links_replacement', async () => {
  const r = await satellite.testMissingChildLinksReplacement()
  assert.equal(r.linkedReplacement, true)
})

test('WHAT[MANAGED-SESSION-011] HOST_014_children_query_failure_does_not_guess_or_create', async () => {
  const r = await satellite.testQueryFailureNoGuess()
  assert.equal(r.created, false)
})
