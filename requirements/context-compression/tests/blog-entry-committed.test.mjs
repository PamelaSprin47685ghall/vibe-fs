// Split from tests/unit/journal/blog-entry-committed.test.mjs (cutover Wave 2a); owner: context-compression
//
// Blog coverage-law half of the ENFORCER-045 atomic fold: coverage strictly
// advances across commits, a zero-advance entry is refused (CTX-011), and a
// squash stays coverage-neutral (CTX-012, CONTEXT-COMPRESSION-011/014).
// The enforcement-projection half lives in
// behavior-diagnosis/tests/blog-entry-committed.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Context/Companion/Blogger/FrameSurface.js'

const entryWithEnforcement = ({
  epoch = 0,
  from,
  to,
  cutoffFrom,
  cutoffTo,
  digest = `d-${cutoffTo}`,
  n = 1,
}) => ({
  epoch,
  previous: from,
  next: to,
  previousCutoff: cutoffFrom,
  nextCutoff: cutoffTo,
  digest,
  frame: blog.frame({ kind: 'Entry', digest: `sha-e${n}`, ref: `blob-e${n}`, coveredFrom: from, coveredThrough: to }),
})

const foldOk = (requests) => {
  let state = blog.empty
  for (const request of requests) {
    const result = blog.applyEntry(request, state)
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }
  return { Blog: state }
}

const foldErr = (requests) => {
  let state = blog.empty
  for (const request of requests) {
    const result = blog.applyEntry(request, state)
    if (!result.ok) return result.error
    state = result.value
  }
  assert.fail('expected fold rejection')
}


// ── ENFORCER-045: coverage laws ─────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-015] ENFORCER_045_coverage_strictly_advances_across_commits', () => {
  const s = foldOk([
    entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1, run: 'msg_r1' }),
    entryWithEnforcement({ from: 1, to: 3, cutoffFrom: 1, cutoffTo: 2, n: 2, run: 'msg_r2' }),
  ])

  assert.equal(blog.frameCount(s.Blog), 2)
  assert.equal(blog.coverage(s.Blog).ingestedThroughSequence, 3)
  assert.equal(blog.coverage(s.Blog).cutoff, 2)
})

test('WHAT[CONTEXT-COMPRESSION-015] ENFORCER_045_zero_advance_rejected', () => {
  const error = foldErr([
    entryWithEnforcement({ from: 1, to: 1, cutoffFrom: 0, cutoffTo: 0, n: 1, run: 'msg_zero' }),
  ])
  assert.ok(error, 'zero advance must be rejected (CTX-011)')
})

// ── squash stays coverage-neutral ───────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_does_not_advance_coverage', () => {
  const squash = {
    previousEpoch: 0,
    nextEpoch: 1,
    count: 1,
    frame: blog.frame({
      kind: 'Squash',
      digest: 'sha-e1',
      ref: 'blob-s1',
      coveredFrom: 0,
      coveredThrough: 2,
    }),
  }

  let state = blog.empty
  state = foldOk([entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1 })]).Blog
  const squashResult = blog.applySquash(squash, state)
  assert.equal(squashResult.ok, true, squashResult.ok ? '' : squashResult.error)
  state = squashResult.value

  assert.equal(blog.coverage(state).ingestedThroughSequence, 1, 'coverage unchanged by squash')
  assert.equal(blog.coverage(state).cutoff, 1)
  assert.equal(blog.frameCount(state), 1, 'squash replaced frame, not appended')
  assert.deepEqual(blog.frameKinds(state), ['Squash'])
})
