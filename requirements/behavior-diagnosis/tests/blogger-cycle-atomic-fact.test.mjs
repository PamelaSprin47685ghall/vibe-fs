// Split from tests/unit/enforcer/blogger-convergence-gaps.test.mjs (cutover Wave 2a); owner: behavior-diagnosis.
//
// BD-012: BlogObservationCommitted is the single atomic cycle fact. The
// remaining C0 convergence-chain assertions moved to context-compression
// (blogger-convergence-gaps.test.mjs).

import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = new URL('../../../', import.meta.url).pathname

const prodText = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[BD-012] C0_no_EnforcementCycleCommitted_fact', () => {
  const fact = prodText('src/Wanxiangshu/Composition/Durable/Fact.fs')
  assert.equal(
    /\| EnforcementCycleCommitted\b/.test(fact),
    false,
    'EnforcementCycleCommitted must stay deleted; BlogObservationCommitted is the atomic fact',
  )
  // FactCodec may list it only as a pre-0.5.0 refuse marker (escaped JSON case name).
  const codec = prodText('src/Wanxiangshu/Persistence/Journal/FactCodec.fs')
  assert.ok(
    codec.includes('EnforcementCycleCommitted') && /pre050Markers|pre-0\.5\.0/.test(codec),
    'FactCodec must keep the legacy refuse marker for old journals',
  )
})
