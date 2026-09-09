import assert from 'node:assert/strict'
import { basename, join, resolve } from 'node:path'
import test from 'node:test'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { planOwnerCompile } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')
const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })

const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))

const projectMetadata = [...subsystemInventory.projects.values()].map((project) => ({
  ...project,
  path: project.projectPath,
  name: basename(project.projectPath),
  compile: project.implementationFiles.map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/')),
}))

const requireShard = (shard) => {
  const matches = projectMetadata.filter((project) => project.shard === shard)
  assert.equal(matches.length, 1, `${shard} must resolve to exactly one compile shard, found ${matches.length}`)
  return matches[0]
}

const planShard = (shard) => {
  const project = requireShard(shard)
  return {
    project,
    plan: planOwnerCompile({ projectPath: project.path, aggregatePath: AGGREGATE }),
  }
}

const productionSources = (plan) => plan.compileItems
  .filter((path) => path.endsWith('.fs'))
  .map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/'))

test('WHAT[HOST-BOUNDARY-026] host session contract compiles independently without runtime or sphinx dependencies', () => {
  const { plan } = planShard('host-session-contract')
  const sources = productionSources(plan)

  assert.ok(sources.includes('OpenCode/Host/SessionContract.fs'), 'host-session-contract must include SessionContract.fs')
  assert.ok(sources.includes('OpenCode/Host/SessionHostPort.fs'), 'host-session-contract must include SessionHostPort.fs')
  assert.ok(sources.includes('OpenCode/Host/SessionSnapshot.fs'), 'host-session-contract must include SessionSnapshot.fs')
  const forbidden = [
    'OpenCode/Codec/ToolHostCodec.fs',
    'OpenCode/Codec/ToolHostSurface.fs',
    'OpenCode/Codec/HostEventCodec.fs',
    'OpenCode/Signals/HostSignalAdapter.fs',
    'OpenCode/Signals/HostSignalSubscribe.fs',
    'OpenCode/Host/Events.fs',
    'OpenCode/Host/SharedTerminalBus.fs',
    'OpenCode/Host/SphinxMcpConfig.fs',
    'OpenCode/Host/SphinxMcpConfigSurface.fs',
    'OpenCode/Host/Diagnostic.fs',
    'OpenCode/Host/ReliabilityDiagnostics.fs',
    'OpenCode/Host/ReliabilityDiagnosticsSurface.fs',
    'OpenCode/Host/HookPolicy.fs',
    'OpenCode/Host/HookPolicySurface.fs',
    'OpenCode/Host/Sessions.fs',
    'OpenCode/Host/SessionSnapshotPort.fs',
    'OpenCode/Host/SessionSnapshotSurface.fs',
    'OpenCode/Host/SessionQuiescenceGate.fs',
    'OpenCode/Host/QuiescenceSurface.fs',
    'OpenCode/Host/HostMessageProjection.fs',
    'OpenCode/Host/HostSessionContext.fs',
    'OpenCode/Host/HostBoundarySurface.fs',
    'OpenCode/Host/HostSessionContextSurface.fs',
  ]

  for (const item of forbidden) {
    assert.ok(!sources.includes(item), `host-session-contract closure must not contain ${item}`)
  }

  const forbiddenProcessRuntime = [
    'Process/ProcessRunner.fs',
    'Process/NodeProcessHost.fs',
    'Process/ProcessRequest.fs',
    'Process/JsSandbox.fs',
    'OpenCode/Tools/PtyTool.fs',
  ]
  for (const item of forbiddenProcessRuntime) {
    assert.ok(!sources.includes(item), `host-session-contract closure must not contain process runtime ${item}`)
  }

  assert.ok(!sources.some((s) => s.startsWith('Sphinx/')), 'host-session-contract closure must not contain Sphinx runtime')

  for (const consumer of ['opencode-host-opencodeport', 'strength-policy']) {
    const consumerSources = productionSources(planShard(consumer).plan)
    assert.ok(consumerSources.includes('OpenCode/Codec/OpencodeTypes.fs'))
    for (const unrelated of ['OpenCode/Signals/EventContract.fs', 'OpenCode/Host/Message.fs']) {
      assert.ok(!consumerSources.includes(unrelated), `${consumer} must not acquire ${unrelated}`)
    }
  }
  const portSources = productionSources(planShard('opencode-host-opencodeport').plan)
  assert.ok(!portSources.includes('Host/Digest.fs'), 'OpenCode port contract must not acquire Host/Digest.fs')

  for (const consumer of ['host-signal-contract', 'delegation-sync-runtime', 'host-diagnostics-runtime', 'opencode-host-messagevisibility']) {
    const consumerSources = productionSources(planShard(consumer).plan)
    for (const unrelated of ['Host/Digest.fs', 'OpenCode/Codec/OpencodeTypes.fs', 'OpenCode/Host/Message.fs']) {
      assert.ok(!consumerSources.includes(unrelated), `${consumer} must not acquire ${unrelated}`)
    }
  }
  for (const consumer of ['host-diagnostics-runtime', 'opencode-host-messagevisibility']) {
    const consumerSources = productionSources(planShard(consumer).plan)
    assert.ok(!consumerSources.includes('OpenCode/Signals/EventContract.fs'), `${consumer} must not acquire terminal event vocabulary`)
  }

  const adapterSources = productionSources(planShard('host-signal-adapter').plan)
  for (const required of ['Execution/Failure/Model.fs', 'Execution/Session/ChatExecution/Facts.fs', 'Persistence/Journal/RuntimePath.fs']) {
    assert.ok(adapterSources.includes(required), `signal adapter must compile its actual dependency ${required}`)
  }
  for (const unrelated of ['OpenCode/Codec/OpencodeTypes.fs', 'OpenCode/Host/Message.fs']) {
    assert.ok(!adapterSources.includes(unrelated), `signal adapter must not acquire ${unrelated}`)
  }
})

test('WHAT[HOST-BOUNDARY-026] Host source ownership follows subsystem inventory and physical boundaries', () => {
  const hostSources = [
    'OpenCode/Host/SessionContract.fs',
    'OpenCode/Signals/EventContract.fs',
    'OpenCode/Host/Message.fs',
    'OpenCode/Codec/OpencodeTypes.fs',
    'OpenCode/Codec/ToolHostCodec.fs',
    'OpenCode/Codec/ToolHostSurface.fs',
    'OpenCode/Host/Diagnostic.fs',
    'OpenCode/Signals/HostSignalAdapter.fs',
    'OpenCode/Host/SessionQuiescenceGate.fs',
    'OpenCode/Host/SphinxMcpConfig.fs',
  ]
  for (const source of hostSources) {
    const owner = shardInventory.sourceProject.get(join(SOURCE_ROOT, source))
    assert.ok(owner, `${source} must have a unique production shard`)
    assert.equal(subsystemInventory.projects.get(owner.projectPath).subsystem, 'host', `${source} belongs to the Host subsystem`)
  }
  const digestOwner = shardInventory.sourceProject.get(join(SOURCE_ROOT, 'Host/Digest.fs'))
  assert.ok(digestOwner, 'HostDigest must have a unique production shard')
  assert.equal(subsystemInventory.projects.get(digestOwner.projectPath).subsystem, 'runtime-platform')

  const sessionContract = requireShard('host-session-contract')
  assert.deepEqual(
    sessionContract.compile.sort(),
    ['OpenCode/Host/SessionContract.fs', 'OpenCode/Host/SessionHostPort.fs', 'OpenCode/Host/SessionSnapshot.fs'].sort(),
  )

  const diagnosticsRuntime = requireShard('host-diagnostics-runtime')
  assert.ok(diagnosticsRuntime.compile.includes('OpenCode/Host/HookPolicy.fs'))
  assert.ok(diagnosticsRuntime.compile.includes('OpenCode/Host/ReliabilityDiagnostics.fs'))
  assert.ok(diagnosticsRuntime.compile.includes('OpenCode/Host/Diagnostic.fs'))

  const signalAdapter = requireShard('host-signal-adapter')
  assert.ok(signalAdapter.compile.includes('OpenCode/Signals/HostSignalAdapter.fs'))
  assert.ok(signalAdapter.compile.includes('OpenCode/Signals/HostSignalSubscribe.fs'))
  assert.ok(signalAdapter.compile.includes('OpenCode/Host/Events.fs'))
  assert.ok(signalAdapter.compile.includes('OpenCode/Host/SharedTerminalBus.fs'))

  const sessionRuntime = requireShard('host-session-runtime')
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/SessionQuiescenceGate.fs'))
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/QuiescenceSurface.fs'))
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/HostMessageProjection.fs'))
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/HostSessionContext.fs'))

  const sphinxAdapter = requireShard('sphinx-host-adapter')
  assert.deepEqual(
    sphinxAdapter.compile.sort(),
    ['OpenCode/Host/SphinxMcpConfig.fs', 'OpenCode/Host/SphinxMcpConfigSurface.fs'].sort(),
  )

  // Unique production ownership, sibling signatures and aggregate coverage are
  // enforced by readCompileShardInventory for every shard, including explicit ones.

  // Verify delegation ref migration in host-boundary consumers
  const sharedStateSurface = projectMetadata.find((p) => p.name === 'Wanxiangshu.Owner.host-boundary.opencode-host-sharedstatesurface.fsproj')
  assert.ok(sharedStateSurface, 'opencode-host-sharedstatesurface.fsproj must exist')
  assert.ok(
    !sharedStateSurface.references.some((r) => r.includes('execution-delegation-handle-surface')),
    'opencode-host-sharedstatesurface must not reference old delegation-handle-surface',
  )

  const hostSignalBootstrap = projectMetadata.find((p) => p.name === 'Wanxiangshu.Owner.host-boundary.opencode-host-hostsignalbootstrap.fsproj')
  assert.ok(hostSignalBootstrap, 'opencode-host-hostsignalbootstrap.fsproj must exist')
  // Physical truth: the plugin composition chain (PluginHooks/PluginSessionWiring) consumes the
  // delegation ledger and now lives in opencode-plugin.plugin-composition (asserted below);
  // bootstrap still consumes SyncDelegateHostObservation (hostturnobservedsurface).
  // SyncDelegateRuntime Host integration lives in delegation-host-adapter while
  // Wait/Store/Prompt/Workflow stay in delegation-sync-runtime (see AGENTS delegation split).
  assert.ok(
    hostSignalBootstrap.references.some((r) => r.includes('execution-delegation-hostturnobservedsurface')),
    'opencode-host-hostsignalbootstrap must reference the delegation host-turn-observed surface',
  )
  // The plugin composition chain (PluginHooks/PluginSessionWiring) consumes the delegation
  // ledger and moved to the opencode-plugin owner; the consuming composition must reference it.
  const pluginComposition = projectMetadata.find((p) => p.name === 'Wanxiangshu.Owner.opencode-plugin.plugin-composition.fsproj')
  assert.ok(pluginComposition, 'opencode-plugin.plugin-composition.fsproj must exist')
  assert.ok(
    pluginComposition.references.some((r) => r.includes('execution-delegation-ledger')),
    'opencode-plugin.plugin-composition must reference the persistence-backed delegation ledger',
  )
  assert.ok(
    !hostSignalBootstrap.references.some((r) => r.includes('delegation-host-adapter')),
    'opencode-host-hostsignalbootstrap must not bypass plugin runtime composition to the delegation Host adapter',
  )
})
