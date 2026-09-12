import assert from 'node:assert/strict'
import test from 'node:test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

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

test('WHAT[HOST-BOUNDARY-027] Host message loop and envelope slices reject the old wide signal closure', () => {
  assertPureContract()
  assertEffectIsInjected('host')
})

test('WHAT[HOST-BOUNDARY-027] production inventory closes Host codec audiences without the wide signal adapter', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const envelope = requireShard(projects, 'host-event-envelope')
  const message = requireShard(projects, 'host-message-codec')
  const loop = requireShard(projects, 'loop-event-codec')

  assert.equal(envelope.subsystem, 'host')
  assert.equal(message.subsystem, 'host')
  assert.equal(loop.subsystem, 'host')

  assert.deepEqual(relSources(envelope), ['src/Wanxiangshu/OpenCode/Codec/HostEventEnvelope.fs'])
  assert.deepEqual(relSources(message), ['src/Wanxiangshu/OpenCode/Codec/HostMessageCodec.fs'])

  const messageSources = closureSources(message, projects)
  assert.ok(messageSources.has('src/Wanxiangshu/OpenCode/Host/Message.fs'))
  for (const unrelated of [
    'src/Wanxiangshu/OpenCode/Codec/OpencodeTypes.fs',
    'src/Wanxiangshu/OpenCode/Signals/EventContract.fs',
    'src/Wanxiangshu/Host/Digest.fs',
  ])
    assert.ok(!messageSources.has(unrelated), `message codec must not acquire ${unrelated}`)
  assert.deepEqual(relSources(loop), ['src/Wanxiangshu/OpenCode/Codec/LoopEventCodec.fs'])
  // LoopEventCodec produces JournalAppendOutcome-shaped values — its decode path legitimately
  // references the runtime-platform Outcome vocabulary (retagged JS codec tier authority).
  assert.deepEqual(refShards(loop, projects), ['host-event-envelope', 'identity', 'outcome'])

  for (const id of ['host-session-runtime', 'authority-runtime-surface', 'opencode-codec-providerprojectionsurface']) {
    const consumer = requireShard(projects, id)
    assert.ok(refShards(consumer, projects).includes('host-message-codec'), `${id} must consume the message codec contract`)
    assert.ok(!refShards(consumer, projects).includes('host-signal-adapter'), `${id} must not consume the wide signal adapter`)
  }

  const loopRuntime = requireShard(projects, 'execution-session-loopdetector')
  assert.ok(refShards(loopRuntime, projects).includes('loop-event-codec'))
  assert.ok(!refShards(loopRuntime, projects).includes('host-signal-adapter'))
  assert.ok(!refShards(loopRuntime, projects).includes('host-diagnostics-runtime'))

  const signalAdapter = requireShard(projects, 'host-signal-adapter')
  assert.ok(refShards(signalAdapter, projects).includes('host-event-envelope'))
  assert.ok(refShards(signalAdapter, projects).includes('loop-event-codec'))
  assert.ok(!relSources(signalAdapter).includes('src/Wanxiangshu/OpenCode/Codec/HostMessageCodec.fs'))
  assert.ok(!relSources(signalAdapter).includes('src/Wanxiangshu/OpenCode/Codec/LoopEventCodec.fs'))

  const visibility = requireShard(projects, 'opencode-host-messagevisibility')
  assert.ok(refShards(visibility, projects).includes('host-event-envelope'))
  assert.ok(!refShards(visibility, projects).includes('host-signal-adapter'))
  assert.ok(refShards(requireShard(projects, 'execution-delegation-hostturnobservedsurface'), projects).includes('host-event-envelope'))
})

test('WHAT[HOST-BOUNDARY-026] tool registration compiles without signal routing or terminal bus implementations', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const tool = [...projects.values()].find((entry) => relSources(entry).includes('src/Wanxiangshu/OpenCode/Codec/ToolHostCodec.fs'))
  assert.ok(tool, 'tool codec must have a production compile shard')

  const sources = closureSources(tool, projects)
  assert.ok(sources.has('src/Wanxiangshu/Host/Contract/ToolResultBound.fs'))
  for (const unrelated of [
    'src/Wanxiangshu/OpenCode/Codec/HostEventCodec.fs',
    'src/Wanxiangshu/OpenCode/Signals/HostSignal.fs',
    'src/Wanxiangshu/OpenCode/Signals/HostSignalAdapter.fs',
    'src/Wanxiangshu/OpenCode/Host/Events.fs',
    'src/Wanxiangshu/OpenCode/Host/SharedTerminalBus.fs',
    'src/Wanxiangshu/Execution/Failure/Model.fs',
    'src/Wanxiangshu/Persistence/Journal/RuntimePath.fs',
  ])
    assert.ok(!sources.has(unrelated), `tool adapter must not acquire ${unrelated}`)

  for (const id of [
    'interaction-attention-fold',
    'interaction-concern-fold',
    'enforcer-institutionallearning-fold',
    'opencode-tools-filemutationtools',
    'opencode-tools-bookkeepertool',
    'opencode-tools-fetchtool',
  ]) {
    const consumerSources = closureSources(requireShard(projects, id), projects)
    assert.ok(consumerSources.has('src/Wanxiangshu/OpenCode/Codec/ToolHostCodec.fs'))
    for (const unrelated of [
      'src/Wanxiangshu/OpenCode/Codec/HostEventCodec.fs',
      'src/Wanxiangshu/OpenCode/Signals/HostSignal.fs',
      'src/Wanxiangshu/OpenCode/Host/Events.fs',
      'src/Wanxiangshu/OpenCode/Host/SharedTerminalBus.fs',
    ])
      assert.ok(!consumerSources.has(unrelated), `${id} must not acquire ${unrelated}`)
  }

  const signalSources = closureSources(requireShard(projects, 'host-signal-adapter'), projects)
  assert.ok(!signalSources.has('src/Wanxiangshu/OpenCode/Codec/ToolHostCodec.fs'), 'signal adapter must not acquire tool registration')
})

test('WHAT[HOST-BOUNDARY-027] Host envelope projection is shared and never mutates the raw payload', () => {
  const payload = { type: 'message.updated', properties: { sessionID: 'session-1', info: { sessionID: 'session-2' } } }
  const input = { directory: '/must-not-cross', payload }

  assert.equal(HostSignalSurface.unwrapEnvelope(input), payload)
  assert.equal(Object.hasOwn(payload, 'directory'), false)
  assert.equal(HostSignalSurface.envelopeEventType(input), 'message.updated')
  assert.equal(HostSignalSurface.envelopeSessionId(input), 'session-1')
  assert.equal(HostSignalSurface.envelopeMessageSessionId(input), 'session-1')
  assert.equal(
    HostSignalSurface.envelopeMessageSessionId({ payload: { type: 'message.updated', properties: { info: { sessionID: 'session-2' } } } }),
    'session-2',
  )
})

test('WHAT[HOST-BOUNDARY-030] Host envelope rejects adjacent malformed event and session carriers without throwing', () => {
  for (const value of ['', ' ', 7, true, {}, [], new String('session-1')]) {
    const raw = { type: value, properties: { sessionID: value, sessionId: value, info: { sessionID: value } }, sessionID: value, sessionId: value }
    assert.doesNotThrow(() => HostSignalSurface.envelopeEventType(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeSessionId(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeMessageSessionId(raw))
    assert.equal(HostSignalSurface.envelopeEventType(raw), '')
    assert.equal(HostSignalSurface.envelopeSessionId(raw), null)
    assert.equal(HostSignalSurface.envelopeMessageSessionId(raw), null)
  }

  for (const raw of [null, 7, true, 'session-1', [], {}]) {
    assert.doesNotThrow(() => HostSignalSurface.unwrapEnvelope(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeEventType(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeSessionId(raw))
    assert.doesNotThrow(() => HostSignalSurface.envelopeMessageSessionId(raw))
  }
})

test('WHAT[HOST-BOUNDARY-028] typed subscription and diagnostic injection preserve one failure owner', async () => {
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

test('WHAT[HOST-BOUNDARY-029] fatal vocabulary stays pure and physical execution is composition-only', () => {
  assertPureContract()
  assertFatalBoundary('host-boundary')
})

test('WHAT[HOST-BOUNDARY-031] RootWorkspace runtime is private and every observer consumes only the typed contract', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const contract = requireShard(projects, 'host-root-workspace-contract')
  const runtime = requireShard(projects, 'host-root-workspace-runtime')

  assert.equal(contract.subsystem, 'host')
  assert.equal(runtime.subsystem, 'host')
  assert.deepEqual(relSources(contract), ['src/Wanxiangshu/OpenCode/Host/RootWorkspace.fs'])
  assert.deepEqual(refShards(contract, projects), [])
  assert.deepEqual(relSources(runtime), ['src/Wanxiangshu/OpenCode/Host/RootWorkspaceRuntime.fs'])
  assert.deepEqual(refShards(runtime, projects), ['host-root-workspace-contract'])

  const runtimeConsumers = [...projects.values()]
    .filter((candidate) => refShards(candidate, projects).includes('host-root-workspace-runtime'))
    .map((candidate) => candidate.shard)
    .sort()
  assert.deepEqual(runtimeConsumers, ['opencode-host-sharedstatesurface', 'plugin-composition'])

  assert.ok(refShards(requireShard(projects, 'opencode-host-hostsignalbootstrap'), projects).includes('host-root-workspace-contract'))
  assert.ok(!refShards(requireShard(projects, 'opencode-host-hostsignalbootstrap'), projects).includes('host-root-workspace-runtime'), 'opencode-host-hostsignalbootstrap must consume only typed contract, not process-local runtime')

  for (const id of [
    'execution-delegation-hostturnobservedsurface',
    'git-integrationgate',
    'interaction-repair-interactionrepair',
    'opencode-host-pluginruntimescope',
    'participant-provider-attempt-fallback-ledger',
  ])
    assert.ok(!refShards(requireShard(projects, id), projects).includes('host-root-workspace-runtime'), `${id} must not acquire the process-local runtime`)
})
