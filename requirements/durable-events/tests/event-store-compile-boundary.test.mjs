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

const projects = [...subsystemInventory.projects.values()]
const projectByPath = new Map(projects.map((project) => [resolve(project.projectPath), project]))

const requireShard = (shard) => {
  const matches = projects.filter((project) => project.shard === shard)
  assert.equal(matches.length, 1, `${shard} must resolve to exactly one compile shard`)
  return matches[0]
}

const planShard = (shard) => {
  const project = requireShard(shard)
  return {
    project,
    plan: planOwnerCompile({ projectPath: project.projectPath, aggregatePath: AGGREGATE }),
  }
}

const productionSources = (plan) => plan.compileItems
  .filter((path) => path.endsWith('.fs'))
  .map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/'))

const CONTRACT_SHARDS = [
  'eventstore-model-contract',
  'eventstore-port-contract',
  'eventstore-event-vocabulary-contract',
  'eventstore-git-contract',
  'strength-event-vocabulary-contract',
]

const FOCUSED_RUNTIME_SHARDS = [
  'eventstore-core-runtime',
  'eventstore-git-runtime',
]

const ALLOWED_CONTRACT_CLOSURE_SHARDS = new Set([
  'eventstore-model-contract',
  'eventstore-port-contract',
  'eventstore-event-vocabulary-contract',
  'eventstore-git-contract',
  'strength-event-vocabulary-contract',
  'sphinx-event-vocabulary-contract',
  'identity',
])

test('WHAT[DURABLE-EVENTS-022] EventStore contracts exclude physical and Strength runtime closure', () => {
  for (const shard of CONTRACT_SHARDS) {
    const { project, plan } = planShard(shard)
    assert.ok(
      project.subsystem === 'persistence' || project.subsystem === 'strength',
      `${shard} belongs to expected contract subsystem`,
    )

    for (const projectPath of plan.projectPaths) {
      const provider = projectByPath.get(resolve(projectPath))
      assert.ok(
        ALLOWED_CONTRACT_CLOSURE_SHARDS.has(provider?.shard),
        `${shard} contract closure contains non-contract shard ${provider?.shardKey ?? basename(projectPath)}`,
      )
    }
  }

  const portSources = productionSources(planShard('eventstore-port-contract').plan)
  for (const forbidden of [
    'Persistence/EventStore/GitObjectDatabase.fs',
    'Persistence/EventStore/ProcessGitRawStore.fs',
    'Persistence/EventStore/ProcessEventLog.fs',
    'Persistence/EventStore/Store.fs',
    'Persistence/EventStore/CanonicalIntegrator.fs',
    'OpenCode/Host/WorkspaceEventStore.fs',
  ]) {
    assert.ok(!portSources.includes(forbidden), `EventStore.Port.Contract leaks ${forbidden}`)
  }

  const vocabularySources = productionSources(planShard('eventstore-event-vocabulary-contract').plan)
  assert.ok(vocabularySources.includes('Strength/EventVocabulary.fs'))
  assert.ok(!vocabularySources.includes('Strength/Events.fs'))
  assert.ok(!vocabularySources.some((path) => path.startsWith('Strength/Prediction/')))
  assert.ok(!vocabularySources.some((path) => path.startsWith('Strength/Replica/')))
  assert.ok(!vocabularySources.includes('Strength/Runtime.fs'))
})

test('WHAT[DURABLE-EVENTS-022] EventStore focused localities stay within compile budgets', () => {
  for (const shard of CONTRACT_SHARDS) {
    const { plan } = planShard(shard)
    assert.ok(
      productionSources(plan).length <= 100,
      `${shard} contract closure exceeds 100 production sources`,
    )
  }

  for (const shard of FOCUSED_RUNTIME_SHARDS) {
    const { project, plan } = planShard(shard)
    assert.equal(project.subsystem, 'persistence', `${shard} must belong to persistence subsystem`)
    assert.ok(
      productionSources(plan).length <= 185,
      `${shard} runtime closure exceeds 185 production sources`,
    )
  }
})
