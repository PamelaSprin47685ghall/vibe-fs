// requirements/distribution/tests/pack-artifact.test.mjs
// Unit tests for packaging artifact member validation and pack output parsing.
// Covers pure helpers exported from scripts/verify-package.mjs.
// Does NOT invoke npm pack or tar (small fast unit tests).

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  BANNED_PREFIXES,
  REQUIRED_MEMBERS,
  assertDuplicateMembers,
  checkPathTraversal,
  isBannedMember,
  normalizeMemberPath,
  parsePackResult,
  validateMemberList,
} from '../../../scripts/verify-package.mjs'

test('normalizeMemberPath strips leading package/ and resolves slashes', () => {
  assert.equal(normalizeMemberPath('package/dist/index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('package\\dist\\index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('./package/dist/index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('dist/index.js'), 'dist/index.js')
  assert.equal(normalizeMemberPath('package/'), '')
  assert.equal(normalizeMemberPath('package'), '')
})

test('checkPathTraversal rejects directory traversal and absolute paths', () => {
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

test('isBannedMember flags dev, test, scripts, source and build artifacts', () => {
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

test('assertDuplicateMembers detects duplicate normalized entries', () => {
  const unique = ['dist/a.js', 'dist/b.js', 'resources/c.md']
  assert.doesNotThrow(() => assertDuplicateMembers(unique))

  const withDup = ['package/dist/a.js', 'dist/a.js']
  assert.throws(
    () => assertDuplicateMembers(withDup),
    (err) => err.code === 'duplicate-member' && err.path === 'dist/a.js',
  )
})

test('validateMemberList accepts valid production artifact manifest', () => {
  const validManifest = [
    'package.json',
    'README.md',
    'LICENSE',
    'dist/OpenCode/Plugin/Plugin.js',
    'dist/Sphinx/ServeEntry.js',
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
    'resources/provider/role/manager/en.md',
    'resources/provider/role/manager/zh-CN.md',
    'resources/provider/world/common-law/en.md',
    'resources/provider/world/common-law/zh-CN.md',
  ]

  assert.equal(validateMemberList(validManifest), true)

  // Accepts objects with path property (npm pack --json structure)
  const objectManifest = validManifest.map((p) => ({ path: p }))
  assert.equal(validateMemberList(objectManifest), true)
})

test('validateMemberList rejects infiltrated source, test, or scripts paths', () => {
  const base = [
    'package.json',
    'README.md',
    'LICENSE',
    'dist/OpenCode/Plugin/Plugin.js',
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
    'resources/provider/role/manager/en.md',
    'resources/provider/role/manager/zh-CN.md',
    'resources/provider/world/common-law/en.md',
    'resources/provider/world/common-law/zh-CN.md',
  ]

  for (const banned of ['src/App.fs', 'tests/test.mjs', 'scripts/build.mjs', 'requirements/WHAT.md', '.fable-build/manifest.json', 'run.log']) {
    assert.throws(
      () => validateMemberList([...base, banned]),
      (err) => err.code === 'infiltrated-member',
      `should reject banned member ${banned}`,
    )
  }
})

test('validateMemberList rejects unauthorized members outside dist, resources, or root files', () => {
  const base = [
    'package.json',
    'README.md',
    'LICENSE',
    'dist/OpenCode/Plugin/Plugin.js',
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
    'resources/provider/role/manager/en.md',
    'resources/provider/role/manager/zh-CN.md',
    'resources/provider/world/common-law/en.md',
    'resources/provider/world/common-law/zh-CN.md',
  ]

  assert.throws(
    () => validateMemberList([...base, 'unknown-dir/extra.txt']),
    (err) => err.code === 'unauthorized-member' && err.path === 'unknown-dir/extra.txt',
  )
})

test('validateMemberList rejects when required members are missing', () => {
  const missingPlugin = [
    'package.json',
    'README.md',
    'LICENSE',
    // 'dist/OpenCode/Plugin/Plugin.js' missing
    'resources/enforcer/primitive-obsession/enforcer.md',
    'resources/enforcer/primitive-obsession/main.md',
    'resources/provider/role/manager/en.md',
    'resources/provider/role/manager/zh-CN.md',
    'resources/provider/world/common-law/en.md',
    'resources/provider/world/common-law/zh-CN.md',
  ]

  assert.throws(
    () => validateMemberList(missingPlugin),
    (err) => err.code === 'missing-required-member' && err.path === 'dist/OpenCode/Plugin/Plugin.js',
  )
})

test('parsePackResult parses valid single-entry npm pack output', () => {
  const validJson = JSON.stringify([
    {
      id: 'wanxiangshu@0.9.0',
      name: 'wanxiangshu',
      version: '0.9.0',
      filename: 'wanxiangshu-0.9.0.tgz',
      files: [{ path: 'package.json' }],
    },
  ])

  const parsed = parsePackResult(validJson, 'wanxiangshu')
  assert.equal(parsed.name, 'wanxiangshu')
  assert.equal(parsed.filename, 'wanxiangshu-0.9.0.tgz')
})

test('parsePackResult rejects invalid JSON, empty array, or multiple entries', () => {
  assert.throws(
    () => parsePackResult('not json at all'),
    (err) => err.code === 'pack-json-invalid',
  )

  assert.throws(
    () => parsePackResult('[]'),
    (err) => err.code === 'pack-result-count',
  )

  assert.throws(
    () => parsePackResult('[{}, {}]'),
    (err) => err.code === 'pack-result-count',
  )
})

test('parsePackResult rejects package name mismatch or missing filename', () => {
  const wrongName = JSON.stringify([{ name: 'other-pkg', filename: 'other.tgz' }])
  assert.throws(
    () => parsePackResult(wrongName, 'wanxiangshu'),
    (err) => err.code === 'pack-name-mismatch',
  )

  const missingFilename = JSON.stringify([{ name: 'wanxiangshu' }])
  assert.throws(
    () => parsePackResult(missingFilename, 'wanxiangshu'),
    (err) => err.code === 'pack-filename-missing',
  )
})
