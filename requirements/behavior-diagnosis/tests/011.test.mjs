import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import { inspectEnforcerBoundsSources } from '../../../scripts/lib/enforcer-bounds-owner.mjs'
import { call, decode } from './support/cycle.mjs'

const MAX_BLOG_TEXT_BYTES = 512 * 1024

const MAX_EVIDENCE_BYTES = 128 * 1024

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

test('WHAT[behavior-diagnosis-011] bounds ownership gate rejects consumer and newly added decoys', () => {
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

test('WHAT[behavior-diagnosis-011] ENFORCER_043_canonical_text_over_512KiB_fails_closed', () => {
  const result = enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES + 1), undefined)
  assert.equal(result.ok, false)
  assert.match(result.error, /MaxBlogTextBytes=524288/)
})

test('WHAT[behavior-diagnosis-011] ENFORCER_043_legacy_evidence_compatibility_bound_remains_128KiB', () => {
  const result = enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES + 1))
  assert.equal(result.ok, false)
  assert.match(result.error, /MaxEvidenceBytes=131072/)
})

test('WHAT[behavior-diagnosis-011] ENFORCER_042_bound_constants_match_utf8_byte_thresholds', () => {
  assert.equal(enforcer.maxBlogTextBytes, MAX_BLOG_TEXT_BYTES)
  assert.equal(enforcer.maxEvidenceBytes, MAX_EVIDENCE_BYTES)
  assert.equal(enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES), undefined).ok, true)
  assert.equal(enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES)).ok, true)
  assert.equal(enforcer.validateBounds('a'.repeat(MAX_BLOG_TEXT_BYTES + 1), undefined).ok, false)
  assert.equal(enforcer.validateBounds('work', 'b'.repeat(MAX_EVIDENCE_BYTES + 1)).ok, false)
  assert.equal(enforcer.validateBounds('界'.repeat(Math.floor(MAX_BLOG_TEXT_BYTES / 3) + 1), undefined).ok, false)
})

test('WHAT[behavior-diagnosis-011] current text and legacy evidence compatibility bounds remain byte-exact', () => {
  for (const [limit, field] of [[MAX_BLOG_TEXT_BYTES, 'text'], [MAX_EVIDENCE_BYTES, 'evidence']]) {
    const content = '界'.repeat(Math.floor(limit / 3)) + 'x'.repeat(limit % 3)
    assert.equal(Buffer.byteLength(content, 'utf8'), limit)
    const valid = field === 'text' ? enforcer.validateBounds(content, '') : enforcer.validateBounds('work', content)
    assert.equal(valid.ok, true)
    assert.equal(valid[field === 'text' ? 'textBytes' : 'evidenceBytes'], limit)
    const invalid = field === 'text' ? enforcer.validateBounds(content + 'x', '') : enforcer.validateBounds('work', content + 'x')
    assert.equal(invalid.ok, false)
  }
})

const cycleCall = (overrides = {}) =>
  call({
    state: {
      status: 'completed',
      input: {
        charge: 'resolve the current question',
        occurrence: 'work',
        settlement: 'result settled',
        consequence: 'continue on the settled path',
        tipName: 'primitive-obsession',
        whatToDoNow: 'take the next concrete step',
        whyThisMatters: 'the boundary decides later work',
        repairStrategy: 'smallest complete repair first',
        decisionBranches: 'follow the branch that fits',
        commonWrongFixes: 'skipping the boundary fails later',
        verification: 'the check must catch a wrong fix',
        doneWhen: 'the boundary holds under the check',
        ...overrides,
      },
    },
  })

test('WHAT[behavior-diagnosis-011] production decode chain rejects tip sections text over 512KiB with its own message', () => {
  const result = decode([cycleCall({ whatToDoNow: 'a'.repeat(MAX_BLOG_TEXT_BYTES + 1) })])
  assert.equal(result.decodedCalls, 1, 'the codec puts no byte cap on a single section; the bounds gate rejects this call')
  assert.equal(result.decision.ok, false)
  assert.equal(
    result.decision.error,
    `blog cycle tip sections exceed MaxBlogTextBytes=${MAX_BLOG_TEXT_BYTES}`
  )
})

test('WHAT[behavior-diagnosis-011] production decode chain rejects merged text over 512KiB before the tip sections axis', () => {
  const result = decode([cycleCall({ charge: 'a'.repeat(MAX_BLOG_TEXT_BYTES + 1) })])
  assert.equal(result.decodedCalls, 1)
  assert.equal(result.decision.ok, false)
  assert.equal(
    result.decision.error,
    `blog cycle text exceeds MaxBlogTextBytes=${MAX_BLOG_TEXT_BYTES}`
  )
})

test('WHAT[behavior-diagnosis-011] production decode chain accepts seven sections whose rendered text stays inside 512KiB', () => {
  const boundedSection = 'a'.repeat(Math.floor((MAX_BLOG_TEXT_BYTES - 1024) / 7))
  const result = decode([cycleCall({
    whatToDoNow: boundedSection,
    whyThisMatters: boundedSection,
    repairStrategy: boundedSection,
    decisionBranches: boundedSection,
    commonWrongFixes: boundedSection,
    verification: boundedSection,
    doneWhen: boundedSection,
  })])
  assert.equal(result.decodedCalls, 1)
  assert.equal(result.decision.ok, true)
})
