import assert from 'node:assert/strict'
import test from 'node:test'
import path from 'node:path'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'

const ROOT = path.resolve(import.meta.dirname, '../../..')
const CONTRACTS = ['foundation-temporal-contract', 'process-deadline-contract', 'execution-session-sessionstartedatprojection']
const NODE = 'process-node-timing-adapter'
const VIRTUAL = 'process-virtual-timing'
const REPRESENTATION = 'foundation-temporal'

const requireShard = (projects, shard) => {
  const matches = [...projects.values()].filter(project => project.shard === shard)
  assert.equal(matches.length, 1, `${shard} has one declared owner`)
  return matches[0]
}

const closure = (projects, project) => {
  const reached = new Set()
  const visit = current => {
    for (const reference of current.references) {
      const target = projects.get(reference)
      assert.ok(target, `declared reference resolves: ${reference}`)
      if (reached.has(target.shard)) continue
      reached.add(target.shard)
      visit(target)
    }
  }
  visit(project)
  return reached
}

const assertTemporalBoundaries = projects => {
  const owners = [...CONTRACTS, NODE, VIRTUAL, REPRESENTATION].map(shard => requireShard(projects, shard))
  const files = owners.flatMap(project => project.implementationFiles)
  assert.equal(new Set(files).size, files.length, 'independent owners do not share implementation files')
  for (const shard of CONTRACTS) {
    const references = closure(projects, requireShard(projects, shard))
    for (const implementation of [NODE, VIRTUAL, REPRESENTATION]) {
      assert.equal(references.has(implementation), false, `${shard} must not depend on ${implementation}`)
    }
  }
  assert.equal(closure(projects, requireShard(projects, NODE)).has(VIRTUAL), false)
  assert.equal(closure(projects, requireShard(projects, VIRTUAL)).has(NODE), false)
  const representation = closure(projects, requireShard(projects, REPRESENTATION))
  for (const shard of [...CONTRACTS, NODE, VIRTUAL]) assert.ok(representation.has(shard))
}

const inventory = () => {
  const compiled = readCompileShardInventory({ repositoryRoot: ROOT })
  const result = buildSubsystemInventory({ compileInventory: compiled })
  assert.ok(result.ok, result.violations.join('\n'))
  return result.projects
}

test('WHAT[time-capability-008] declared temporal contracts have no transitive implementation dependency', () => {
  assertTemporalBoundaries(inventory())
})

test('WHAT[time-capability-008] the same boundary check rejects a contract dependency on virtual runtime', () => {
  const projects = inventory()
  const contract = requireShard(projects, CONTRACTS[0])
  const [virtualPath] = [...projects.entries()].find(([, project]) => project.shard === VIRTUAL)
  const [contractPath] = [...projects.entries()].find(([, project]) => project === contract)
  const violating = new Map(projects)
  violating.set(contractPath, { ...contract, references: [...contract.references, virtualPath] })
  assert.throws(() => assertTemporalBoundaries(violating), /must not depend on process-virtual-timing/)
})

test.todo('WHAT[time-capability-008] actual compilation rejects implementation access from pure consumers and runtime requires injected time capabilities')
