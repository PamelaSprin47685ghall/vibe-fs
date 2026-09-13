import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { loopDetectorRepositoryInputFiles } from '../../../scripts/lib/loop-detector-repository-corpus.mjs'
import {
  assertBuildFresh,
  collectCompilerInputs,
  collectGeneratedInputs,
  collectArtifactInputs,
  collectOutputs,
  computeDigest,
  readManifest,
  writeManifest,
  invalidateManifest,
  MANIFEST_SCHEMA,
} from '../../../scripts/lib/build-state.mjs'
import { planImpactCompile, resetOutputDirectory } from '../../../scripts/lib/owner-compile.mjs'
import { runBuild } from '../../../scripts/build.mjs'
import { assertProductionSourcesAssigned } from '../../../scripts/lib/compile-shards.mjs'

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
