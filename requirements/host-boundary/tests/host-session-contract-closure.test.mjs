import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { basename, join, resolve } from 'node:path'
import test from 'node:test'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { planOwnerCompile } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')
const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })

const projectMetadata = readdirSync(SOURCE_ROOT)
  .filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name))
  .map((name) => {
    const path = join(SOURCE_ROOT, name)
    const xml = readFileSync(path, 'utf8')
    const owner = xml.match(/<WanxiangshuSemanticOwner>([^<]+)<\/WanxiangshuSemanticOwner>/)?.[1]?.trim()
    const locality = xml.match(/<WanxiangshuOwnerLocality>([^<]+)<\/WanxiangshuOwnerLocality>/)?.[1]?.trim()
    const kind = xml.match(/<WanxiangshuOwnerLocalityKind>([^<]+)<\/WanxiangshuOwnerLocalityKind>/)?.[1]?.trim()
    const compile = [...xml.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/?\s*>/g)].map((m) => m[1].replaceAll('\\', '/'))
    const references = [...xml.matchAll(/<ProjectReference\s+Include="([^"]+\.fsproj)"\s*\/?\s*>/g)].map((m) => m[1].replaceAll('\\', '/'))
    return {
      path,
      name,
      owner,
      locality,
      kind,
      compile,
      references,
      xml,
    }
  })

const projectByPath = new Map(projectMetadata.map((project) => [resolve(project.path), project]))

const requireLocality = (locality) => {
  const matches = projectMetadata.filter((project) => project.locality === locality)
  assert.equal(matches.length, 1, `${locality} must resolve to exactly one owner project, found ${matches.length}`)
  return matches[0]
}

const planLocality = (locality) => {
  const project = requireLocality(locality)
  return {
    project,
    plan: planOwnerCompile({ projectPath: project.path, aggregatePath: AGGREGATE }),
  }
}

const productionSources = (plan) => plan.compileItems
  .filter((path) => path.endsWith('.fs'))
  .map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/'))

const TARGET_HOST_LOCALITIES = new Map([
  ['host-session-contract', 'contract'],
  ['host-signal-contract', 'contract'],
  ['host-diagnostics-runtime', 'runtime'],
  ['host-signal-adapter', 'adapter'],
  ['host-session-runtime', 'runtime'],
  ['sphinx-host-adapter', 'adapter'],
])

test('WHAT[HOST-BOUNDARY-026] host session contract compiles independently without runtime or sphinx dependencies', () => {
  const { project, plan } = planLocality('host-session-contract')
  assert.equal(project.kind, 'contract', 'host-session-contract must declare contract kind')

  // Transitive closure must not contain runtime or adapter localities
  for (const projectPath of plan.projectPaths) {
    const meta = projectByPath.get(resolve(projectPath))
    if (meta?.kind) {
      assert.equal(
        meta.kind,
        'contract',
        `host-session-contract contract closure contains non-contract project ${basename(projectPath)} (${meta.kind})`,
      )
    }
  }

  const sources = productionSources(plan)

  assert.ok(sources.includes('OpenCode/Host/SessionContract.fs'), 'host-session-contract must include SessionContract.fs')
  assert.ok(sources.includes('OpenCode/Host/SessionHostPort.fs'), 'host-session-contract must include SessionHostPort.fs')
  assert.ok(sources.includes('OpenCode/Host/SessionSnapshot.fs'), 'host-session-contract must include SessionSnapshot.fs')
  const forbidden = [
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
    const consumerSources = productionSources(planLocality(consumer).plan)
    assert.ok(consumerSources.includes('OpenCode/Codec/OpencodeTypes.fs'))
    for (const unrelated of ['OpenCode/Signals/EventContract.fs', 'OpenCode/Host/Message.fs']) {
      assert.ok(!consumerSources.includes(unrelated), `${consumer} must not acquire ${unrelated}`)
    }
  }
  const portSources = productionSources(planLocality('opencode-host-opencodeport').plan)
  assert.ok(!portSources.includes('Host/Digest.fs'), 'OpenCode port contract must not acquire Host/Digest.fs')
})

test('WHAT[HOST-BOUNDARY-026] host boundary projects declare explicit locality kinds and exact compile ownership', () => {
  for (const [locality, expectedKind] of TARGET_HOST_LOCALITIES) {
    const { project, plan } = planLocality(locality)
    assert.equal(project.kind, expectedKind, `${locality} must declare kind '${expectedKind}'`)

    const sources = productionSources(plan)
    const budget = expectedKind === 'contract' ? 100 : 185
    assert.ok(
      sources.length <= budget,
      `${locality} (${expectedKind}) production source count ${sources.length} exceeds budget <= ${budget}`,
    )
  }

  const sessionContract = requireLocality('host-session-contract')
  assert.deepEqual(
    sessionContract.compile.sort(),
    ['OpenCode/Host/SessionContract.fs', 'OpenCode/Host/SessionHostPort.fs', 'OpenCode/Host/SessionSnapshot.fs'].sort(),
  )

  const diagnosticsRuntime = requireLocality('host-diagnostics-runtime')
  assert.ok(diagnosticsRuntime.compile.includes('OpenCode/Host/HookPolicy.fs'))
  assert.ok(diagnosticsRuntime.compile.includes('OpenCode/Host/ReliabilityDiagnostics.fs'))
  assert.ok(diagnosticsRuntime.compile.includes('OpenCode/Host/Diagnostic.fs'))

  const signalAdapter = requireLocality('host-signal-adapter')
  assert.ok(signalAdapter.compile.includes('OpenCode/Signals/HostSignalAdapter.fs'))
  assert.ok(signalAdapter.compile.includes('OpenCode/Signals/HostSignalSubscribe.fs'))
  assert.ok(signalAdapter.compile.includes('OpenCode/Host/Events.fs'))
  assert.ok(signalAdapter.compile.includes('OpenCode/Host/SharedTerminalBus.fs'))

  const sessionRuntime = requireLocality('host-session-runtime')
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/SessionQuiescenceGate.fs'))
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/QuiescenceSurface.fs'))
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/HostMessageProjection.fs'))
  assert.ok(sessionRuntime.compile.includes('OpenCode/Host/HostSessionContext.fs'))

  const sphinxAdapter = requireLocality('sphinx-host-adapter')
  assert.deepEqual(
    sphinxAdapter.compile.sort(),
    ['OpenCode/Host/SphinxMcpConfig.fs', 'OpenCode/Host/SphinxMcpConfigSurface.fs'].sort(),
  )

  // Verify the compile-shard inventory is the single source of production ownership.
  const hostBoundaryFiles = new Set(
    [...shardInventory.sourceProject]
      .filter(([, project]) => project.legacyOwner === 'host-boundary')
      .map(([sourcePath]) => sourcePath.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/')),
  )

  const hostProjects = projectMetadata.filter((project) => project.owner === 'host-boundary')
  const compiledFiles = hostProjects.flatMap((project) => project.compile).sort()

  assert.deepEqual(
    compiledFiles,
    [...hostBoundaryFiles].sort(),
    'all host-boundary files must be compiled by exactly one host-boundary owner project',
  )

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
