// requirements/speculative-investigation/tests/strength-replay-surface.test.mjs — WHAT[SPEC-INV-008]
//
// Verifies that StrengthReplay owns applyBeforeXTrace entry point for replay before xtrace.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[SPEC-INV-008] StrengthReplay owns applyBeforeXTrace entry point for replay before xtrace', () => {
  const replay = read('src/Wanxiangshu/Strength/OpenCode/Replay.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(replay, /let\s+applyBeforeXTrace/)
  assert.match(replay, /plansOrFailClosed/)
  assert.match(replay, /XTraceProjection\.tryContiguousHostRange/)
  assert.match(replay, /XTraceProjection\.orderedSemanticParts/)
  assert.doesNotMatch(replay, /XTraceProjection\.(?:tryHostMessageId|parts|currentGenerationParts)|XTracePartRef/)
  assert.doesNotMatch(replay, /stableHostIdOfProvenance|IndexOf\("\\\/part:|isContiguousFromFirst/)
  assert.match(pt, /StrengthReplay\.applyBeforeXTrace/)
})
