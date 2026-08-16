// P6 wave: SyntheticTomlSurface — canonical synthetic-TOML writer surface.
// owner: provider-projection. ARCH-010 string rules + document layout through
// the registered surface: JS-native in/out (string, number, string array),
// no Fable shapes (JS-SEMANTIC-SURFACE-005).

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const toml = await import('../../../dist/Foundation/SyntheticTomlSurface.js')

/** Parse a rendered value back with a real parser. The oracle, not a reimplementation. */
const valueOf = (rendered) => parseToml(`x = ${rendered}`).x

test('WHAT[PROVIDER-PROJECTION-008] P6_TOML_SURFACE_writer_contract_is_callable', () => {
  assert.equal(typeof toml.renderString, 'function')
  assert.equal(typeof toml.renderDocument, 'function')
  assert.equal(typeof toml.byteCount, 'function')
  assert.equal(toml.renderString('ok'), '"ok"')
})

test('WHAT[PROVIDER-PROJECTION-008] P6_TOML_SURFACE_render_string_uses_basic_and_literal_forms', () => {
  assertJsData(toml.renderString('hello'), 'renderString output')
  assert.equal(toml.renderString('hello'), '"hello"')
  assert.equal(toml.renderString('修复了 fallback 的竞态'), '"修复了 fallback 的竞态"')
  assert.equal(toml.renderString('say "hi"'), '"say \\"hi\\""')

  const body = 'first\nsecond'
  assert.equal(toml.renderString(body), "'''\nfirst\nsecond\n'''")
  assert.equal(valueOf(toml.renderString(body)), 'first\nsecond\n')
})

test('WHAT[PROVIDER-PROJECTION-008] P6_TOML_SURFACE_document_lays_out_header_body_and_ordering', () => {
  const document = toml.renderDocument(['Diagnose the first causal failure.'], [
    toml.field('tool', toml.renderString('dotnet')),
    toml.field('exit_code', '1'),
  ])
  assert.equal(document, ['# Diagnose the first causal failure.', '', 'tool = "dotnet"', 'exit_code = 1', ''].join('\n'))
  assert.equal(toml.renderDocument([], []), '')
  assert.equal(toml.renderDocument([], [toml.field('status', toml.renderString('ok'))]), 'status = "ok"\n')

  // Bare fields precede table arrays (the measured TOML absorption hazard).
  const mixed = toml.renderDocument([], [
    toml.tableArrayEntry('item', [toml.field('turn', '1')]),
    toml.field('operation', toml.renderString('rebase')),
  ])
  assert.equal(mixed.startsWith('operation = "rebase"'), true)
  assert.deepEqual(parseToml(mixed).item, [{ turn: 1 }])
})

test('WHAT[PROVIDER-PROJECTION-012] P6_TOML_SURFACE_byte_count_measures_utf8_not_characters', () => {
  assert.equal(toml.byteCount('abc'), 3)
  assert.equal(toml.byteCount('é'), 2)
  assert.equal(toml.byteCount('中'), 3)
  assert.equal(toml.byteCount('😀'), 4)
  assert.equal(toml.byteCount(''), 0)
})

test('WHAT[PROVIDER-PROJECTION-010] P6_TOML_SURFACE_renderer_returns_data_only_values', () => {
  assert.equal(typeof toml.renderString, 'function')
  assert.equal(typeof toml.renderDocument, 'function')
  assert.equal(toml.renderDocument([], [toml.field('status', toml.renderString('ok'))]), 'status = "ok"\n')
})
