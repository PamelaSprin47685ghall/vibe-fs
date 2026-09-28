import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
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

test('WHAT[requirement-grounding-002] self-subtree declarations are configuration errors even when they do not match WHAT.md', { todo: 'GAP-085: rule validation currently probes only the WHAT.md path' }, () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha', '/requirements/alpha/tests/**\n')
    assert.throws(() => grounding.discoverPackages(dir), /must not declare package self coverage/)
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
