import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const requireShard = (projects, shardId) => {
  const matches = [...projects.values()].filter((candidate) => candidate.shard === shardId)
  assert.equal(matches.length, 1, `${shardId} must resolve to exactly one production compile shard`)
  return matches[0]
}
const relSources = (project) => project.implementationFiles.map((p) => path.relative(ROOT, p)).sort()
const refShards = (project, projects) => project.references.map((refPath) => projects.get(refPath).shard).sort()
const closureSources = (root, projects) => {
  const closure = new Set()
  const pending = [root]
  while (pending.length > 0) {
    const project = pending.pop()
    if (closure.has(project)) continue
    closure.add(project)
    for (const refPath of project.references) {
      pending.push(projects.get(refPath))
    }
  }
  return new Set([...closure].flatMap(relSources))
}

test('WHAT[host-boundary-028] typed subscription and diagnostic injection preserve one failure owner', async () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const adapter = requireShard(projects, 'host-signal-adapter')
  const composition = requireShard(projects, 'opencode-host-hostsignalbootstrap')

  assert.ok(!refShards(adapter, projects).includes('host-diagnostics-runtime'))
  assert.ok(!refShards(adapter, projects).includes('foundation-temporal'))
  assert.ok(refShards(composition, projects).includes('host-signal-adapter'))
  assert.ok(refShards(composition, projects).includes('host-diagnostics-runtime'))
  await assertOptionalObservationNoninterference()
  assertEffectIsInjected('console')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");
const ReconcileSurface = await import("../../../dist/Composition/Turn/ReconcileSurface.js");
const HostSignalSubscribeSurface = await import("../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const idleWake = ReconcileSurface.idleWake('s1', 1n)

test('WHAT[host-boundary-028] HOST_signal_subscribe_defaults_to_local_event_hook', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ serverUrl: 'http://localhost:4096', client: null }, () => {})
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'LocalEventHook')
})
test('WHAT[host-boundary-028] HOST_signal_subscribe_embedded_uses_legacy_listen_when_present', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ events: { listen: () => () => {} } }, () => {})
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'EventsListen')
})
test('WHAT[host-boundary-028] HOST_signal_subscribe_bad_listener_fails_closed', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ events: { listen: () => null } }, () => {})
  assert.equal(result.ok, false)
  assert.match(result.error, /invalid disposer/)
})
test('WHAT[host-boundary-028] HOST_signal_subscribe_client_events_listen_supported', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe({ client: { events: { listen: () => () => {} } } }, () => {})
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'EventsListen')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const HostSignalSubscribeSurface = await import("../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const idleRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'idle' } } })
const dedicatedIdleRaw = (sessionId) => ({ type: 'session.idle', properties: { sessionID: sessionId } })
const retryRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'retry', attempt: '2', message: 'rate limited' } } })
const deletedRaw = (sessionId, parentID) => ({ type: 'session.deleted', sessionID: sessionId, properties: { parentID } })
const errorRaw = (sessionId, name = 'TimeoutError') => ({ type: 'session.error', sessionID: sessionId, properties: { error: { name } } })
const trySubscribe = async (input = {}) => HostSignalSubscribeSurface.trySubscribe(input, () => {})

test('WHAT[host-boundary-028] MISC_signals_subscription_mode_is_closed', async () => {
  const local = await trySubscribe({})
  assert.deepEqual(local, { ok: true, mode: 'LocalEventHook', dispose: null })

  let disposed = false
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { events: { listen: () => () => { disposed = true } } },
    () => {},
  )
  assert.equal(result.mode, 'EventsListen')
  assert.equal(typeof result.dispose, 'function')
  result.dispose()
  assert.equal(disposed, true)
})
test('WHAT[host-boundary-028] MISC_signals_listener_capability_fails_closed', async () => {
  const noListen = await trySubscribe({ events: {} })
  assert.deepEqual(noListen, { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen unavailable' })

  for (const listen of [null, 7, {}, 'listen']) {
    assert.deepEqual(await trySubscribe({ events: { listen } }), noListen)
  }

  const clientListen = () => () => {}
  assert.deepEqual(
    await trySubscribe({ events: 7, client: { events: { listen: clientListen } } }),
    { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' },
  )
})
test('WHAT[host-boundary-028] MISC_signals_disposer_capability_fails_closed', async () => {
  const expected = { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen returned invalid disposer' }

  for (const disposer of [null, 7, {}, 'dispose', Promise.resolve()]) {
    assert.deepEqual(await trySubscribe({ events: { listen: () => disposer } }), expected)
  }
})
test('WHAT[host-boundary-028] MISC_signals_input_carriers_fail_closed', async () => {
  const expected = { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' }

  for (const input of [null, 7, 'input', [], new String('input'), new Date(0)]) {
    assert.deepEqual(await trySubscribe(input), expected)
  }

  for (const input of [
    { events: [] },
    { events: 'events' },
    { client: 7 },
    { client: [] },
    { client: new String('client') },
    { client: new Date(0) },
    { client: { events: [] } },
  ]) {
    assert.deepEqual(await trySubscribe(input), expected)
  }

  assert.deepEqual(await trySubscribe({ events: null, client: null }), { ok: true, mode: 'LocalEventHook', dispose: null })
})
test('WHAT[host-boundary-028] MISC_signals_listener_throw_is_typed', async () => {
  const result = await trySubscribe({ events: { listen: () => { throw new Error('listener boom') } } })
  assert.deepEqual(result, { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen failed: listener boom' })

  for (const thrown of [null, 7, 'wire boom']) {
    const adjacent = await trySubscribe({ events: { listen: () => { throw thrown } } })
    assert.equal(adjacent.ok, false)
    assert.match(adjacent.error, /^OPENCODE-SIGNAL-SUBSCRIBE: events\.listen failed:/)
  }
})
test('WHAT[host-boundary-028] MISC_signals_throwing_accessors_resolve_typed_failure', async () => {
  const topLevelEvents = {}
  Object.defineProperty(topLevelEvents, 'events', { get: () => { throw new Error('events getter boom') } })
  assert.deepEqual(await trySubscribe(topLevelEvents), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })

  const topLevelClient = {}
  Object.defineProperty(topLevelClient, 'client', { get: () => { throw new Error('client getter boom') } })
  assert.deepEqual(await trySubscribe(topLevelClient), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })

  const client = {}
  Object.defineProperty(client, 'events', { get: () => { throw new Error('client events getter boom') } })
  assert.deepEqual(await trySubscribe({ client }), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })

  const events = {}
  Object.defineProperty(events, 'listen', { get: () => { throw new Error('listen getter boom') } })
  assert.deepEqual(
    await trySubscribe({ events }),
    { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen failed: listen getter boom' },
  )

  const proxy = new Proxy({}, { get: () => { throw new Error('proxy boom') } })
  assert.deepEqual(await trySubscribe(proxy), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })
})
test('WHAT[host-boundary-028] MISC_signals_disposer_throw_reaches_resource_owner', async () => {
  const result = await trySubscribe({ events: { listen: () => () => { throw new Error('dispose boom') } } })
  assert.equal(result.mode, 'EventsListen')
  assert.throws(() => result.dispose(), /dispose boom/)
})
test('WHAT[host-boundary-028] MISC_signals_invalid_callback_fails_closed_at_surface', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { events: { listen: () => () => {} } },
    null,
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /callback unavailable/)
})
test('WHAT[host-boundary-028] MISC_signals_default_input_resolves_to_local_event_hook', async () => {
  const result = await trySubscribe({})
  assert.deepEqual(result, { ok: true, mode: 'LocalEventHook', dispose: null })
})
test('WHAT[host-boundary-028] MISC_signals_opencode_class_client_without_legacy_events_uses_local_hook', async () => {
  class OpenCodeClient {
    constructor(events) { this.events = events }
  }

  assert.deepEqual(
    await trySubscribe({ client: new OpenCodeClient(undefined) }),
    { ok: true, mode: 'LocalEventHook', dispose: null },
  )

  let disposed = false
  const legacy = await HostSignalSubscribeSurface.trySubscribe(
    { client: new OpenCodeClient({ listen: () => () => { disposed = true } }) },
    () => {},
  )
  assert.equal(legacy.mode, 'EventsListen')
  legacy.dispose()
  assert.equal(disposed, true)
})
test('WHAT[host-boundary-028] MISC_signals_client_events_listen_fallback', async () => {
  let called = false
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { client: { events: { listen: () => () => { called = true } } } },
    () => {},
  )
  assert.equal(result.mode, 'EventsListen')
  result.dispose()
  assert.equal(called, true)
})
test('WHAT[host-boundary-028] MISC_signals_server_url_ignored_in_favor_of_local_hook', async () => {
  const result = await trySubscribe({ serverUrl: 'http://localhost:4096' })
  assert.deepEqual(result, { ok: true, mode: 'LocalEventHook', dispose: null })
})
}
