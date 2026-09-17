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

test('WHAT[REQUIREMENT-GROUNDING-004] returns every overlapping package in deterministic package-name order', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'zeta', '/src/shared/**\n')
    pkg(dir, 'alpha', '/src/shared/**\n')
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', 'shared', 'x.fs')), ['alpha', 'zeta'])
  } finally { cleanup() }
})
