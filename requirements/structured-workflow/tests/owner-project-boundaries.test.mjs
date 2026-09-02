import assert from 'node:assert/strict'
import { mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { checkOwnerProjects } from '../../../scripts/checks/owner-projects.mjs'
import { planOwnerCompile, materializeOwnerCompile } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SRC = join(ROOT, 'src/Wanxiangshu')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')

test('WHAT[STRUCTURED-WORKFLOW-011] flattened Fable emitter mirrors owner-locality source coverage', () => {
  const rootProject = readFileSync(join(SRC, 'Wanxiangshu.fsproj'), 'utf8')
  assert.match(rootProject, /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
  assert.doesNotMatch(rootProject, /<ProjectReference Include=/, 'emit project must not source-merge owner project graph')

  const ownerProjects = readdirSync(SRC).filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name))
  assert.ok(ownerProjects.length > 1, '57.15 requires independent owner-locality projects')

  for (const project of ownerProjects) {
    const xml = readFileSync(join(SRC, project), 'utf8')
    assert.match(xml, /<WanxiangshuSemanticOwner>[^<]+<\/WanxiangshuSemanticOwner>/)
    assert.match(xml, /<WanxiangshuOwnerLocality>[^<]+<\/WanxiangshuOwnerLocality>/)
  }

  const props = readFileSync(join(SRC, 'Directory.Build.props'), 'utf8')
  assert.match(props, /<DisableTransitiveProjectReferences>true<\/DisableTransitiveProjectReferences>/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] owner-locality project graph is complete, authorized, and acyclic', () => {
  const result = checkOwnerProjects()
  assert.equal(result.ok, true, result.violations.join('\n'))
  assert.ok(result.sourceCount > 0, 'owner-locality graph must cover production sources')
  assert.equal(result.contractLeakSourceCount, 0, 'published contract compile closure must contain no runtime/private source')
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection planner produces exact closure and canonical aggregate order', () => {
  const aggregatePath = join(FIXTURE, 'Emitter.fsproj')
  const leakyConsumerPath = join(FIXTURE, 'LeakyConsumer.fsproj')
  const leakyContractPath = join(FIXTURE, 'LeakyContract.fsproj')
  const runtimePath = join(FIXTURE, 'Runtime.fsproj')

  const plan = planOwnerCompile({
    projectPath: leakyConsumerPath,
    aggregatePath,
  })

  // Exact project closure
  const expectedProjects = [leakyConsumerPath, leakyContractPath, runtimePath].sort()
  assert.deepEqual(plan.projectPaths, expectedProjects)

  // Exact compile items filtered in Emitter.fsproj document order
  const expectedCompileItems = [
    join(FIXTURE, 'Runtime.fs'),
    join(FIXTURE, 'LeakyContract.fs'),
    join(FIXTURE, 'LeakyConsumer.fs'),
  ]
  assert.deepEqual(plan.compileItems, expectedCompileItems)

  // Unreferenced files must NOT be in compileItems
  assert.ok(!plan.compileItems.includes(join(FIXTURE, 'Provider.fs')))
  assert.ok(!plan.compileItems.includes(join(FIXTURE, 'GreenConsumer.fs')))
  assert.ok(!plan.compileItems.includes(join(FIXTURE, 'RedConsumer.fs')))

  // Signature order verification: .fsi must precede .fs
  const signedPlan = planOwnerCompile({
    projectPath: join(FIXTURE, 'SignedProvider.fsproj'),
    aggregatePath,
  })
  assert.deepEqual(signedPlan.compileItems, [
    join(FIXTURE, 'SignedProvider.fsi'),
    join(FIXTURE, 'SignedProvider.fs'),
  ])
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materializes zero ProjectReference and isolated scratch props', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-materialize-test-'))
  const rootPropsPath = join(ROOT, 'Directory.Build.props')
  try {
    const aggregatePath = join(FIXTURE, 'Emitter.fsproj')
    const plan = planOwnerCompile({
      projectPath: join(FIXTURE, 'LeakyConsumer.fsproj'),
      aggregatePath,
    })

    const materialized = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })

    // Generated project file must contain zero ProjectReference and strip WanxiangshuEmitProject identity
    const generatedXml = readFileSync(materialized.projectPath, 'utf8')
    assert.doesNotMatch(generatedXml, /<ProjectReference\b/i, 'generated flat project must contain zero ProjectReference')
    assert.match(readFileSync(aggregatePath, 'utf8'), /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
    assert.doesNotMatch(
      generatedXml,
      /<WanxiangshuEmitProject\b/i,
      'generated flat project must strip WanxiangshuEmitProject emitter identity',
    )

    // Generated project must preserve aggregate shell and contain absolute Compile entries in order
    assert.match(generatedXml, /<TargetFramework>net10\.0<\/TargetFramework>/)
    assert.match(generatedXml, /<PackageReference Include="Fable\.Core"/)
    assert.match(generatedXml, new RegExp(`<Compile Include="${join(FIXTURE, 'Runtime.fs')}"\\s*/>`))
    assert.match(generatedXml, new RegExp(`<Compile Include="${join(FIXTURE, 'LeakyContract.fs')}"\\s*/>`))
    assert.match(generatedXml, new RegExp(`<Compile Include="${join(FIXTURE, 'LeakyConsumer.fs')}"\\s*/>`))

    // Scratch Directory.Build.props must define isolated ArtifactsDir and import root props
    const scratchProps = readFileSync(join(materialized.scratchDir, 'Directory.Build.props'), 'utf8')
    assert.match(scratchProps, /<ArtifactsDir>\$\(MSBuildThisFileDirectory\)artifacts\/<\/ArtifactsDir>/)
    assert.match(scratchProps, new RegExp(`<Import Project="${rootPropsPath}"\\s*/>`))

    // Deterministic fingerprint
    const materializedAgain = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.equal(materializedAgain.fingerprint, materialized.fingerprint)
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat projection rejects missing or stale ProjectReference before compiler invocation', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-reject-test-'))
  try {
    const aggregatePath = join(FIXTURE, 'Emitter.fsproj')

    // Stale/missing ProjectReference
    const missingRefProject = join(scratchRoot, 'MissingRef.fsproj')
    writeFileSync(
      missingRefProject,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="NonExistentTarget.fsproj"/>
    <Compile Include="${join(FIXTURE, 'Provider.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )

    assert.throws(
      () => planOwnerCompile({ projectPath: missingRefProject, aggregatePath }),
      /Missing ProjectReference.*NonExistentTarget\.fsproj/i,
      'must reject non-existent ProjectReference target before compiler invocation',
    )

    // ProjectReference cycle
    const cycleA = join(scratchRoot, 'CycleA.fsproj')
    const cycleB = join(scratchRoot, 'CycleB.fsproj')
    writeFileSync(
      cycleA,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="CycleB.fsproj"/>
    <Compile Include="${join(FIXTURE, 'Provider.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )
    writeFileSync(
      cycleB,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="CycleA.fsproj"/>
    <Compile Include="${join(FIXTURE, 'Runtime.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )

    assert.throws(
      () => planOwnerCompile({ projectPath: cycleA, aggregatePath }),
      /ProjectReference cycle detected/i,
      'must reject ProjectReference cycle before compiler invocation',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materialization escapes XML metacharacters in paths and strips emitter identity', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-xml-metachar-proof-'))
  try {
    const compileFile = join(scratchRoot, 'Special&Source.fs')
    writeFileSync(compileFile, 'module SpecialSource\nlet x = 1\n', 'utf8')

    const rootPropsPath = join(scratchRoot, 'Root&<Props>\'Test".props')
    writeFileSync(rootPropsPath, '<Project />', 'utf8')

    const aggregatePath = join(scratchRoot, 'Emitter.fsproj')
    writeFileSync(
      aggregatePath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuEmitProject>true</WanxiangshuEmitProject>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Special&amp;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    const ownerPath = join(scratchRoot, 'Owner.fsproj')
    writeFileSync(
      ownerPath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Special&amp;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    const plan = planOwnerCompile({
      projectPath: ownerPath,
      aggregatePath,
    })

    // Planner resolves the real decoded ampersand filename
    const normalizedCompileFile = resolve(compileFile).replace(/\\/g, '/')
    assert.deepEqual(plan.compileItems, [normalizedCompileFile])
    assert.ok(
      plan.compileItems[0].endsWith('/Special&Source.fs'),
      'planner must resolve real decoded ampersand filename',
    )

    const materialized = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })

    const generatedXml = readFileSync(materialized.projectPath, 'utf8')
    const scratchProps = readFileSync(join(materialized.scratchDir, 'Directory.Build.props'), 'utf8')

    // Materialized project strips WanxiangshuEmitProject identity
    assert.match(readFileSync(aggregatePath, 'utf8'), /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
    assert.doesNotMatch(
      generatedXml,
      /<WanxiangshuEmitProject\b/i,
      'materialized project must strip WanxiangshuEmitProject emitter identity',
    )

    // Compile Include attribute escaping proof
    const expectedCompileAttr = normalizedCompileFile
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')
    assert.ok(expectedCompileAttr.includes('&amp;'))
    const compileMatch = generatedXml.match(/<Compile Include="([^"]*)"\s*\/>/)
    assert.ok(compileMatch, 'generated XML must contain Compile element with Include attribute')
    assert.equal(compileMatch[1], expectedCompileAttr)
    assert.doesNotMatch(compileMatch[1], /[<>'"]/)
    assert.doesNotMatch(compileMatch[1], /&(?!(amp|lt|gt|quot|apos);)/)
    assert.ok(!generatedXml.includes(`Include="${normalizedCompileFile}"`), 'generated XML must not contain raw unescaped Compile Include attribute')

    // Root props Import attribute escaping proof
    const normalizedRootPropsPath = resolve(rootPropsPath).replace(/\\/g, '/')
    const expectedPropsAttr = normalizedRootPropsPath
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')
    assert.ok(expectedPropsAttr.includes('&amp;&lt;Props&gt;&apos;Test&quot;.props'))
    const propsMatch = scratchProps.match(/<Import Project="([^"]*)"\s*\/>/)
    assert.ok(propsMatch, 'Directory.Build.props must contain Import element with Project attribute')
    assert.equal(propsMatch[1], expectedPropsAttr)
    assert.doesNotMatch(propsMatch[1], /[<>'"]/)
    assert.doesNotMatch(propsMatch[1], /&(?!(amp|lt|gt|quot|apos);)/)
    assert.ok(!scratchProps.includes(`Project="${normalizedRootPropsPath}"`), 'Directory.Build.props must not contain raw unescaped Import Project attribute')

    // Fail-closed unknown-entity assertion
    const unknownEntityPath = join(scratchRoot, 'UnknownEntity.fsproj')
    writeFileSync(
      unknownEntityPath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Unknown&unknown;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    assert.throws(
      () => planOwnerCompile({ projectPath: unknownEntityPath, aggregatePath }),
      /(?:Unknown|Malformed|Invalid) XML (?:entity|character) reference/i,
      'must reject unknown XML entity reference fail-closed before compiler invocation',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
