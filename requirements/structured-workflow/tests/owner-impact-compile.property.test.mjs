import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import fc from 'fast-check'
import { planImpactCompile } from '../../../scripts/lib/owner-compile.mjs'

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

const writeFixture = (graph) => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-property-'))
  const sourceDirectory = join(root, 'Source')
  mkdirSync(sourceDirectory)
  writeFileSync(join(root, 'Directory.Build.props'), '<Project/>\n')

  const projects = Array.from({ length: graph.nodeCount }, (_, node) => {
    const sourceName = `Node${node}`
    const projectPath = join(root, `Owner.${String(node).padStart(2, '0')}.fsproj`)
    writeFileSync(join(sourceDirectory, `${sourceName}.fsi`), `namespace Fixture\nval node${node}: string\n`)
    writeFileSync(join(sourceDirectory, `${sourceName}.fs`), `namespace Fixture\nlet node${node} = "${node}"\n`)

    const references = graph.referenceOrderFlags[node]
      ? [...graph.references[node]].reverse()
      : graph.references[node]
    writeFileSync(projectPath, `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
${references.map((provider) => `    <ProjectReference Include="Owner.${String(provider).padStart(2, '0')}.fsproj"/>`).join('\n')}
    <Compile Include="Source/${sourceName}.fsi"/>
    <Compile Include="Source/${sourceName}.fs"/>
  </ItemGroup>
</Project>
`)
    return projectPath
  })

  const aggregate = join(root, 'Aggregate.fsproj')
  writeFileSync(aggregate, `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
${graph.aggregateOrder.flatMap((node) => [
    `    <Compile Include="Source/Node${node}.fsi"/>`,
    `    <Compile Include="Source/Node${node}.fs"/>`,
  ]).join('\n')}
  </ItemGroup>
</Project>
`)

  return { aggregate, projects, root, sourceDirectory }
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
  const plan = planImpactCompile({
    changedPaths: changePaths(fixture, changes),
    projectDirectory: fixture.root,
    aggregatePath: fixture.aggregate,
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
  const fixture = writeFixture(graph)
  try {
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

    const reorderedPlan = planImpactCompile({
      changedPaths: [...changePaths(fixture, mixedChanges).reverse(), ...changePaths(fixture, mixedChanges)],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
      fullThreshold: 1,
    })
    assert.deepEqual(reorderedPlan.projectPaths, mixedPlan.projectPaths)
    assert.deepEqual(reorderedPlan.compileItems, mixedPlan.compileItems)

    const singleChangeUnion = new Set(mixedChanges.flatMap((change) => focusedPlan(fixture, [change]).projectPaths))
    assert.deepEqual(new Set(mixedPlan.projectPaths), singleChangeUnion)

    const disconnectedProject = fixture.projects.at(-1)
    assert.ok(!mixedPlan.projectPaths.includes(disconnectedProject))

    for (const changedPath of [fixture.projects[graph.changeNodes[0]], join(fixture.root, 'Directory.Build.props')]) {
      const fullPlan = planImpactCompile({
        changedPaths: [changedPath],
        projectDirectory: fixture.root,
        aggregatePath: fixture.aggregate,
      })
      assert.equal(fullPlan.mode, 'full')
      assert.deepEqual(fullPlan.projectPaths, [...fixture.projects].sort())
      assert.deepEqual(fullPlan.compileItems, compileItemsForProjects(fixture, graph, new Set(fixture.projects)))
    }
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
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
