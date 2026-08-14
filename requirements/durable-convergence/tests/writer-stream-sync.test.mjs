// FROZEN — 2026-08-14. Written before implementation by explicit user request.
// Intentionally NOT executed before implementation.
//
// DURABLE-CONVERGENCE-002/003/007/008:
// k-way merge works over whole WriterId streams; remote Git operations are the only sync trigger;
// each complete local writer file is exactly one Git blob at the sync boundary.

import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('DURABLE_CONVERGENCE_002_003_one_k_way_primitive_is_shared_by_integrator_and_sync', async () => {
  const primitive = await read('src/Wanxiangshu/Infrastructure/Persist/EventKWayMerge.fs')
  const integrator = await read('src/Wanxiangshu/Infrastructure/Persist/CanonicalIntegrator.fs')
  const sync = await read('src/Wanxiangshu/Infrastructure/Persist/WriterStreamSync.fs')

  assert.match(primitive, /module EventKWayMerge/)
  assert.match(primitive, /checkIdentity/)
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(sync, /EventKWayMerge\.merge/)
  assert.doesNotMatch(integrator, /sortBy.*EventId.*writerId/is, 'Integrator must not own a second k-way implementation')
  assert.doesNotMatch(sync, /observed_at.*runtime_id.*local_seq/is, 'sync must not invent a second event-ordering algorithm')
})

test('DURABLE_CONVERGENCE_003_sync_blobifies_each_complete_writer_file_once_without_segments_or_index', async () => {
  const source = await read('src/Wanxiangshu/Infrastructure/Persist/WriterStreamSync.fs')

  assert.match(source, /WriterId|writerId/)
  assert.match(source, /WriteBlob/)
  assert.doesNotMatch(source, /SegmentMaxBytes|segment|chunk|index\/|EventId.*Oid|delta/i)

  // Contract shape: materialization iterates writer files, writing one blob for the complete bytes.
  assert.match(source, /materialize.*writer|writer.*materialize/is)
  assert.doesNotMatch(source, /split|rotate/i)
})

test('DURABLE_CONVERGENCE_008_startup_only_ensures_hooks_and_user_Git_process_runs_full_sync', async () => {
  const boot = await read('src/Wanxiangshu/Infrastructure/OpenCode/Plugin/PluginBoot.fs')
  const hook = await read('src/Wanxiangshu/Infrastructure/Git/HookDispatcher.fs')
  const runner = await read('resources/git/wanxiang-hook.mjs')
  const hookSync = await read('src/Wanxiangshu/Infrastructure/Git/HookSync.fs')

  assert.match(boot, /ensure.*hook|HookDispatcher.*ensure/is)
  assert.match(hook, /ReferenceTransaction/)
  assert.match(hook, /PrePush/)
  assert.match(hook, /full.*converge|ConvergeFull/is, 'both hook kinds must run full bidirectional convergence')
  assert.doesNotMatch(hook, /ConvergeObserved/, 'reference-transaction is not a one-way observed/import path')

  assert.match(runner, /reference-transaction/)
  assert.match(runner, /pre-push/)
  assert.match(runner, /HookSync/)
  assert.match(hookSync, /GitGateway\.converge/)
  assert.match(await read('src/Wanxiangshu/Infrastructure/Git/GitGateway.fs'), /WriterStreamSync\.syncWriterStreams/)
  assert.doesNotMatch(runner, /WorkspaceEventStore|CanonicalIntegrator|PluginHost/,
    'hook runner must work when Wanxiangshu/OpenCode is not running')

  const productGit = await read('src/Wanxiangshu/Infrastructure/Git/GitGateway.fs')
  assert.doesNotMatch(productGit, /member _\.(Fetch|Pull|Push)\(/,
    'Wanxiangshu product process must not own user fetch/pull/push triggers')

  const persistSources = [
    await read('src/Wanxiangshu/Infrastructure/Persist/EventStore.fs'),
    await read('src/Wanxiangshu/Infrastructure/Persist/ProcessEventLog.fs'),
  ].join('\n')
  assert.doesNotMatch(persistSources, /Converge\(|Fetch\(|Pull\(|Push\(/, 'ordinary local append/replay must not trigger remote sync')
})

test('DURABLE_CONVERGENCE_003_runtime_append_and_external_hook_share_one_physical_store_gate', async () => {
  const log = await read('src/Wanxiangshu/Infrastructure/Persist/ProcessEventLog.fs')
  const store = await read('src/Wanxiangshu/Infrastructure/Persist/EventStore.fs')
  const hook = await read('src/Wanxiangshu/Infrastructure/Git/HookSync.fs')

  assert.match(log, /proper-lockfile/)
  assert.match(store, /ProcessEventLog\.withStoreLock/)
  assert.match(hook, /ProcessEventLog\.withStoreLock/)
  assert.match(log, /"forever"\s*==>|forever.*true/s, 'physical lock wait must not inherit a business timeout window')
})

test('DURABLE_CONVERGENCE_007_sync_does_not_integrate_business_history', async () => {
  const source = await read('src/Wanxiangshu/Infrastructure/Persist/WriterStreamSync.fs')
  assert.doesNotMatch(source, /StrengthProjection|CasebookProjection|AgentProjection|MagicTodo|JsTransactionPrepared/)
  assert.doesNotMatch(source, /Fold\.apply|StrengthProjection\.fold|CasebookProjection\.fold/)
})
