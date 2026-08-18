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

test('WHAT[DURABLE-CONVERGENCE-002] one k-way primitive is shared by integrator and sync', async () => {
  const primitive = await read('src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs')
  const integrator = await read('src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(primitive, /module EventKWayMerge/)
  assert.match(primitive, /checkIdentity/)
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(sync, /EventKWayMerge\.merge/)
  assert.doesNotMatch(integrator, /sortBy.*EventId.*writerId/is, 'Integrator must not own a second k-way implementation')
  assert.doesNotMatch(sync, /observed_at.*runtime_id.*local_seq/is, 'sync must not invent a second event-ordering algorithm')
})

test('WHAT[DURABLE-CONVERGENCE-003] sync blobifies each complete writer file once without segments or index', async () => {
  const source = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(source, /WriterId|writerId/)
  assert.match(source, /WriteBlob/)
  assert.doesNotMatch(source, /SegmentMaxBytes|segment|chunk|index\/|EventId.*Oid|delta/i)

  // Contract shape: materialization iterates writer files, writing one blob for the complete bytes.
  assert.match(source, /materialize.*writer|writer.*materialize/is)
  assert.doesNotMatch(source, /splitWriter|rotateWriter|writerSegment|writerChunk/i)
})

test('WHAT[DURABLE-CONVERGENCE-008] activation only ensures hooks and user Git process runs full sync', async () => {
  const boot = await read('src/Wanxiangshu/OpenCode/Plugin/PluginBoot.fs')
  const activation = await read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const hook = await read('src/Wanxiangshu/Git/Hook/Dispatcher.fs')
  const runner = await read('resources/git/wanxiang-hook.mjs')
  const hookSync = await read('src/Wanxiangshu/Git/Hook/Sync.fs')

  assert.doesNotMatch(boot, /HookDispatcher\.ensure/)
  assert.match(activation, /lazy[\s\S]*HookDispatcher\.ensure/)
  assert.match(hook, /ReferenceTransaction/)
  assert.match(hook, /PrePush/)
  assert.match(hook, /full.*converge|ConvergeFull/is, 'both hook kinds must run full bidirectional convergence')
  assert.doesNotMatch(hook, /ConvergeObserved/, 'reference-transaction is not a one-way observed/import path')

  assert.match(runner, /reference-transaction/)
  assert.match(runner, /pre-push/)
  assert.match(runner, /HookSync/)
  assert.match(hookSync, /GitGateway\.converge/)
  assert.match(await read('src/Wanxiangshu/Git/Gateway.fs'), /WriterStreamSync\.syncWriterStreams/)
  assert.doesNotMatch(runner, /WorkspaceEventStore|CanonicalIntegrator|PluginHost/,
    'hook runner must work when Wanxiangshu/OpenCode is not running')

  const productGit = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.doesNotMatch(productGit, /member _\.(Fetch|Pull|Push)\(/,
    'Wanxiangshu product process must not own user fetch/pull/push triggers')

  const persistSources = [
    await read('src/Wanxiangshu/Persistence/EventStore/Store.fs'),
    await read('src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs'),
  ].join('\n')
  assert.doesNotMatch(persistSources, /Converge\(|Fetch\(|Pull\(|Push\(/, 'ordinary local append/replay must not trigger remote sync')
})

test('WHAT[DURABLE-CONVERGENCE-003] runtime append and external hook share one physical store gate', async () => {
  const log = await read('src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs')
  const store = await read('src/Wanxiangshu/Persistence/EventStore/Store.fs')
  const hook = await read('src/Wanxiangshu/Git/Hook/Sync.fs')

  assert.match(log, /proper-lockfile/)
  assert.match(store, /ProcessEventLog\.withStoreLock/)
  assert.match(hook, /ProcessEventLog\.withStoreLock/)
  assert.match(log, /"forever"\s*==>|forever.*true/s, 'physical lock wait must not inherit a business timeout window')
})

test('WHAT[DURABLE-CONVERGENCE-007] sync does not integrate business history', async () => {
  const source = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')
  assert.doesNotMatch(source, /StrengthProjection|CasebookProjection|AgentProjection|MagicTodo|JsTransactionPrepared/)
  assert.doesNotMatch(source, /Fold\.apply|StrengthProjection\.fold|CasebookProjection\.fold/)
})
