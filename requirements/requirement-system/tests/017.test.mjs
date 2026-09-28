import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { inspectRequirements } from './support/structure.mjs'

test('WHAT[requirement-system-017] the executable verifier scans the complete requirements tree', () => {
  const problems = inspectRequirements(fileURLToPath(new URL('../../..', import.meta.url)))
  assert.deepEqual(problems, [], problems.join('\n'))
})

test('WHAT[requirement-system-017] a deleted clause reports its surviving test, including nested files', () => {
  const root = mkdtempSync(join(tmpdir(), 'spec-tree-'))
  try {
    const pkg = join(root, 'requirements/sample')
    mkdirSync(join(pkg, 'tests/nested'), { recursive: true })
    writeFileSync(join(root, 'requirements/INDEX.md'), '| `sample` | example |\n')
    writeFileSync(join(pkg, 'WHY.md'), '# Reasons\n')
    writeFileSync(join(pkg, 'WHAT.md'), '## [001] Rule\n')
    writeFileSync(join(pkg, 'tests/nested/001.test.mjs'), 'test("WHAT[sample-001] example", () => {})\n')
    assert.deepEqual(inspectRequirements(root), [])
    writeFileSync(join(pkg, 'WHAT.md'), '# No surviving clauses\n')
    const problems = inspectRequirements(root)
    assert.equal(problems.length, 1)
    assert.match(problems[0], /nested\/001.test.mjs: clause 001 is absent; notify the user/)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
