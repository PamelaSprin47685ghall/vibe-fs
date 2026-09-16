import assert from 'node:assert/strict'
import test from 'node:test'
import * as session from '../../../dist/Knowledge/Bookkeeper/SessionSurface.js'
import * as synth from '../../../dist/Knowledge/Bookkeeper/SynthesisSurface.js'
import * as store from '../../../dist/Knowledge/Casebook/StoreSurface.js'
import * as hostReuse from '../../../dist/OpenCode/Host/HostReuseFinalizeSurface.js'
import * as inspectorTool from '../../../dist/OpenCode/Tools/InspectorToolFinalizeSurface.js'
import * as wiring from '../../../dist/OpenCode/Host/KnowledgeLifecycleSurface.js'
import * as uLoop from '../../../dist/OpenCode/Host/UniversalLoopFinalizeSurface.js'
import * as casebookLifecycleSurface from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_create_child_once_and_cleanup_never_runs_bookkeeper', async () => {
  const r = await session.testFinalizeRunsOnce('ses-1')
  assert.equal(r.runs, 1)
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_uses_synthesizer_not_raw_noteAnswer', async () => {
  const r = await synth.testFinalizeUsesSynth('ses-1')
  assert.equal(r.usedSynth, true)
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_cleanup_never_synthesizes', async () => {
  const r = await synth.testCleanupNoSynth('ses-1')
  assert.equal(r.synthCalled, false)
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_is_exactly_once_per_scope', async () => {
  let s = store.empty
  const f1 = store.tryFinalizeScope('scope-1', 'q', 'a', [], s)
  assert.equal(f1.published, true)
  const f2 = store.tryFinalizeScope('scope-1', 'q', 'a', [], f1.state)
  assert.equal(f2.published, false)
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_host_reusable_inspector_one_finalize_then_cold_fetch', async () => {
  const r = await hostReuse.testInspectorOneFinalize('ses-1')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_inspector_tool_sync_delegate_lifecycle_bookkeeper_fetch', async () => {
  const r = await inspectorTool.testInspectorToolFetch('ses-1')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-010] lifecycle_cleanupInspector_never_publishes_case', async () => {
  const r = await wiring.cleanupInspector('ses-1')
  assert.equal(r.published, false)
})

test('WHAT[KNOWLEDGE-REUSE-010] lifecycle_missing_answer_is_noop_finalize', async () => {
  const r = await wiring.finalizeMissingAnswer('ses-1')
  assert.equal(r.published, false)
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_universal_loop_archive_finalize_fetch', async () => {
  const r = await uLoop.testUniversalLoopArchive('ses-1')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_lifecycle_note_finalize_fetch_and_cleanup', async () => {
  const r = await uLoop.testLifecycleNoteFinalize('ses-1')
  assert.equal(r.ok, true)
})

test('WHAT[KNOWLEDGE-REUSE-010] G6_G_cancel_session_cleanup_no_publication', async () => {
  const r = await uLoop.testCancelSessionCleanup('ses-1')
  assert.equal(r.published, false)
})
