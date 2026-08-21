// requirements/context-compression/tests/companion-ordinary-material-surface.test.mjs — WHAT[CONTEXT-COMPRESSION-018]
//
// Verifies that CompanionTransform owns applyCompanionForOrdinaryMaterial entry point.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[CONTEXT-COMPRESSION-018] CompanionTransform owns ordinary-material entry and consumes Host suppression as a capability', () => {
  const companion = read('src/Wanxiangshu/Context/Companion/Transform.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(companion, /let\s+applyCompanionForOrdinaryMaterial/)
  assert.match(companion, /\(isExplicitResume:\s*string option -> obj -> bool\)/)
  assert.match(companion, /if isExplicitResume projectionSessionIdOpt outObj then/)
  assert.doesNotMatch(companion, /ExplicitResumeSuppression/)
  assert.match(pt, /CompanionTransform\.applyCompanionForOrdinaryMaterial/)
  // Public contract: host-boundary ExplicitResumeSuppression is the owner surface for CRASH-018, consumed via capability — not via deleted private helper
  assert.match(read('src/Wanxiangshu/OpenCode/Host/ExplicitResumeSuppression.fs'), /let\s+isCurrentMaterial/)
  assert.match(read('src/Wanxiangshu/OpenCode/Host/ExplicitResumeSuppression.fs'), /let\s+isExplicitResumeBinding/)
  assert.match(pt, /ExplicitResumeSuppression\.isCurrentMaterial/)
  assert.match(pt, /ExplicitResumeSuppression\.isExplicitResumeBinding/)
  assert.match(pt, /TransformBranchCapabilities[\s\S]*IsExplicitResume/)
  // Explicit TransformMode shape — same as plugin-transforms-invariant gate, strongest public contract for composition topology
  assert.match(pt, /type\s+private\s+TransformMode/)
  assert.match(pt, /\|\s*ExplicitResumeDisclosure/)
  assert.match(pt, /\|\s*StrengthReplica\s+of\s+StrengthReplicaRuntime/)
  assert.match(pt, /\|\s*Ordinary/)
  assert.match(pt, /let\s+private\s+determineTransformMode/)
  assert.match(pt, /match\s+determineTransformMode/)
  assert.doesNotMatch(pt, /let\s+private\s+isExplicitResumeProviderMaterial/)
})
