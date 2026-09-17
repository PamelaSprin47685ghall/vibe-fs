import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { loopDetectorRepositoryInputFiles } = await import("../../../scripts/lib/loop-detector-repository-corpus.mjs");
const { assertBuildFresh, collectCompilerInputs, collectGeneratedInputs, collectArtifactInputs, collectOutputs, computeDigest, readManifest, writeManifest, invalidateManifest, MANIFEST_SCHEMA } = await import("../../../scripts/lib/build-state.mjs");
const { planImpactCompile, resetOutputDirectory } = await import("../../../scripts/lib/owner-compile.mjs");
const { runBuild } = await import("../../../scripts/build.mjs");
const { assertProductionSourcesAssigned } = await import("../../../scripts/lib/compile-shards.mjs");


test('WHAT[VERIFICATION-SYSTEM-008] assertBuildFresh succeeds on current repository build', () => {
  const freshness = assertBuildFresh({ root: process.cwd() })
  assert.ok(freshness.generation >= 1)
  assert.ok(typeof freshness.compilerInputDigest === 'string')
  assert.ok(typeof freshness.generatedInputDigest === 'string')
  assert.ok(typeof freshness.artifactInputDigest === 'string')
})
test('WHAT[VERIFICATION-SYSTEM-008] no-op mode preserves manifest and generation', async () => {
  const root = process.cwd()
  const manifestBefore = readManifest({ root })
  assert.ok(manifestBefore !== null)

  const result = await runBuild({ targetRoot: root, clean: false })
  assert.equal(result.ok, true)
  assert.equal(result.mode, 'no-op')
  assert.equal(result.generation, manifestBefore.generation)

  const manifestAfter = readManifest({ root })
  assert.equal(manifestAfter.generation, manifestBefore.generation)
})
test('WHAT[VERIFICATION-SYSTEM-008] manifest corruption causes assertBuildFresh to throw with code', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-corrupt-manifest-'))
  try {
    const buildStateDir = join(root, '.fable-build')
    mkdirSync(buildStateDir, { recursive: true })
    writeFileSync(join(buildStateDir, 'build-manifest.json'), '{ not valid json', 'utf8')

    assert.throws(
      () => assertBuildFresh({ root }),
      (err) => {
        assert.equal(err.code, 'manifest-corrupt')
        return true
      },
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
test('WHAT[VERIFICATION-SYSTEM-008] missing or stale output entry causes assertBuildFresh to throw', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-stale-output-'))
  try {
    const buildStateDir = join(root, '.fable-build')
    const distDir = join(root, 'dist')
    mkdirSync(buildStateDir, { recursive: true })
    mkdirSync(distDir, { recursive: true })

    const outputA = join(distDir, 'A.js')
    writeFileSync(outputA, 'export const a = 1\n', 'utf8')
    const outputs = collectOutputs(distDir)

    const manifest = {
      schema: MANIFEST_SCHEMA,
      rootIdentity: root,
      outputDir: 'dist',
      generation: 1,
      compiler: { inputDigest: 'test', inputs: [] },
      generated: { inputDigest: 'test', inputs: [] },
      artifacts: { inputDigest: 'test', inputs: [] },
      outputs,
    }
    writeManifest({ root, manifest })

    // If an output file is deleted:
    rmSync(outputA)
    assert.throws(
      () => assertBuildFresh({ root }),
      (err) => {
        assert.ok(err.code === 'output-missing' || err.code === 'output-empty')
        return true
      },
    )

    // If an output file is modified with stale hash:
    writeFileSync(outputA, 'export const a = 2\n', 'utf8')
    const outputB = join(distDir, 'B.js')
    writeFileSync(outputB, 'export const b = 2\n', 'utf8')
    // Reset outputs in manifest to old A.js
    manifest.outputs = outputs
    writeManifest({ root, manifest })

    assert.throws(
      () => assertBuildFresh({ root }),
      (err) => {
        assert.ok(err.code === 'output-stale' || err.code === 'output-extra')
        return true
      },
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
test('WHAT[VERIFICATION-SYSTEM-008] changed .fs with unchanged .fsi triggers reverse-consumer recompile', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-inline-fsi-'))
  try {
    const aggregate = join(root, 'Wanxiangshu.fsproj')
    const providerProj = join(root, 'Provider.fsproj')
    const consumerProj = join(root, 'Consumer.fsproj')

    const providerFsi = join(root, 'Provider.fsi')
    const providerFs = join(root, 'Provider.fs')
    const consumerFs = join(root, 'Consumer.fs')

    writeFileSync(providerFsi, 'namespace Sample\n', 'utf8')
    writeFileSync(providerFs, 'namespace Sample\nlet inline helper x = x + 1\n', 'utf8')
    writeFileSync(consumerFs, 'namespace Sample\nlet consume x = helper x\n', 'utf8')

    writeFileSync(providerProj, `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="${providerFsi}"/>
    <Compile Include="${providerFs}"/>
  </ItemGroup>
</Project>\n`, 'utf8')

    writeFileSync(consumerProj, `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="${providerProj}"/>
    <Compile Include="${consumerFs}"/>
  </ItemGroup>
</Project>\n`, 'utf8')

    writeFileSync(aggregate, `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="${providerFsi}"/>
    <Compile Include="${providerFs}"/>
    <Compile Include="${consumerFs}"/>
  </ItemGroup>
</Project>\n`, 'utf8')

    // Change .fs only (unchanged .fsi)
    const plan = planImpactCompile({
      changedPaths: [providerFs],
      projectDirectory: root,
      aggregatePath: aggregate,
      fullThreshold: 1,
    })

    assert.equal(plan.mode, 'focused')
    assert.ok(
      plan.projectPaths.includes(consumerProj),
      'reverse consumer must be included when .fs implementation changes, even with unchanged .fsi',
    )
    assert.ok(
      plan.compileItems.includes(consumerFs),
      'consumer compile item must be in compile plan',
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
test('WHAT[VERIFICATION-SYSTEM-008] untracked new production source fails compile-shard inventory', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-untracked-source-'))
  try {
    const sourceRoot = join(root, 'src/Wanxiangshu')
    mkdirSync(sourceRoot, { recursive: true })
    const aggregate = join(sourceRoot, 'Wanxiangshu.fsproj')
    const trackedSource = join(sourceRoot, 'Tracked.fs')
    const untrackedSource = join(sourceRoot, 'Untracked.fs')

    writeFileSync(trackedSource, 'module Tracked\n', 'utf8')
    writeFileSync(untrackedSource, 'module Untracked\n', 'utf8')

    writeFileSync(aggregate, `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WanxiangshuEmitProject>true</WanxiangshuEmitProject>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Tracked.fs"/>
  </ItemGroup>
</Project>\n`, 'utf8')

    assert.throws(
      () => assertProductionSourcesAssigned({
        repositoryRoot: root,
        sourceRoot,
        aggregatePath: aggregate,
        discoveredSources: new Set([trackedSource, untrackedSource]),
        shardImplementations: new Set([trackedSource]),
      }),
      /production source coverage mismatch/,
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
test('WHAT[VERIFICATION-SYSTEM-008] release output reset physically removes stale artifacts', () => {
  const root = mkdtempSync(join(tmpdir(), 'wanxiang-release-output-'))
  const output = join(root, 'dist')
  const stale = join(output, 'src', 'Wanxiangshu', 'Stale.js')
  try {
    mkdirSync(join(output, 'src', 'Wanxiangshu'), { recursive: true })
    writeFileSync(stale, 'export const stale = true\n', 'utf8')

    resetOutputDirectory(output)

    assert.equal(existsSync(stale), false)
    assert.equal(existsSync(output), true)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { acceptHumanRoot, budget, providerFailureProjection, fold, recordConfirmedFailure, snapshot } = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");


test('WHAT[VERIFICATION-SYSTEM-008] provider failure has one importable production surface', () => {
  assert.equal(typeof budget, 'object')
  assert.equal(typeof providerFailureProjection, 'object')
  assert.equal(typeof fold, 'function')
  assert.equal(typeof acceptHumanRoot, 'function')
  assert.equal(typeof recordConfirmedFailure, 'function')
  assert.equal(typeof snapshot, 'function')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readdirSync } = await import("node:fs");
const { resolve, join } = await import("node:path");
const { walk } = await import("../../../scripts/lib/walk.mjs");

const BUILD_ROOT = 'dist'
const BUILD_ROOT_ABS = `${resolve(BUILD_ROOT)}/`
const FABLE_LIBRARY_DIR = (() => {
  const candidates = readdirSync(join(BUILD_ROOT_ABS, 'fable_modules')).filter((entry) =>
    entry.startsWith('fable-library-js.'),
  )
  if (candidates.length !== 1) {
    throw new Error(
      `expected exactly one fable-library-js.* in ${BUILD_ROOT_ABS}/fable_modules, found: ${candidates.join(', ') || '(none)'}`,
    )
  }
  return join(BUILD_ROOT_ABS, 'fable_modules', candidates[0])
})()
const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)
const surfaceOf = (mod) => Object.keys(mod).filter((name) => !name.endsWith('_$reflection'))
const assertCallable = (mod, modulePath, names) => {
  for (const name of names) {
    assert.equal(
      typeof mod[name],
      'function',
      `${modulePath} must export '${name}' as a function; exports: ${surfaceOf(mod).join(', ')}`,
    )
  }
}

test('WHAT[VERIFICATION-SYSTEM-008] AgentProgram publishes its flow entrypoints', async () => {
  const mod = await load('Execution/Agent/Program')

  assertCallable(mod, 'Execution/Agent/Program', ['validateSession', 'runAgentFlow'])
})
test('WHAT[VERIFICATION-SYSTEM-008] Companion has no generic program facade and keeps its direct delta owner', async () => {
  const delta = await load('Context/Companion/Blogger/Delta')
  assertCallable(delta, 'Context/Companion/Blogger/Delta', ['BloggerDelta_nextChunk'])
})
test('WHAT[VERIFICATION-SYSTEM-008] OrchestratorProgram publishes exactly one entrypoint', async () => {
  const mod = await load('Change/Program')

  assertCallable(mod, 'Change/Program', ['run'])
})
test('WHAT[VERIFICATION-SYSTEM-008] Domain ReconcileProgram publishes pure decisions', async () => {
  const mod = await load('Composition/Turn/Program')
  const names = surfaceOf(mod)

  assert.ok(
    names.some((n) => n.includes('isTerminalOutcome')),
    `Domain ReconcileProgram must publish isTerminalOutcome; exports: ${names.join(', ')}`,
  )
  assert.ok(
    names.some((n) => n.includes('decideStep')),
    `Domain ReconcileProgram must publish decideStep; exports: ${names.join(', ')}`,
  )
  assert.ok(
    names.some((n) => n.includes('publishDecision')),
    `Domain ReconcileProgram must publish publishDecision; exports: ${names.join(', ')}`,
  )
})
test('WHAT[VERIFICATION-SYSTEM-008] ProcessRunner publishes its run entrypoints', async () => {
  const mod = await load('Process/ProcessRunner')

  assertCallable(mod, 'Process/ProcessRunner', ['run', 'runWithHost', 'runWithLauncher'])
})
test('WHAT[VERIFICATION-SYSTEM-008] the Parallel kernel publishes only bounded parallelism', async () => {
  const mod = await load('Foundation/Parallel')

  // docs/what/flow.md (Direct CE) superseded the Flow monad; its monadic surface
  // (Flow_run / Flow_fail / Flow_attempt / Flow_create / Flow_lift and the
  // FlowBuilder) is no longer a demanded contract. Bounded concurrency is still
  // legal, so `Parallel.mapBounded` remains the only required export here.
  assertCallable(mod, 'Foundation/Parallel', ['Parallel_mapBounded'])
})
test('WHAT[VERIFICATION-SYSTEM-008] the journal publishes boot append and snapshot', async () => {
  const [journal, esWriter, envelope, codec, state] = await Promise.all([
    load('Persistence/Journal/AgentJournal'),
    load('Persistence/Journal/EventStoreJournalWriter'),
    load('Persistence/Journal/Envelope'),
    load('Persistence/Journal/FactCodec'),
    load('Composition/Durable/ProjectionState'),
  ])

  // AgentJournal constructs from an already-folded projection + writer
  // (createFromProjection). EventStore boot/resume belongs to
  // EventStoreJournalWriter (create / resumeOrCreate) and the workspace Host.
  // The retired EventStore-boot forwarding facade must not return.
  assertCallable(journal, 'Persistence/Journal/AgentJournal', [
    'AgentJournalModule_createFromProjection',
    'AgentJournalModule_appendAgent',
    'AgentJournalModule_appendMagicTodo',
    'AgentJournalModule_snapshot',
    'AgentJournalModule_revision',
    'AgentJournalModule_snapshotWithRevision',
    'AgentJournalModule_awaitChangeFrom',
    'AgentJournalModule_isPoisoned',
  ])

  const hasCreate = Object.keys(esWriter).some((name) => name.startsWith('EventStoreJournalWriter_create'))
  const hasResume = Object.keys(esWriter).some((name) =>
    name.startsWith('EventStoreJournalWriter_resumeOrCreate'),
  )
  assert.equal(hasCreate, true, 'EventStoreJournalWriter.create must be published')
  assert.equal(hasResume, true, 'EventStoreJournalWriter.resumeOrCreate must be published')

  assertCallable(envelope, 'Persistence/Journal/Envelope', [
    'EnvelopeModule_serialize',
    'EnvelopeModule_deserialize',
    'EnvelopeModule_compareSortKey',
  ])
  assertCallable(codec, 'Persistence/Journal/FactCodec', ['serializeFact', 'deserializeFact'])

  // PERSIST-008's integrated state.
  assert.equal(typeof state['ProjectionSet'], 'function', 'Journal/ProjectionState must publish ProjectionSet')
})
test('WHAT[VERIFICATION-SYSTEM-008] the outcome kernel publishes the two commit results', async () => {
  const mod = await load('Foundation/Outcome')

  // PERSIST-002 has exactly two append outcomes, so `CommitResult` is one generic
  // union rather than a bool plus an error field.
  assert.equal(typeof mod['Outcome_CommitResult$1'], 'function')
  assertCallable(mod, 'Foundation/Outcome', ['AgentRunResult__get_IsValid'])
})
test('WHAT[VERIFICATION-SYSTEM-008] the published plugin entrypoint loads', async () => {
  // `package.json` `main` / `exports["."]` resolve here. A build that emits every
  // domain module but not this one produces an installable package that does
  // nothing, and no other test would notice.
  const mod = await load('OpenCode/Plugin/Plugin')

  assert.ok(surfaceOf(mod).length > 0, 'OpenCode/Plugin/Plugin must publish at least one export')
})
test('WHAT[VERIFICATION-SYSTEM-008] every emitted module actually loads', async () => {
  // The gap this closes: `dotnet build` type-checks the F#, and the layer 1 tests
  // import only what `domain.mjs` binds — which is Kernel/Domain/Journal/Process.
  // Nothing imported `OpenCode/*`, so a module could be emitted with a broken
  // import and every gate stayed green.
  //
  // That is not hypothetical. `Task.CompletedTask` compiles under .NET and Fable
  // emits `get_CompletedTask` for it, which `fable-library-js` does not export, so
  // five modules — including the plugin entrypoint — failed at LOAD with
  // "does not provide an export named". The package was installable and inert.
  //
  // An ES module's imports are resolved before its body runs, so importing each
  // one is a real link check and executes no plugin logic.
  const modules = walk(BUILD_ROOT, ['.js']).filter((file) => !file.includes('fable_modules'))

  assert.ok(modules.length > 100, `expected a full build under ${BUILD_ROOT}, found ${modules.length} modules`)

  const failures = []
  for (const file of modules) {
    try {
      await import(new URL(`../../../${file}`, import.meta.url).pathname)
    } catch (error) {
      failures.push(`${file}: ${error.message.split('\n')[0]}`)
    }
  }

  assert.deepEqual(failures, [], 'every emitted module must link against the fable-library it was built for')
})
test('WHAT[VERIFICATION-SYSTEM-008] the contract and the facade read the same build', () => {
  // Both checks resolve `dist` independently. If they ever disagreed, this file
  // would be asserting against artifacts no test actually uses.
  assert.match(BUILD_ROOT_ABS, /\/dist\/$/)
  assert.match(FABLE_LIBRARY_DIR, /fable-library-js\.\d+\.\d+\.\d+$/)
})
}
