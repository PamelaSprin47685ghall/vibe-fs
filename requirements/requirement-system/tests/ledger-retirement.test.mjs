import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { analyzeRetirement } from '../../../scripts/checks/ledger-retirement-gate.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const GATE = join(ROOT, 'scripts/checks/ledger-retirement-gate.mjs')

function spawnGate() {
  try {
    return { code: 0, out: execFileSync('node', [GATE], { encoding: 'utf8' }) }
  } catch (e) {
    return { code: e.status ?? 1, out: String(e.stdout ?? '') + String(e.stderr ?? '') }
  }
}

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_001 retired ledger paths stay absent from the working tree', () => {
  for (const p of [
    'scripts/checks/migration-ledger.json',
    'scripts/checks/migration-ledger.mjs',
    'requirements/migration-ledger',
  ]) {
    assert.equal(existsSync(join(ROOT, p)), false, `${p} must not exist after retirement`)
  }
})

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_002 check entry no longer wires the retired gate and the retirement gate is green on the real tree', () => {
  const checkSource = readFileSync(join(ROOT, 'scripts/check.mjs'), 'utf8')
  assert.equal(checkSource.includes('migration-ledger'), false, 'check.mjs must not reference the retired gate')

  const result = spawnGate()
  assert.equal(result.code, 0, `ledger-retirement-gate must pass on the real tree: ${result.out}`)
  assert.match(result.out, /stays retired/)
})

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_003 formal surfaces carry no retired-ledger references and 019 stays retired', () => {
  const what = readFileSync(join(ROOT, 'requirements/requirement-system/WHAT.md'), 'utf8')
  assert.doesNotMatch(what, /##\s+REQUIREMENT-SYSTEM-019/, '019 must not be re-declared')
  assert.match(what, /##\s+REQUIREMENT-SYSTEM-020/, '020 must own the retirement law')
  for (const doc of [
    'requirements/INDEX.md',
    'requirements/README.md',
    'requirements/verification-system/WHAT.md',
    'requirements/host-boundary/WHAT.md',
  ]) {
    const text = readFileSync(join(ROOT, doc), 'utf8')
    assert.equal(text.includes('migration-ledger'), false, `${doc} must not reference the retired ledger`)
  }
})

// ── Analyzer branch coverage: every rejection branch driven via injected
// facts, no tree access. Each fixture breaks exactly one branch. ──

const CLEAN = { retiredPathsExisting: [], checkWiring: false, offendingReferences: [], whatIds: ['REQUIREMENT-SYSTEM-020'] }

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_004 analyzer accepts a fully clean fact set', () => {
  assert.deepEqual(analyzeRetirement(CLEAN), [])
})

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_005 analyzer rejects a resurrected ledger path', () => {
  const errors = analyzeRetirement({ ...CLEAN, retiredPathsExisting: ['scripts/checks/migration-ledger.json'] })
  assert.equal(errors.length, 1)
  assert.match(errors[0], /retired path reappeared: scripts\/checks\/migration-ledger\.json/)
})

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_006 analyzer rejects retired-gate wiring in the check entry', () => {
  const errors = analyzeRetirement({ ...CLEAN, checkWiring: true })
  assert.equal(errors.length, 1)
  assert.match(errors[0], /check\.mjs still references/)
})

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_007 analyzer rejects formal-surface references one per site', () => {
  const errors = analyzeRetirement({
    ...CLEAN,
    offendingReferences: ['requirements/other-package/HOW.md', 'src/Wanxiangshu/Ghost.fs'],
  })
  assert.equal(errors.length, 2)
  assert.match(errors[0], /formal surface references retired ledger: requirements\/other-package\/HOW\.md/)
  assert.match(errors[1], /formal surface references retired ledger: src\/Wanxiangshu\/Ghost\.fs/)
})

test('WHAT[REQUIREMENT-SYSTEM-020] RETIRE_008 analyzer rejects re-declaring the retired 019 number', () => {
  const errors = analyzeRetirement({ ...CLEAN, whatIds: ['REQUIREMENT-SYSTEM-019', 'REQUIREMENT-SYSTEM-020'] })
  assert.equal(errors.length, 1)
  assert.match(errors[0], /REQUIREMENT-SYSTEM-019 is retired and must not be re-declared/)
})
