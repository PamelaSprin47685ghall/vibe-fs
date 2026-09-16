import assert from 'node:assert/strict'
import test from 'node:test'
import * as term from '../../../dist/Context/Companion/TerminalValiditySurface.js'

test('WHAT[CONTEXT-COMPRESSION-004] CTX_004_empty_terminal_is_not_a_result', () => {
  assert.equal(term.isValidTerminal(''), false)
})

test('WHAT[CONTEXT-COMPRESSION-004] CTX_004_xml_only_terminal_is_not_a_result', () => {
  assert.equal(term.isValidTerminal('<xml>content</xml>'), false)
})

test('WHAT[CONTEXT-COMPRESSION-004] CTX_004_prose_is_a_result', () => {
  assert.equal(term.isValidTerminal('This is a valid response summary.'), true)
})

test('WHAT[CONTEXT-COMPRESSION-004] CTX_004_isValid_agrees_with_check', () => {
  assert.equal(term.isValid('Hello'), term.check('Hello').ok)
})

test('WHAT[CONTEXT-COMPRESSION-004] CTX_004_rejection_reasons_are_distinguishable_for_diagnostics', () => {
  assert.notEqual(term.check('').reason, term.check('<xml></xml>').reason)
})
