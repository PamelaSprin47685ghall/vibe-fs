import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import path from 'node:path'
import test from 'node:test'

{
const { existsSync, mkdtempSync, mkdirSync, rmSync, writeFileSync } = await import('node:fs')
const { tmpdir } = await import('node:os')
const { join } = await import('node:path')
const { assertBuildFresh, collectOutputs, readManifest, writeManifest, MANIFEST_SCHEMA } = await import('../../../scripts/lib/build-state.mjs')
const { resetOutputDirectory } = await import('../../../scripts/lib/owner-compile.mjs')
const { runBuild } = await import('../../../scripts/build.mjs')

test('WHAT[distribution-005] assertBuildFresh succeeds on current repository build', () => {
  const freshness = assertBuildFresh({ root: process.cwd() })
  assert.ok(freshness.generation >= 1)
  assert.ok(typeof freshness.compilerInputDigest === 'string')
  assert.ok(typeof freshness.generatedInputDigest === 'string')
  assert.ok(typeof freshness.artifactInputDigest === 'string')
})

test('WHAT[distribution-005] no-op mode preserves manifest and generation', async () => {
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

test('WHAT[distribution-005] manifest corruption causes assertBuildFresh to throw with code', () => {
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

test('WHAT[distribution-005] missing or stale output entry causes assertBuildFresh to throw', () => {
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

test('WHAT[distribution-005] release output reset physically removes stale artifacts', () => {
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
import { fileURLToPath, pathToFileURL } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

const packageResourcesUrl = pathToFileURL(
  path.join(root, 'dist/Resources/PackageResources.js'),
).href

const entryUrl = pathToFileURL(
  path.join(root, 'dist/OpenCode/Plugin/Plugin.js'),
).href

const RESOURCE_SAMPLES = [
  'provider/role/manager/en.md',
  'provider/role/manager/zh-CN.md',
  'provider/world/common-law/en.md',
  'enforcer/primitive-obsession/enforcer.md',
  'enforcer/primitive-obsession/main.md',
]

test('WHAT[distribution-005] repository layout has one resources directory beside dist without a dist/resources copy', () => {
  // PackageResources 的 ../../../resources 必须恰好是仓库/安装根的 resources/。
  // 只核对目录布局；实际定位行为由002验证。
  const moduleDir = path.dirname(fileURLToPath(packageResourcesUrl))
  const resolvedResources = path.resolve(moduleDir, '../..', 'resources')
  const expected = path.join(root, 'resources')
  assert.equal(path.normalize(resolvedResources), path.normalize(expected))

  for (const relative of RESOURCE_SAMPLES) {
    const full = path.join(resolvedResources, relative)
    assert.ok(existsSync(full), `expected fixed path must exist: ${full}`)
    assert.ok(readFileSync(full, 'utf8').trim().length > 0, `expected fixed path non-empty: ${full}`)
  }

  // distribution-005：资源单份发布——resources/ 只存在于包根，不得复制进 dist/ 形成双副本。
  assert.equal(
    readdirSync(path.join(root, 'dist')).includes('resources'),
    false,
    'an exact lowercase resources entry must not be duplicated into dist/ (single-copy publish)',
  )
})

test.todo('WHAT[distribution-005] GAP-210: clean and incremental production outputs have identical bytes for identical inputs')
