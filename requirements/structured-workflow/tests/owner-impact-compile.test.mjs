import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import {
  compileOwnerProject,
  materializeOwnerCompile,
  planImpactCompile,
} from '../../../scripts/lib/owner-compile.mjs'

const writeProject = (root, name, locality, kind, refs, source) => {
  const path = join(root, `${name}.fsproj`)
  writeFileSync(path, `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuSemanticOwner>fixture</WanxiangshuSemanticOwner>
    <WanxiangshuOwnerLocality>${locality}</WanxiangshuOwnerLocality>
    <WanxiangshuOwnerLocalityKind>${kind}</WanxiangshuOwnerLocalityKind>
  </PropertyGroup>
  <ItemGroup>
${refs.map((ref) => `    <ProjectReference Include="${ref}.fsproj"/>`).join('\n')}
    <Compile Include="Source/${source}.fsi"/>
    <Compile Include="Source/${source}.fs"/>
  </ItemGroup>
</Project>
`)
  return path
}

const createFixture = () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiangshu-impact-'))
  mkdirSync(join(root, 'Source'))
  writeFileSync(join(root, 'Directory.Build.props'), '<Project/>\n')

  const sources = ['Base', 'Contract', 'Runtime', 'Consumer', 'Composition', 'Unrelated']
  for (const source of sources) {
    writeFileSync(join(root, 'Source', `${source}.fsi`), `namespace Fixture\nval ${source.toLowerCase()}: string\n`)
    writeFileSync(join(root, 'Source', `${source}.fs`), `namespace Fixture\nlet ${source.toLowerCase()} = "${source}"\n`)
  }

  const projects = {
    base: writeProject(root, 'Owner.Base', 'base-contract', 'contract', [], 'Base'),
    contract: writeProject(root, 'Owner.Provider.Contract', 'provider-contract', 'contract', ['Owner.Base'], 'Contract'),
    runtime: writeProject(root, 'Owner.Provider.Runtime', 'provider-runtime', 'runtime', ['Owner.Provider.Contract'], 'Runtime'),
    consumer: writeProject(root, 'Owner.Consumer.Runtime', 'consumer-runtime', 'runtime', ['Owner.Provider.Contract'], 'Consumer'),
    composition: writeProject(root, 'Owner.Composition.Runtime', 'composition-runtime', 'composition', ['Owner.Provider.Runtime', 'Owner.Consumer.Runtime'], 'Composition'),
    unrelated: writeProject(root, 'Owner.Unrelated.Runtime', 'unrelated-runtime', 'runtime', ['Owner.Base'], 'Unrelated'),
  }

  const aggregate = join(root, 'Aggregate.fsproj')
  writeFileSync(aggregate, `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><AssemblyName>Fixture</AssemblyName></PropertyGroup>
  <ItemGroup>
${sources.flatMap((source) => [
    `    <Compile Include="Source/${source}.fsi"/>`,
    `    <Compile Include="Source/${source}.fs"/>`,
  ]).join('\n')}
  </ItemGroup>
</Project>
`)

  return { root, aggregate, projects }
}

const sourceNames = (plan) => plan.compileItems.map((path) => path.split('/').at(-1))

test('WHAT[STRUCTURED-WORKFLOW-012] implementation changes exclude reverse consumers', () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Runtime.fs')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })

    assert.equal(plan.mode, 'focused')
    assert.deepEqual(sourceNames(plan), ['Base.fsi', 'Base.fs', 'Contract.fsi', 'Contract.fs', 'Runtime.fsi', 'Runtime.fs'])
    assert.ok(!plan.projectPaths.includes(fixture.projects.consumer))
    assert.ok(!plan.projectPaths.includes(fixture.projects.composition))
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] impact watch launches one Fable process on the flat project', async () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Runtime.fs')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })
    const calls = []
    const spawn = (command, args) => {
      calls.push({ command, args })
      const output = args[args.indexOf('-o') + 1]
      mkdirSync(output, { recursive: true })
      writeFileSync(join(output, 'Runtime.js'), 'export const runtime = true\n')
      const child = new EventEmitter()
      child.stdout = new EventEmitter()
      child.stderr = new EventEmitter()
      setImmediate(() => child.emit('close', 0, null))
      return child
    }

    const result = await compileOwnerProject({
      compilePlan: plan,
      rootPropsPath: join(fixture.root, 'Directory.Build.props'),
      scratchRoot: join(fixture.root, '.scratch-watch'),
      spawn,
      stdio: 'pipe',
      watch: true,
    })

    assert.equal(result.ok, true)
    assert.equal(calls.length, 1)
    assert.equal(calls[0].command, 'dotnet')
    assert.equal(calls[0].args.filter((arg) => arg === '--watch').length, 1)
    assert.notEqual(calls[0].args[4], fixture.projects.runtime)
    assert.ok(!readFileSync(calls[0].args[4], 'utf8').includes('<ProjectReference'))
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] signature changes include every reverse consumer and exact forward union', () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Contract.fsi')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
      fullThreshold: 1,
    })

    assert.equal(plan.mode, 'focused')
    assert.deepEqual(sourceNames(plan), [
      'Base.fsi', 'Base.fs',
      'Contract.fsi', 'Contract.fs',
      'Runtime.fsi', 'Runtime.fs',
      'Consumer.fsi', 'Consumer.fs',
      'Composition.fsi', 'Composition.fs',
    ])
    assert.deepEqual(
      new Set(plan.rootProjectPaths),
      new Set([fixture.projects.contract, fixture.projects.runtime, fixture.projects.consumer, fixture.projects.composition]),
    )
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] toolchain changes and oversized impact select one full flat build', () => {
  const fixture = createFixture()
  try {
    const oversized = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Contract.fsi')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })
    assert.equal(oversized.mode, 'full')
    assert.equal(oversized.compileItems.length, 12)

    const toolchain = planImpactCompile({
      changedPaths: [join(fixture.root, 'Directory.Build.props')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })
    assert.equal(toolchain.mode, 'full')
    assert.equal(toolchain.compileItems.length, 12)
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] materialized impact project has exact canonical inputs and zero ProjectReference', () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Runtime.fs')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })
    const materialized = materializeOwnerCompile(plan, {
      rootPropsPath: join(fixture.root, 'Directory.Build.props'),
      scratchRoot: join(fixture.root, '.scratch'),
    })
    const xml = readFileSync(materialized.projectPath, 'utf8')

    assert.ok(!xml.includes('<ProjectReference'))
    assert.deepEqual(
      [...xml.matchAll(/<Compile Include="([^"]+)"\/>/g)].map((match) => match[1].split('/').at(-1)),
      sourceNames(plan),
    )
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})
