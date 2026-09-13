// FROZEN — 2026-08-14. Written before implementation by explicit user request.
// Intentionally NOT executed before implementation.
//
// DURABLE-CONVERGENCE-002/003/007/008:
// k-way merge works over whole WriterId streams; remote Git operations are the only sync trigger;
// each complete local writer file is exactly one Git blob at the sync boundary.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as retention from '../../../dist/Persistence/EventStore/RetentionSurface.js'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')
const make = (id, stream, parents = []) => ({ id, stream, type: 'JobRequested', parents, payload: {}, payloadRefs: [] })

test('WHAT[DURABLE-CONVERGENCE-002] one k-way primitive is shared by integrator and sync', async () => {
  const primitive = await read('src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs')
  const integrator = await read('src/Wanxiangshu/Persistence/EventStore/IntegratorEngine.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(primitive, /module EventKWayMerge/)
  assert.match(primitive, /checkIdentity/)
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(sync, /EventKWayMerge\.merge/)
  assert.doesNotMatch(integrator, /sortBy.*EventId.*writerId/is, 'Integrator must not own a second k-way implementation')
  assert.doesNotMatch(sync, /observed_at.*runtime_id.*local_seq/is, 'sync must not invent a second event-ordering algorithm')
})

test('WHAT[DURABLE-CONVERGENCE-002] k-way cursor readiness is one finite state not parallel mutable axes', async () => {
  const primitive = await read('src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs')

  assert.match(primitive, /type private CursorReadiness\s*=\s*[\s\S]*Waiting[\s\S]*Queued[\s\S]*Exhausted/)
  assert.doesNotMatch(primitive, /mutable\s+(Remaining|Generation|MissingParents|Queued)\b/)
  assert.doesNotMatch(primitive, /\bGeneration\b/, 'the writer offset itself must be the waiter generation witness')
})

test('WHAT[DURABLE-CONVERGENCE-003] sync blobifies each complete writer file once without segments or index', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-writer-blobify-'))
  const repoA = join(root, 'a')
  const repoB = join(root, 'b')
  execFileSync('git', ['init', '-q', repoA])
  execFileSync('git', ['init', '-q', repoB])
  const commonA = join(repoA, '.git')
  const commonB = join(repoB, '.git')

  const now = Date.now()
  const A = 'a'.repeat(40)
  const B = 'b'.repeat(40)
  const C = 'c'.repeat(40)

  try {
    // 1. Repo A appends an event to writer-a and syncs to a snapshot
    const handleA = eventStore.create(commonA, 'writer-a')
    await eventStore.append(handleA, [make(A, 'stream/1')])
    eventStore.dispose(handleA)

    const syncA1 = await retention.syncAt(repoA, commonA, null, now)
    assert.equal(syncA1.ok, true, syncA1.ok ? '' : JSON.stringify(syncA1.error))

    // Prove complete writer file is exactly one Git blob in writers/ tree
    const treeA = execFileSync('git', ['-C', repoA, 'ls-tree', `${syncA1.root}:writers`], { encoding: 'utf8' })
    assert.match(treeA.trim(), /^100644 blob [0-9a-f]{40}\twriter-a\.ndjson$/)

    const blobOidA = treeA.trim().split(/\s+/)[2]
    const blobContentA = execFileSync('git', ['-C', repoA, 'cat-file', '-p', blobOidA], { encoding: 'utf8' })
    const fileContentA = readFileSync(join(commonA, 'wanxiang', 'events', 'writer-a.ndjson'), 'utf8')
    assert.equal(blobContentA, fileContentA, 'entire local writer file is one Git blob without segments')

    // 2. Repo B has writer-b, fetches Repo A objects, and syncs against rootA -> convergence
    execFileSync('git', ['-C', repoB, 'fetch', '-q', repoA, syncA1.root])
    const handleB = eventStore.create(commonB, 'writer-b')
    await eventStore.append(handleB, [make(B, 'stream/2')])
    eventStore.dispose(handleB)

    const syncB1 = await retention.syncAt(repoB, commonB, syncA1.root, now)
    assert.equal(syncB1.ok, true, syncB1.ok ? '' : JSON.stringify(syncB1.error))
    assert.equal(existsSync(join(commonB, 'wanxiang', 'events', 'writer-a.ndjson')), true)
    assert.equal(existsSync(join(commonB, 'wanxiang', 'events', 'writer-b.ndjson')), true)

    // 3. Repeat sync on Repo B is idempotent
    const syncB2 = await retention.syncAt(repoB, commonB, syncB1.root, now)
    assert.equal(syncB2.ok, true)
    assert.equal(syncB2.root, syncB1.root, 'repeat sync produces identical snapshot root')

    // 4. Two offline sides converge to identical snapshot root
    execFileSync('git', ['-C', repoA, 'fetch', '-q', repoB, syncB1.root])
    const syncA2 = await retention.syncAt(repoA, commonA, syncB1.root, now)
    assert.equal(syncA2.ok, true)
    assert.equal(syncA2.root, syncB1.root, 'two sides converge to identical snapshot root')
    assert.equal(existsSync(join(commonA, 'wanxiang', 'events', 'writer-b.ndjson')), true)

    // 5. Corrupted / divergent writer history fails closed
    const canonicalLine = (event) => JSON.stringify({
      event_id: event.id,
      event_type: event.type,
      parents: [...event.parents].sort(),
      payload: event.payload,
      payload_refs: [...event.payloadRefs].sort(),
      stream_id: event.stream,
    }) + '\n'
    writeFileSync(join(commonA, 'wanxiang', 'events', 'writer-b.ndjson'), canonicalLine(make(C, 'stream/divergent')))
    const syncDivergent = await retention.syncAt(repoA, commonA, syncB1.root, now)
    assert.equal(syncDivergent.ok, false, 'divergent writer history must fail closed')
    assert.match(String(syncDivergent.error), /writer history diverged/i)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
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
