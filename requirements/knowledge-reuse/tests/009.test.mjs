import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Knowledge/Casebook/StoreSurface.js'
import * as fetchTool from '../../../dist/OpenCode/Tools/KnowledgeFetchSurface.js'
import * as wiring from '../../../dist/OpenCode/Host/KnowledgeLifecycleSurface.js'

test('WHAT[KNOWLEDGE-REUSE-009] CASE009_marker_gates_the_surface', () => {
  assert.equal(store.isMarkerEnabled({ knowledgeReuse: true }), true)
  assert.equal(store.isMarkerEnabled({ knowledgeReuse: false }), false)
})

test('WHAT[KNOWLEDGE-REUSE-009] CASE009_fetch_execution_rejects_a_workspace_without_the_marker', async () => {
  const r = await fetchTool.fetchWithMarkerCheck('c1', false)
  assert.equal(r.ok, false)
})

test('WHAT[KNOWLEDGE-REUSE-009] lifecycle_disabled_marker_skips_publication', async () => {
  const r = await wiring.finalizeWithMarker(false)
  assert.equal(r.published, false)
})
