// tests/unit/js-tools/js-anchors.test.mjs — G5 Phase B: failure algebra +
// ordered anchor declaration rules (JS-006/JS-019).
//
// JS-019: stable codes, frozen once shipped. AnchorRules owns the pure
// declaration refusals (empty anchor, non-positive occurrence); the other
// refusal classes live in the sandbox matcher / transaction layer.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  AnchorRules_validateDeclaration as validateDeclaration,
  AnchorRules_validateOccurrence as validateOccurrence,
  AnchorSpec,
} from '../../../dist/Domain/JsAnchor.js'
import {
  JsFailure,
  JsFailureModule_code as failureCode,
  JsFailureModule_reason as failureReason,
} from '../../../dist/Domain/JsFailure.js'
import { resultOf } from '../support/domain.mjs'

const declaration = (spec, occurrence) => ({ Spec: spec, Occurrence: occurrence })
const ok = (result) => resultOf(result).ok
// resolve case ordinals from the emitted cases() at load time, never by hand
const anchorCaseIndex = (name) => Object.create(AnchorSpec.prototype).cases().indexOf(name)
const exact = (text) => new AnchorSpec(anchorCaseIndex('Exact'), [text])
const regex = (pattern) => new AnchorSpec(anchorCaseIndex('Regex'), [pattern])
const failureOf = (name, payload) => {
  const index = Object.create(JsFailure.prototype).cases().indexOf(name)
  return new JsFailure(index, payload === undefined ? [] : [payload])
}

test('JS019_failure_codes_are_stable_and_unique', () => {
  const cases = [
    [failureOf('InvalidProgram'), 'INVALID_PROGRAM'],
    [failureOf('ProgramFailed', ''), 'PROGRAM_FAILED'],
    [failureOf('ProgramTimeout'), 'PROGRAM_TIMEOUT'],
    [failureOf('ProgramResourceLimit'), 'PROGRAM_RESOURCE_LIMIT'],
    [failureOf('FileNotFound', 'a'), 'FILE_NOT_FOUND'],
    [failureOf('FileAlreadyExists', 'a'), 'FILE_ALREADY_EXISTS'],
    [failureOf('InvalidUtf8', 'a'), 'INVALID_UTF8'],
    [failureOf('AnchorNotFound', 'missing'), 'ANCHOR_NOT_FOUND'],
    [failureOf('AnchorNotUnique'), 'ANCHOR_NOT_UNIQUE'],
    [failureOf('DuplicateMutationTarget', 'a'), 'DUPLICATE_MUTATION_TARGET'],
    [failureOf('ResultTooLarge', undefined), 'RESULT_TOO_LARGE'],
    [failureOf('InvalidReturnValue'), 'INVALID_RETURN_VALUE'],
    [failureOf('FileChanged', 'a'), 'FILE_CHANGED'],
    [failureOf('TransactionPrepareFailed'), 'TRANSACTION_PREPARE_FAILED'],
    [failureOf('TransactionCommitFailed'), 'TRANSACTION_COMMIT_FAILED'],
    [failureOf('TransactionRecoveryRequired'), 'TRANSACTION_RECOVERY_REQUIRED'],
    [failureOf('UnknownMember'), 'UNKNOWN_MEMBER'],
  ]
  const seen = new Set()
  for (const [failure, expected] of cases) {
    const code = failureCode(failure)
    assert.equal(code, expected)
    assert.equal(seen.has(code), false, `duplicate code ${code}`)
    seen.add(code)
    assert.equal(typeof failureReason(failure), 'string')
    assert.ok(failureReason(failure).length > 0)
  }
})

test('JS006_empty_anchor_declaration_is_refused', () => {
  assert.equal(ok(validateDeclaration(declaration(exact(''), undefined))), false)
  assert.equal(ok(validateDeclaration(declaration(regex(''), undefined))), false)
  assert.equal(ok(validateDeclaration(declaration(exact('hello'), undefined))), true)
  assert.equal(ok(validateDeclaration(declaration(regex('^\\s*$'), undefined))), true)
})

test('JS006_non_positive_occurrence_is_refused', () => {
  assert.equal(ok(validateOccurrence(declaration(exact('x'), 0))), false)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), -1))), false)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), 1))), true)
  assert.equal(ok(validateOccurrence(declaration(exact('x'), undefined))), true)
})
