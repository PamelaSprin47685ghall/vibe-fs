import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import * as tr from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

const root = resolve(import.meta.dirname, '../../..')

const read = (p) => readFileSync(resolve(root, p), 'utf8')

const fissionProduction = () => [
  'src/Wanxiangshu/Execution/Fission/Model.fs',
  'src/Wanxiangshu/Execution/Fission/Admission.fs',
  'src/Wanxiangshu/Execution/Fission/Runtime.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
].map(read).join('\n')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors', () => {
  const code = fissionProduction()
  assert.doesNotMatch(code, /session\s*\.\s*fork|\/session\/[^"']*\/fork|CreateForkedSession|ForkSession/i)

  const facts = read('src/Wanxiangshu/Execution/Fission/Facts.fs')
  // The aggregate-typed `Fold.fs` wrapper is gone (2026-09-12); the replay
  // anchors live in the slice-owning projection fold, which is what this
  // assertion has always been about.
  const fold = read('src/Wanxiangshu/Execution/Fission/Projection.fs')
  assert.match(facts, /FissionAdmitted/)
  assert.match(facts, /FissionLaneMaterialized/)
  assert.match(facts, /FissionCompletionDelivered/)
  assert.match(facts, /FissionTakeoverClaimed/)
  assert.match(facts, /FissionTakeoverStarted/)
  assert.match(facts, /FissionConverged/)
  assert.match(fold, /FissionAdmitted/)
  assert.match(fold, /FissionTakeoverClaimed/)
  assert.match(fold, /FissionTakeoverStarted/)
  assert.match(fold, /FissionConverged/)
})
