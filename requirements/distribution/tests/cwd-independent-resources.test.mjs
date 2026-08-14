// requirements/distribution/tests/cwd-independent-resources.test.mjs
// DISTRIBUTION-002 oracle：runtime resource lookup 必须独立于 caller cwd。
//
// 生产实现是 fixed package-relative lookup（import.meta.url → ../../../resources），
// 见 src/Wanxiangshu/Resources/PackageResources.fs。无 cwd walk、
// 无 candidate search、无 dist/src fallback。
//
// 本测试不 spawn `npm pack`（与 tests/integration/package/* 头注释同一设计决定）；
// 真实 tarball membership 由 release proof L5 `npm pack --dry-run` 承担。
// 只 import：node: 内置 + dist/（已构建且新鲜）。

import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
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

// 每个语义包声明的 runtime resource 的代表样本：
// resources/provider/** → provider 语义包；resources/enforcer/** → behavior-diagnosis。
const RESOURCE_SAMPLES = [
  'provider/role/manager/en.md',
  'provider/role/manager/zh-CN.md',
  'provider/world/common-law/en.md',
  'enforcer/primitive-obsession/enforcer.md',
  'enforcer/primitive-obsession/main.md',
]

test('DISTRIBUTION_resource_reads_resolve_under_package_root_regardless_of_cwd', async () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const { readText } = await import(packageResourcesUrl)
    for (const relative of RESOURCE_SAMPLES) {
      const text = readText(relative)
      assert.ok(
        text.trim().length > 0,
        `${relative} must be readable while process.cwd() is outside the package`,
      )
    }
  } finally {
    process.chdir(previous)
  }
})

test('DISTRIBUTION_fresh_process_with_foreign_cwd_imports_entry_and_reads_resources', () => {
  // 干净子进程 + cwd=/：既不能靠 cwd 找到 resources，也不能靠源码树。唯一能成功
  // 的路径是包内 fixed-relative lookup（import.meta.url → ../../../resources）。
  const script = `
    import { readText } from ${JSON.stringify(packageResourcesUrl)};
    const text = readText('provider/role/manager/en.md');
    if (!text.includes('Manager')) process.exit(2);
    if (!readText('enforcer/primitive-obsession/enforcer.md').trim()) process.exit(3);
    await import(${JSON.stringify(entryUrl)});
    console.log('ok');
  `
  const result = spawnSync(process.execPath, ['--input-type=module', '--eval', script], {
    cwd: '/',
    encoding: 'utf8',
    timeout: 30_000,
  })
  assert.equal(result.status, 0, `child exited ${result.status}\n${result.stderr}`)
  assert.match(result.stdout, /ok/)
})

test('DISTRIBUTION_lookup_is_single_fixed_relative_path_not_candidate_search', () => {
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
})
