// requirements/speculative-investigation/tests/strength-speculate-surface.test.mjs — WHAT[SPEC-INV-002]
//
// Verifies that StrengthSpeculate owns tryApply entry point for transform speculation.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[SPEC-INV-002] StrengthSpeculate owns tryApply entry point for transform speculation', () => {
  const speculate = read('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(speculate, /let\s+tryApply/)
  assert.match(speculate, /applyBoundOwner/)
  assert.match(pt, /StrengthSpeculate\.tryApply/)
})
