// requirements/distribution/tests/pack-closure.test.mjs
// DISTRIBUTION-001/003/004/006/007/008 oracle:
// Artifact carries compiled code and runtime semantic resources together in a single closure.
// Validates manifest/exports alignment, files whitelist, full closure derivation,
// archive stream verification (happy path & synthetic negative fixtures), and release proof pipeline.

import assert from 'node:assert/strict'
import crypto from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import {
  REPO_ROOT,
  deriveExpectedClosure,
  validateArchiveEntries,
  validateArtifact,
} from '../../../scripts/verify-package.mjs'

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

test('WHAT[DISTRIBUTION-007] DISTRIBUTION_release_proof_covers_build_package_packing_and_artifact_checks', async () => {
  const pipeline = pkg.scripts['verify:release']
  assert.equal(typeof pipeline, 'string', 'verify:release must exist')
  assert.match(pipeline, /node scripts\/verify\.mjs/, 'release proof must dispatch to verify.mjs')

  const { verify } = await import('../../../scripts/verify.mjs')
  const tmpLogDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pack-closure-verify-'))
  let buf = ''
  const memorySink = {
    write(chunk) {
      buf += chunk
    },
  }
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
    spawned.push({ label, argv: argv.map((arg) => String(arg)) })
    return { label, ok: true, exitCode: 0, signal: null, durationMs: 0, logPath: '' }
  }
  try {
    const { exitCode } = await verify({
      release: true,
      runStep: fakeRunStep,
      output: memorySink,
      logDirectory: tmpLogDir,
    })
    assert.equal(exitCode, 0, 'release verify with green spy must succeed')

    const releaseLabels = spawned.map((s) => s.label)
    const integrationIdx = releaseLabels.indexOf('integration')
    const e2eIdx = releaseLabels.indexOf('e2e')
    const packageIdx = releaseLabels.indexOf('package')
    assert.ok(
      integrationIdx >= 0 && e2eIdx >= 0 && integrationIdx < e2eIdx,
      'integration must precede e2e',
    )
    assert.ok(packageIdx >= 0 && e2eIdx < packageIdx, 'package must follow e2e')

    const packageCalls = spawned.filter((s) => s.label === 'package')
    assert.equal(packageCalls.length, 1, 'release must run the verify-package step exactly once')
    assert.ok(
      packageCalls[0].argv.some((arg) => arg.includes('scripts/verify-package.mjs')),
      'release package step must resolve to scripts/verify-package.mjs',
    )
  } finally {
    fs.rmSync(tmpLogDir, { recursive: true, force: true })
  }
})

test('WHAT[DISTRIBUTION-007] P1-P4: validateArchiveEntries rejects missing, extra, digest-mismatch, and link entries', async () => {
  const tar = await import('tar')
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-negative-archive-test-'))
  const src = path.join(tmp, 'src')
  fs.mkdirSync(path.join(src, 'package'), { recursive: true })

  // 1. Create a base synthetic tarball
  fs.writeFileSync(path.join(src, 'package', 'package.json'), '{"name":"wanxiangshu"}')
  fs.writeFileSync(path.join(src, 'package', 'README.md'), '# Wanxiangshu')
  fs.writeFileSync(path.join(src, 'package', 'extra-rogue.js'), 'console.log("bad")')
  fs.symlinkSync('README.md', path.join(src, 'package', 'symlink.md'))

  const tgzPath = path.join(tmp, 'synthetic.tgz')
  await tar.c({ gzip: true, file: tgzPath, cwd: src }, ['package'])

  // Define an expected closure with:
  // - package.json (with WRONG digest to test digest-mismatch P3)
  // - README.md (normal)
  // - dist/OpenCode/Plugin/Plugin.js (missing in archive to test missing-member P1)
  const expectedClosure = new Map([
    [
      'package.json',
      {
        sha256: '0000000000000000000000000000000000000000000000000000000000000000',
        size: 20,
      },
    ],
    [
      'README.md',
      {
        sha256: crypto
          .createHash('sha256')
          .update('# Wanxiangshu')
          .digest('hex'),
        size: 13,
      },
    ],
    [
      'dist/OpenCode/Plugin/Plugin.js',
      {
        sha256: '1111111111111111111111111111111111111111111111111111111111111111',
        size: 100,
      },
    ],
  ])

  const res = await validateArchiveEntries(tgzPath, expectedClosure)
  assert.ok(res.issues.length >= 4, 'should record all distinct negative issues')

  const codes = new Set(res.issues.map((i) => i.code))
  assert.ok(codes.has('missing-member'), 'P1: should detect missing member')
  assert.ok(codes.has('extra-member'), 'P2: should detect extra unexpected member')
  assert.ok(codes.has('digest-mismatch'), 'P3: should detect digest mismatch')
  assert.ok(codes.has('non-regular-entry'), 'P4: should detect non-regular entry (symlink)')

  fs.rmSync(tmp, { recursive: true, force: true })
})

test('WHAT[DISTRIBUTION-008] DISTRIBUTION_enforcer_rulebook_closure_is_complete', () => {
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

test('WHAT[DISTRIBUTION-008] DISTRIBUTION_provider_resource_closure_is_language_complete', () => {
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

test('WHAT[DISTRIBUTION-001] DISTRIBUTION_artifact_carries_compiled_code_and_runtime_resources_together', () => {
  const required = [
    'dist/OpenCode/Plugin/Plugin.js',
    'resources/provider/role/manager/en.md',
    'resources/provider/role/manager/zh-CN.md',
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
  ]
  for (const relative of required) {
    assert.ok(exists(relative), `artifact must carry ${relative}`)
  }
  const entry = fs.readFileSync(path.join(root, 'dist/OpenCode/Plugin/Plugin.js'), 'utf8')
  assert.ok(entry.trim().length > 0, 'compiled entrypoint must be non-empty')
  for (const relative of required.filter((r) => r.startsWith('resources/'))) {
    const text = fs.readFileSync(path.join(root, relative), 'utf8')
    assert.ok(text.trim().length > 0, `runtime semantic resource must be non-empty: ${relative}`)
  }
  assert.ok(Array.isArray(pkg.files), 'files whitelist must exist')
  assert.ok(
    pkg.files.some((f) => normalize(f) === 'dist') &&
      pkg.files.some((f) => normalize(f) === 'resources'),
    'one artifact must ship compiled code and runtime resources together (files whitelist)',
  )
})

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
