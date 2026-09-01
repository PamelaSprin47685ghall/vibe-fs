import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'
import test from 'node:test'

const ROOT = resolve(import.meta.dirname, '../../../..')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')

function compile(project) {
  const outDir = mkdtempSync(join(tmpdir(), 'wanxiangshu-owner-boundary-'))
  try {
    return spawnSync(
      'dotnet',
      ['tool', 'run', 'fable', '--', join(FIXTURE, project), '-o', outDir, '--noGitignore'],
      { cwd: ROOT, encoding: 'utf8' },
    )
  } finally {
    rmSync(outDir, { recursive: true, force: true })
  }
}

test('WHAT[STRUCTURED-WORKFLOW-011] independent Fable checks enforce compile-input locality boundaries', () => {
  const green = compile('GreenConsumer.fsproj')
  assert.equal(green.status, 0, `public contract must compile\n${green.stdout}\n${green.stderr}`)

  const red = compile('RedConsumer.fsproj')
  assert.notEqual(red.status, 0, 'runtime symbol without a ProjectReference must be a compiler error')
  assert.match(`${red.stdout}\n${red.stderr}`, /Runtime|secretValue|not defined/i)

  const merged = compile('MergedConsumer.fsproj')
  assert.equal(
    merged.status,
    0,
    `Fable ProjectReference source-merging canary: internal is not an assembly firewall\n${merged.stdout}\n${merged.stderr}`,
  )

  const transitiveLeak = compile('LeakyConsumer.fsproj')
  assert.equal(
    transitiveLeak.status,
    0,
    `Fable transitively source-merges ProjectReference closure even when DisableTransitiveProjectReferences=true\n${transitiveLeak.stdout}\n${transitiveLeak.stderr}`,
  )

  const privateModuleLeak = compile('PrivateConsumer.fsproj')
  assert.equal(
    privateModuleLeak.status,
    0,
    `Fable source-merging canary: top-level private module is not a foreign-owner firewall\n${privateModuleLeak.stdout}\n${privateModuleLeak.stderr}`,
  )

  const privateBinding = compile('PrivateBindingConsumer.fsproj')
  assert.notEqual(privateBinding.status, 0, 'module-local private binding must stay inaccessible after Fable source merge')
  assert.match(`${privateBinding.stdout}\n${privateBinding.stderr}`, /privateValue|not accessible|not defined|private/i)

  const signedGreen = compile('SignedGreenConsumer.fsproj')
  assert.equal(signedGreen.status, 0, `F# signature must expose declared contract\n${signedGreen.stdout}\n${signedGreen.stderr}`)

  const signedRed = compile('SignedRedConsumer.fsproj')
  assert.notEqual(signedRed.status, 0, 'F# signature must hide implementation symbols from source-merged consumers')
  assert.match(`${signedRed.stdout}\n${signedRed.stderr}`, /hiddenValue|not defined|not accessible/i)

  const signatureOnly = compile('SignatureOnlyGreenConsumer.fsproj')
  assert.notEqual(signatureOnly.status, 0, 'Fable does not materialize a consumable module from a signature-only project')
  assert.match(`${signatureOnly.stdout}\n${signatureOnly.stderr}`, /SignedProvider|not defined/i)
})
