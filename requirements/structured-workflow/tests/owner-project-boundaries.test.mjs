import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { checkOwnerProjects, projectArchitectureViolations } from '../../../scripts/checks/owner-projects.mjs'
import { planOwnerCompile, materializeOwnerCompile, compileOwnerProject } from '../../../scripts/lib/owner-compile.mjs'

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

test('WHAT[STRUCTURED-WORKFLOW-011] GitGateway exact contract has an isolated compiler boundary', () => {
  const contracts = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/published-contracts.json'), 'utf8'))
  const contract = contracts.contracts.find((entry) => entry.path === 'src/Wanxiangshu/Git/Gateway.fs')
  assert.ok(contract)
  assert.deepEqual(contract.consumers, ['durable-convergence'])
  assert.deepEqual(contract.symbols, [
    'Wanxiangshu.Git.GitGateway.converge',
    'Wanxiangshu.Git.GitGateway.createDefaultRunner',
    'Wanxiangshu.Git.GitGatewayRunner',
  ])

  const providerName = 'Wanxiangshu.Owner.change-integration.git-gateway.fsproj'
  const provider = readFileSync(join(SRC, providerName), 'utf8')
  assert.match(provider, /<WanxiangshuOwnerLocality>git-gateway<\/WanxiangshuOwnerLocality>/)
  assert.match(provider, /<Compile Include="Git\/Gateway\.fsi"\s*\/>\s*<Compile Include="Git\/Gateway\.fs"\s*\/>/)
  assert.equal([...provider.matchAll(/<Compile Include="[^"]+\.fs"\s*\/>/g)].length, 1)

  const consumer = readFileSync(join(SRC, 'Wanxiangshu.Owner.durable-convergence.git-hook-sync.fsproj'), 'utf8')
  assert.match(consumer, /<ProjectReference Include="Wanxiangshu\.Owner\.change-integration\.git-gateway\.fsproj"\s*\/>/)
  assert.doesNotMatch(consumer, /ProjectReference Include="Wanxiangshu\.Owner\.change-integration\.git-integrationgate\.fsproj"/)

  const signature = readFileSync(join(SRC, 'Git/Gateway.fsi'), 'utf8')
  assert.doesNotMatch(signature, /SyncActiveEnv|discoverRemote/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] locality kinds enforce contract purity, direction, and closure budget', () => {
  const contract = resolve('/architecture/Contract.fsproj')
  const nestedContract = resolve('/architecture/NestedContract.fsproj')
  const runtime = resolve('/architecture/Runtime.fsproj')
  const foreignRuntime = resolve('/architecture/ForeignRuntime.fsproj')
  const composition = resolve('/architecture/Composition.fsproj')
  const missing = resolve('/architecture/Missing.fsproj')
  const project = (projectPath, owner, kind, compile, references = []) => ({
    projectPath,
    owner,
    kind,
    compile,
    references,
  })

  const violations = projectArchitectureViolations(
    new Map([
      [runtime, project(runtime, 'provider', 'runtime', ['Runtime.fs'])],
      [foreignRuntime, project(foreignRuntime, 'foreign', 'runtime', ['ForeignRuntime.fs'])],
      [nestedContract, project(nestedContract, 'provider', 'contract', ['NestedA.fs', 'NestedB.fs'], [runtime])],
      [contract, project(contract, 'consumer', 'contract', ['Contract.fs'], [nestedContract, foreignRuntime])],
      [composition, project(composition, 'composition', 'composition', ['Composition.fs'], [foreignRuntime])],
      [missing, project(missing, 'missing', '', ['Missing.fs'])],
    ]),
    { contractSourceBudget: 2 },
  )

  assert.ok(violations.some((violation) => /Missing\.fsproj: missing WanxiangshuOwnerLocalityKind/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj: contract closure contains non-contract .*Runtime\.fsproj/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj: contract closure contains non-contract .*ForeignRuntime\.fsproj/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj: contract closure has 5 production \.fs; budget is 2/.test(violation)))
  assert.ok(violations.some((violation) => /Contract\.fsproj -> .*ForeignRuntime\.fsproj: only composition may reference foreign runtime/.test(violation)))
  assert.ok(!violations.some((violation) => /\/Composition\.fsproj -> .*ForeignRuntime\.fsproj/.test(violation)))
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
    assert.match(scratchProps, /<NuGetAudit>false<\/NuGetAudit>/)
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

test('WHAT[STRUCTURED-WORKFLOW-011] flat Fable projection materialization escapes XML metacharacters, strips emitter identity, and binds source bytes into isolated fingerprints', () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-xml-metachar-proof-'))
  try {
    const signatureFile = join(scratchRoot, 'Special&Signature.fsi')
    writeFileSync(signatureFile, 'namespace Special\nmodule SpecialSource\nval x : int\n', 'utf8')

    const compileFile = join(scratchRoot, 'Special&Source.fs')
    writeFileSync(compileFile, 'namespace Special\nmodule SpecialSource\nlet x = 1\n', 'utf8')

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
    <Compile Include="Special&amp;Signature.fsi" />
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
    <Compile Include="Special&amp;Signature.fsi" />
    <Compile Include="Special&amp;Source.fs" />
  </ItemGroup>
</Project>`,
      'utf8',
    )

    const plan = planOwnerCompile({
      projectPath: ownerPath,
      aggregatePath,
    })

    // Planner resolves the real decoded ampersand filenames in canonical order
    const normalizedSignatureFile = resolve(signatureFile).replace(/\\/g, '/')
    const normalizedCompileFile = resolve(compileFile).replace(/\\/g, '/')
    assert.deepEqual(plan.compileItems, [normalizedSignatureFile, normalizedCompileFile])
    assert.ok(
      plan.compileItems[0].endsWith('/Special&Signature.fsi'),
      'planner must resolve real decoded signature filename',
    )
    assert.ok(
      plan.compileItems[1].endsWith('/Special&Source.fs'),
      'planner must resolve real decoded source filename',
    )

    const initialMaterialized = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })

    const generatedXml = readFileSync(initialMaterialized.projectPath, 'utf8')
    const scratchProps = readFileSync(join(initialMaterialized.scratchDir, 'Directory.Build.props'), 'utf8')

    // Materialized project strips WanxiangshuEmitProject identity
    assert.match(readFileSync(aggregatePath, 'utf8'), /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
    assert.doesNotMatch(
      generatedXml,
      /<WanxiangshuEmitProject\b/i,
      'materialized project must strip WanxiangshuEmitProject emitter identity',
    )

    // Compile Include attribute escaping proof for signature and implementation
    const expectedSigAttr = normalizedSignatureFile
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')
    const expectedCompileAttr = normalizedCompileFile
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&apos;')

    assert.ok(expectedSigAttr.includes('&amp;'))
    assert.ok(expectedCompileAttr.includes('&amp;'))
    assert.match(
      generatedXml,
      new RegExp(`<Compile Include="${expectedSigAttr.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}"\\s*/>`),
    )
    assert.match(
      generatedXml,
      new RegExp(`<Compile Include="${expectedCompileAttr.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}"\\s*/>`),
    )
    assert.ok(
      !generatedXml.includes(`Include="${normalizedSignatureFile}"`),
      'generated XML must not contain raw unescaped signature Include attribute',
    )
    assert.ok(
      !generatedXml.includes(`Include="${normalizedCompileFile}"`),
      'generated XML must not contain raw unescaped Compile Include attribute',
    )

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

    // 1. Invalidation proof: mutating signature file bytes invalidates fingerprint and isolates scratch/output
    writeFileSync(signatureFile, 'namespace Special\nmodule SpecialSource\nval x : int\nval y : string\n', 'utf8')
    const materializedAfterSigChange = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.notEqual(
      materializedAfterSigChange.fingerprint,
      initialMaterialized.fingerprint,
      'signature source byte change must invalidate fingerprint',
    )
    assert.notEqual(
      materializedAfterSigChange.scratchDir,
      initialMaterialized.scratchDir,
      'signature source byte change must isolate scratch directory',
    )
    assert.notEqual(
      materializedAfterSigChange.projectPath,
      initialMaterialized.projectPath,
      'signature source byte change must isolate project path',
    )
    assert.notEqual(
      materializedAfterSigChange.outputPath,
      initialMaterialized.outputPath,
      'signature source byte change must isolate output path',
    )
    assert.notEqual(
      materializedAfterSigChange.assetsPath,
      initialMaterialized.assetsPath,
      'signature source byte change must isolate assets path',
    )

    // 2. Invalidation proof: mutating implementation source file bytes invalidates fingerprint and isolates scratch/output
    writeFileSync(compileFile, 'namespace Special\nmodule SpecialSource\nlet x = 2\nlet y = "hello"\n', 'utf8')
    const materializedAfterSrcChange = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.notEqual(
      materializedAfterSrcChange.fingerprint,
      materializedAfterSigChange.fingerprint,
      'implementation source byte change must invalidate fingerprint',
    )
    assert.notEqual(
      materializedAfterSrcChange.fingerprint,
      initialMaterialized.fingerprint,
      'implementation source byte change must invalidate initial fingerprint',
    )
    assert.notEqual(
      materializedAfterSrcChange.scratchDir,
      materializedAfterSigChange.scratchDir,
      'implementation source byte change must isolate scratch directory',
    )
    assert.notEqual(
      materializedAfterSrcChange.projectPath,
      materializedAfterSigChange.projectPath,
      'implementation source byte change must isolate project path',
    )
    assert.notEqual(
      materializedAfterSrcChange.outputPath,
      materializedAfterSigChange.outputPath,
      'implementation source byte change must isolate output path',
    )
    assert.notEqual(
      materializedAfterSrcChange.assetsPath,
      materializedAfterSigChange.assetsPath,
      'implementation source byte change must isolate assets path',
    )

    // 3. Cache stability proof: unchanged inputs with same plan must produce identical fingerprint and reuse scratch/output
    const materializedStable = materializeOwnerCompile(plan, {
      scratchRoot,
      rootPropsPath,
    })
    assert.equal(
      materializedStable.fingerprint,
      materializedAfterSrcChange.fingerprint,
      'unchanged inputs must preserve deterministic fingerprint',
    )
    assert.equal(
      materializedStable.scratchDir,
      materializedAfterSrcChange.scratchDir,
      'unchanged inputs must reuse scratch directory',
    )
    assert.equal(
      materializedStable.projectPath,
      materializedAfterSrcChange.projectPath,
      'unchanged inputs must reuse project path',
    )
    assert.equal(
      materializedStable.outputPath,
      materializedAfterSrcChange.outputPath,
      'unchanged inputs must reuse output path',
    )
    assert.equal(
      materializedStable.assetsPath,
      materializedAfterSrcChange.assetsPath,
      'unchanged inputs must reuse assets path',
    )

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

test('WHAT[STRUCTURED-WORKFLOW-011] failure lifecycle prevents false-green warm cache and enforces success marker contract', async () => {
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-sw011-failure-proof-'))
  try {
    const aggregatePath = join(FIXTURE, 'Emitter.fsproj')
    const projectPath = join(FIXTURE, 'LeakyConsumer.fsproj')
    const rootPropsPath = join(ROOT, 'Directory.Build.props')

    let spawnInvocations = 0
    let run4SawPreservedOutput = false
    let run5SawPreservedOutput = false

    const fakeSpawn = (command, args, options) => {
      spawnInvocations++
      const child = new EventEmitter()
      child.stdout = new EventEmitter()
      child.stderr = new EventEmitter()

      const outIndex = args.indexOf('-o')
      const targetOutputDir = outIndex !== -1 ? args[outIndex + 1] : null

      setImmediate(() => {
        if (spawnInvocations === 1) {
          // First run: writes partial JS output then exits nonzero
          if (targetOutputDir) {
            writeFileSync(join(targetOutputDir, 'Partial.fs.js'), '// partial output from crashed run 1\n', 'utf8')
          }
          child.emit('close', 1, null)
        } else if (spawnInvocations === 2) {
          // Second run: would falsely return zero ONLY if partial output from run 1 survived
          if (targetOutputDir && existsSync(join(targetOutputDir, 'Partial.fs.js'))) {
            child.emit('close', 0, null)
          } else {
            // Correct behavior: partial output from run 1 was cleaned up; this run also writes partial output and fails
            if (targetOutputDir) {
              writeFileSync(join(targetOutputDir, 'Partial2.fs.js'), '// partial output from crashed run 2\n', 'utf8')
            }
            child.emit('close', 2, null)
          }
        } else if (spawnInvocations === 3) {
          // Third run: simulated successful emit writing verified JS files and exiting 0
          if (targetOutputDir) {
            writeFileSync(join(targetOutputDir, 'Runtime.fs.js'), 'export const runtime = true;\n', 'utf8')
            writeFileSync(join(targetOutputDir, 'LeakyConsumer.fs.js'), 'export const consumer = true;\n', 'utf8')
          }
          child.emit('close', 0, null)
        } else if (spawnInvocations === 4) {
          // Fourth run (warm compile): must invoke injected spawn again, preserve output before invocation
          if (
            targetOutputDir &&
            existsSync(join(targetOutputDir, 'Runtime.fs.js')) &&
            existsSync(join(targetOutputDir, 'LeakyConsumer.fs.js'))
          ) {
            run4SawPreservedOutput = true
            writeFileSync(join(targetOutputDir, 'Runtime.fs.js'), 'export const runtime = true; // refreshed\n', 'utf8')
            writeFileSync(join(targetOutputDir, 'LeakyConsumer.fs.js'), 'export const consumer = true; // refreshed\n', 'utf8')
            child.emit('close', 0, null)
          } else {
            child.emit('close', 40, null)
          }
        } else if (spawnInvocations === 5) {
          // Fifth run (warm compile with failure): output preserved before spawn, but compiler fails
          if (
            targetOutputDir &&
            existsSync(join(targetOutputDir, 'Runtime.fs.js')) &&
            existsSync(join(targetOutputDir, 'LeakyConsumer.fs.js'))
          ) {
            run5SawPreservedOutput = true
            child.emit('close', 5, null)
          } else {
            child.emit('close', 50, null)
          }
        } else {
          // Unexpected spawn call
          child.emit('close', 99, null)
        }
      })

      return child
    }

    // Call 1: First compilation fails after partial emit
    const result1 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result1.ok, false, 'first compilation must fail on nonzero child exit')
    assert.equal(result1.code, 1, 'first compilation must propagate exit code 1')
    assert.equal(spawnInvocations, 1, 'first compilation must invoke spawn exactly once')
    assert.equal(
      existsSync(join(result1.outputPath, 'Partial.fs.js')),
      false,
      'partial JS output must be cleaned up after first compilation failure',
    )
    assert.equal(
      existsSync(join(result1.scratchDir, '.success')),
      false,
      'success marker must not exist after first compilation failure',
    )

    // Call 2: Second compilation with same inputs must fail (not falsely return zero due to stale partial output)
    const result2 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result2.ok, false, 'second compilation must fail without valid success marker')
    assert.equal(result2.code, 2, 'second compilation must fail with clean-state failure code')
    assert.equal(spawnInvocations, 2, 'second compilation must invoke spawn because cache is unvalidated')
    assert.equal(
      existsSync(join(result2.outputPath, 'Partial.fs.js')),
      false,
      'stale partial output from run 1 must not exist after second compilation',
    )
    assert.equal(
      existsSync(join(result2.outputPath, 'Partial2.fs.js')),
      false,
      'partial output from run 2 must be cleaned up after failure',
    )
    assert.equal(
      existsSync(join(result2.scratchDir, '.success')),
      false,
      'success marker must not exist after second compilation failure',
    )

    // Call 3: Simulated successful compilation emit creates success marker and preserves JS outputs
    const result3 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result3.ok, true, 'third compilation with valid emit must succeed')
    assert.equal(result3.code, 0, 'third compilation must return exit code 0')
    assert.equal(spawnInvocations, 3, 'third compilation must invoke spawn')
    assert.equal(
      existsSync(join(result3.outputPath, 'Runtime.fs.js')),
      true,
      'emitted Runtime.fs.js must exist on successful compilation',
    )
    assert.equal(
      existsSync(join(result3.outputPath, 'LeakyConsumer.fs.js')),
      true,
      'emitted LeakyConsumer.fs.js must exist on successful compilation',
    )
    const markerFile = join(result3.scratchDir, '.success')
    assert.equal(existsSync(markerFile), true, 'success marker must be created on successful zero-exit compilation')

    // Call 4: Next warm call must invoke injected spawn again, preserve output before invocation, and require a new successful compiler result
    const result4 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result4.ok, true, 'fourth compilation must succeed with new compiler result')
    assert.equal(result4.code, 0, 'fourth compilation must return exit code 0')
    assert.equal(spawnInvocations, 4, 'fourth compilation must invoke injected spawn again (no marker-only cache bypass)')
    assert.equal(run4SawPreservedOutput, true, 'fourth compilation must preserve existing output before spawn invocation')
    assert.equal(
      existsSync(join(result4.outputPath, 'Runtime.fs.js')),
      true,
      'emitted Runtime.fs.js must exist on successful warm compilation',
    )
    assert.equal(
      existsSync(join(result4.outputPath, 'LeakyConsumer.fs.js')),
      true,
      'emitted LeakyConsumer.fs.js must exist on successful warm compilation',
    )
    assert.equal(existsSync(markerFile), true, 'success marker must remain intact after successful warm compilation')

    // Call 5: Warm compilation failure must remove marker and output
    const result5 = await compileOwnerProject({
      projectPath,
      aggregatePath,
      scratchRoot,
      rootPropsPath,
      spawn: fakeSpawn,
      stdio: 'pipe',
    })

    assert.equal(result5.ok, false, 'fifth compilation must fail when injected spawn returns nonzero')
    assert.equal(result5.code, 5, 'fifth compilation must propagate exit code 5')
    assert.equal(spawnInvocations, 5, 'fifth compilation must invoke injected spawn')
    assert.equal(run5SawPreservedOutput, true, 'fifth compilation must preserve existing output before spawn invocation')
    assert.equal(
      existsSync(join(result5.outputPath, 'Runtime.fs.js')),
      false,
      'injected warm failure must remove output directory / JS outputs',
    )
    assert.equal(
      existsSync(join(result5.outputPath, 'LeakyConsumer.fs.js')),
      false,
      'injected warm failure must remove output directory / JS outputs',
    )
    assert.equal(
      existsSync(markerFile),
      false,
      'injected warm failure must remove success marker',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
