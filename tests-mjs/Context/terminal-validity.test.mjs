// tests-mjs/Context/terminal-validity.test.mjs — CTX-004.
//
// The one content-level validity check in the system. Its scope is deliberately
// narrow: it reads produced text and answers "is this usable as a result". It
// never sees a provider, an error name or a context limit, because CTX-005
// forbids inferring why an attempt failed.
//
// Three consumers depend on this one answer: FALLBACK-008 repair eligibility,
// CTX-007's entry/squash commit gate, and CTX-012's probe promote gate. A second
// implementation could answer differently for the same text, which would let one
// caller commit a fact another would have refused — so these tests pin the
// predicate itself, not any caller's use of it.

import assert from 'node:assert/strict'
import test from 'node:test'
import { terminalValidity } from '../domain.mjs'

test('CTX_004_empty_terminal_is_not_a_result', () => {
  // Whitespace-only counts as empty: a model that emitted nothing but a newline
  // did not answer, and treating it as valid would commit an empty frame.
  for (const text of ['', ' ', '\n', '\t\n  ']) {
    assert.deepEqual(
      terminalValidity.check(text),
      { ok: false, error: 'Empty' },
      `${JSON.stringify(text)} must be refused as empty`,
    )
  }
})

test('CTX_004_xml_only_terminal_is_not_a_result', () => {
  // Tool-call markup where prose was required. Containment, not well-formedness:
  // a truncated tag still means the model was trying to call a tool.
  const markups = [
    '<tool_call>{"name":"read"}</tool_call>',
    '<invoke name="edit">',
    '</function_call>',
    'partial <use_tool',
    'TEXT BEFORE <call> AND AFTER',
  ]

  for (const text of markups) {
    assert.deepEqual(
      terminalValidity.check(text),
      { ok: false, error: 'XmlOnly' },
      `${JSON.stringify(text)} must be refused as XML-only`,
    )
  }
})

test('CTX_004_prose_is_a_result', () => {
  const texts = [
    'Fixed the race in next/Fallback.fs by moving the cursor advance behind the gate.',
    '修复了 fallback 的竞态。',
    'a',
    // Angle brackets alone are not tool markup. A model discussing generics or
    // comparisons must not be judged as having made a tool call.
    'The comparison a < b holds, and List<string> is the return type.',
    'Use <em>emphasis</em> in the rendered output.',
  ]

  for (const text of texts) {
    assert.deepEqual(terminalValidity.check(text), { ok: true }, `${JSON.stringify(text)} must be accepted`)
    assert.equal(terminalValidity.isValid(text), true)
  }
})

test('CTX_004_isValid_agrees_with_check', () => {
  // One predicate, two shapes. If these ever disagree, a caller reading the bool
  // and a caller reading the reason would commit different facts for one text.
  const samples = ['', '   ', '<tool_call/>', 'real answer', 'a < b']

  for (const text of samples) {
    assert.equal(
      terminalValidity.isValid(text),
      terminalValidity.check(text).ok,
      `isValid and check disagree on ${JSON.stringify(text)}`,
    )
  }
})

test('CTX_004_rejection_reasons_are_distinguishable_for_diagnostics', () => {
  // HOST-007 lets diagnostics report which shape was refused. The two reasons
  // must render differently, or an operator cannot tell "the model said nothing"
  // from "the model tried to call a tool".
  const empty = terminalValidity.describe('Empty')
  const xmlOnly = terminalValidity.describe('XmlOnly')

  assert.equal(empty, 'empty terminal')
  assert.equal(xmlOnly, 'XML-only terminal')
  assert.notEqual(empty, xmlOnly)
})

test('CTX_005_validity_does_not_depend_on_failure_cause', () => {
  // The predicate must not treat provider error prose as a signal. A completed
  // response that happens to discuss an overflow is still a valid result, and a
  // failed attempt is not this function's business at all.
  const discussesOverflow =
    'The request failed with context_overflow earlier; I have summarised the findings instead.'

  assert.equal(terminalValidity.isValid(discussesOverflow), true)
})
