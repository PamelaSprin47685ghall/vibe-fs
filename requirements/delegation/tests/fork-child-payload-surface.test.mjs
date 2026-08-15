// P3 pilot: ForkChildPayloadSurface — JSON-shaped semantic surface proof.
// owner: delegation. JS-SEMANTIC-SURFACE-002/003/005: the registered surface
// (scripts/lib/test-surface-scan.mjs SURFACE_MODULES) is the legal entry
// point; input/output are JS-native data (assertJsData), no Fable shapes.
// Semantics stay byte-identical with ForkChildPayload (FORK_CHILD_PAYLOAD_*).

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const { instructions, render } = await import('../../../dist/Execution/Delegation/Fork/Surface.js')

const ASSIGNMENT = 'Write host_restart_proof.txt with OK.'
const RECORD = 'Parent session investigated the fallback race.'

const input = (over = {}) => ({
  Assignment: ASSIGNMENT,
  CommissionerRecord: undefined,
  Attachment: undefined,
  RootRequirements: [],
  Payload: undefined,
  ...over,
})

// ── surface shape is JS-native ──────────────────────────────────────────────

test('P3_SURFACE_instructions_are_js_native_data', () => {
  const instr = instructions('en')
  assertJsData(instr, 'instructions')
  assert.equal(typeof instr.CommissionerRecord, 'string')
  assert.equal(typeof instr.Attachment, 'string')
  assert.equal(typeof instr.Requirements, 'string')
  assert.ok(Array.isArray(instr.Base), 'Base must be a JS string array')
  assert.equal(instr.Base.every((line) => typeof line === 'string'), true)
})

test('P3_SURFACE_render_output_is_js_native_and_deterministic', () => {
  const doc = render('en', input({ RootRequirements: ['Ship it.'] }))
  assertJsData(doc, 'rendered document')
  assert.equal(doc, render('en', input({ RootRequirements: ['Ship it.'] })))
})

// ── semantics match the ForkChildPayload contract ───────────────────────────

test('P3_SURFACE_assignment_is_instruction_header_not_data_field', () => {
  const doc = render('en', input())
  assert.ok(doc.startsWith(`# ${ASSIGNMENT}\n`), 'assignment must be the first instruction comment')
  assert.equal(parseToml(doc).assignment, undefined)
})

test('P3_SURFACE_commissioner_record_is_toml_data_field', () => {
  const doc = render('en', input({ CommissionerRecord: RECORD }))
  const parsed = parseToml(doc)
  assert.equal(parsed.commissioner_record, RECORD)
  assert.ok(doc.includes('commissioner_record ='))
  assert.ok(!doc.includes(`# ${RECORD}`))
})

test('P3_SURFACE_requirements_render_table_array_with_one_based_ordinals', () => {
  const doc = render('en', input({ RootRequirements: ['Ship it.', 'Add tests.'] }))
  const parsed = parseToml(doc)
  assert.deepEqual(parsed.root_requirement, [
    { ordinal: 1, text: 'Ship it.' },
    { ordinal: 2, text: 'Add tests.' },
  ])
})

test('P3_SURFACE_payload_renders_content_field_first', () => {
  const doc = render('en', input({ Payload: 'hello' }))
  const parsed = parseToml(doc)
  assert.equal(parsed.content, 'hello')
  assert.ok(doc.includes('content = "hello"'))

  // With requirements present, content still precedes the table array.
  const both = render('en', input({ Payload: 'hello', RootRequirements: ['Ship it.'] }))
  assert.ok(both.indexOf('content =') < both.indexOf('[[root_requirement]]'))
})

test('P3_SURFACE_undefined_optional_fields_are_absent_not_empty', () => {
  const doc = render('en', input({ CommissionerRecord: undefined, Payload: undefined }))
  assert.ok(!doc.includes('commissioner_record ='))
  assert.ok(!doc.includes('content ='))
  assert.equal(parseToml(doc).commissioner_record, undefined)
})
