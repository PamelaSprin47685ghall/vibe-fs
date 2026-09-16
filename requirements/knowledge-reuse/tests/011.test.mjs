import assert from 'node:assert/strict'
import test from 'node:test'
import * as fetchTool from '../../../dist/OpenCode/Tools/KnowledgeFetchSurface.js'

test('WHAT[KNOWLEDGE-REUSE-011] CASE011_fetch_single_flight_serializes_same_shelfmark', async () => {
  const runs = await fetchTool.singleFlightTest('shelf-1', 3)
  assert.equal(runs.executionCount, 1)
})
