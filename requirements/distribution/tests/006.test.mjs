import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { spawnSync } = await import("node:child_process");
const { existsSync, readFileSync, readdirSync } = await import("node:fs");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath, pathToFileURL } = await import("node:url");

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

test('WHAT[DISTRIBUTION-006] DISTRIBUTION_resource_missing_fails_fast_no_fallback', async () => {
  // 资源缺失必须抛错终止（package resource missing: <full>），不得 fallback、不得静默降级；
  // rulebook 元数据不以 catalog.json 为第二真源（目录即清单）。
  const { readText } = await import(packageResourcesUrl)
  assert.throws(
    () => readText('enforcer/does-not-exist-rule/enforcer.md'),
    (err) => {
      const message = String(err?.message ?? err)
      assert.match(message, /package resource missing/)
      assert.match(message, /does-not-exist-rule/)
      return true
    },
    'missing resource must throw package resource missing, never fall back',
  )
  assert.equal(
    existsSync(path.join(root, 'resources', 'catalog.json')),
    false,
    'catalog.json must not exist — directory is the single source of truth for the rulebook',
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: crypto } = await import("node:crypto");
const { default: fs } = await import("node:fs");
const { default: os } = await import("node:os");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");
const { REPO_ROOT, deriveExpectedClosure, validateArchiveEntries, validateArtifact } = await import("../../../scripts/verify-package.mjs");

const root = REPO_ROOT
const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
const exists = (relative) => fs.existsSync(path.join(root, relative))
const normalize = (entry) => String(entry).replace(/\\/g, '/').replace(/\/+$/, '')
const walkFs = (dir) => {
  const out = []
  if (!fs.existsSync(dir)) return out
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) out.push(...walkFs(full))
    else if (entry.isFile() && entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}

test('WHAT[DISTRIBUTION-006] DISTRIBUTION_resource_io_lives_only_under_infrastructure_resources', () => {
  const resourcesDir = path.join(root, 'src', 'Wanxiangshu', 'Resources')
  const productionFiles = walkFs(path.join(root, 'src', 'Wanxiangshu'))
  const offenders = productionFiles.filter(
    (f) =>
      !f.startsWith(resourcesDir) &&
      f.endsWith('.fs') &&
      /PackageResources\./.test(fs.readFileSync(f, 'utf8')),
  )
  assert.deepEqual(
    offenders,
    [],
    `PackageResources. references outside ${path.relative(root, resourcesDir)}: ${offenders.join(', ')}`,
  )
})
}
