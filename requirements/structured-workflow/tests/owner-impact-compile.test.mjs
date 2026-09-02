import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { EventEmitter } from 'node:events'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import {
  compileIncremental,
  compileOwnerProject,
  materializeOwnerCompile,
  planImpactCompile,
} from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')

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

test('WHAT[STRUCTURED-WORKFLOW-012] incremental compile executes focused flat compile and records cache', async () => {
  const fixture = createFixture()
  try {
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

    const outputDir = join(fixture.root, 'dist')
    const manifestPath = join(fixture.root, '.fable-build/build-manifest.json')

    const result = await compileIncremental({
      changedPaths: [join(fixture.root, 'Source/Runtime.fs')],
      aggregatePath: fixture.aggregate,
      rootPropsPath: join(fixture.root, 'Directory.Build.props'),
      scratchRoot: join(fixture.root, '.scratch'),
      outputDir,
      manifestPath,
      spawn,
      stdio: 'pipe',
    })

    assert.equal(result.ok, true)
    assert.equal(result.cached, false)
    assert.equal(calls.length, 1)
    assert.equal(calls[0].command, 'dotnet')
    assert.notEqual(calls[0].args[4], fixture.projects.runtime)
    assert.ok(!readFileSync(calls[0].args[4], 'utf8').includes('<ProjectReference'))
    assert.ok(existsSync(manifestPath), 'manifest must be recorded on successful compilation')
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

test('WHAT[STRUCTURED-WORKFLOW-012] multi-change union compiles each closure once', () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [
        join(fixture.root, 'Source/Runtime.fs'),
        join(fixture.root, 'Source/Unrelated.fs'),
      ],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
      fullThreshold: 1,
    })

    assert.equal(plan.mode, 'focused')
    assert.deepEqual(sourceNames(plan), [
      'Base.fsi', 'Base.fs',
      'Contract.fsi', 'Contract.fs',
      'Runtime.fsi', 'Runtime.fs',
      'Unrelated.fsi', 'Unrelated.fs',
    ])
    assert.ok(!plan.projectPaths.includes(fixture.projects.consumer))
    assert.ok(!plan.projectPaths.includes(fixture.projects.composition))
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] project file changes select one full flat build', () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [fixture.projects.runtime],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })
    assert.equal(plan.mode, 'full')
    assert.equal(plan.reason, 'toolchain-or-project-change')
    assert.equal(plan.compileItems.length, 12)
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] production impact-set ladder classifies fs fsi project and toolchain', () => {
  const impl = planImpactCompile({
    changedPaths: [join(SOURCE_ROOT, 'Foundation/FatalProcess.fs')],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  assert.equal(impl.mode, 'focused')
  assert.equal(impl.reason, 'focused-impact')
  assert.deepEqual(
    impl.compileItems.map((path) => path.split('/').at(-1)),
    ['FatalProcess.fsi', 'FatalProcess.fs'],
  )

  const signature = planImpactCompile({
    changedPaths: [join(SOURCE_ROOT, 'Foundation/FatalProcess.fsi')],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  assert.equal(signature.mode, 'full')
  assert.equal(signature.reason, 'impact-exceeds-full-threshold')
  assert.ok(signature.compileItems.length > impl.compileItems.length)

  const project = planImpactCompile({
    changedPaths: [join(SOURCE_ROOT, 'Wanxiangshu.Owner.host-boundary.host-fatal-effect.fsproj')],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  assert.equal(project.mode, 'full')
  assert.equal(project.reason, 'toolchain-or-project-change')

  const toolchain = planImpactCompile({
    changedPaths: [join(ROOT, 'package.json')],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  assert.equal(toolchain.mode, 'full')
  assert.equal(toolchain.reason, 'toolchain-or-project-change')
})

test('WHAT[STRUCTURED-WORKFLOW-012] compile-impact CLI plan-only smoke matches the planner', () => {
  const changed = join(SOURCE_ROOT, 'Foundation/FatalProcess.fs')
  const result = spawnSync(
    process.execPath,
    ['scripts/compile-impact.mjs', changed, '--plan-only'],
    { cwd: ROOT, encoding: 'utf8' },
  )
  assert.equal(result.status, 0, result.stderr || result.stdout)
  const cli = JSON.parse(result.stdout)
  const plan = planImpactCompile({
    changedPaths: [changed],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  assert.equal(cli.mode, plan.mode)
  assert.equal(cli.reason, plan.reason)
  assert.deepEqual(cli.compileItems, plan.compileItems)
})

test('WHAT[STRUCTURED-WORKFLOW-012] obsolete recursive-graph compile probes stay deleted', () => {
  assert.equal(existsSync(join(ROOT, 'scripts/analyze-closures.mjs')), false)
  assert.equal(existsSync(join(SOURCE_ROOT, 'FableBarrier.fs')), false)

  const ownerCli = readFileSync(join(ROOT, 'scripts/compile-owner.mjs'), 'utf8')
  const impactCli = readFileSync(join(ROOT, 'scripts/compile-impact.mjs'), 'utf8')
  const lib = readFileSync(join(ROOT, 'scripts/lib/owner-compile.mjs'), 'utf8')

  assert.match(lib, /generateFlatProjectXml/)
  assert.match(lib, /zero ProjectReference|Wanxiangshu\.Impact\.fsproj/)
  assert.doesNotMatch(ownerCli, /tool',\s*'run',\s*'fable'/)
  assert.doesNotMatch(impactCli, /tool',\s*'run',\s*'fable'/)
  assert.match(ownerCli, /compileOwnerProject/)
  assert.match(impactCli, /compileOwnerProject/)
  assert.match(impactCli, /planImpactCompile/)
})
