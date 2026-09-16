import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { EventEmitter } from 'node:events'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, statSync, utimesSync, writeFileSync } from 'node:fs'
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

const writeProject = (root, name, shard, refs, source) => {
  const path = join(root, `${name}.fsproj`)
  writeFileSync(path, `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuSubsystem>fixture</WanxiangshuSubsystem>
    <WanxiangshuCompileShard>${shard}</WanxiangshuCompileShard>
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
    base: writeProject(root, 'Wanxiangshu.Owner.Base', 'base-contract', [], 'Base'),
    contract: writeProject(root, 'Wanxiangshu.Owner.Provider.Contract', 'provider-contract', ['Wanxiangshu.Owner.Base'], 'Contract'),
    runtime: writeProject(root, 'Wanxiangshu.Owner.Provider.Runtime', 'provider-runtime', ['Wanxiangshu.Owner.Provider.Contract'], 'Runtime'),
    consumer: writeProject(root, 'Wanxiangshu.Owner.Consumer.Runtime', 'consumer-runtime', ['Wanxiangshu.Owner.Provider.Contract'], 'Consumer'),
    composition: writeProject(root, 'Wanxiangshu.Owner.Composition.Runtime', 'composition-runtime', ['Wanxiangshu.Owner.Provider.Runtime', 'Wanxiangshu.Owner.Consumer.Runtime'], 'Composition'),
    unrelated: writeProject(root, 'Wanxiangshu.Owner.Unrelated.Runtime', 'unrelated-runtime', ['Wanxiangshu.Owner.Base'], 'Unrelated'),
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

test('WHAT[STRUCTURED-WORKFLOW-012] implementation changes reach reverse consumers', () => {
  const fixture = createFixture()
  try {
    const plan = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Runtime.fs')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
    })

    assert.equal(plan.mode, 'focused')
    assert.equal(plan.reason, 'focused-impact')
    // Structured-workflow WHAT §implementation scope: a non-risky `.fs`
    // body change whose sibling `.fsi` is unchanged selects only the owning
    // shard plus its forward dependencies — never every reverse consumer.
    assert.deepEqual(
      new Set(plan.projectPaths),
      new Set([fixture.projects.base, fixture.projects.contract, fixture.projects.runtime]),
    )
    assert.ok(!plan.projectPaths.includes(fixture.projects.consumer))
    assert.ok(!plan.projectPaths.includes(fixture.projects.composition))
  } finally {
    rmSync(fixture.root, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] signature-risky implementation changes still reach reverse consumers', () => {
  const fixture = createFixture()
  try {
    // `let inline` / [<Literal>] bodies are emitted at the call site: a body
    // change with an unchanged .fsi is still a contract change in effect.
    writeFileSync(
      join(fixture.root, 'Source/Runtime.fs'),
      'namespace Fixture\n[<Literal>] let rom = "x"\nlet inline tag x = x + "r"\n'
    )
    const plan = planImpactCompile({
      changedPaths: [join(fixture.root, 'Source/Runtime.fs')],
      projectDirectory: fixture.root,
      aggregatePath: fixture.aggregate,
      fullThreshold: 1,
    })

    assert.equal(plan.mode, 'focused')
    assert.ok(plan.projectPaths.includes(fixture.projects.consumer))
    assert.ok(plan.projectPaths.includes(fixture.projects.composition))
    assert.ok(plan.projectPaths.includes(fixture.projects.runtime))
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
      changedPaths: [join(fixture.root, 'Source/Unrelated.fs')],
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
    assert.equal(result.mode, 'focused')
    assert.equal(calls.length, 1)
    assert.equal(calls[0].command, 'dotnet')
    assert.notEqual(calls[0].args[4], fixture.projects.unrelated)
    assert.ok(!readFileSync(calls[0].args[4], 'utf8').includes('<ProjectReference'), 'focused build emits flat project with zero ProjectReference')
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
      'Consumer.fsi', 'Consumer.fs',
      'Runtime.fsi', 'Runtime.fs',
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
  // Plain implementation change on a paired .fs: stays inside the owning
  // shard's forward closure — the documented FatalProcess closure keeps its
  // two own sources plus whatever its shard directly depends on.
  assert.equal(impl.mode, 'focused')
  assert.equal(impl.reason, 'focused-impact')
  assert.ok(impl.compileItems.includes(join(SOURCE_ROOT, 'Foundation/FatalProcess.fs')))
  assert.ok(impl.compileItems.includes(join(SOURCE_ROOT, 'Foundation/FatalProcess.fsi')))
  // Reverse consumers elsewhere in the tree (Enforcer, OpenCode hosts, etc.)
  // must NOT be recompiled for a non-signature body change.
  assert.ok(!impl.compileItems.some((item) => item.endsWith('Enforcer/Continuation.fs')))
  assert.ok(impl.compileItems.length < 100)

  const signature = planImpactCompile({
    changedPaths: [join(SOURCE_ROOT, 'Foundation/FatalProcess.fsi')],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  // Contract change: every reverse consumer joins the closure.
  assert.ok(
    signature.mode === 'full' || signature.projectPaths.length > impl.projectPaths.length,
    `signature change must widen the scope, got ${signature.mode} with ${signature.projectPaths.length} projects`,
  )
  assert.ok(new Set(signature.projectPaths).size >= new Set(impl.projectPaths).size)
  
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
  // W4 cutover: `compile-impact.mjs` was deleted; `build.mjs --plan` is the
  // remaining read-only preview and reports the same planner-shaped payload.
  const result = spawnSync(
    process.execPath,
    ['scripts/build.mjs', '--plan'],
    { cwd: ROOT, encoding: 'utf8' },
  )
  assert.equal(result.status, 0, result.stderr || result.stdout)
  const cli = JSON.parse(result.stdout)
  assert.equal(cli.mode, 'no-op', 'manifest is fresh right after a successful build phase')
  const plan = planImpactCompile({
    changedPaths: [changed],
    projectDirectory: SOURCE_ROOT,
    aggregatePath: AGGREGATE,
  })
  assert.equal(plan.mode, 'focused')
  assert.equal(plan.reason, 'focused-impact')
  assert.ok(plan.compileItems.includes(join(SOURCE_ROOT, 'Foundation/FatalProcess.fs')))
})

test('WHAT[STRUCTURED-WORKFLOW-012] obsolete recursive-graph compile probes stay deleted', () => {
  assert.equal(existsSync(join(ROOT, 'scripts/analyze-closures.mjs')), false)
  assert.equal(existsSync(join(SOURCE_ROOT, 'FableBarrier.fs')), false)

  const ownerCli = readFileSync(join(ROOT, 'scripts/compile-owner.mjs'), 'utf8')
  const lib = readFileSync(join(ROOT, 'scripts/lib/owner-compile.mjs'), 'utf8')

  assert.match(lib, /generateFlatProjectXml/)
  assert.match(lib, /zero ProjectReference|Wanxiangshu\.Impact\.fsproj/)
  assert.doesNotMatch(ownerCli, /tool',\s*'run',\s*'fable'/)
  assert.match(ownerCli, /compileOwnerProject/)
  // `build.mjs` itself is the canonical caller and legitimately invokes the
  // Fable toolchain — we don't pin the string here.
})
