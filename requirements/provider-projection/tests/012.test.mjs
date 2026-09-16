import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

// requirements/provider-projection/tests/blogger-toml.test.mjs — CTX-013 Blogger delta schema.
// Moved from tests/unit/context/blogger-toml.test.mjs (cutover Wave 2a); owner: provider-projection.
//
// Schema: every delta part is `[[new_work_to_record]]`; kind is the field name.
// Historic frames: `[[do_not_exec]] historic_frame = …`.
// String rules / instruction layout stay in SyntheticToml (ARCH-010).

const bt = await import('../../../dist/Context/Companion/Blogger/TomlSurface.js')
const syn = await import('../../../dist/Foundation/SyntheticTomlSurface.js')

const part = {
  text: (text) => ({ Kind: 'text', Text: text, Tool: '', Args: '', MediaType: '' }),
  reasoning: (text) => ({ Kind: 'reasoning', Text: text, Tool: '', Args: '', MediaType: '' }),
  toolCall: (tool, args) => ({ Kind: 'toolCall', Text: '', Tool: tool, Args: args, MediaType: '' }),
  toolResult: (text) => ({ Kind: 'toolResult', Text: text, Tool: '', Args: '', MediaType: '' }),
  imageOmitted: (mediaType) => ({ Kind: 'imageOmitted', Text: '', Tool: '', Args: '', MediaType: mediaType }),
}

const item = (partValue, { role = 'user', truncated = false } = {}) => ({
  Role: role,
  Part: partValue,
  Truncated: truncated,
})

// ── item shape and key order ───────────────────────────────────────────────

// P6 wave: SyntheticTomlSurface — canonical synthetic-TOML writer surface.
// owner: provider-projection. ARCH-010 string rules + document layout through
// the registered surface: JS-native in/out (string, number, string array),
// no Fable shapes (JS-SEMANTIC-SURFACE-005).

const toml = await import('../../../dist/Foundation/SyntheticTomlSurface.js')

/** Parse a rendered value back with a real parser. The oracle, not a reimplementation. */
const valueOf = (rendered) => parseToml(`x = ${rendered}`).x

/**
 * The lines of a document that are TOML syntax rather than string content.
 *
 * Needed because a `'''` body may legitimately contain `#` and `[[table]]` lines — that is the
 * containment ARCH-010 requires — so a naive scan for `#` would report the renderer violating a
 * rule it is in fact enforcing. Tracks literal blocks: `key = '''` opens one, a line that is
 * exactly `'''` closes it.
 */
const syntaxLines = (document) => {
  const lines = []
  let inLiteral = false

  for (const line of document.split('\n')) {
    if (inLiteral) {
      if (line === "'''") inLiteral = false
      continue
    }
    if (/=\s*'''$/.test(line)) {
      inLiteral = true
      continue
    }
    lines.push(line)
  }

  return lines
}

// ── newline normalisation happens before anything else ──────────────────────

test('WHAT[PROVIDER-PROJECTION-012] CTX_013_identical_input_renders_byte_identical_output', () => {
  const build = () => [
    item(part.text('请修复 fallback 的竞态。'), { role: 'user' }),
    item(part.toolCall('edit', '{"a":1,"b":2}'), { role: 'assistant' }),
    item(part.toolResult('The edit was applied successfully.')),
  ]

  assert.equal(bt.render(build()), bt.render(build()))
})

test('WHAT[PROVIDER-PROJECTION-012] CTX_013_document_ends_with_exactly_one_LF', () => {
  const rendered = bt.render([item(part.text('a')), item(part.text('b'))])

  assert.equal(rendered.endsWith('\n'), true)
  assert.equal(rendered.endsWith('\n\n'), false)
})

test('WHAT[PROVIDER-PROJECTION-012] CTX_013_an_empty_document_is_empty_not_a_bare_newline', () => {
  assert.equal(bt.render([]), '')
  assert.equal(syn.byteCount(bt.render([])), 0)
})

test('WHAT[PROVIDER-PROJECTION-012] CTX_013_no_timestamps_or_host_ids_are_emitted', () => {
  const rendered = bt.render([
    item(part.text('work')),
    item(part.toolResult('contents')),
  ])

  assert.doesNotMatch(rendered, /\d{4}-\d{2}-\d{2}/, 'no dates')
  assert.doesNotMatch(rendered, /msg_[A-Za-z0-9]/, 'no Host message ids')
  assert.doesNotMatch(rendered, /callId|callID/, 'no tool call ids')
})

// ── the instruction header CTX-013 now permits ──────────────────────────────

test('WHAT[PROVIDER-PROJECTION-012] P6_TOML_SURFACE_byte_count_measures_utf8_not_characters', () => {
  assert.equal(toml.byteCount('abc'), 3)
  assert.equal(toml.byteCount('é'), 2)
  assert.equal(toml.byteCount('中'), 3)
  assert.equal(toml.byteCount('😀'), 4)
  assert.equal(toml.byteCount(''), 0)
})

test('WHAT[PROVIDER-PROJECTION-012] ARCH_010_CRLF_and_lone_CR_normalise_to_LF', () => {
  // Without this, identical logical content renders as different bytes depending on which platform
  // produced it, and 「同一 semantic input 必须产生相同 bytes」 fails for a reason nobody can see.
  assert.equal(toml.normalizeNewlines('a\r\nb\rc\nd'), 'a\nb\nc\nd')
  assert.equal(toml.normalizeNewlines(''), '')

  assert.equal(toml.renderString('line one\r\nline two'), toml.renderString('line one\nline two'))
})

// ── string form selection ──────────────────────────────────────────────────

test('WHAT[PROVIDER-PROJECTION-012] ARCH_010_identical_input_renders_byte_identical_output', () => {
  const build = () =>
    toml.renderDocument(['Use the result below as evidence.'], [
      toml.field('tool', toml.renderString('shell')),
      toml.field('output', toml.renderString('line one\nline two')),
    ])

  assert.equal(build(), build())
})

// ── UTF-8 byte counting is the measurement every limit uses ────────────────

test('WHAT[PROVIDER-PROJECTION-012] ARCH_010_byteCount_measures_UTF8_not_characters', () => {
  assert.equal(toml.byteCount('abc'), 3)
  assert.equal(toml.byteCount('é'), 2, 'U+00E9 is two bytes')
  assert.equal(toml.byteCount('中'), 3, 'CJK is three bytes')
  assert.equal(toml.byteCount('中文测试'), 12)
  assert.equal(toml.byteCount('😀'), 4, 'a surrogate pair is four bytes')
  assert.equal(toml.byteCount(''), 0)

  // The distinction that matters: a CJK payload is three times its character count, so measuring
  // `.length` would let a chunk exceed its limit threefold.
  const cjk = '中'.repeat(100)
  assert.equal(cjk.length, 100)
  assert.equal(toml.byteCount(cjk), 300)
})

test('WHAT[PROVIDER-PROJECTION-012] ARCH_010_byteCount_agrees_with_the_platform_encoder', () => {
  // The hand-rolled counter exists because Fable has no GetByteCount. It must agree with Node's
  // encoder on every shape, or a limit means one thing in tests and another in production.
  const encoder = new TextEncoder()
  const samples = [
    '',
    'plain ascii',
    'é',
    '中文',
    '😀🎉',
    'mixed ascii 中文 é 😀',
    '\u0000\u001f',
    'a\nb\tc',
    '\uD800', // lone high surrogate
    '\uDC00', // lone low surrogate
    'x\uD800y', // unpaired surrogate between text
  ]

  for (const text of samples) {
    assert.equal(
      toml.byteCount(text),
      encoder.encode(text).length,
      `byteCount disagrees with TextEncoder for ${JSON.stringify(text)}`,
    )
  }
})
