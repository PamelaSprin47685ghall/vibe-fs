// requirements/cognitive-environment/tests/session-execution-attempt-surface.test.mjs — WHAT[COGNITIVE-ENVIRONMENT-013]
//
// Verifies that SessionExecutionBinding owns beginPhysicalProviderAttemptForTransform entry point.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-013] SessionExecutionBinding owns beginPhysicalProviderAttemptForTransform entry point', () => {
  const binding = read('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(binding, /let\s+beginPhysicalProviderAttemptForTransform/)
  assert.match(binding, /beginQuiescence\s+sessionId/)
  assert.match(pt, /SessionExecutionBinding\.beginPhysicalProviderAttemptForTransform/)
})
