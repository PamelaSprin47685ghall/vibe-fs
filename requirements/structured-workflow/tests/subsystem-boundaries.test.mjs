import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, join, resolve } from 'node:path'
import test from 'node:test'

import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'

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

test('WHAT[STRUCTURED-WORKFLOW-011] subsystem is the only semantic governance identity', (t) => {
  const inventory = buildSubsystemInventory()
  assert.equal(inventory.ok, true, inventory.violations.join('\n'))

  for (const entry of inventory.projects.values()) {
    assert.ok(entry.subsystem, `${entry.projectRepoPath}: shard must resolve to exactly one subsystem`)
    assert.ok(entry.shard, `${entry.projectRepoPath}: shard must carry a stable shard id`)
  }
  const shardKeys = [...inventory.projects.values()].map((entry) => entry.shardKey)
  assert.equal(new Set(shardKeys).size, shardKeys.length, 'compile shard keys must be unique')
  for (const entry of inventory.compileInventory.projects.values()) {
    for (const file of entry.implementationFiles) {
      assert.equal(
        inventory.compileInventory.sourceProject.get(file)?.projectPath,
        entry.projectPath,
        'every production source must be owned by exactly its compiling shard',
      )
    }
  }

  for (const component of inventory.cyclicComponents) {
    assert.ok(component.length > 1, 'reported subsystem cycles must be real cycles')
    for (const member of component) assert.ok(inventory.subsystemIds.has(member), `cycle member ${member} must be a known subsystem`)
  }
  assert.ok(
    inventory.largestSubsystemCycle.length === 0
      || inventory.cyclicComponents.some((component) => component.join('\0') === inventory.largestSubsystemCycle.join('\0')),
    'largest subsystem cycle must be one of the reported cycles',
  )
  for (const [consumer, provider] of inventory.subsystemEdges) {
    assert.ok(
      inventory.subsystemIds.has(consumer) && inventory.subsystemIds.has(provider),
      'subsystem edges must stay inside known subsystems',
    )
  }

  const growthDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'] },
  ])
  const growthPolicy = fakePolicy(['alpha', 'beta'], [['test-alpha', 'alpha'], ['test-beta', 'beta']])
  const solo = readRepo(growthDir)
  assert.equal(solo.sourceCount, 1)
  assert.equal(buildSubsystemInventory({ compileInventory: solo, policyState: growthPolicy }).ok, true)
  writeFileSync(join(growthDir, 'Beta.fsi'), 'module Beta\n', 'utf8')
  writeFileSync(join(growthDir, 'Beta.fs'), 'module Beta\n', 'utf8')
  writeFileSync(
    join(growthDir, 'Wanxiangshu.Owner.test-beta.fsproj'),
    SHARD_XML({ owner: 'test-beta', locality: 'beta', files: ['Beta.fsi', 'Beta.fs'] }),
    'utf8',
  )
  writeFileSync(join(growthDir, 'Wanxiangshu.fsproj'), AGGREGATE_XML(['Alpha.fsi', 'Alpha.fs', 'Beta.fsi', 'Beta.fs']), 'utf8')
  const grown = readRepo(growthDir)
  assert.equal(grown.sourceCount, 2)
  assert.equal(
    buildSubsystemInventory({ compileInventory: grown, policyState: growthPolicy }).ok,
    true,
    'legal growth with one more shard and source must pass instead of pinning a count',
  )

  const cycleDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'], refs: ['Wanxiangshu.Owner.test-beta.fsproj'] },
    { name: 'Wanxiangshu.Owner.test-beta.fsproj', owner: 'test-beta', locality: 'beta', modules: ['Beta'], refs: ['Wanxiangshu.Owner.test-alpha.fsproj'] },
  ])
  assert.throws(() => readRepo(cycleDir), /contains a cycle/, 'mutual ProjectReference must be rejected before any verdict')

  const ringDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha-one.fsproj', owner: 'test-alpha-one', locality: 'a-one', modules: ['AlphaOne'], refs: ['Wanxiangshu.Owner.test-beta.fsproj'] },
    { name: 'Wanxiangshu.Owner.test-beta.fsproj', owner: 'test-beta', locality: 'b-one', modules: ['Beta'], refs: ['Wanxiangshu.Owner.test-alpha-two.fsproj'] },
    { name: 'Wanxiangshu.Owner.test-alpha-two.fsproj', owner: 'test-alpha-two', locality: 'a-two', modules: ['AlphaTwo'] },
  ])
  const ring = buildSubsystemInventory({
    compileInventory: readRepo(ringDir),
    policyState: fakePolicy(['alpha', 'beta'], [['test-alpha-one', 'alpha'], ['test-alpha-two', 'alpha'], ['test-beta', 'beta']]),
  })
  assert.equal(ring.ok, true, ring.violations.join('\n'))
  assert.deepEqual(ring.cyclicComponents, [['alpha', 'beta']])
  assert.deepEqual(ring.largestSubsystemCycle, ['alpha', 'beta'])

  const chainDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'], refs: ['Wanxiangshu.Owner.test-beta.fsproj'] },
    { name: 'Wanxiangshu.Owner.test-beta.fsproj', owner: 'test-beta', locality: 'beta', modules: ['Beta'] },
  ])
  const chain = buildSubsystemInventory({
    compileInventory: readRepo(chainDir),
    policyState: fakePolicy(['alpha', 'beta'], [['test-alpha', 'alpha'], ['test-beta', 'beta']]),
  })
  assert.equal(chain.ok, true, chain.violations.join('\n'))
  assert.deepEqual(chain.cyclicComponents, [])
  assert.deepEqual(chain.largestSubsystemCycle, [])

  const duplicateDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'] },
    { name: 'Wanxiangshu.Owner.test-beta.fsproj', owner: 'test-beta', locality: 'beta', modules: ['Alpha'] },
  ])
  assert.throws(() => readRepo(duplicateDir), /compiled by both/, 'a source compiled by two shards must be rejected')

  const missingDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'] },
  ])
  rmSync(join(missingDir, 'Alpha.fsi'))
  assert.throws(() => readRepo(missingDir), /must carry its sibling \.fsi/, 'a source without its sibling signature must be rejected')

  const staleAggregateDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'] },
    { name: 'Wanxiangshu.Owner.test-beta.fsproj', owner: 'test-beta', locality: 'beta', modules: ['Beta'] },
  ])
  writeFileSync(join(staleAggregateDir, 'Wanxiangshu.fsproj'), AGGREGATE_XML(['Alpha.fsi', 'Alpha.fs']), 'utf8')
  assert.throws(() => readRepo(staleAggregateDir), /aggregate \.fs compile set differs/, 'an aggregate missing a shard source must be rejected')

  const unassignedDir = withShardRepo(t, [
    { name: 'Wanxiangshu.Owner.test-alpha.fsproj', owner: 'test-alpha', locality: 'alpha', modules: ['Alpha'] },
  ])
  writeFileSync(join(unassignedDir, 'Stray.fsi'), 'module Stray\n', 'utf8')
  writeFileSync(join(unassignedDir, 'Stray.fs'), 'module Stray\n', 'utf8')
  assert.throws(() => readRepo(unassignedDir), /production source coverage mismatch/, 'a production source owned by no shard must be rejected')

  const stray = buildSubsystemInventory({
    compileInventory: fakeCompileInventory([
      fakeShard('Wanxiangshu.Owner.stray.fsproj', { owner: 'unknown-owner', locality: 'stray' }),
    ]),
    policyState: fakePolicy(['alpha']),
  })
  assert.equal(stray.ok, false, 'a shard with no valid subsystem must go red')
  assert.match(stray.violations.join('\n'), /has no valid subsystem|is not mapped to a subsystem/)

  const clash = buildSubsystemInventory({
    compileInventory: fakeCompileInventory([
      fakeShard('Wanxiangshu.Owner.a.fsproj', { subsystem: 'alpha', shard: 'same' }),
      fakeShard('Wanxiangshu.Owner.b.fsproj', { subsystem: 'alpha', shard: 'same' }),
    ]),
    policyState: fakePolicy(['alpha']),
  })
  assert.equal(clash.ok, false, 'a duplicated subsystem/shard identity must go red')
  assert.match(clash.violations.join('\n'), /duplicate compile shard/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] shared gravity wells are split by knowledge instead of copied ACLs', () => {
  const inventory = buildSubsystemInventory()
  const identity = project(inventory, 'Wanxiangshu.Owner.dispatch-protocol.foundation-identity.fsproj')
  const outcome = project(inventory, 'Wanxiangshu.Owner.dispatch-protocol.foundation-outcome.fsproj')
  const canonical = project(inventory, 'Wanxiangshu.Owner.dispatch-protocol.foundation-canonical-json.fsproj')
  const taskResult = project(inventory, 'Wanxiangshu.Owner.intra-participant-parallelism.foundation-taskresult.fsproj')
  const parallel = project(inventory, 'Wanxiangshu.Owner.intra-participant-parallelism.foundation-parallel.fsproj')
  const asyncSupport = project(inventory, 'Wanxiangshu.Owner.intra-participant-parallelism.foundation-async-support.fsproj')
  const fission = project(inventory, 'Wanxiangshu.Owner.intra-participant-parallelism.execution-fission-facts.fsproj')

  assert.deepEqual(sources(identity), ['src/Wanxiangshu/Foundation/Identity.fs'])
  assert.deepEqual(sources(outcome), ['src/Wanxiangshu/Foundation/Outcome.fs', 'src/Wanxiangshu/Foundation/OutcomeSurface.fs'])
  assert.deepEqual(sources(canonical), ['src/Wanxiangshu/Foundation/CanonicalJson.fs', 'src/Wanxiangshu/OpenCode/Codec/CanonicalJsonSurface.fs'])
  assert.deepEqual(sources(taskResult), ['src/Wanxiangshu/Foundation/FsToolkitFableCompat.fs', 'src/Wanxiangshu/Foundation/TaskResult.fs'])
  assert.deepEqual(sources(parallel), ['src/Wanxiangshu/Foundation/Parallel.fs', 'src/Wanxiangshu/Foundation/ParallelSurface.fs'])
  assert.deepEqual(sources(asyncSupport), ['src/Wanxiangshu/Foundation/AsyncSupport.fs'])
  assert.deepEqual(sources(fission), ['src/Wanxiangshu/Execution/Fission/Facts.fs'])

  assert.deepEqual(refs(canonical), [])
  assert.deepEqual(refs(taskResult), [])
  assert.deepEqual(refs(parallel), [])
  assert.deepEqual(refs(asyncSupport), [])
  assert.deepEqual(refs(fission), ['Wanxiangshu.Owner.dispatch-protocol.foundation-identity.fsproj'])
})

test('WHAT[STRUCTURED-WORKFLOW-013] reusable platform shards depend on no domain subsystem', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-016] release architecture has one subsystem authority', () => {
  const check = readFileSync(resolve(ROOT, 'scripts/check.mjs'), 'utf8')
  assert.match(check, /checks\/subsystems\.mjs/)
  assert.doesNotMatch(check, /checks\/semantic-owners\.mjs/)
  assert.doesNotMatch(check, /checks\/owner-contracts\.mjs/)
  assert.doesNotMatch(check, /checks\/owner-projects\.mjs/)
})
