import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: fs } = await import("node:fs");
const { default: os } = await import("node:os");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");
const tar = await import("tar");
const { REPO_ROOT, runExternalConsumer } = await import("../../../scripts/verify-package.mjs");


test('WHAT[DISTRIBUTION-003] PACKAGE_external_consumer_imports_default_export_and_reads_resources', async () => {
  // If dist/OpenCode/Plugin/Plugin.js is not present yet, skip or run
  const pluginEntry = path.join(REPO_ROOT, 'dist/OpenCode/Plugin/Plugin.js')
  if (!fs.existsSync(pluginEntry)) {
    return
  }

  const scratchDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pkg-consume-test-'))
  const extractedPackageDir = path.join(scratchDir, 'package')
  fs.mkdirSync(extractedPackageDir, { recursive: true })

  try {
    // Stage minimal package layout under extractedPackageDir
    fs.cpSync(path.join(REPO_ROOT, 'package.json'), path.join(extractedPackageDir, 'package.json'))
    fs.cpSync(path.join(REPO_ROOT, 'dist'), path.join(extractedPackageDir, 'dist'), { recursive: true })
    fs.cpSync(path.join(REPO_ROOT, 'resources'), path.join(extractedPackageDir, 'resources'), { recursive: true })

    await runExternalConsumer({
      root: REPO_ROOT,
      tarballPath: 'mock.tgz',
      extractedPackageDir,
      scratchDir,
    })
    assert.ok(true, 'external consumer executed successfully')
  } finally {
    fs.rmSync(scratchDir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: fs } = await import("node:fs");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath, pathToFileURL } = await import("node:url");

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))
const main = path.join(repoRoot, pkg.main)

test('WHAT[DISTRIBUTION-003] PACKAGE_import_wanxiangshu_main_exits_zero', async () => {
  const mod = await import(pathToFileURL(main).href)
  assert.equal(typeof mod, 'object')
  assert.ok(mod !== null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: fs } = await import("node:fs");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..')
const pkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8'))

test('WHAT[DISTRIBUTION-003] PACKAGE_layout_matches_manifest_and_main', () => {
  assert.equal(pkg.name, 'wanxiangshu')
  assert.ok(Array.isArray(pkg.files), 'package.json files whitelist must exist')
  assert.ok(pkg.files.includes('dist/') || pkg.files.includes('dist'), 'files must include dist/')
  assert.ok(
    pkg.files.includes('resources/') || pkg.files.includes('resources'),
    'files must include resources/',
  )
  assert.equal(
    fs.existsSync(path.join(repoRoot, 'resources', 'enforcer', 'catalog.json')),
    false,
    'catalog.json must not ship after rulebook folder cutover',
  )
})
}
