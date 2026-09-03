import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { spawn } from 'node:child_process'
import test from 'node:test'
import { compileOwnerProject } from '../../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../../..')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')

function compile(project) {
  const outDir = mkdtempSync(join(tmpdir(), 'wanxiangshu-owner-boundary-'))

  return new Promise((resolveResult, reject) => {
    const child = spawn(
      'dotnet',
      ['tool', 'run', 'fable', '--', join(FIXTURE, project), '-o', outDir, '--noGitignore'],
      { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] },
    )
    let stdout = ''
    let stderr = ''
    child.stdout.setEncoding('utf8')
    child.stderr.setEncoding('utf8')
    child.stdout.on('data', (chunk) => { stdout += chunk })
    child.stderr.on('data', (chunk) => { stderr += chunk })
    child.once('error', reject)
    child.once('close', (status, signal) => {
      rmSync(outDir, { recursive: true, force: true })
      resolveResult({ status, signal, stdout, stderr })
    })
  })
}

test('WHAT[STRUCTURED-WORKFLOW-011] independent Fable checks enforce compile-input locality boundaries', async () => {
  const [
    green,
    red,
    merged,
    transitiveLeak,
    privateModuleLeak,
    privateBinding,
    signedGreen,
    signedRed,
    signatureOnly,
  ] = await Promise.all([
    'GreenConsumer.fsproj',
    'RedConsumer.fsproj',
    'MergedConsumer.fsproj',
    'LeakyConsumer.fsproj',
    'PrivateConsumer.fsproj',
    'PrivateBindingConsumer.fsproj',
    'SignedGreenConsumer.fsproj',
    'SignedRedConsumer.fsproj',
    'SignatureOnlyGreenConsumer.fsproj',
  ].map(compile))
  assert.equal(green.status, 0, `public contract must compile\n${green.stdout}\n${green.stderr}`)

  assert.notEqual(red.status, 0, 'runtime symbol without a ProjectReference must be a compiler error')
  assert.match(`${red.stdout}\n${red.stderr}`, /Runtime|secretValue|not defined/i)

  assert.equal(
    merged.status,
    0,
    `Fable ProjectReference source-merging canary: internal is not an assembly firewall\n${merged.stdout}\n${merged.stderr}`,
  )

  assert.equal(
    transitiveLeak.status,
    0,
    `Fable transitively source-merges ProjectReference closure even when DisableTransitiveProjectReferences=true\n${transitiveLeak.stdout}\n${transitiveLeak.stderr}`,
  )

  assert.equal(
    privateModuleLeak.status,
    0,
    `Fable source-merging canary: top-level private module is not a foreign-owner firewall\n${privateModuleLeak.stdout}\n${privateModuleLeak.stderr}`,
  )

  assert.notEqual(privateBinding.status, 0, 'module-local private binding must stay inaccessible after Fable source merge')
  assert.match(`${privateBinding.stdout}\n${privateBinding.stderr}`, /privateValue|not accessible|not defined|private/i)

  assert.equal(signedGreen.status, 0, `F# signature must expose declared contract\n${signedGreen.stdout}\n${signedGreen.stderr}`)

  assert.notEqual(signedRed.status, 0, 'F# signature must hide implementation symbols from source-merged consumers')
  assert.match(`${signedRed.stdout}\n${signedRed.stderr}`, /hiddenValue|not defined|not accessible/i)

  assert.notEqual(signatureOnly.status, 0, 'Fable does not materialize a consumable module from a signature-only project')
  assert.match(`${signatureOnly.stdout}\n${signatureOnly.stderr}`, /SignedProvider|not defined/i)
})

test('WHAT[STRUCTURED-WORKFLOW-011] flat closure compilation compiles transitive closure green and keeps unreferenced sources red', async () => {
  const emitterPath = join(FIXTURE, 'Emitter.fsproj')
  const scratchRoot = mkdtempSync(join(tmpdir(), 'wanxiangshu-owner-flat-compile-'))
  const rootPropsPath = join(ROOT, 'Directory.Build.props')

  try {
    // 1. LeakyConsumer projected closure compiles GREEN
    const greenResult = await compileOwnerProject({
      projectPath: join(FIXTURE, 'LeakyConsumer.fsproj'),
      aggregatePath: emitterPath,
      scratchRoot,
      rootPropsPath,
      stdio: 'pipe',
    })
    assert.equal(greenResult.ok, true, `LeakyConsumer flat closure must compile GREEN\n${greenResult.stdout}\n${greenResult.stderr}`)
    assert.equal(greenResult.code, 0)

    // 2. RedConsumer stays RED although Runtime.fs exists in Emitter.fsproj (because Runtime.fsproj is outside its closure)
    const redResult = await compileOwnerProject({
      projectPath: join(FIXTURE, 'RedConsumer.fsproj'),
      aggregatePath: emitterPath,
      scratchRoot,
      rootPropsPath,
      stdio: 'pipe',
    })
    assert.equal(redResult.ok, false, 'RedConsumer without ProjectReference to Runtime must fail compile')
    assert.notEqual(redResult.code, 0)
    assert.match(`${redResult.stdout}\n${redResult.stderr}`, /Runtime|secretValue|not defined/i)

    // 3. Stale ProjectReference fails before Fable
    const staleFsprojPath = join(scratchRoot, 'StaleConsumer.fsproj')
    writeFileSync(
      staleFsprojPath,
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="NonExistentProvider.fsproj"/>
    <Compile Include="${join(FIXTURE, 'RedConsumer.fs')}"/>
  </ItemGroup>
</Project>`,
      'utf8',
    )

    await assert.rejects(
      async () => {
        await compileOwnerProject({
          projectPath: staleFsprojPath,
          aggregatePath: emitterPath,
          scratchRoot,
          rootPropsPath,
          stdio: 'pipe',
        })
      },
      /Missing ProjectReference.*NonExistentProvider\.fsproj/i,
      'stale ProjectReference must fail before Fable compilation',
    )
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
