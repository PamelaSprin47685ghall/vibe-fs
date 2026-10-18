import assert from 'node:assert/strict'
import crypto from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  ROOT_FILE_WHITELIST,
  ROOT_OPTIONAL_FILES,
  ROOT_REQUIRED_FILES,
  REPO_ROOT,
  classifyMember,
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

test('WHAT[distribution-007] release orchestration schedules integration, E2E and the package verifier in order', async () => {
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

test('WHAT[distribution-007] P1-P4: validateArchiveEntries rejects missing, extra, digest-mismatch, and link entries', async () => {
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

test('WHAT[distribution-007] the archive validator accepts the complete declared byte closure', async () => {
  const tar = await import('tar')
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-package-valid-'))
  try {
    fs.mkdirSync(path.join(dir, 'package/dist'), { recursive: true })
    fs.mkdirSync(path.join(dir, 'package/resources'), { recursive: true })
    const content = new Map([
      ['package.json', '{"name":"wanxiangshu"}'],
      ['dist/entry.js', 'export default {}\n'],
      ['resources/example.md', '规则\n'],
    ])
    const expected = new Map()
    for (const [name, text] of content) {
      const bytes = Buffer.from(text)
      fs.writeFileSync(path.join(dir, 'package', name), bytes)
      expected.set(name, { sha256: crypto.createHash('sha256').update(bytes).digest('hex'), size: bytes.length })
    }
    const archive = path.join(dir, 'valid.tgz')
    await tar.c({ gzip: true, file: archive, cwd: dir }, ['package'])
    assert.deepEqual((await validateArchiveEntries(archive, expected)).issues, [])
  } finally { fs.rmSync(dir, { recursive: true, force: true }) }
})

test.todo('WHAT[distribution-007] GAP-210: full release executes clean build, complete tests and real pack/extract/isolated consume on one generation')

test('WHAT[distribution-007] the explicit member contract classifies root metadata, payload, bundles and violations', () => {
  // The boundary is the contract's, stated in WHAT[003]/[004]; the checker must
  // implement exactly these three member families and nothing else.
  assert.deepEqual(ROOT_REQUIRED_FILES, ['package.json'])
  assert.deepEqual(ROOT_OPTIONAL_FILES, ['README.md', 'LICENSE'])
  assert.deepEqual(ROOT_FILE_WHITELIST, [...ROOT_REQUIRED_FILES, ...ROOT_OPTIONAL_FILES])

  assert.equal(classifyMember('package.json').kind, 'root-required')
  assert.equal(classifyMember('README.md').kind, 'root-optional')
  assert.equal(classifyMember('LICENSE').kind, 'root-optional')
  assert.equal(classifyMember('node_modules/wanxiangli/dist/lifecycle/Work.js').kind, 'bundle')

  // A non-contract package-root member is rejected by the contract itself, not
  // only because some closure happened to forget it.
  assert.equal(classifyMember('SECURITY.md').code, 'non-contract-root-member')
  assert.equal(classifyMember('CHANGELOG.md').code, 'non-contract-root-member')
  assert.equal(classifyMember('SECURITY.md').allowed, false)

  const real = deriveExpectedClosure()
  for (const required of ROOT_REQUIRED_FILES) {
    assert.ok(real.has(required), `required root member absent from the declared closure: ${required}`)
  }
  for (const optional of ROOT_OPTIONAL_FILES) {
    assert.equal(
      real.has(optional),
      exists(optional),
      `optional root member ${optional} must be in the closure exactly when the source tree has it`,
    )
  }
})

test('WHAT[distribution-007] a contract violation archive goes red on non-contract root members, banned prefixes and payload extras, and the same closure stays green without them', async () => {
  const tar = await import('tar')
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-member-contract-'))
  const pkgRoot = path.join(dir, 'package')
  try {
    const payload = new Map([
      ['package.json', '{"name":"wanxiangshu"}'],
      ['README.md', '# Wanxiangshu'],
      ['dist/entry.js', 'export default {}\n'],
    ])
    const violations = new Map([
      ['SECURITY.md', '# security policy'],
      ['src/App.fs', '// source leak'],
      ['artifacts/rogue.js', 'export default 1'],
    ])

    const expected = new Map()
    for (const [name, text] of payload) {
      const bytes = Buffer.from(text)
      expected.set(name, {
        sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
        size: bytes.length,
      })
    }

    const writeArchive = async (archiveName, include) => {
      for (const [name, text] of [...payload, ...violations]) {
        if (!include(name)) continue
        fs.mkdirSync(path.dirname(path.join(pkgRoot, name)), { recursive: true })
        fs.writeFileSync(path.join(pkgRoot, name), text)
      }
      const archive = path.join(dir, archiveName)
      await tar.c({ gzip: true, file: archive, cwd: dir }, ['package'])
      return archive
    }

    // Clean control first: the same payload closure without any contract violation.
    fs.rmSync(pkgRoot, { recursive: true, force: true })
    const cleanArchive = await writeArchive('clean.tgz', (name) => payload.has(name))
    const clean = await validateArchiveEntries(cleanArchive, expected)
    assert.deepEqual(clean.issues, [], 'contract-conformant archive must stay green')

    // Violation archive: every violation must be reported by the contract.
    fs.rmSync(pkgRoot, { recursive: true, force: true })
    const badArchive = await writeArchive('violations.tgz', () => true)
    const bad = await validateArchiveEntries(badArchive, expected)
    const codesByPath = new Map()
    for (const issue of bad.issues) {
      if (!codesByPath.has(issue.path)) codesByPath.set(issue.path, new Set())
      codesByPath.get(issue.path).add(issue.code)
    }

    assert.ok(
      codesByPath.get('SECURITY.md')?.has('non-contract-root-member'),
      `non-contract root member must be rejected by name: ${JSON.stringify(bad.issues)}`,
    )
    assert.ok(
      codesByPath.get('src/App.fs')?.has('infiltrated-member'),
      'banned prefix must be rejected with infiltrated-member',
    )
    assert.ok(
      codesByPath.get('artifacts/rogue.js')?.has('extra-member'),
      'a non-payload member outside the closure must be rejected as extra-member',
    )
    for (const name of payload.keys()) {
      assert.equal(codesByPath.has(name), false, `contract member must not be rejected: ${name}`)
    }
  } finally {
    fs.rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[distribution-007] a contract-required root member missing from the archive is a missing-member failure', async () => {
  const tar = await import('tar')
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-required-member-'))
  try {
    fs.mkdirSync(path.join(dir, 'package/dist'), { recursive: true })
    const content = new Map([
      ['README.md', '# Wanxiangshu'],
      ['dist/entry.js', 'export default {}\n'],
    ])
    const expected = new Map()
    for (const [name, text] of content) {
      const bytes = Buffer.from(text)
      fs.writeFileSync(path.join(dir, 'package', name), bytes)
      expected.set(name, {
        sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
        size: bytes.length,
      })
    }
    expected.set('package.json', {
      sha256: crypto.createHash('sha256').update('{"name":"wanxiangshu"}').digest('hex'),
      size: 20,
    })

    const archive = path.join(dir, 'missing-manifest.tgz')
    await tar.c({ gzip: true, file: archive, cwd: dir }, ['package'])

    const { issues } = await validateArchiveEntries(archive, expected)
    assert.deepEqual(
      issues.filter((issue) => issue.path === 'package.json').map((issue) => issue.code),
      ['missing-member'],
      'the required root member must be reported when the archive lacks it',
    )
  } finally {
    fs.rmSync(dir, { recursive: true, force: true })
  }
})
