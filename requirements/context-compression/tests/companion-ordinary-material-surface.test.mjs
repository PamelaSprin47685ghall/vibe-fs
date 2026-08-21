// requirements/context-compression/tests/companion-ordinary-material-surface.test.mjs — WHAT[CONTEXT-COMPRESSION-018]
//
// Verifies that CompanionTransform owns applyCompanionForOrdinaryMaterial entry point.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[CONTEXT-COMPRESSION-018] CompanionTransform owns applyCompanionForOrdinaryMaterial entry point', () => {
  const companion = read('src/Wanxiangshu/Context/Companion/Transform.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(companion, /let\s+applyCompanionForOrdinaryMaterial/)
  assert.match(companion, /ExplicitResumeSuppression\.isCurrentMaterial/)
  assert.match(pt, /CompanionTransform\.applyCompanionForOrdinaryMaterial/)
})
