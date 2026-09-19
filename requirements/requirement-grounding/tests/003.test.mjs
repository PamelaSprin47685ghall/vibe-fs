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

test('WHAT[requirement-grounding-003] evaluates APPLIES-TO as ordered positive wildmatch includes with bang exclusions', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'alpha', '/src/**\n!/src/generated/**\n/src/generated/keep.fs\n')
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', 'main.fs')), ['alpha'])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', 'generated', 'drop.fs')), [])
    assert.deepEqual(grounding.resolvePackages(dir, join(dir, 'src', 'generated', 'keep.fs')), ['alpha'])
  } finally { cleanup() }
})
