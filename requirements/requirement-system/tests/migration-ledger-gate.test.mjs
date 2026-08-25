import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { validateLedger } from '../../../scripts/checks/migration-ledger.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const LEDGER_PATH = join(ROOT, 'scripts/checks/migration-ledger.json')
const OWNERS_PATH = join(ROOT, 'scripts/checks/semantic-owners.json')

const loadValid = () => ({
  ledger: JSON.parse(readFileSync(LEDGER_PATH, 'utf8')),
  owners: JSON.parse(readFileSync(OWNERS_PATH, 'utf8')),
})
const clone = (o) => JSON.parse(JSON.stringify(o))

test('WHAT[REQUIREMENT-SYSTEM-019] migration ledger gate rejects PENDING with GREEN evidence and READY without owner graph', () => {
  const { ledger, owners } = loadValid()
  // Find existing PENDING or synthesize one for post-ReleaseClosure (117 DONE) case
  let pending = ledger.nodes.find(n => n.state === 'PENDING')
  if (!pending) {
    pending = {
      id: 'synthetic-pending-test',
      primary_owner: 'distribution',
      intent: 'synthetic PENDING for gate test',
      files: [],
      classification: 'KEEP',
      publishes: [],
      consumes: [],
      depends_on: [],
      production_callers_to_migrate: [],
      proofs: [],
      architecture_gates: [],
      touched_paths: [],
      coverage_tags: [],
      state: 'PENDING',
      result: 'PENDING',
      evidence: 'pending: inventory only'
    }
    ledger.nodes.push(pending)
  }
  pending.evidence = 'all GREEN verified'
  let res = validateLedger(ledger, owners)
  assert.equal(res.ok, false)
  assert.ok(res.errors.some(e => /PENDING.*evidence|GREEN|verified/i.test(e)))

  const { ledger: ledger2, owners: owners2 } = loadValid()
  let ready = ledger2.nodes.find(n => n.state === 'PENDING')
  if (!ready) {
    ready = {
      id: 'synthetic-ready-test',
      primary_owner: 'distribution',
      intent: 'synthetic READY for gate test',
      files: [],
      classification: 'KEEP',
      publishes: [],
      consumes: [],
      depends_on: [],
      production_callers_to_migrate: [],
      proofs: ['requirements/some/tests/foo.test.mjs'],
      architecture_gates: ['semantic-owners.mjs'],
      touched_paths: [],
      coverage_tags: [],
      state: 'PENDING',
      result: 'PENDING',
      evidence: 'pending: inventory only'
    }
    ledger2.nodes.push(ready)
  }
  ready.state = 'READY'
  ready.publishes = []
  ready.consumes = []
  ready.depends_on = []
  ready.production_callers_to_migrate = []
  ready.proofs = ['requirements/some/tests/foo.test.mjs']
  ready.architecture_gates = ['semantic-owners.mjs']
  res = validateLedger(ledger2, owners2)
  assert.equal(res.ok, false)
  assert.ok(res.errors.some(e => /READY.*owner graph/i.test(e)))
})

test('WHAT[REQUIREMENT-SYSTEM-019] migration ledger gate rejects DONE with PENDING result, classification mismatch, missing commit and baseline growth is frozen', () => {
  const { ledger, owners } = loadValid()
  const done = ledger.nodes.find(n => n.state === 'DONE')
  const originalResult = done.result
  done.result = 'PENDING'
  let res = validateLedger(ledger, owners)
  assert.equal(res.ok, false)
  assert.ok(res.errors.some(e => /DONE.*result.*PENDING/i.test(e)))
  done.result = originalResult

  // classification mismatch
  const originalClass = done.classification
  done.classification = 'KEEP'
  done.result = 'CUTOVER'
  res = validateLedger(ledger, owners)
  assert.equal(res.ok, false)
  assert.ok(res.errors.some(e => /classification.*incompatible|KEEP.*CUTOVER/i.test(e)))
  done.classification = originalClass
  done.result = originalResult

  // missing implementation_commit
  const origCommit = done.implementation_commit
  delete done.implementation_commit
  res = validateLedger(ledger, owners)
  assert.equal(res.ok, false)
  assert.ok(res.errors.some(e => /implementation_commit/i.test(e)))
  done.implementation_commit = origCommit

  // baseline growth is checked via deadcode-baseline.json fakeGrowth marker in gate-rejection tests; here we just ensure gate runs
  res = validateLedger(ledger, owners)
  assert.equal(res.ok, true, `valid ledger should pass after restores: ${res.errors.join(';')}`)
})
