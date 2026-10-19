import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");

const closure = (inventory, root) => {
  const visited = new Set()
  const visit = (path) => {
    if (visited.has(path)) return
    visited.add(path)
    for (const reference of inventory.projects.get(path).references) visit(reference)
  }
  visit(root.projectPath)
  return [...visited].flatMap((path) => inventory.projects.get(path).implementationFiles)
}

test('WHAT[structured-workflow-013] session-started-at ledger consumes its own port without durable aggregate or runtime containers while attention/concern shards remain retired', () => {
  const inventory = buildSubsystemInventory()
  assert.ok(inventory.ok, inventory.violations.join('\n'))
  assert.equal(
    [...inventory.projects.values()].some((project) =>
      project.implementationFiles.some((file) =>
        /\/(?:Interaction\/Attention|Interaction\/Concern|OpenCode\/Tools\/AttentionTools|OpenCode\/Tools\/ConcernTools|Composition\/Durable\/AttentionConcernJournalAdapter)\b/.test(file))),
    false,
    'retired Attention/Concern tools and adapters must not remain in the compile shard inventory',
  )
  const ledger = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/Execution/Session/SessionStartedAtLedger.fs')))
  assert.ok(ledger, 'the real SessionStartedAtLedger consumer must have a compile shard')
  assert.equal(ledger.subsystem, 'session-lifecycle')
  for (const file of closure(inventory, ledger)) {
    assert.doesNotMatch(file, /\/(?:Composition\/Durable|Persistence\/Journal)\//,
      `session-started-at ledger closure must not acquire aggregate persistence: ${file}`)
    assert.doesNotMatch(file, /\/(?:PluginRuntimeScope|ToolRuntimeScope)\.fs$/,
      `session-started-at ledger closure must not acquire an application runtime container: ${file}`)
  }
  for (const file of [...ledger.implementationFiles, ...ledger.signatureFiles]) {
    assert.doesNotMatch(readFileSync(file, 'utf8'), /\b(?:AgentJournal|AgentFact|ProjectionSet|AgentProjectionSet)\b/,
      `session-started-at ledger boundary must not expose a foreign aggregate: ${file}`)
  }
  const port = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/Execution/Session/SessionStartedAtPort.fs')))
  assert.equal(port?.subsystem, 'session-lifecycle', 'the port vocabulary belongs to the consumer domain')
  const adapter = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/Composition/Durable/SessionStartedAtJournalAdapter.fs')))
  assert.equal(adapter?.subsystem, 'durable-composition', 'outer routing belongs to durable composition')
  const preparation = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/OpenCode/Host/TurnRuntimePreparation.fs')))
  assert.ok(preparation.references.includes(adapter.projectPath), 'the real host consumer must wire the durable adapter')
  const preparationFile = preparation.implementationFiles.find((file) => file.endsWith('/OpenCode/Host/TurnRuntimePreparation.fs'))
  assert.doesNotMatch(readFileSync(preparationFile, 'utf8'), /HostFact\.SessionStartedAtBound\b/,
    'the host consumer must not take ownership of durable routing')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { EventEmitter } = await import("node:events");
const { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, relative, resolve } = await import("node:path");
const { default: test } = await import("node:test");
const { checkSubsystems } = await import("../../../scripts/checks/subsystems.mjs");
const { planOwnerCompile, materializeOwnerCompile, compileOwnerProject } = await import("../../../scripts/lib/owner-compile.mjs");

const ROOT = resolve(import.meta.dirname, '../../..')
const SRC = join(ROOT, 'src/Wanxiangshu')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')
const inventory = checkSubsystems()
assert.ok(inventory.ok, inventory.violations.join('\n'))
function productionProject(projectName) {
  const project = inventory.projects.get(join(SRC, projectName))
  assert.ok(project, `missing production compile shard ${projectName}`)
  return project
}
function compileItems(projectName) {
  const project = productionProject(projectName)
  return [...project.signatureFiles, ...project.implementationFiles].map((path) => relative(SRC, path))
}
function references(projectName) {
  return productionProject(projectName).references.map((path) => relative(SRC, path))
}

test('WHAT[structured-workflow-013] RETIRED-GATE: GitGateway and git-hook-sync shards remain deleted under durable-events [009]/[011]', () => {
  const providerName = 'Wanxiangshu.Owner.change-integration.git-gateway.fsproj'
  const consumerName = 'Wanxiangshu.Owner.durable-convergence.git-hook-sync.fsproj'
  assert.equal(inventory.projects.has(join(SRC, providerName)), false, `${providerName} must remain retired`)
  assert.equal(inventory.projects.has(join(SRC, consumerName)), false, `${consumerName} must remain retired`)
  assert.equal(existsSync(join(SRC, 'Git/Gateway.fsi')), false, 'Git/Gateway.fsi must remain deleted')
  assert.equal(existsSync(join(SRC, 'Git/Gateway.fs')), false, 'Git/Gateway.fs must remain deleted')
})
test('WHAT[structured-workflow-013] request kind and fallback facts remain disjoint compile shards', () => {
  const requestProject = 'Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-requestkind.fsproj'
  const factsProject = 'Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-fallback-facts.fsproj'

  assert.deepEqual(compileItems(requestProject), [
    'Participant/Provider/Attempt/RequestKind.fsi',
    'Participant/Provider/Attempt/RequestKind.fs',
  ])
  assert.deepEqual(compileItems(factsProject), [
    'Participant/Provider/Attempt/Fallback/Facts.fsi',
    'Participant/Provider/Attempt/Fallback/Facts.fs',
  ])

  const capabilityRefs = references(
    'Wanxiangshu.Owner.capability-enforcement.opencode-host-managedagentconfig.fsproj',
  )
  assert.ok(capabilityRefs.includes(requestProject))
  assert.ok(!capabilityRefs.includes(factsProject))

  const durableFactRefs = references('Wanxiangshu.Owner.durable-events.composition-durable-fact.fsproj')
  assert.ok(durableFactRefs.includes(factsProject))
  assert.ok(!durableFactRefs.includes(requestProject))

  const durableCodecRefs = references('Wanxiangshu.Owner.durable-events.persistence-journal-promptfactcodec.fsproj')
  assert.ok(durableCodecRefs.includes(factsProject))
  assert.ok(!durableCodecRefs.includes(requestProject))

  const verificationRefs = references(
    'Wanxiangshu.Owner.verification-system.verification-eventstorewritersurface.fsproj',
  )
  assert.ok(verificationRefs.includes(factsProject))
  assert.ok(!verificationRefs.includes(requestProject))

  const ownerFallbackRefs = references(
    'Wanxiangshu.Owner.provider-attempt-recovery.participant-provider-attempt-fallback-fact.fsproj',
  )
  assert.ok(ownerFallbackRefs.includes(requestProject))
  assert.ok(ownerFallbackRefs.includes(factsProject))

})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { basename, join, resolve } = await import("node:path");
const { default: test } = await import("node:test");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");

const ROOT = resolve(import.meta.dirname, '../../..')
const project = (inventory, name) => {
  const match = [...inventory.projects.values()].find((entry) => basename(entry.projectPath) === name)
  assert.ok(match, `missing compile shard ${name}`)
  return match
}
const sources = (entry) => entry.implementationFiles.map((path) => path.replace(ROOT.replace(/\\/g, '/') + '/', '').replace(/\\/g, '/')).sort()
const refs = (entry) => entry.references.map((path) => basename(path)).sort()
const fakeShard = (name, { subsystem = '', shard = '', owner = '', locality = '', refs = [] } = {}) => ({
  projectPath: `/fake/${name}`,
  projectRepoPath: `src/Wanxiangshu/${name}`,
  explicitSubsystem: subsystem,
  explicitCompileShard: shard,
  legacyOwner: owner,
  legacyLocality: locality,
  references: refs.map((ref) => `/fake/${ref}`),
})
const fakeCompileInventory = (entries) => ({
  projects: new Map(entries.map((entry) => [entry.projectPath, entry])),
  sourceCount: entries.length,
  projectReferenceCount: entries.reduce((sum, entry) => sum + entry.references.length, 0),
})
const fakePolicy = (ids, legacyPairs = []) => ({ ids: new Set(ids), legacyOwnerSubsystem: new Map(legacyPairs) })
const SHARD_XML = ({ owner, locality, refs = [], files = [] }) => `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuSemanticOwner>${owner}</WanxiangshuSemanticOwner>
    <WanxiangshuOwnerLocality>${locality}</WanxiangshuOwnerLocality>
    <WanxiangshuOwnerLocalityKind>contract</WanxiangshuOwnerLocalityKind>
  </PropertyGroup>
  <ItemGroup>
${[...refs.map((ref) => `    <ProjectReference Include="${ref}" />`), ...files.map((file) => `    <Compile Include="${file}" />`)].join('\n')}
  </ItemGroup>
</Project>
`
const AGGREGATE_XML = (files) => `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
${files.map((file) => `    <Compile Include="${file}" />`).join('\n')}
  </ItemGroup>
</Project>
`
const makeShardRepo = (shards) => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiangshu-subsystem-boundaries-'))
  const aggregate = []
  for (const shard of shards) {
    const files = []
    for (const module of shard.modules) {
      writeFileSync(join(dir, `${module}.fsi`), `module ${module}\n`, 'utf8')
      writeFileSync(join(dir, `${module}.fs`), `module ${module}\n`, 'utf8')
      files.push(`${module}.fsi`, `${module}.fs`)
    }
    aggregate.push(...files)
    writeFileSync(join(dir, shard.name), SHARD_XML({ owner: shard.owner, locality: shard.locality, refs: shard.refs ?? [], files }), 'utf8')
  }
  writeFileSync(join(dir, 'Wanxiangshu.fsproj'), AGGREGATE_XML(aggregate), 'utf8')
  return dir
}
const readRepo = (dir) => readCompileShardInventory({ repositoryRoot: dir, sourceRoot: dir, aggregatePath: join(dir, 'Wanxiangshu.fsproj') })
const withShardRepo = (t, shards) => {
  const dir = makeShardRepo(shards)
  t.after(() => rmSync(dir, { recursive: true, force: true }))
  return dir
}

test('WHAT[structured-workflow-013] reusable platform shards depend on no domain subsystem', () => {
  const inventory = buildSubsystemInventory()
  let checkedPlatformShards = 0
  for (const entry of inventory.projects.values()) {
    if (entry.subsystem !== 'runtime-platform') continue
    checkedPlatformShards += 1
    for (const reference of entry.references) {
      const provider = inventory.projects.get(reference)
      assert.equal(provider.subsystem, 'runtime-platform', `${entry.shardKey} depends on ${provider.subsystem}/${provider.shard}`)
    }
  }
  assert.ok(checkedPlatformShards > 0, 'runtime-platform shards must be present and verified')

  const policy = fakePolicy(
    ['runtime-platform', 'dispatch'],
    [['legacy-platform-owner', 'runtime-platform'], ['dispatch-protocol', 'dispatch']],
  )
  const check = (consumer, provider) => buildSubsystemInventory({
    compileInventory: fakeCompileInventory([consumer, provider]),
    policyState: policy,
  })
  const explicitPrimitive = fakeShard('Wanxiangshu.Owner.test-primitives.fsproj', { subsystem: 'runtime-platform', shard: 'primitives' })
  const legacyPrimitive = fakeShard('Wanxiangshu.Owner.legacy-platform-owner.primitives.fsproj', { owner: 'legacy-platform-owner', locality: 'primitives' })
  const domainShard = fakeShard('Wanxiangshu.Owner.dispatch-protocol.runtime-nudge.fsproj', { subsystem: 'dispatch', shard: 'runtime-nudge' })

  const explicitLegal = check(
    fakeShard('Wanxiangshu.Owner.test-tool.fsproj', { subsystem: 'runtime-platform', shard: 'tool', refs: ['Wanxiangshu.Owner.test-primitives.fsproj'] }),
    explicitPrimitive,
  )
  assert.equal(explicitLegal.ok, true, explicitLegal.violations.join('\n'))

  const explicitRed = check(
    fakeShard('Wanxiangshu.Owner.test-tool.fsproj', { subsystem: 'runtime-platform', shard: 'tool', refs: ['Wanxiangshu.Owner.dispatch-protocol.runtime-nudge.fsproj'] }),
    domainShard,
  )
  assert.equal(explicitRed.ok, false, 'an explicit platform shard with a domain dependency must go red')
  assert.match(explicitRed.violations.join('\n'), /reusable runtime-platform shard depends on domain subsystem dispatch\/runtime-nudge/)

  const legacyLegal = check(
    fakeShard('Wanxiangshu.Owner.legacy-platform-owner.tool.fsproj', { owner: 'legacy-platform-owner', locality: 'tool', refs: ['Wanxiangshu.Owner.legacy-platform-owner.primitives.fsproj'] }),
    legacyPrimitive,
  )
  assert.equal(legacyLegal.ok, true, legacyLegal.violations.join('\n'))
  assert.equal(
    legacyLegal.projects.get('/fake/Wanxiangshu.Owner.legacy-platform-owner.tool.fsproj').subsystem,
    'runtime-platform',
    'legacy mapping must resolve the platform shard before the dependency rule applies',
  )

  const legacyRed = check(
    fakeShard('Wanxiangshu.Owner.legacy-platform-owner.tool.fsproj', { owner: 'legacy-platform-owner', locality: 'tool', refs: ['Wanxiangshu.Owner.dispatch-protocol.runtime-nudge.fsproj'] }),
    domainShard,
  )
  assert.equal(legacyRed.ok, false, 'a legacy-mapped platform shard with a domain dependency must go red')
  assert.match(legacyRed.violations.join('\n'), /reusable runtime-platform shard depends on domain subsystem dispatch\/runtime-nudge/)
})
}
