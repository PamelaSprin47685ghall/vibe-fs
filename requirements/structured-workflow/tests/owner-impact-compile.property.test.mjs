import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import fc from 'fast-check'
import {
  planImpactCompile,
  planImpactFromInventory,
  readImpactInventory,
} from '../../../scripts/lib/owner-compile.mjs'

const TOPOLOGIES = ['chain', 'diamond', 'fanout', 'arbitrary']
const SEED = 0x494d5043
const RUNS_PER_TOPOLOGY = 25

const distinct = (values) => [...new Set(values)]

const referencesFor = (topology, nodeCount, rawReferences) => {
  const connectedCount = nodeCount - 1

  return Array.from({ length: nodeCount }, (_, consumer) => {
    if (consumer === 0 || consumer === connectedCount) {
      return []
    }
    if (topology === 'chain') {
      return [consumer - 1]
    }
    if (topology === 'fanout') {
      return [0]
    }
    if (topology === 'diamond') {
      if (consumer === 1 || consumer === 2) return [0]
      if (consumer === 3) return [1, 2]
      return [consumer - 1]
    }
    return distinct(rawReferences[consumer].filter((provider) => provider < consumer)).sort((left, right) => left - right)
  })
}

const graphCase = (topology) => fc.integer({ min: 5, max: 9 }).chain((nodeCount) => fc.record({
  aggregateOrder: fc.shuffledSubarray(
    Array.from({ length: nodeCount }, (_, index) => index),
    { minLength: nodeCount, maxLength: nodeCount },
  ),
  changeNodes: fc.shuffledSubarray(
    Array.from({ length: nodeCount - 1 }, (_, index) => index),
    { minLength: 1, maxLength: Math.min(4, nodeCount - 1) },
  ),
  signatureFlags: fc.array(fc.boolean(), { minLength: nodeCount, maxLength: nodeCount }),
  referenceOrderFlags: fc.array(fc.boolean(), { minLength: nodeCount, maxLength: nodeCount }),
  rawReferences: fc.array(
    fc.array(fc.integer({ min: 0, max: nodeCount - 1 }), { maxLength: nodeCount }),
    { minLength: nodeCount, maxLength: nodeCount },
  ),
}).map((generated) => ({
  ...generated,
  nodeCount,
  references: referencesFor(topology, nodeCount, generated.rawReferences),
})))

const projectName = (node) => `Owner.${String(node).padStart(2, '0')}.fsproj`

// Pure in-memory inventory: mirrors the on-disk fixture layout (one .fsi + one
// .fs per project, aggregate in aggregateOrder) without touching the fs.
const buildInventory = (graph) => {
  const root = '/memory-impact'
  const sourceDirectory = join(root, 'Source')
  const projects = new Map()
  const sourceOwner = new Map()
  const projectPaths = []

  for (let node = 0; node < graph.nodeCount; node += 1) {
    const projectPath = join(root, projectName(node))
    const compileItems = [
      join(sourceDirectory, `Node${node}.fsi`),
      join(sourceDirectory, `Node${node}.fs`),
    ]
    const references = graph.references[node].map((provider) => join(root, projectName(provider)))
    projects.set(projectPath, {
      path: projectPath,
      dir: root,
      rawText: `<memory project ${node}>`,
      references,
      compileItems,
    })
    projectPaths.push(projectPath)
    for (const sourcePath of compileItems) {
      sourceOwner.set(sourcePath, projectPath)
    }
  }

  const aggregateCompileItems = graph.aggregateOrder.flatMap((node) => [
    join(sourceDirectory, `Node${node}.fsi`),
    join(sourceDirectory, `Node${node}.fs`),
  ])
  const aggregatePath = join(root, 'Aggregate.fsproj')
  const aggregate = {
    path: aggregatePath,
    dir: root,
    rawText: '<memory aggregate>',
    compileItems: aggregateCompileItems,
  }

  const reverseReferences = new Map(projectPaths.map((projectPath) => [projectPath, new Set()]))
  for (const [consumerPath, project] of projects) {
    for (const providerPath of project.references) {
      reverseReferences.get(providerPath).add(consumerPath)
    }
  }

  return {
    aggregate: aggregatePath,
    projects: projectPaths,
    root,
    sourceDirectory,
    inventory: {
      aggregate,
      projectDirectory: root,
      projectPaths,
      projects,
      sourceOwner,
      reverseReferences,
    },
  }
}

const changePaths = (fixture, changes) => changes.map(({ node, extension }) =>
  join(fixture.sourceDirectory, `Node${node}.${extension}`))

const compileItemsForProjects = (fixture, graph, projectPaths) => graph.aggregateOrder
  .filter((node) => projectPaths.has(fixture.projects[node]))
  .flatMap((node) => [
    join(fixture.sourceDirectory, `Node${node}.fsi`),
    join(fixture.sourceDirectory, `Node${node}.fs`),
  ])

const focusedPlan = (fixture, changes) => {
  const plan = planImpactFromInventory({
    inventory: fixture.inventory,
    changedPaths: changePaths(fixture, changes),
    fullThreshold: 1,
  })

  assert.equal(plan.mode, 'focused')
  return plan
}

const assertCanonicalFlatInputs = (fixture, graph, plan) => {
  assert.deepEqual(plan.compileItems, compileItemsForProjects(fixture, graph, new Set(plan.projectPaths)))
  assert.equal(new Set(plan.compileItems).size, plan.compileItems.length)
}

const assertSubset = (subset, superset) => {
  for (const item of subset) assert.ok(superset.has(item), `${item} must be preserved by the larger impact`)
}

const verifyGraph = (graph) => {
  const fixture = buildInventory(graph)
  const implementationChanges = graph.changeNodes.map((node) => ({ node, extension: 'fs' }))
  const signatureChanges = graph.changeNodes.map((node) => ({ node, extension: 'fsi' }))
  const mixedChanges = graph.changeNodes.map((node) => ({
    node,
    extension: graph.signatureFlags[node] ? 'fsi' : 'fs',
  }))

  const implementationPlan = focusedPlan(fixture, implementationChanges)
  const signaturePlan = focusedPlan(fixture, signatureChanges)
  const mixedPlan = focusedPlan(fixture, mixedChanges)

  for (const plan of [implementationPlan, signaturePlan, mixedPlan]) {
    assertCanonicalFlatInputs(fixture, graph, plan)
    for (const node of graph.changeNodes) assert.ok(plan.projectPaths.includes(fixture.projects[node]))
  }
  assertSubset(new Set(implementationPlan.projectPaths), new Set(signaturePlan.projectPaths))

  // SPEC: a plain .fs body change must select exactly the forward closure
  // of the changed shards — never pull reverse consumers in. Compute the
  // forward closure independently and assert equality.
  const forwardSet = new Set()
  const pushForward = (projectPath) => {
    if (forwardSet.has(projectPath)) return
    forwardSet.add(projectPath)
    for (const ref of fixture.inventory.projects.get(projectPath).references) pushForward(ref)
  }
  for (const node of graph.changeNodes) pushForward(fixture.projects[node])
  assert.deepEqual(
    [...implementationPlan.projectPaths].sort(),
    [...forwardSet].sort(),
    'implementation changes select only the forward dependency closure, never reverse consumers',
  )

  const reorderedPlan = planImpactFromInventory({
    inventory: fixture.inventory,
    changedPaths: [...changePaths(fixture, mixedChanges).reverse(), ...changePaths(fixture, mixedChanges)],
    fullThreshold: 1,
  })
  assert.deepEqual(reorderedPlan.projectPaths, mixedPlan.projectPaths)
  assert.deepEqual(reorderedPlan.compileItems, mixedPlan.compileItems)

  const singleChangeUnion = new Set(mixedChanges.flatMap((change) => focusedPlan(fixture, [change]).projectPaths))
  assert.deepEqual(new Set(mixedPlan.projectPaths), singleChangeUnion)

  const disconnectedProject = fixture.projects.at(-1)
  assert.ok(!mixedPlan.projectPaths.includes(disconnectedProject))

  for (const changedPath of [fixture.projects[graph.changeNodes[0]], join(fixture.root, 'Directory.Build.props')]) {
    const fullPlan = planImpactFromInventory({
      inventory: fixture.inventory,
      changedPaths: [changedPath],
    })
    assert.equal(fullPlan.mode, 'full')
    assert.deepEqual(fullPlan.projectPaths, [...fixture.projects].sort())
    assert.deepEqual(fullPlan.compileItems, compileItemsForProjects(fixture, graph, new Set(fixture.projects)))
  }
}

test('WHAT[STRUCTURED-WORKFLOW-012] generated impact DAGs preserve change union signature monotonicity and canonical flat inputs', () => {
  for (const [index, topology] of TOPOLOGIES.entries()) {
    fc.assert(fc.property(graphCase(topology), verifyGraph), {
      seed: SEED + index,
      numRuns: RUNS_PER_TOPOLOGY,
    })
  }
})

// Single fs-backed parity case: the on-disk XML fixture must plan identically
// through the legacy entry and through readImpactInventory + planning.
const writeDiskFixture = () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-parity-'))
  const sourceDirectory = join(root, 'Source')
  mkdirSync(sourceDirectory)
  writeFileSync(join(root, 'Directory.Build.props'), '<Project/>\n')

  const nodes = [0, 1, 2]
  const projects = nodes.map((node) => {
    const sourceName = `Node${node}`
    const projectPath = join(root, projectName(node))
    writeFileSync(join(sourceDirectory, `${sourceName}.fsi`), `namespace Fixture\nval node${node}: string\n`)
    writeFileSync(join(sourceDirectory, `${sourceName}.fs`), `namespace Fixture\nlet node${node} = "${node}"\n`)
    const refs = node === 0
      ? []
      : [`    <ProjectReference Include="${projectName(node - 1)}"/>`]
    writeFileSync(projectPath, `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n${refs.join('\n')}${refs.length > 0 ? '\n' : ''}    <Compile Include="Source/${sourceName}.fsi"/>\n    <Compile Include="Source/${sourceName}.fs"/>\n  </ItemGroup>\n</Project>\n`)
    return projectPath
  })

  const aggregate = join(root, 'Aggregate.fsproj')
  writeFileSync(aggregate, `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n${nodes.flatMap((node) => [
    `    <Compile Include="Source/Node${node}.fsi"/>`,
    `    <Compile Include="Source/Node${node}.fs"/>`,
  ]).join('\n')}\n  </ItemGroup>\n</Project>\n`)

  return { aggregate, projects, root, sourceDirectory }
}

test('WHAT[STRUCTURED-WORKFLOW-012] disk inventory plans identically through the split stages', () => {
  const fixture = writeDiskFixture()
  try {
    for (const changedPaths of [
      [join(fixture.sourceDirectory, 'Node1.fs')],
      [fixture.projects[0]],
    ]) {
      const legacy = planImpactCompile({
        changedPaths,
        projectDirectory: fixture.root,
        aggregatePath: fixture.aggregate,
      })
      const inventory = readImpactInventory({
        projectDirectory: fixture.root,
        aggregatePath: fixture.aggregate,
      })
      const split = planImpactFromInventory({ inventory, changedPaths })
      assert.deepEqual(split, legacy)
    }
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})
