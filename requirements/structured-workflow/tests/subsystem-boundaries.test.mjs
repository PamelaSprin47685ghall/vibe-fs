import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { basename, resolve } from 'node:path'
import test from 'node:test'

import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const project = (inventory, name) => {
  const match = [...inventory.projects.values()].find((entry) => basename(entry.projectPath) === name)
  assert.ok(match, `missing compile shard ${name}`)
  return match
}
const sources = (entry) => entry.implementationFiles.map((path) => path.replace(ROOT.replace(/\\/g, '/') + '/', '').replace(/\\/g, '/')).sort()
const refs = (entry) => entry.references.map((path) => basename(path)).sort()

test('WHAT[STRUCTURED-WORKFLOW-011] subsystem is the only semantic governance identity', () => {
  const inventory = buildSubsystemInventory()
  assert.equal(inventory.ok, true, inventory.violations.join('\n'))
  assert.equal(inventory.sourceCount, 703)
  assert.equal(inventory.subsystemCount, 26)
  assert.ok(inventory.shardCount > inventory.subsystemCount)
  assert.ok(inventory.largestSubsystemCycle.length > 1, 'current subsystem cycles must remain visible as migration debt')
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
  for (const entry of inventory.projects.values()) {
    if (entry.explicitSubsystem !== 'runtime-platform' || !entry.explicitCompileShard) continue
    for (const reference of entry.references) {
      const provider = inventory.projects.get(reference)
      assert.equal(provider.subsystem, 'runtime-platform', `${entry.shardKey} depends on ${provider.subsystem}/${provider.shard}`)
    }
  }
})

test('WHAT[STRUCTURED-WORKFLOW-016] release architecture has one subsystem authority', () => {
  const check = readFileSync(resolve(ROOT, 'scripts/check.mjs'), 'utf8')
  assert.match(check, /checks\/subsystems\.mjs/)
  assert.doesNotMatch(check, /checks\/semantic-owners\.mjs/)
  assert.doesNotMatch(check, /checks\/owner-contracts\.mjs/)
  assert.doesNotMatch(check, /checks\/owner-projects\.mjs/)
})
