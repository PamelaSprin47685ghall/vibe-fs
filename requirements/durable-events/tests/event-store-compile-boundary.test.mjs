import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { basename, join, resolve } from 'node:path'
import test from 'node:test'
import { planOwnerCompile } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')

const projectMetadata = readdirSync(SOURCE_ROOT)
  .filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name))
  .map((name) => {
    const path = join(SOURCE_ROOT, name)
    const xml = readFileSync(path, 'utf8')
    return {
      path,
      name,
      locality: xml.match(/<WanxiangshuOwnerLocality>([^<]+)<\/WanxiangshuOwnerLocality>/)?.[1],
      kind: xml.match(/<WanxiangshuOwnerLocalityKind>([^<]+)<\/WanxiangshuOwnerLocalityKind>/)?.[1],
    }
  })

const projectByPath = new Map(projectMetadata.map((project) => [resolve(project.path), project]))

const requireLocality = (locality) => {
  const matches = projectMetadata.filter((project) => project.locality === locality)
  assert.equal(matches.length, 1, `${locality} must resolve to exactly one owner project`)
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

const CONTRACT_LOCALITIES = [
  'eventstore-model-contract',
  'eventstore-port-contract',
  'eventstore-event-vocabulary-contract',
  'eventstore-git-contract',
  'strength-event-vocabulary-contract',
]

const FOCUSED_RUNTIME_LOCALITIES = [
  'eventstore-core-runtime',
  'eventstore-git-runtime',
]

test('WHAT[DURABLE-EVENTS-022] EventStore contracts exclude physical and Strength runtime closure', () => {
  for (const locality of CONTRACT_LOCALITIES) {
    const { project, plan } = planLocality(locality)
    assert.equal(project.kind, 'contract', `${locality} must declare contract kind`)

    for (const projectPath of plan.projectPaths) {
      assert.equal(
        projectByPath.get(resolve(projectPath))?.kind,
        'contract',
        `${locality} contract closure contains non-contract ${basename(projectPath)}`,
      )
    }
  }

  const portSources = productionSources(planLocality('eventstore-port-contract').plan)
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

  const vocabularySources = productionSources(planLocality('eventstore-event-vocabulary-contract').plan)
  assert.ok(vocabularySources.includes('Strength/EventVocabulary.fs'))
  assert.ok(!vocabularySources.includes('Strength/Events.fs'))
  assert.ok(!vocabularySources.some((path) => path.startsWith('Strength/Prediction/')))
  assert.ok(!vocabularySources.some((path) => path.startsWith('Strength/Replica/')))
  assert.ok(!vocabularySources.includes('Strength/Runtime.fs'))
})

test('WHAT[DURABLE-EVENTS-022] EventStore focused localities stay within compile budgets', () => {
  for (const locality of CONTRACT_LOCALITIES) {
    const { plan } = planLocality(locality)
    assert.ok(
      productionSources(plan).length <= 100,
      `${locality} contract closure exceeds 100 production sources`,
    )
  }

  for (const locality of FOCUSED_RUNTIME_LOCALITIES) {
    const { project, plan } = planLocality(locality)
    assert.equal(project.kind, 'runtime', `${locality} must declare runtime kind`)
    assert.ok(
      productionSources(plan).length <= 185,
      `${locality} runtime closure exceeds 185 production sources`,
    )
  }
})
