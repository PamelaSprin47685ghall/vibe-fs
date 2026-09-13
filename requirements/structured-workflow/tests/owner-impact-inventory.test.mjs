import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  planImpactCompile,
  planImpactFromInventory,
  readImpactInventory,
} from '../../../scripts/lib/owner-compile.mjs'

const projectName = (node) => `Owner.${String(node).padStart(2, '0')}.fsproj`

const writeProject = (root, node, { references = [], compileItems = [`Source/Node${node}.fsi`, `Source/Node${node}.fs`], rawReferences = null } = {}) => {
  const projectPath = join(root, projectName(node))
  const refs = (rawReferences ?? references.map((provider) => projectName(provider)))
    .map((include) => `    <ProjectReference Include="${include}"/>`).join('\n')
  writeFileSync(projectPath, `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n${refs}${refs ? '\n' : ''}${compileItems.map((item) => `    <Compile Include="${item}"/>`).join('\n')}\n  </ItemGroup>\n</Project>\n`)
  return projectPath
}

// Small two-project chain: Owner.00 (Node0 sources) <- Owner.01 (Node1 sources).
const writeChainFixture = () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-inventory-'))
  const sourceDirectory = join(root, 'Source')
  mkdirSync(sourceDirectory)
  writeFileSync(join(root, 'Directory.Build.props'), '<Project/>\n')
  for (const node of [0, 1]) {
    writeFileSync(join(sourceDirectory, `Node${node}.fsi`), `namespace Fixture\nval node${node}: string\n`)
    writeFileSync(join(sourceDirectory, `Node${node}.fs`), `namespace Fixture\nlet node${node} = "${node}"\n`)
  }
  writeProject(root, 0)
  writeProject(root, 1, { references: [0] })
  const aggregate = join(root, 'Aggregate.fsproj')
  writeFileSync(aggregate, `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n${[0, 1].flatMap((node) => [
    `    <Compile Include="Source/Node${node}.fsi"/>`,
    `    <Compile Include="Source/Node${node}.fs"/>`,
  ]).join('\n')}\n  </ItemGroup>\n</Project>\n`)
  return { aggregate, root, sourceDirectory }
}

test('WHAT[STRUCTURED-WORKFLOW-012] disk inventory matches the legacy plan and flags unmapped added sources as full', () => {
  const fixture = writeChainFixture()
  try {
    const inventory = readImpactInventory({
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })
    assert.equal(inventory.projects.size, 2)
    assert.equal(inventory.sourceOwner.size, 4)
    assert.deepEqual([...inventory.projectPaths].sort(), [...inventory.projectPaths])

    const changedPaths = [join(fixture.sourceDirectory, 'Node0.fs')]
    const legacy = planImpactCompile({
      changedPaths,
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
      fullThreshold: 1,
    })
    const split = planImpactFromInventory({ inventory, changedPaths, fullThreshold: 1 })
    assert.equal(legacy.mode, 'focused')
    assert.deepEqual(split, legacy)

    // A newly added source file owned by no project is an unmapped .fs change.
    writeFileSync(join(fixture.sourceDirectory, 'Added.fs'), 'namespace Fixture\nlet added = 1\n')
    const addedPlan = planImpactFromInventory({
      inventory,
      changedPaths: [join(fixture.sourceDirectory, 'Added.fs')],
    })
    assert.equal(addedPlan.mode, 'full')
    assert.equal(addedPlan.reason, 'unmapped-source-change')
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] inventory rejects bad topology inputs and planning rejects bad change inputs', () => {
  const fixture = writeChainFixture()
  try {
    // Duplicate Compile item across owner projects.
    writeProject(fixture.root, 1, { references: [0], compileItems: ['Source/Node1.fsi', 'Source/Node1.fs', 'Source/Node0.fs'] })
    assert.throws(
      () => readImpactInventory({ projectDirectory: fixture.root, aggregatePath: fixture.aggregate }),
      /Duplicate Compile item across owner projects/,
    )

    // Reference to an existing project outside the scanned topology.
    mkdirSync(join(fixture.root, 'Other'))
    const outside = join(fixture.root, 'Other', 'Extra.fsproj')
    writeFileSync(outside, '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup></ItemGroup></Project>\n')
    writeProject(fixture.root, 1, { rawReferences: ['Other/Extra.fsproj'], compileItems: ['Source/Node1.fsi', 'Source/Node1.fs'] })
    assert.throws(
      () => readImpactInventory({ projectDirectory: fixture.root, aggregatePath: fixture.aggregate }),
      /references project outside owner topology/,
    )

    // Missing referenced project file surfaces from the underlying parse.
    writeProject(fixture.root, 1, { rawReferences: ['Missing.fsproj'], compileItems: ['Source/Node1.fsi', 'Source/Node1.fs'] })
    assert.throws(
      () => readImpactInventory({ projectDirectory: fixture.root, aggregatePath: fixture.aggregate }),
      /Missing ProjectReference/,
    )

    // Malformed project XML is rejected.
    writeFileSync(join(fixture.root, projectName(1)), 'not xml at all\n')
    assert.throws(
      () => readImpactInventory({ projectDirectory: fixture.root, aggregatePath: fixture.aggregate }),
      /lacks <Project> root/,
    )

    // Bad aggregate path never reaches planning.
    assert.throws(
      () => readImpactInventory({ projectDirectory: fixture.root, aggregatePath: join(fixture.root, 'Nope.fsproj') }),
      /Aggregate project file does not exist/,
    )

    // Planning-stage input validation matches the legacy entry errors.
    const fresh = writeChainFixture()
    try {
      const inventory = readImpactInventory({
        projectDirectory: fresh.root,
        aggregatePath: fresh.aggregate,
      })
      assert.throws(
        () => planImpactFromInventory({ inventory, changedPaths: [] }),
        /changedPaths must contain at least one path/,
      )
      for (const fullThreshold of [0, -0.5, 1.5]) {
        assert.throws(
          () => planImpactFromInventory({ inventory, changedPaths: [join(fresh.sourceDirectory, 'Node0.fs')], fullThreshold }),
          /fullThreshold must be within/,
        )
      }
    } finally {
      rmSync(fresh.root, { recursive: true, force: true })
    }
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})
