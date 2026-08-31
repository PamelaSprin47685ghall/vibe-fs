// Enforcer fail-closed content bounds on the exact-one commit path.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import { inspectEnforcerBoundsSources } from '../../../scripts/lib/enforcer-bounds-owner.mjs'

const MAX_BLOG_TEXT_BYTES = enforcer.maxBlogTextBytes
const MAX_EVIDENCE_BYTES = enforcer.maxEvidenceBytes

const ownedSources = () => [
  {
    path: 'Cycle/Model.fs',
    text: `let MaxBlogTextBytes = 512 * 1024
let MaxEvidenceBytes = 128 * 1024
let validateContentBounds byteCount text evidence = Ok ()`,
  },
  {
    path: 'Cycle/Decode.fs',
    text: 'EnforcerCycle.validateContentBounds LlmFacing.byteCount text evidence',
  },
  {
    path: 'Surface.fs',
    text: 'EnforcerCycle.validateContentBounds LlmFacing.byteCount text evidence',
  },
  { path: 'Host.fs', text: 'let mainContextFromChunk chunk = chunk' },
]

test('WHAT[BD-011] bounds ownership gate rejects consumer and newly added decoys', () => {
  assert.deepEqual(inspectEnforcerBoundsSources(ownedSources()), [])

  for (const decoy of [
    { path: 'Host.fs', text: 'let MaxEvidenceBytes = 128 * 1024' },
    { path: 'Cycle/Decode.fs', text: 'LlmFacing.byteCount text > maxBytes' },
    { path: 'Future/Replica.fs', text: 'let replicaLimit = 524288' },
  ]) {
    const sources = ownedSources().filter(({ path }) => path !== decoy.path).concat(decoy)
    assert.notDeepEqual(inspectEnforcerBoundsSources(sources), [], decoy.path)
  }
})

test('WHAT[BD-011] ENFORCER_043_canonical_text_over_512KiB_fails_closed', () => {
  const result = enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES + 1), undefined)
  assert.equal(result.ok, false)
  assert.match(result.error, /MaxBlogTextBytes=524288/)
})

test('WHAT[BD-011] ENFORCER_043_canonical_evidence_over_128KiB_fails_closed', () => {
  const result = enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES + 1))
  assert.equal(result.ok, false)
  assert.match(result.error, /MaxEvidenceBytes=131072/)
})

test('WHAT[BD-011] ENFORCER_042_bound_constants_match_utf8_byte_thresholds', () => {
  assert.equal(enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES), undefined).ok, true)
  assert.equal(enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES)).ok, true)
  assert.equal(enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES + 1), undefined).ok, false)
  assert.equal(enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES + 1)).ok, false)
  assert.equal(enforcer.validateBounds('界'.repeat(Math.floor(MAX_BLOG_TEXT_BYTES / 3) + 1), undefined).ok, false)
})
