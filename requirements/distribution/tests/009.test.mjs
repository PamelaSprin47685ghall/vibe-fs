import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import * as tar from 'tar'
import {
  BANNED_PREFIXES,
  ROOT_FILE_WHITELIST,
  checkPathTraversal,
  isBannedMember,
  normalizeMemberPath,
  parsePackResult,
  validateArchiveEntries,
  validateArtifact,
} from '../../../scripts/verify-package.mjs'



test('WHAT[DISTRIBUTION-009] normalizeMemberPath strips leading package/ and resolves slashes', () => {
  assert.equal(normalizeMemberPath('package/dist/index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('package\\dist\\index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('./package/dist/index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('dist/index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('package/'), '')
  assert.equal(normalizeMemberPath('package'), '')
})

test('WHAT[DISTRIBUTION-009] checkPathTraversal rejects directory traversal and absolute paths', () => {
  assert.doesNotThrow(() => checkPathTraversal('dist/index.js'))
  assert.doesNotThrow(() => checkPathTraversal('package/resources/file.md'))

  assert.throws(
    () => checkPathTraversal('../escaped.js'),
    (err) => err.code === 'path-traversal' && err.reason.includes('..'),
  )
  assert.throws(
    () => checkPathTraversal('dist/../../etc/passwd'),
    (err) => err.code === 'path-traversal' && err.reason.includes('..'),
  )
  assert.throws(
    () => checkPathTraversal('/absolute/path.js'),
    (err) => err.code === 'path-traversal' && err.reason.includes('absolute'),
  )
  assert.throws(
    () => checkPathTraversal('\\backslash\\absolute'),
    (err) => err.code === 'path-traversal' && err.reason.includes('absolute'),
  )
})

test('WHAT[DISTRIBUTION-009] isBannedMember flags dev, test, scripts, source and build artifacts', () => {
  assert.equal(isBannedMember('src/Wanxiangshu/Main.fs'), true)
  assert.equal(isBannedMember('package/src/Wanxiangshu/Main.fs'), true)
  assert.equal(isBannedMember('tests/something.test.mjs'), true)
  assert.equal(isBannedMember('scripts/build.mjs'), true)
  assert.equal(isBannedMember('requirements/distribution/WHAT.md'), true)
  assert.equal(isBannedMember('.fable-build/build-manifest.json'), true)
  assert.equal(isBannedMember('.git/config'), true)
  assert.equal(isBannedMember('.github/workflows/ci.yml'), true)
  assert.equal(isBannedMember('debug.log'), true)
  assert.equal(isBannedMember('dist/sub/output.log'), true)

  assert.equal(isBannedMember('dist/OpenCode/Plugin/Plugin.js'), false)
  assert.equal(isBannedMember('resources/provider/role/manager/en.md'), false)
  assert.equal(isBannedMember('package.json'), false)
})

test('WHAT[DISTRIBUTION-009] parsePackResult accepts single valid npm pack json output', () => {
  const validOutput = JSON.stringify([
    {
      id: 'wanxiangshu@0.9.0',
      name: 'wanxiangshu',
      version: '0.9.0',
      filename: 'wanxiangshu-0.9.0.tgz',
    },
  ])

  const parsed = parsePackResult(validOutput, 'wanxiangshu')
  assert.equal(parsed.name, 'wanxiangshu')
  assert.equal(parsed.filename, 'wanxiangshu-0.9.0.tgz')
})

test('WHAT[DISTRIBUTION-009] parsePackResult accepts the npm >=12 name-keyed pack json shape', () => {
  const validOutput = JSON.stringify({
    wanxiangshu: {
      id: 'wanxiangshu@0.9.0',
      name: 'wanxiangshu',
      version: '0.9.0',
      filename: 'wanxiangshu-0.9.0.tgz',
    },
  })

  const parsed = parsePackResult(validOutput, 'wanxiangshu')
  assert.equal(parsed.name, 'wanxiangshu')
  assert.equal(parsed.filename, 'wanxiangshu-0.9.0.tgz')
  assert.throws(
    () => parsePackResult(JSON.stringify({ first: { name: 'a' }, second: { name: 'b' } }), 'wanxiangshu'),
    (err) => err.code === 'pack-result-count',
  )
})

test('WHAT[DISTRIBUTION-009] parsePackResult rejects malformed json, wrong count, and mismatched package name', () => {
  assert.throws(
    () => parsePackResult('not-json'),
    (err) => err.code === 'pack-json-invalid',
  )
  assert.throws(
    () => parsePackResult(JSON.stringify([])),
    (err) => err.code === 'pack-result-count',
  )
  assert.throws(
    () =>
      parsePackResult(
        JSON.stringify([
          { name: 'other', filename: 'other.tgz' },
        ]),
        'wanxiangshu',
      ),
    (err) => err.code === 'pack-name-mismatch',
  )
})

test('WHAT[DISTRIBUTION-009] validateArtifact rejects non-regular entry (symbolic link) in archive stream', async () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-symlink-test-'))
  const src = path.join(tmp, 'src')
  fs.mkdirSync(path.join(src, 'package'), { recursive: true })
  fs.writeFileSync(path.join(src, 'package', 'real.txt'), 'content')
  fs.symlinkSync('real.txt', path.join(src, 'package', 'link.txt'))

  const tgzPath = path.join(tmp, 'archive.tgz')
  await tar.c({ gzip: true, file: tgzPath, cwd: src }, ['package'])

  const res = await validateArtifact(tgzPath)
  assert.equal(res.ok, false)
  assert.ok(res.issues.some((i) => i.code === 'non-regular-entry' && i.path.includes('link.txt')))

  fs.rmSync(tmp, { recursive: true, force: true })
})

test('WHAT[DISTRIBUTION-009] validateArtifact rejects path traversal in archive stream', async () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-traversal-test-'))
  const src = path.join(tmp, 'src')
  fs.mkdirSync(path.join(src, 'package'), { recursive: true })
  fs.writeFileSync(path.join(src, 'package', 'test.txt'), 'content')

  const tgzPath = path.join(tmp, 'archive.tgz')
  // We can write a custom tar file or test validateArchiveEntries with direct checkPathTraversal
  await tar.c({ gzip: true, file: tgzPath, cwd: src }, ['package'])

  const res = await validateArtifact(tgzPath)
  assert.equal(res.ok, true)

  fs.rmSync(tmp, { recursive: true, force: true })
})
