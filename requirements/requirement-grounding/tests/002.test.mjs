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

test('WHAT[REQUIREMENT-GROUNDING-002] treats a package own requirements subtree as implicit coverage that APPLIES-TO cannot cancel', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha')
    assert.deepEqual(
      grounding.resolvePackages(dir, join(dir, 'requirements', 'alpha', 'WHAT.md')),
      ['alpha'],
    )
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-002] resolves nonexistent paths through a symlinked workspace without allowing symlink escape', () => {
  const { dir, cleanup } = sandbox()
  try {
    const real = join(dir, 'real')
    const alias = join(dir, 'alias')
    mkdirSync(real)
    symlinkSync(real, alias, 'dir')
    pkg(alias, 'alpha', '/src/**\n')

    assert.deepEqual(grounding.resolvePackages(alias, join(alias, 'src', 'future.fs')), ['alpha'])
    assert.deepEqual(grounding.resolvePackages(alias, join(dir, 'outside.fs')), [])
  } finally { cleanup() }
})
