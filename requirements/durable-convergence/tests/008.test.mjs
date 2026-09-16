import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[DURABLE-CONVERGENCE-008] reference-transaction and pre-push both call the same full bidirectional converge', async () => {
  const sync = await read('src/Wanxiangshu/Git/Hook/Sync.fs')
  assert.match(sync, /let runPrePush/)
  assert.match(sync, /converge remote None/)
  assert.match(sync, /let runReferenceTransaction/)
  assert.match(sync, /converge remote observed/)
  assert.doesNotMatch(sync, /ConvergeObserved|downloadOnly|uploadOnly/i)
})

test('WHAT[DURABLE-CONVERGENCE-008] reference-transaction observed root changes discovery only not sync direction', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.match(gateway, /let converge/)
  assert.match(gateway, /match observedRemote with/)
  assert.match(gateway, /WriterStreamSync\.syncWriterStreams/)
  assert.match(gateway, /pushSnapshot/)
  assert.match(gateway, /discoverRemote/)
  assert.doesNotMatch(gateway, /IEventStore|CanonicalIntegrator|WorkspaceEventStore/)
})

test('WHAT[DURABLE-CONVERGENCE-008] lease race refetches and repeats the same k-way sync boundedly', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.match(gateway, /--force-with-lease/)
  assert.match(gateway, /retriesLeft/)
  assert.match(gateway, /discoverRemote run remote/)
  assert.match(gateway, /ConvergeRetryExhausted/)
})

test('WHAT[DURABLE-CONVERGENCE-008] product process has no fetch pull push remote API', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  const boot = await read('src/Wanxiangshu/OpenCode/Plugin/PluginBoot.fs')
  const activation = await read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  assert.doesNotMatch(gateway, /type IGitGateway|member _\.(Fetch|Pull|Push)\(/)
  assert.doesNotMatch(boot, /HookDispatcher\.ensure/, 'plugin load must not mutate Git')
  assert.match(activation, /lazy[\s\S]*HookDispatcher\.ensure/)
  assert.doesNotMatch(activation, /GitGateway\.converge|\.(Fetch|Pull|Push)\(/i)
})

test('WHAT[DURABLE-CONVERGENCE-008] hook-internal Git commands are recursion guarded and pre-push is not reentered', async () => {
  const runner = await read('resources/git/wanxiang-hook.mjs')
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.match(runner, /WANXIANG_GIT_SYNC_ACTIVE/)
  assert.match(gateway, /--no-verify/)
  assert.match(gateway, /WANXIANG_GIT_SYNC_ACTIVE|SyncActiveEnv/)
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
