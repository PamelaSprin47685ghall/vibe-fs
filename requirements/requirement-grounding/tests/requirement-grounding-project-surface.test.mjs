// requirements/requirement-grounding/tests/requirement-grounding-project-surface.test.mjs — WHAT[REQUIREMENT-GROUNDING-007]
//
// Verifies that RequirementGroundingTransform owns projectOrTerminate entry point.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[REQUIREMENT-GROUNDING-007] RequirementGroundingTransform owns projectOrTerminate entry point', () => {
  const grounding = read('src/Wanxiangshu/OpenCode/Host/RequirementGrounding/Transform.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(grounding, /let\s+projectOrTerminate/)
  assert.match(grounding, /tryProject/)
  assert.match(pt, /RequirementGroundingTransform\.projectOrTerminate/)
})
