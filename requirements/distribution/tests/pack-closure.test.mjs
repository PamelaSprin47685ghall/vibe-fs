// requirements/distribution/tests/pack-closure.test.mjs
// DISTRIBUTION-003/004/008 oracle：manifest/exports ↔ shipped paths 一致、
// files whitelist 排除开发/测试/legacy authority、所有声明 runtime resource 的
// semantic packages 的资源在 shipped closure 中完整可得。
//
// 不 spawn `npm pack`（与 tests/integration/package/* 头注释同一设计决定）；真实
// tarball membership 由 release proof L5 `npm pack --dry-run` 承担（见 PROOF.md）。
// 本测试读仓库内 package.json + dist/ + resources/ 的实际路径，作为 pack 的静态前置。
// 只 import：node: 内置。

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
const exists = (relative) => fs.existsSync(path.join(root, relative))

const normalize = (entry) => String(entry).replace(/\\/g, '/').replace(/\/+$/, '')

test('DISTRIBUTION_manifest_entry_matches_exports_and_shipped_path', () => {
  assert.equal(typeof pkg.main, 'string', 'main must be declared')
  assert.equal(pkg.exports['.'], pkg.main, 'exports["."] must equal main')
  assert.match(pkg.main, /^\.?\/?dist\//, 'main must live under dist/')
  assert.ok(exists(pkg.main), `main must exist on disk: ${pkg.main}`)
})

test('DISTRIBUTION_files_whitelist_is_explicit_and_excludes_dev_test_legacy', () => {
  assert.ok(Array.isArray(pkg.files), 'package.json files whitelist must exist')
  assert.ok(
    pkg.files.some((f) => normalize(f) === 'dist'),
    'files whitelist must include dist/ (compiled runtime code)',
  )
  assert.ok(
    pkg.files.some((f) => normalize(f) === 'resources'),
    'files whitelist must include resources/ (runtime semantic resources)',
  )
  for (const entry of pkg.files) {
    const normalized = normalize(entry)
    for (const banned of ['src', 'tests', 'scripts', 'docs', 'artifacts', 'spec']) {
      assert.ok(
        normalized !== banned && !normalized.startsWith(`${banned}/`),
        `files whitelist must not ship ${banned}*, found ${entry}`,
      )
    }
    assert.ok(
      !normalized.endsWith('.fs') && !normalized.endsWith('.fsproj'),
      `files whitelist must not ship F# sources, found ${entry}`,
    )
  }
})

test('DISTRIBUTION_release_proof_covers_build_package_packing_and_artifact_checks', () => {
  // DISTRIBUTION-007 本地 pin：release proof（format-build-test）必须包含 build/package/
  // packing 与 install/import/resource availability 检查。阶梯的层序治理（谁先谁后、
  // watchdog、晋级纪律）归 verification-system；本断言只锁「release proof 覆盖 closure」。
  const pipeline = pkg.scripts['format-build-test']
  assert.equal(typeof pipeline, 'string', 'format-build-test must exist')
  assert.match(pipeline, /node scripts\/build\.mjs/, 'release proof must build')
  assert.match(pipeline, /node tests\/integration\/package\/run\.mjs/, 'release proof must run package install/import/resources checks')
  assert.match(pipeline, /npm pack --dry-run$/, 'release proof must end with npm pack --dry-run (packing membership)')
})

test('DISTRIBUTION_enforcer_rulebook_closure_is_complete', () => {
  // resources/enforcer/<TipName>/{enforcer.md,main.md} → behavior-diagnosis 声明的资源。
  // 闭包规则：目录枚举到的每个 tip 双文件都必须存在（不是硬编码名单）。
  const enforcerRoot = path.join(root, 'resources', 'enforcer')
  const tipDirs = fs
    .readdirSync(enforcerRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  assert.ok(tipDirs.length >= 1, 'rulebook must contain at least one tip directory')
  for (const tip of tipDirs) {
    assert.ok(exists(`resources/enforcer/${tip}/enforcer.md`), `missing enforcer.md for ${tip}`)
    assert.ok(exists(`resources/enforcer/${tip}/main.md`), `missing main.md for ${tip}`)
  }
})

test('DISTRIBUTION_provider_resource_closure_is_language_complete', () => {
  // resources/provider/** → 各 provider 语义包（office-capability / provider-language /
  // cognitive-environment / action-affordance / delegation / …）声明的资源。
  // 闭包规则：role 每角色双语、world/library 共享资产双语。
  const roleRoot = path.join(root, 'resources', 'provider', 'role')
  const roles = fs
    .readdirSync(roleRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  assert.ok(roles.length >= 1, 'provider role tree must contain at least one role')
  for (const role of roles) {
    assert.ok(exists(`resources/provider/role/${role}/en.md`), `missing Role Law ${role}/en.md`)
    assert.ok(
      exists(`resources/provider/role/${role}/zh-CN.md`),
      `missing Role Law ${role}/zh-CN.md`,
    )
  }
  for (const leaf of ['world/common-law', 'library/ingress', 'library/closing']) {
    assert.ok(exists(`resources/provider/${leaf}/en.md`), `missing provider asset ${leaf}/en.md`)
    assert.ok(
      exists(`resources/provider/${leaf}/zh-CN.md`),
      `missing provider asset ${leaf}/zh-CN.md`,
    )
  }
})
