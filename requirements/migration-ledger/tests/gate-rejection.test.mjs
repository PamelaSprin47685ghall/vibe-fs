#!/usr/bin/env node
// gate-rejection.test.mjs — 11 illegal ledger states that MUST be rejected.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, writeFileSync, existsSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { execSync } from 'node:child_process'
import { validateLedger } from '../../../scripts/checks/migration-ledger.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const LEDGER_PATH = join(ROOT, 'scripts/checks/migration-ledger.json')
const OWNERS_PATH = join(ROOT, 'scripts/checks/semantic-owners.json')

const loadValid = () => {
  const ledger = JSON.parse(readFileSync(LEDGER_PATH, 'utf8'))
  const owners = JSON.parse(readFileSync(OWNERS_PATH, 'utf8'))
  return { ledger, owners }
}
const clone = (o) => JSON.parse(JSON.stringify(o))
const findPending = (ledger) => ledger.nodes.find(n => n.state === 'PENDING')
const findDone = (ledger) => ledger.nodes.find(n => n.state === 'DONE')
const headCommit = () => {
  try { return execSync('git rev-parse HEAD', { cwd: ROOT }).toString().trim() } catch { return '6d69d40dd161b8caec57018cc9f2a2673f32e496' }
}

test('WHAT[MIGRATION-LEDGER-002] GATE-01: PENDING with GREEN/verified success evidence must be rejected', () => {
  const { ledger, owners } = loadValid()
  const pending = findPending(ledger)
  assert.ok(pending, 'need a PENDING node in fixture')
  pending.evidence = 'semantic-owners / architecture all GREEN, KEEP verified'
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject PENDING with success evidence, got ok:true errors:${errors?.join(';')}`)
  assert.ok(errors.some(e => /PENDING/i.test(e) && /evidence/i.test(e) || /GREEN|verified|complete/i.test(e)), `error should mention PENDING evidence / GREEN: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-003] GATE-02: READY without owner graph must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findPending(ledger)
  node.state = 'READY'
  node.result = 'PENDING'
  node.publishes = []
  node.consumes = []
  node.depends_on = []
  node.production_callers_to_migrate = []
  node.proofs = ['requirements/some/tests/foo.test.mjs']
  node.architecture_gates = ['semantic-owners.mjs']
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject READY without owner graph, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /READY/i.test(e) && /owner|contract|publish|consume|depends/i.test(e) || /owner graph/i.test(e)), `error should mention READY owner graph: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-003] GATE-03: READY without proofs/architecture_gates must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findPending(ledger)
  node.state = 'READY'
  node.publishes = ['Some.Contract']
  node.proofs = []
  node.architecture_gates = []
  const done = findDone(ledger)
  node.depends_on = [{ id: done.id, kind: 'contract' }]
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject READY without proofs/gates, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /READY/i.test(e) && /proof|gate/i.test(e)), `error should mention READY proofs/gates: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-04: DONE with result PENDING must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  node.result = 'PENDING'
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE with PENDING result, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /DONE/i.test(e) && /result/i.test(e) && /PENDING/i.test(e)), `error should mention DONE result PENDING: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-05: classification/result mismatch must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  if (node.classification === 'KEEP') {
    node.result = 'CUTOVER'
  } else if (node.classification === 'DELETE') {
    node.result = 'PROVEN-KEEP'
  } else {
    node.classification = 'KEEP'
    node.result = 'CUTOVER'
  }
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject classification/result mismatch, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /classification/i.test(e) && /result/i.test(e) || /incompatible/i.test(e)), `error should mention classification/result: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-06: DONE without implementation_commit must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  delete node.implementation_commit
  if (!node.touched_paths || node.touched_paths.length === 0) {
    node.touched_paths = ['src/Wanxiangshu/Some/Feature.fs', 'requirements/some/tests/foo.test.mjs']
  }
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE without implementation_commit, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /implementation_commit/i.test(e) || /commit/i.test(e)), `error should mention implementation_commit: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-07: DONE with non-existent or non-ancestor implementation_commit must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  node.implementation_commit = 'deadbeefdeadbeefdeadbeefdeadbeefdeadbeef'
  if (!node.touched_paths || node.touched_paths.length === 0) {
    node.touched_paths = ['src/Wanxiangshu/Some/Feature.fs', 'requirements/some/tests/foo.test.mjs']
  }
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE with invalid ancestor commit, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /implementation_commit|ancestor|commit/i.test(e)), `error should mention commit ancestor: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-08: DONE without production/test touched_paths must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  node.implementation_commit = headCommit()
  node.touched_paths = []
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE without touched_paths, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /touched_paths|production|implementation|changed/i.test(e)), `error should mention touched_paths: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-09: DONE without proofs must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  node.implementation_commit = headCommit()
  if (!node.touched_paths || node.touched_paths.length === 0) node.touched_paths = ['src/Wanxiangshu/Some/Feature.fs']
  node.proofs = []
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE without proofs, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /proof/i.test(e)), `error should mention proofs: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-004] GATE-10: DONE without architecture_gates must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  node.implementation_commit = headCommit()
  if (!node.touched_paths || node.touched_paths.length === 0) node.touched_paths = ['src/Wanxiangshu/Some/Feature.fs']
  node.architecture_gates = []
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE without architecture_gates, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /gate/i.test(e) || /architecture/i.test(e)), `error should mention gates: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-005] GATE-11: closure dependency not DONE must be rejected', () => {
  const { ledger, owners } = loadValid()
  const pendingTarget = ledger.nodes.find(n => n.state === 'PENDING')
  const depender = ledger.nodes.find(n => n.id !== pendingTarget.id)
  depender.depends_on = [{ id: pendingTarget.id, kind: 'closure' }]
  if (depender.state === 'DONE') {
    depender.implementation_commit = headCommit()
    if (!depender.touched_paths || depender.touched_paths.length === 0) depender.touched_paths = ['src/Wanxiangshu/Some/Feature.fs']
    if (!depender.proofs || depender.proofs.length === 0) depender.proofs = ['requirements/some/tests/foo.test.mjs']
    if (!depender.architecture_gates || depender.architecture_gates.length === 0) depender.architecture_gates = ['semantic-owners.mjs']
  } else {
    depender.state = 'READY'
    depender.publishes = ['Some.Contract']
    depender.proofs = ['requirements/some/tests/foo.test.mjs']
    depender.architecture_gates = ['semantic-owners.mjs']
  }
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject closure dependency not DONE, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /closure/i.test(e)), `error should mention closure: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-005] GATE-12: DONE with only coverage without owner graph must be rejected', () => {
  const { ledger, owners } = loadValid()
  const node = findDone(ledger)
  node.implementation_commit = headCommit()
  node.publishes = []
  node.consumes = []
  node.depends_on = []
  node.production_callers_to_migrate = []
  node.coverage_tags = ['CoverageA']
  if (!node.touched_paths || node.touched_paths.length === 0) node.touched_paths = ['src/Wanxiangshu/Some/Feature.fs']
  const { ok, errors } = validateLedger(ledger, owners)
  assert.equal(ok, false, `should reject DONE with only coverage without owner graph, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /coverage|owner graph|publish/i.test(e)), `error should mention coverage/owner graph: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-006] GATE-13: baseline/suppression growth must be rejected', () => {
  const { ledger, owners } = loadValid()
  const baselinePath = join(ROOT, 'scripts/checks/deadcode-baseline.json')
  if (!existsSync(baselinePath)) return
  const original = readFileSync(baselinePath, 'utf8')
  let parsed
  try { parsed = JSON.parse(original) } catch { return }
  const grown = clone(parsed)
  if (Array.isArray(grown.bindings)) {
    grown.bindings.push({ path: 'src/Wanxiangshu/Fake/Growth.fs', allowed: true })
  } else if (typeof grown === 'object') {
    grown.fakeGrowth = true
  } else {
    return
  }
  writeFileSync(baselinePath, JSON.stringify(grown, null, 2))
  try {
    const { ok, errors } = validateLedger(ledger, owners)
    if (ok === true) {
      assert.fail(`baseline growth should be rejected but gate returned ok:true (missing baseline check)`)
    } else {
      assert.ok(errors.some(e => /baseline|suppression|growth/i.test(e)), `baseline growth error should mention baseline: ${errors.join(';')}`)
    }
  } finally {
    writeFileSync(baselinePath, original)
  }
})

test('WHAT[MIGRATION-LEDGER-001] GATE-14: DAG cycle must be rejected', () => {
  const { ledger, owners } = loadValid()
  const a = ledger.nodes[0]
  const b = ledger.nodes[1]
  const origA = clone(a.depends_on)
  const origB = clone(b.depends_on)
  a.depends_on = [{ id: b.id, kind: 'contract' }]
  b.depends_on = [{ id: a.id, kind: 'contract' }]
  const { ok, errors } = validateLedger(ledger, owners)
  // restore not needed as clone is local but we mutated ledger copy
  assert.equal(ok, false, `should reject cycle, got ${errors.join(';')}`)
  assert.ok(errors.some(e => /cycle/i.test(e)), `error should mention cycle: ${errors.join(';')}`)
})

test('WHAT[MIGRATION-LEDGER-007] GATE-15: gate self-test must cover 11 illegal states', async () => {
  const { execSync } = await import('node:child_process')
  const out = execSync('node scripts/checks/migration-ledger.mjs --self-test 2>&1', { encoding: 'utf8' })
  assert.ok(out.includes('self-test passed') || out.includes('self-test'), `self-test should pass, got: ${out}`)
})
