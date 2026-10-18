import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as grounding from '../../../dist/Requirement/Grounding/Surface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-scope-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const pkg = (root, name, applies = null) => {
  const dir = join(root, 'requirements', name)
  mkdirSync(dir, { recursive: true })
  writeFileSync(join(dir, 'WHAT.md'), `# ${name}\n`, 'utf8')
  if (applies !== null) writeFileSync(join(dir, 'APPLIES-TO'), applies, 'utf8')
}

test('WHAT[requirement-grounding-002] treats a package own requirements subtree as implicit coverage that APPLIES-TO cannot cancel', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha')
    assert.deepEqual(
      grounding.resolvePackages(dir, join(dir, 'requirements', 'alpha', 'WHAT.md')),
      ['alpha'],
    )
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'requirements', 'alpha', 'tests', 'future.test.mjs')), ['alpha'])
    for (const rule of ['/requirements/alpha/**', '!/requirements/alpha/**']) {
      writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), rule)
      assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/)
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] rejects every self-subtree declaration before any future descendant exists', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha')
    assert.equal(existsSync(join(dir, 'requirements', 'alpha', 'tests')), false)
    const rules = [
      '/requirements/alpha/tests/**',
      '/requirements/alpha',
      '/requirements/**/future?.fs',
      '/requirements/[a-c]lpha/tests/**',
      '/requirements/alph?/tests/**',
      '/requirements/alpha/tests/[!x].fs',
      '/requirements/alpha/tests/[]a].fs',
      '/requirements/alpha/[]]',
      '/requirements/alpha/**/future.fs',
      '/requirements/al**/a/**',
      '/requirements/alpha/tests/雪😀.fs',
      '**/tests/**',
      '**/*.fs',
      '*.fs',
      'future.fs',
    ]
    for (const rule of rules) {
      for (const prefix of ['', '!']) {
        writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), `${prefix}${rule}\n`)
        assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/, `${prefix}${rule}`)
      }
    }
    writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'),
      '/src/**\n/requirements/alpha/tests/**\n!/requirements/alpha/tests/**\n')
    assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/,
      'a later exclusion cannot cancel an illegal declaration')
    assert.equal(existsSync(join(dir, 'requirements', 'alpha', 'tests')), false)
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] keeps disjoint package declarations and external recursive patterns', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha')
    const cases = [
      ['/requirements/alpha-old/**', 'requirements/alpha-old/future.fs'],
      ['/requirements/selfalpha/**', 'requirements/selfalpha/future.fs'],
      ['/requirements/[b-z]lpha/**', 'requirements/blpha/future.fs'],
      ['/requirements/zz**/a/**', 'requirements/zz/a/future.fs'],
      ['/requirements/beta/**', 'requirements/beta/future.fs'],
      ['/src/**', 'src/future.fs'],
    ]
    for (const [rule, target] of cases) {
      writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), `# package-external scope\n${rule}\n`)
      assert.deepEqual(grounding.discoverPackages(dir), ['alpha'], rule)
      assert.deepEqual(grounding.resolvePackages(dir, join(dir, target)), ['alpha'], rule)
      writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), `!${rule}\n`)
      assert.deepEqual(grounding.discoverPackages(dir), ['alpha'], `!${rule}`)
      assert.deepEqual(grounding.resolvePackages(dir, join(dir, target)), [], `!${rule}`)
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] uses the existing UTF16 glob dialect for package names', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, '😀')
    const mapping = join(dir, 'requirements', '😀', 'APPLIES-TO')
    for (const rule of ['/requirements/?', '/requirements/[😀]']) {
      writeFileSync(mapping, rule)
      assert.deepEqual(grounding.discoverPackages(dir), ['😀'], rule)
    }
    for (const rule of ['/requirements/??', '/requirements/[😀][😀]', '/requirements/😀/tests/**']) {
      writeFileSync(mapping, rule)
      assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/, rule)
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] excludes impossible NUL and backslash-only matches from normalized path scope', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha')
    for (const rule of ['/requirements/alpha/[\\]', '/requirements/alpha/[\0]']) {
      writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), rule)
      assert.deepEqual(grounding.discoverPackages(dir), ['alpha'], rule)
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] preserves recursive-directory separators and the existing line-end dialect', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'x\n', '/requirements/**/[!a]')
    assert.deepEqual(grounding.discoverPackages(dir), ['x\n'])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'requirements', 'z')), ['x\n'])
    for (const rule of ['/requirements/***/[!a]', '/requirements/*/[!a]', '/requirements/x?']) {
      writeFileSync(join(dir, 'requirements', 'x\n', 'APPLIES-TO'), rule)
      assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/, rule)
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] keeps a leading closing bracket inside its glob character class', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha', '/src/[]|]')
    assert.deepEqual(grounding.discoverPackages(dir), ['alpha'])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', ']')), ['alpha'])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', '|')), ['alpha'])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', 'a')), [])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'other', 'future]')), [],
      'class members cannot escape into an unanchored regular-expression alternative')
    for (const rule of ['/requirements/alpha/[]|a]', '!/requirements/alpha/[]|a]']) {
      writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), rule)
      assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/, rule)
    }
  } finally { cleanup() }
})

test('WHAT[requirement-grounding-002] resolves nonexistent paths through a symlinked workspace without allowing symlink escape', () => {
  const { dir, cleanup } = sandbox()
  try {
    const real = join(dir, 'real')
    const alias = join(dir, 'alias')
    mkdirSync(real)
    symlinkSync(real, alias, 'dir')
    pkg(alias, 'alpha', '/src/**\n')

    assert.deepEqual(grounding.resolvePackages(alias, join(alias, 'src', 'future.fs')), ['alpha'])
    assert.deepEqual(grounding.resolvePackages(alias, join(dir, 'outside.fs')), [])
    const outside = join(dir, 'outside')
    mkdirSync(outside)
    mkdirSync(join(real, 'src'))
    symlinkSync(outside, join(real, 'src', 'escape'), 'dir')
    assert.deepEqual(grounding.resolvePackages(alias, join(alias, 'src', 'escape', 'missing.fs')), [])
    symlinkSync(join(dir, 'missing-target'), join(real, 'src', 'broken'), 'dir')
    assert.deepEqual(grounding.resolvePackages(alias, join(alias, 'src', 'broken', 'missing.fs')), [])
    assert.deepEqual(grounding.resolvePackages(alias, join(alias, 'src', 'new-directory', 'future.fs')), ['alpha'])
  } finally { cleanup() }
})
