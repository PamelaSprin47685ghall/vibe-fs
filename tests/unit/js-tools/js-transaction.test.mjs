// tests/unit/js-tools/js-transaction.test.mjs — G5 Phase B: pure transaction
// rules (JS-012/013/014/015/026).
//
// The rules are deterministic given injected filesystem facts; commit/rollback
// side effects are the adapter's job. These tests prove the decision algebra:
// same-path-once, target existence, freshness conflict, preflight ordering,
// and the commit/rollback plans.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  JsStagedMutation,
  JsTransaction_validateSingleIntent as validateSingleIntent,
  JsTransaction_validateTargets as validateTargets,
  JsTransaction_validateFreshness as validateFreshness,
  JsTransaction_preflight as preflight,
  JsTransaction_commitPlan as commitPlan,
  JsTransaction_rollbackPlan as rollbackPlan,
  JsFailureModule_code as failureCode,
} from '../../../dist/Domain/JsTools.js'
import { listItems, resultOf, toList } from '../support/domain.mjs'

const ok = (result) => resultOf(result).ok
const codeOf = (result) => failureCode(resultOf(result).error)
// resolve case ordinals from the emitted cases() at load time, never by hand
const mutationCase = (name, payload) => {
  const index = Object.create(JsStagedMutation.prototype).cases().indexOf(name)
  return new JsStagedMutation(index, payload)
}
const rewrite = (path, originalText, newText) => mutationCase('Rewrite', [path, originalText, newText])
const create = (path, text) => mutationCase('Create', [path, text])

const exists = (existing) => (path) => existing.includes(path)
const content = (map) => (path) => (path in map ? map[path] : undefined)

test('JS026_same_path_once_rejects_duplicate_mutation_targets', () => {
  const dup = [rewrite('a.txt', 'x', 'y'), create('a.txt', 'z')]
  const result = validateSingleIntent(toList(dup))
  assert.equal(ok(result), false)
  assert.equal(codeOf(result), 'DUPLICATE_MUTATION_TARGET')

  const distinct = [rewrite('a.txt', 'x', 'y'), create('b.txt', 'z')]
  assert.equal(ok(validateSingleIntent(toList(distinct))), true)
})

test('JS008_009_rewrite_requires_existing_target_create_requires_missing', () => {
  const existing = ['a.txt']
  assert.equal(ok(validateTargets(exists(existing), toList([rewrite('a.txt', 'x', 'y')]))), true)
  assert.equal(codeOf(validateTargets(exists(existing), toList([rewrite('missing.txt', 'x', 'y')]))), 'FILE_NOT_FOUND')
  assert.equal(ok(validateTargets(exists(existing), toList([create('new.txt', 'n')]))), true)
  assert.equal(codeOf(validateTargets(exists(existing), toList([create('a.txt', 'n')]))), 'FILE_ALREADY_EXISTS')
})

test('JS014_stale_rewrite_is_a_conflict_with_no_retry', () => {
  const current = { 'a.txt': 'current' }
  const fresh = [rewrite('a.txt', 'current', 'new')]
  const stale = [rewrite('a.txt', 'old', 'new')]
  assert.equal(ok(validateFreshness(content(current), toList(fresh))), true)
  assert.equal(codeOf(validateFreshness(content(current), toList(stale))), 'FILE_CHANGED')
  // create targets are not freshness-checked
  assert.equal(ok(validateFreshness(content(current), toList([create('b.txt', 'n')]))), true)
})

test('JS013_preflight_orders_rules_and_short_circuits', () => {
  const current = { 'a.txt': 'current' }
  // duplicate intent wins over everything
  assert.equal(codeOf(preflight(exists(['a.txt']), content(current), toList([rewrite('a.txt', 'current', 'x'), create('a.txt', 'y')]))), 'DUPLICATE_MUTATION_TARGET')
  // missing target beats freshness
  assert.equal(codeOf(preflight(exists(['a.txt']), content(current), toList([rewrite('missing.txt', 'anything', 'x')]))), 'FILE_NOT_FOUND')
  // all good
  assert.equal(ok(preflight(exists(['a.txt']), content(current), toList([rewrite('a.txt', 'current', 'x'), create('b.txt', 'y')]))), true)
})

test('JS013_015_commit_and_rollback_plans_are_exact', () => {
  const mutations = [rewrite('a.txt', 'oldA', 'newA'), create('b.txt', 'newB')]
  assert.deepEqual(listItems(commitPlan(toList(mutations))), [
    ['a.txt', 'newA'],
    ['b.txt', 'newB'],
  ])
  // rollback restores rewrites and marks creates for removal, reversed order
  assert.deepEqual(listItems(rollbackPlan(toList(mutations))), [
    ['b.txt', undefined],
    ['a.txt', 'oldA'],
  ])
})
