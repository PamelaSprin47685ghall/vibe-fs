import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import path from 'node:path'
import test from 'node:test'
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

test('WHAT[distribution-005] DISTRIBUTION_lookup_is_single_fixed_relative_path_not_candidate_search', () => {
  // PackageResources 的 ../../../resources 必须恰好是仓库/安装根的 resources/。
  // 这是「单份发布、无 dist 双副本、无 fallback」的实现证据（docs/why/enforcer.md）。
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
