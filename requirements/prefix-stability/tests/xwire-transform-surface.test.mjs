// requirements/prefix-stability/tests/xwire-transform-surface.test.mjs — WHAT[PREFIX-STABILITY-001]
//
// Verifies that XWire owns applyTransform entry point for provider transform.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[PREFIX-STABILITY-001] XWire owns applyTransform entry point for provider transform', () => {
  const wire = read('src/Wanxiangshu/Context/Prefix/Wire.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(wire, /let\s+applyTransform/)
  assert.match(wire, /applySessionTransform/)
  assert.match(pt, /XWire\.applyTransform/)
})
