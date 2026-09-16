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

test('WHAT[REQUIREMENT-GROUNDING-001] discovers requirement packages from the current workspace without a Wanxiangshu package list', () => {
  const { dir, cleanup } = sandbox()
  try {
    pkg(dir, 'zeta')
    pkg(dir, 'alpha')
    mkdirSync(join(dir, 'requirements', 'not-a-package'), { recursive: true })
    assert.deepEqual(grounding.discoverPackages(dir), ['alpha', 'zeta'])
  } finally { cleanup() }
})
