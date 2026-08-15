// P6 wave: BloggerTomlSurface — Blogger delta TOML schema surface (CTX-013).
// owner: provider-projection. Delta parts cross as JSON-shaped discriminated
// values; rendering is JS-native in/out (JS-SEMANTIC-SURFACE-005).

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const toml = await import('../../../dist/Context/Companion/Blogger/TomlSurface.js')

const item = (part, { role = 'user', truncated = false } = {}) => ({
  Role: role,
  Part: part,
  Truncated: truncated,
})

const part = {
  text: (text) => ({ Kind: 'text', Text: text, Tool: '', Args: '', MediaType: '' }),
  reasoning: (text) => ({ Kind: 'reasoning', Text: text, Tool: '', Args: '', MediaType: '' }),
  toolCall: (tool, args) => ({ Kind: 'toolCall', Text: '', Tool: tool, Args: args, MediaType: '' }),
  toolResult: (text) => ({ Kind: 'toolResult', Text: text, Tool: '', Args: '', MediaType: '' }),
  imageOmitted: (mediaType) => ({ Kind: 'imageOmitted', Text: '', Tool: '', Args: '', MediaType: mediaType }),
  mediaOmitted: (mediaType) => ({ Kind: 'mediaOmitted', Text: '', Tool: '', Args: '', MediaType: mediaType }),
}

test('P6_BLOGGER_SURFACE_exports_exact_schema_names', () => {
  assert.deepEqual(Object.getOwnPropertyNames(toml).sort(), [
    'DoNotExecTable',
    'NewWorkTable',
    'TruncationMarker',
    'render',
    'renderHistoricFrame',
    'renderItem',
    'renderPreviousEnforcerTip',
    'renderWith',
  ])
})

test('P6_BLOGGER_SURFACE_tool_call_renders_as_new_work_table', () => {
  const rendered = toml.renderItem(item(part.toolCall('edit', '{"filePath":"a.fs"}'), { role: 'assistant' }))
  assertJsData(rendered, 'rendered item')
  assert.equal(
    rendered,
    [
      '[[new_work_to_record]]',
      'tool_call = "edit"',
      'arguments = "{\\"filePath\\":\\"a.fs\\"}"',
    ].join('\n'),
  )
})

test('P6_BLOGGER_SURFACE_part_kind_is_the_field_name', () => {
  assert.equal(
    toml.renderItem(item(part.text('Fix the race.'))),
    ['[[new_work_to_record]]', 'user = "Fix the race."'].join('\n'),
  )
  assert.equal(
    toml.renderItem(item(part.reasoning('considered'))),
    ['[[new_work_to_record]]', 'reasoning = "considered"'].join('\n'),
  )
})

test('P6_BLOGGER_SURFACE_media_omitted_defaults_to_untyped', () => {
  assert.equal(toml.renderItem(item(part.imageOmitted('image/png'))).includes('media_omitted = "image/png"'), true)
  assert.equal(toml.renderItem(item(part.imageOmitted(undefined))).includes('media_omitted = "untyped"'), true)
  assert.equal(toml.renderItem(item(part.imageOmitted(''))).includes('media_omitted = "untyped"'), true)
})

test('P6_BLOGGER_SURFACE_render_orders_and_ends_with_single_lf', () => {
  const rendered = toml.render([
    item(part.text('work')),
    item(part.toolCall('read', '{}'), { role: 'assistant' }),
    item(part.toolResult('ok')),
  ])
  assert.equal(rendered.endsWith('\n'), true)
  assert.equal(rendered.endsWith('\n\n'), false)
  assert.equal(rendered.includes('[[message]]'), false)
  assert.equal(rendered.includes('kind ='), false)
})

test('P6_BLOGGER_SURFACE_historic_frame_renders_as_do_not_exec', () => {
  assert.equal(
    toml.renderHistoricFrame('frame body 0'),
    ['[[do_not_exec]]', 'historic_frame = "frame body 0"', ''].join('\n'),
  )
  assert.equal(parseToml(toml.renderHistoricFrame('frame body 0')).do_not_exec[0].historic_frame, 'frame body 0')
})
