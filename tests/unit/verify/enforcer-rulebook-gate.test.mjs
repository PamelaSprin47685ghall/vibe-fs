// Structural enforcer-rulebook-gate (folder SSOT).
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  scanRulebook,
  scanRepoRulebook,
  EXPECTED_RULE_COUNT,
} from '../../../scripts/checks/enforcer-rulebook-gate.mjs'

test('enforcer_rulebook_gate_repo_is_green', () => {
  const result = scanRepoRulebook()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.count, EXPECTED_RULE_COUNT)
})

test('enforcer_rulebook_gate_rejects_third_file_and_catalog', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-rulebook-gate-'))
  try {
    const tip = join(root, 'sample-tip')
    mkdirSync(tip)
    writeFileSync(join(tip, 'enforcer.md'), '# sample-tip — Enforcer\n\nbody\n', 'utf8')
    writeFileSync(join(tip, 'main.md'), '# sample-tip — Main\n\nbody\n', 'utf8')
    writeFileSync(join(tip, 'extra.txt'), 'nope\n', 'utf8')
    writeFileSync(join(root, 'catalog.json'), '{}\n', 'utf8')

    const result = scanRulebook(root, { expectedCount: 1 })
    assert.equal(result.ok, false)
    const codes = result.violations.map((v) => v.code)
    assert.ok(codes.includes('extra-entry'), codes.join(','))
    assert.ok(codes.includes('catalog-json-forbidden'), codes.join(','))
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
