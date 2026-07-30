// tests-mjs/Context/blogger-toml.test.mjs — CTX-013 deterministic rendering.
//
// TOML is a one-way human-readable wire form. The canonical digest always comes
// from `ProviderSemanticProjection`, never from this text, so the renderer owes
// exactly two things: byte-identical output for identical input, and output that
// parses. It owes nothing about reversibility, and there is deliberately no parser.
//
// The string-form choice is where this can silently go wrong. A multi-line BASIC
// string (`"""…"""`) still processes escapes, so a body containing a backslash —
// every non-trivial tool-call argument, every Windows path, every regex — is either
// misread or unparseable. These tests pin that `'''` is the only multi-line form
// emitted.

import assert from 'node:assert/strict'
import test from 'node:test'
import { bloggerToml as toml } from '../domain.mjs'

const item = (part, { turn = 0, role = 'user', truncated = false } = {}) =>
  toml.item({ turn, role, part, truncated })

// ── newline normalisation happens before anything else ──────────────────────

test('CTX_013_CRLF_and_lone_CR_normalise_to_LF', () => {
  // Without this, identical logical content renders as different bytes depending
  // on which platform produced it — and the 200 KiB measurement differs too.
  assert.equal(toml.normalizeNewlines('a\r\nb\rc\nd'), 'a\nb\nc\nd')
  assert.equal(toml.normalizeNewlines(''), '')

  const crlf = toml.render([item(toml.text('line one\r\nline two'))])
  const lf = toml.render([item(toml.text('line one\nline two'))])
  assert.equal(crlf, lf, 'CRLF and LF input must render to the same bytes')
})

// ── string form selection ──────────────────────────────────────────────────

test('CTX_013_single_line_text_uses_a_basic_string', () => {
  assert.equal(toml.renderString('hello'), '"hello"')
  assert.equal(toml.renderString('修复了 fallback 的竞态'), '"修复了 fallback 的竞态"')
})

test('CTX_013_basic_string_escapes_are_the_standard_set', () => {
  assert.equal(toml.renderString('say "hi"'), '"say \\"hi\\""')
  assert.equal(toml.renderString('a\\b'), '"a\\\\b"')

  // A tab does NOT force the multi-line form: it has a basic-string escape, so a
  // one-line value stays one line. Only a newline forces `'''`.
  assert.equal(toml.renderString('tab\there'), '"tab\\there"')
})

test('CTX_013_multiline_text_uses_a_literal_string_so_bodies_stay_verbatim', () => {
  const body = 'first\nsecond'
  assert.equal(toml.renderString(body), "'''\nfirst\nsecond'''")
})

test('CTX_013_a_multiline_body_with_backslashes_survives_verbatim', () => {
  // The case that rules out `"""`. Inside a basic multi-line string `\d` is not a
  // valid TOML escape and `\n` would become a real newline; inside `'''` both are
  // literal characters.
  const regex = 'match: \\d+\\.\\d+\nreplace: C:\\Users\\dev\\path'
  const rendered = toml.renderString(regex)

  assert.equal(rendered.startsWith("'''\n"), true, 'must be a literal multi-line string')
  assert.equal(rendered.includes('\\\\'), false, 'a literal string must not escape backslashes')
  assert.equal(rendered.includes('\\d+'), true, 'the backslash reaches the Companion unchanged')
})

test('CTX_013_multiline_text_containing_triple_single_quotes_falls_back_to_basic', () => {
  // `'''` inside a literal string would close it early. Escaping everything in a
  // basic string is the only always-valid fallback.
  const body = "line\nwith ''' inside"
  const rendered = toml.renderString(body)

  assert.equal(rendered.startsWith('"'), true, 'must fall back to a basic string')
  assert.equal(rendered.includes('\\n'), true, 'the newline is escaped, not literal')
  assert.equal(rendered.includes("'''"), true, 'the quotes themselves need no escaping in a basic string')
})

test('CTX_013_multiline_text_ending_in_a_single_quote_falls_back_to_basic', () => {
  // A trailing `'` would extend the closing `'''` into `''''`, which does not parse.
  const rendered = toml.renderString("first line\nends with '")

  assert.equal(rendered.startsWith('"'), true, 'must fall back to a basic string')
  assert.equal(rendered.endsWith("'\""), true, 'the quote is the last content character')
  assert.equal(rendered.includes('\\n'), true, 'the newline is escaped')
})

test('CTX_013_control_characters_never_appear_raw', () => {
  // TOML forbids raw control characters other than tab and newline, in both string
  // forms. A NUL reaching the wire would make the document unparseable.
  const rendered = toml.renderString('before\u0000after')

  assert.equal(rendered.includes('\u0000'), false)
  assert.equal(rendered, '"before\\u0000after"')

  // Even with a newline present, a control character forces the basic form.
  const multiline = toml.renderString('a\nb\u0007c')
  assert.equal(multiline.startsWith('"'), true)
  assert.equal(multiline.includes('\u0007'), false)
})

// ── item shape and key order ───────────────────────────────────────────────

test('CTX_013_fixed_key_order_is_turn_role_kind_then_the_body', () => {
  // A single-line args body stays a basic string, so this pins the whole item as
  // one exact document rather than asserting field presence one at a time.
  const rendered = toml.renderItem(item(toml.toolCall('edit', '{"filePath":"a.fs"}'), { turn: 3, role: 'assistant' }))

  assert.equal(
    rendered,
    [
      '[[item]]',
      'turn = 3',
      'role = "assistant"',
      'kind = "tool_call"',
      'tool = "edit"',
      'args = "{\\"filePath\\":\\"a.fs\\"}"',
    ].join('\n'),
  )
})

test('CTX_013_a_multiline_body_keeps_the_key_order_and_uses_a_literal_string', () => {
  const rendered = toml.renderItem(item(toml.toolCall('edit', '{\n  "a": 1\n}'), { turn: 1, role: 'assistant' }))

  assert.equal(
    rendered,
    ['[[item]]', 'turn = 1', 'role = "assistant"', 'kind = "tool_call"', 'tool = "edit"', "args = '''", '{', '  "a": 1', "}'''"].join(
      '\n',
    ),
  )
})

test('CTX_013_absent_optional_fields_are_omitted_not_emitted_empty', () => {
  // "no media type" and "a media type that is the empty string" are different
  // claims. Emitting `media_type = ""` would make the second unrepresentable.
  const withType = toml.renderItem(item(toml.imageOmitted('image/png')))
  const withoutType = toml.renderItem(item(toml.imageOmitted(undefined)))

  assert.equal(withType.includes('media_type = "image/png"'), true)
  assert.equal(withoutType.includes('media_type'), false)

  // Neither form carries a body: CTX-013 allows the marker to say an image was
  // here, and nothing about what it showed.
  assert.equal(withoutType.includes('text'), false)
  assert.equal(withType.includes('contentDigest'), false)
})

test('CTX_013_truncated_flag_appears_only_when_set', () => {
  assert.equal(toml.renderItem(item(toml.text('x'))).includes('truncated'), false)
  assert.equal(toml.renderItem(item(toml.text('x'), { truncated: true })).includes('truncated = true'), true)
})

test('CTX_013_every_part_kind_renders_its_own_kind_string', () => {
  const kinds = [
    [toml.text('t'), 'text'],
    [toml.reasoning('r'), 'reasoning'],
    [toml.toolCall('read', '{}'), 'tool_call'],
    [toml.toolResult('read', 'ok'), 'tool_result'],
    [toml.imageOmitted('image/png'), 'image_omitted'],
    [toml.mediaOmitted('application/pdf'), 'media_omitted'],
  ]

  for (const [part, expected] of kinds) {
    assert.equal(
      toml.renderItem(item(part)).includes(`kind = "${expected}"`),
      true,
      `expected kind = "${expected}"`,
    )
  }
})

// ── document determinism ───────────────────────────────────────────────────

test('CTX_013_identical_input_renders_byte_identical_output', () => {
  const build = () => [
    item(toml.text('请修复 fallback 的竞态。'), { turn: 0, role: 'user' }),
    item(toml.toolCall('edit', '{"a":1,"b":2}'), { turn: 1, role: 'assistant' }),
    item(toml.toolResult('edit', 'The edit was applied successfully.'), { turn: 1, role: 'tool' }),
  ]

  assert.equal(toml.render(build()), toml.render(build()))
})

test('CTX_013_document_ends_with_exactly_one_LF', () => {
  const rendered = toml.render([item(toml.text('a')), item(toml.text('b'), { turn: 1 })])

  assert.equal(rendered.endsWith('\n'), true)
  assert.equal(rendered.endsWith('\n\n'), false)
})

test('CTX_013_an_empty_document_is_empty_not_a_bare_newline', () => {
  // An empty chunk is never sent, so a lone newline could only ever be a
  // byte-count discrepancy against the 200 KiB limit.
  assert.equal(toml.render([]), '')
  assert.equal(toml.byteCount(toml.render([])), 0)
})

test('CTX_013_no_comments_timestamps_or_ids_are_emitted', () => {
  const rendered = toml.render([
    item(toml.text('work'), { turn: 0 }),
    item(toml.toolResult('read', 'contents'), { turn: 1, role: 'tool' }),
  ])

  assert.equal(rendered.includes('#'), false, 'no comments')
  assert.doesNotMatch(rendered, /\d{4}-\d{2}-\d{2}/, 'no dates')
  assert.doesNotMatch(rendered, /msg_[A-Za-z0-9]/, 'no Host message ids')
  assert.doesNotMatch(rendered, /callId|callID/, 'no tool call ids')
})

// ── UTF-8 byte counting is the measurement CTX-003 uses ────────────────────

test('CTX_003_byteCount_measures_UTF8_not_characters', () => {
  assert.equal(toml.byteCount('abc'), 3)
  assert.equal(toml.byteCount('é'), 2, 'U+00E9 is two bytes')
  assert.equal(toml.byteCount('中'), 3, 'CJK is three bytes')
  assert.equal(toml.byteCount('中文测试'), 12)
  assert.equal(toml.byteCount('😀'), 4, 'a surrogate pair is four bytes')
  assert.equal(toml.byteCount(''), 0)

  // The distinction that matters: a CJK delta is three times its character count,
  // so measuring `.length` would let a chunk exceed the limit threefold.
  const cjk = '中'.repeat(100)
  assert.equal(cjk.length, 100)
  assert.equal(toml.byteCount(cjk), 300)
})

test('CTX_003_byteCount_agrees_with_the_platform_encoder', () => {
  // The hand-rolled counter exists because Fable has no GetByteCount. It must agree
  // with Node's encoder on every shape, or the limit means one thing in tests and
  // another in production.
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
