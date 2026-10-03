import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");

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

test('WHAT[provider-projection-012] CTX_013_identical_input_renders_byte_identical_output', () => {
  const build = () => [
    item(part.text('请修复 fallback 的竞态。'), { role: 'user' }),
    item(part.toolCall('edit', '{"a":1,"b":2}'), { role: 'assistant' }),
    item(part.toolResult('The edit was applied successfully.')),
  ]

  assert.equal(bt.render(build()), bt.render(build()))
})
test('WHAT[provider-projection-012] CTX_013_document_ends_with_exactly_one_LF', () => {
  const rendered = bt.render([item(part.text('a')), item(part.text('b'))])

  assert.equal(rendered.endsWith('\n'), true)
  assert.equal(rendered.endsWith('\n\n'), false)
})
test('WHAT[provider-projection-012] CTX_013_an_empty_document_is_empty_not_a_bare_newline', () => {
  assert.equal(bt.render([]), '')
  assert.equal(syn.byteCount(bt.render([])), 0)
})
test('WHAT[provider-projection-012] CTX_013_no_timestamps_or_host_ids_are_emitted', () => {
  const rendered = bt.render([
    item(part.text('work')),
    item(part.toolResult('contents')),
  ])

  assert.doesNotMatch(rendered, /\d{4}-\d{2}-\d{2}/, 'no dates')
  assert.doesNotMatch(rendered, /msg_[A-Za-z0-9]/, 'no Host message ids')
  assert.doesNotMatch(rendered, /callId|callID/, 'no tool call ids')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const { assertJsData } = await import("../../verification-system/tests/support/js-contract.mjs");

const toml = await import('../../../dist/Foundation/SyntheticTomlSurface.js')
const valueOf = (rendered) => parseToml(`x = ${rendered}`).x

test('WHAT[provider-projection-012] P6_TOML_SURFACE_byte_count_measures_utf8_not_characters', () => {
  assert.equal(toml.byteCount('abc'), 3)
  assert.equal(toml.byteCount('é'), 2)
  assert.equal(toml.byteCount('中'), 3)
  assert.equal(toml.byteCount('😀'), 4)
  assert.equal(toml.byteCount(''), 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");

const toml = await import('../../../dist/Foundation/SyntheticTomlSurface.js')
const valueOf = (rendered) => parseToml(`x = ${rendered}`).x
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

test('WHAT[provider-projection-012] ARCH_010_layout_uses_LF_without_normalizing_data_values', () => {
  assert.equal(toml.normalizeNewlines('a\r\nb\rc\nd'), 'a\nb\nc\nd')
  assert.equal(toml.normalizeNewlines(''), '')

  for (const raw of ['line one\r\nline two', 'line one\rline two', 'line one\nline two\n']) {
    const rendered = toml.renderDocument(['first\r\nsecond\rthird'], [toml.field('data', toml.renderString(raw))])
    assert.equal(rendered.includes('\r'), false, 'physical layout must contain only LF line endings')
    assert.ok(rendered.startsWith('# first\n# second\n# third\n\n'))
    assert.equal(parseToml(rendered).data, raw)
    assert.equal(Buffer.byteLength(rendered, 'utf8'), toml.byteCount(rendered))
  }
})
test('WHAT[provider-projection-012] ARCH_010_identical_input_renders_byte_identical_output', () => {
  const build = () =>
    toml.renderDocument(['Use the result below as evidence.'], [
      toml.field('tool', toml.renderString('shell')),
      toml.field('output', toml.renderString('line one\nline two')),
    ])

  assert.equal(build(), build())
})
test('WHAT[provider-projection-012] ARCH_010_byteCount_measures_UTF8_not_characters', () => {
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
test('WHAT[provider-projection-012] ARCH_010_byteCount_agrees_with_the_platform_encoder', () => {
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
test('WHAT[provider-projection-012] prefix byte counts equal the actual rendered UTF8 bytes across string form boundaries', () => {
  const samples = ['', 'a\nb\n', 'a\r\nb\r', "a\n''b'", '中😀\n', '\u0000\t\n', '\uD83D', 'x\\"\n']
  const suffixes = ['', '\n', '\n\n', '\r\n', "'\n", "''\n", '\n[truncated]', '\uDE00', '\uDE00\n']
  for (const text of samples) {
    for (const suffix of suffixes) {
      for (let length = -1; length <= text.length + 1; length += 1) {
        const prefix = text.slice(0, Math.max(0, Math.min(length, text.length)))
        const rendered = toml.renderString(prefix + suffix)
        assert.equal(
          toml.renderStringByteCountPrefix(text, length, suffix),
          Buffer.byteLength(rendered, 'utf8'),
          `byte count differs for ${JSON.stringify({ text, length, suffix, rendered })}`,
        )
      }
    }
  }
})
}
