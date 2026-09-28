import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { E2E_ROOT_REL, SOLE_ENTRY, e2eTestCaseFiles } from './e2e/support/watchdog-feed-scan.mjs'

test('WHAT[verification-system-002] the registered Long Stroke entry exists', () => {
  assert.deepEqual(e2eTestCaseFiles().map((file) => file.split('/').at(-1)), [SOLE_ENTRY])
})

test('WHAT[verification-system-002] a missing sole Long Stroke entry fails closed', () => {
  const root = mkdtempSync(join(tmpdir(), 'long-stroke-entry-'))
  try {
    const directory = join(root, E2E_ROOT_REL)
    mkdirSync(directory, { recursive: true })
    writeFileSync(join(directory, 'other.test.mjs'), '// unrelated test\n')
    assert.throws(() => e2eTestCaseFiles(root), /missing sole top-level e2e entry/)
    writeFileSync(join(directory, SOLE_ENTRY), '// registered entry\n')
    assert.deepEqual(e2eTestCaseFiles(root), [join(directory, SOLE_ENTRY)])
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
