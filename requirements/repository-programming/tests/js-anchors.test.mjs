// tests/unit/js-tools/js-anchors.test.mjs — G5 Phase B: failure algebra +
// ordered anchor declaration rules (JS-006/JS-019).
//
// JS-019: stable codes, frozen once shipped. AnchorRules owns the pure
// declaration refusals (empty anchor, non-positive occurrence); the other
// refusal classes live in the sandbox matcher / transaction layer.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  failureCatalog,
  validateAnchorDeclaration as validateDeclaration,
  validateAnchorOccurrence as validateOccurrence,
} from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const declaration = (spec, occurrence) => ({ ...spec, occurrence })
const ok = (result) => result.ok
const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_failure_codes_are_stable_and_unique', () => {
  const expectedCodes = [
    'INVALID_PROGRAM',
    'PROGRAM_FAILED',
    'PROGRAM_TIMEOUT',
    'PROGRAM_RESOURCE_LIMIT',
    'FILE_NOT_FOUND',
    'FILE_ALREADY_EXISTS',
    'INVALID_UTF8',
    'ANCHOR_NOT_FOUND',
    'ANCHOR_NOT_UNIQUE',
    'INVALID_EDIT',
    'EDIT_NOT_FOUND',
    'EDIT_AMBIGUOUS',
    'EDIT_OVERLAP',
    'DUPLICATE_MUTATION_TARGET',
    'RESULT_TOO_LARGE',
    'INVALID_RETURN_VALUE',
    'FILE_CHANGED',
    'TRANSACTION_PREPARE_FAILED',
    'TRANSACTION_COMMIT_FAILED',
    'TRANSACTION_RECOVERY_REQUIRED',
    'UNKNOWN_MEMBER',
  ]
  const cases = failureCatalog()
  assert.equal(cases.length, expectedCodes.length)
  const seen = new Set()
  for (const expected of expectedCodes) {
    const entry = cases.find(({ code }) => code === expected)
    assert.ok(entry, `missing code ${expected}`)
    assert.equal(seen.has(entry.code), false, `duplicate code ${entry.code}`)
    seen.add(entry.code)
    assert.equal(typeof entry.reason, 'string')
    assert.ok(entry.reason.length > 0)
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-007] JS006_empty_anchor_declaration_is_refused', () => {
  assert.equal(ok(validateDeclaration(declaration(exact(''), undefined))), false)
  assert.equal(ok(validateDeclaration(declaration(regex(''), undefined))), false)
  assert.equal(ok(validateDeclaration(declaration(exact('hello'), undefined))), true)
  assert.equal(ok(validateDeclaration(declaration(regex('^\\s*$'), undefined))), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-007] JS006_non_positive_occurrence_is_refused', () => {
  assert.equal(ok(validateOccurrence(declaration(exact('x'), 0))), false)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), -1))), false)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), 1))), true)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), undefined))), true)
})
