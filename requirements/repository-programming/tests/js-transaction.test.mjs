// JS-012/015: transaction decisions consume plain mutation facts; durable
// effects stay behind the EventStore-backed owner surfaces.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  validateSingleIntent,
  validateTargets,
  validateFreshness,
  preflight,
  commitPlan,
  rollbackPlan,
} from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const ok = (result) => result.ok
const codeOf = (result) => result.code
const rewrite = (path, originalText, newText) => ({ kind: 'rewrite', path, originalText, newText })
const create = (path, text) => ({ kind: 'create', path, text })

const current = { 'a.txt': 'current' }

test('WHAT[REPOSITORY-PROGRAMMING-010] JS026_same_path_once_rejects_duplicate_mutation_targets', () => {
  const dup = [rewrite('a.txt', 'x', 'y'), create('a.txt', 'z')]
  const result = validateSingleIntent(dup)
  assert.equal(ok(result), false)
  assert.equal(codeOf(result), 'DUPLICATE_MUTATION_TARGET')

  const distinct = [rewrite('a.txt', 'x', 'y'), create('b.txt', 'z')]
  assert.equal(ok(validateSingleIntent(distinct)), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-010] JS008_009_rewrite_requires_existing_target_create_requires_missing', () => {
  const existing = ['a.txt']
  assert.equal(ok(validateTargets(existing, [rewrite('a.txt', 'x', 'y')])), true)
  assert.equal(codeOf(validateTargets(existing, [rewrite('missing.txt', 'x', 'y')])), 'FILE_NOT_FOUND')
  assert.equal(ok(validateTargets(existing, [create('new.txt', 'n')])), true)
  assert.equal(codeOf(validateTargets(existing, [create('a.txt', 'n')])), 'FILE_ALREADY_EXISTS')
})

test('WHAT[REPOSITORY-PROGRAMMING-014] JS014_stale_rewrite_is_a_conflict_with_no_retry', () => {
  const fresh = [rewrite('a.txt', 'current', 'new')]
  const stale = [rewrite('a.txt', 'old', 'new')]
  assert.equal(ok(validateFreshness(current, fresh)), true)
  assert.equal(codeOf(validateFreshness(current, stale)), 'FILE_CHANGED')
  // create targets are not freshness-checked
  assert.equal(ok(validateFreshness(current, [create('b.txt', 'n')])), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_preflight_orders_rules_and_short_circuits', () => {
  // duplicate intent wins over everything
  assert.equal(
    codeOf(preflight(['a.txt'], current, [], [rewrite('a.txt', 'current', 'x'), create('a.txt', 'y')])),
    'DUPLICATE_MUTATION_TARGET',
  )
  // missing target beats freshness
  assert.equal(codeOf(preflight(['a.txt'], current, [], [rewrite('missing.txt', 'anything', 'x')])), 'FILE_CHANGED')
  // all good
  assert.equal(ok(preflight(['a.txt'], current, [], [rewrite('a.txt', 'current', 'x'), create('b.txt', 'y')])), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-014] JS014_preflight_covers_read_only_snapshots_and_create_absence', () => {
  const changedRead = preflight(
    ['dependency.txt'],
    { 'dependency.txt': 'external' },
    [{ path: 'dependency.txt', text: 'snapshot' }],
    [],
  )
  assert.equal(codeOf(changedRead), 'FILE_CHANGED')
  assert.equal(codeOf(preflight(['new.txt'], { 'new.txt': 'external' }, [], [create('new.txt', 'tool')])), 'FILE_CHANGED')
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commit_plan_is_exact', () => {
  const mutations = [rewrite('a.txt', 'oldA', 'newA'), create('b.txt', 'newB')]
  assert.deepEqual(commitPlan(mutations), [
    { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
    { kind: 'create', path: 'b.txt', newText: 'newB' },
  ])
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollback_plan_is_exact', () => {
  const mutations = [rewrite('a.txt', 'oldA', 'newA'), create('b.txt', 'newB')]
  // rollback restores rewrites and marks creates for removal, reversed order
  assert.deepEqual(rollbackPlan(mutations), [
    { kind: 'removeCreated', path: 'b.txt', expectedCurrent: 'newB' },
    { kind: 'restore', path: 'a.txt', expectedCurrent: 'newA', originalText: 'oldA' },
  ])
})
