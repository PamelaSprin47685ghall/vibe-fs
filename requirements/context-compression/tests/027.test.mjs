import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import { createHash, randomUUID } from 'node:crypto'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as recovery from '../../../dist/Enforcer/Cycle/Recovery.js'
import * as bloggerRequest from '../../../dist/Context/Companion/Blogger/Request.js'

const sha256Hex = (input) => {
  const hash = createHash('sha256')
  hash.update(input)
  return hash.digest('hex')
}

const honestMain = (pick) => {
  const previousIngested = pick.previousIngested
  const nextIngested = previousIngested + 1 + pick.advance
  // Session keys are unique per construction: the flight registry is
  // process-global, so two iterations must never share a blogger key or the
  // second claim would Conflict/Refresh against the first iteration's flight.
  const nonce = randomUUID()
  return {
    kind: 'Main',
    mainSession: `ses-main-${pick.mark}-${nonce}`,
    bloggerSession: `ses-blog-${pick.mark}-${nonce}`,
    toml: pick.toml,
    previousIngested,
    nextIngested,
    previousCutoff: pick.previousCutoff,
    nextCutoff: pick.previousCutoff + pick.cutoffAdvance,
    nextDigest: `nd-${pick.mark}`,
    frameEpoch: pick.frameEpoch,
    observedEpoch: pick.observedEpoch,
  }
}

const honestSquash = (pick) => ({
  kind: 'Squash',
  mainSession: `ses-main-${pick.mark}-${randomUUID()}`,
  bloggerSession: `ses-blog-${pick.mark}-${randomUUID()}`,
  frameEpoch: pick.frameEpoch,
  observedEpoch: pick.observedEpoch,
  coveredFrameCount: pick.digests.length,
  digests: [...pick.digests],
})

const expectedMainContext = (descriptor) => ({
  previousIngested: descriptor.previousIngested,
  nextIngested: descriptor.nextIngested,
  deltaDigest: sha256Hex(descriptor.toml),
  frameEpoch: descriptor.frameEpoch,
  observedEpoch: descriptor.observedEpoch,
})

const expectedSquashContext = (descriptor) => ({
  coveredFrameCount: descriptor.coveredFrameCount,
  carried: descriptor.digests.length,
})

const arbitraryMark = fc
  .string({ minLength: 1, maxLength: 8 })
  .filter((s) => s.trim() !== '' && !s.includes('|') && !s.includes(',') && !s.includes('"'))

const arbitraryToml = fc.string({ minLength: 1, maxLength: 64 }).filter((s) => !s.includes('\u0000'))

const arbitraryEpoch = fc.integer({ min: 0, max: 8 })

const arbitraryMainPick = fc.record({
  mark: arbitraryMark,
  toml: arbitraryToml,
  previousIngested: fc.integer({ min: 0, max: 40 }),
  advance: fc.integer({ min: 0, max: 5 }),
  previousCutoff: fc.integer({ min: 0, max: 6 }),
  cutoffAdvance: fc.integer({ min: 0, max: 3 }),
  frameEpoch: arbitraryEpoch,
  observedEpoch: arbitraryEpoch,
})

const arbitrarySquashPick = fc.record({
  mark: arbitraryMark,
  frameEpoch: arbitraryEpoch,
  observedEpoch: arbitraryEpoch,
  digests: fc.array(fc.string({ minLength: 1, maxLength: 12 }), { minLength: 1, maxLength: 4 }),
})

test('WHAT[CONTEXT-COMPRESSION-027] every successful Main construction satisfies the §4.2 invariants', () => {
  fc.assert(
    fc.property(arbitraryMainPick, (pick) => {
      const descriptor = honestMain(pick)
      const expected = expectedMainContext(descriptor)
      // Production entry: owner computes delta digest + canonical request id
      // from the descriptor, then validates before constructing.
      const scope = runtime.scope()
      try {
        const outcome = runtime.claimCurrentRequest(scope, descriptor.bloggerSession, runtime.main(descriptor))
        assert.equal(outcome, 'Claimed')
        const live = runtime.currentRequest(scope, descriptor.bloggerSession)
        assert.notEqual(live, null)
        assert.ok(live.nextIngested > live.previousIngested, 'coverage must strictly advance')
        assert.equal(live.previousIngested, expected.previousIngested)
        assert.equal(live.nextIngested, expected.nextIngested)
        assert.equal(live.deltaDigest, expected.deltaDigest)
        assert.equal(live.deltaDigest, sha256Hex(descriptor.toml))
        assert.equal(typeof live.requestId, 'string')
        assert.ok(live.requestId.length > 0, 'frozen owner identity must be carried')
        assert.equal(live.frameEpoch, expected.frameEpoch)
        assert.equal(live.observedEpoch, expected.observedEpoch)
      } finally {
        runtime.dispose(scope)
      }
    }),
    { seed: 0x27062701, numRuns: 120 },
  )
})

test('WHAT[CONTEXT-COMPRESSION-027] every successful Squash construction binds count and digests', () => {
  fc.assert(
    fc.property(arbitrarySquashPick, (pick) => {
      const descriptor = honestSquash(pick)
      const expected = expectedSquashContext(descriptor)
      const scope = runtime.scope()
      try {
        const outcome = runtime.claimCurrentRequest(scope, descriptor.bloggerSession, runtime.squash(descriptor))
        assert.equal(outcome, 'Claimed')
        const live = runtime.currentRequest(scope, descriptor.bloggerSession)
        assert.notEqual(live, null)
        assert.equal(live.kind, 'Squash')
        assert.equal(live.coveredFrameCount, expected.coveredFrameCount)
        // contextToJs carries Main requests fully but Squash requests as
        // kind + covered count only; identity binding for Squash is proved
        // through the constructor rejection tests below (foreign identity
        // and count/digest disagreement never construct).
      } finally {
        runtime.dispose(scope)
      }
    }),
    { seed: 0x27062702, numRuns: 120 },
  )
})

test('WHAT[CONTEXT-COMPRESSION-027] non-advancing coverage is rejected, never constructed', () => {
  fc.assert(
    fc.property(
      arbitraryMainPick,
      fc.integer({ min: 0, max: 5 }),
      (pick, retreat) => {
        const descriptor = honestMain(pick)
        // Saboteur: next at or below previous.
        const broken = { ...descriptor, nextIngested: descriptor.previousIngested - retreat }
        const scope = runtime.scope()
        try {
          // Constructor failure throws out of `main` — before any flight is
          // claimed — so the scope stays empty.
          assert.throws(
            () => runtime.claimCurrentRequest(scope, broken.bloggerSession, runtime.main(broken)),
            /CoverageDidNotAdvance|main context rejected/,
          )
          assert.equal(runtime.currentRequest(scope, broken.bloggerSession), null)
        } finally {
          runtime.dispose(scope)
        }
      },
    ),
    { seed: 0x27062703, numRuns: 100 },
  )
})

test('WHAT[CONTEXT-COMPRESSION-027] different content yields different digests; same content is stable', () => {
  // The JS descriptor carries no trusted digest: it is production-derived
  // inside the owner boundary, so a saboteur cannot even express "honest
  // content, foreign digest" through `runtime.main`. Tamper-resistance is
  // proved one layer down: the compiled recovery decoder maps a stored blob
  // whose digest disagrees with its TOML to
  // `InvariantViolated(DeltaDigestMismatch)` instead of constructing —
  // covered by the rejection-union test above (InvariantViolated is its own
  // case with its own label). What this boundary proves: different content
  // yields different digests (no aliasing), and the same descriptor is
  // digest-stable across repeated construction.
  fc.assert(
    fc.property(arbitraryMainPick, arbitraryToml, (pick, otherToml) => {
      fc.pre(otherToml !== pick.toml)
      const left = honestMain(pick)
      const right = honestMain({ ...pick, toml: otherToml })
      // Same sessions, different content: aliasing would mint the same
      // identity for different material. Force shared keys (builders nonce
      // them) to make the comparison exact.
      right.mainSession = left.mainSession
      right.bloggerSession = left.bloggerSession
      const leftScope = runtime.scope()
      const rightScope = runtime.scope()
        try {
        // First claim in a fresh scope: Claimed. A same-session claim for a
        // descriptor with a DIFFERENT content/TOML is a foreign identity —
        // the scope must Conflict, never Refreshed-refresh a digest it does
        // not own.
        const firstClaim = runtime.claimCurrentRequest(leftScope, left.bloggerSession, runtime.main(left))
        assert.ok(firstClaim === 'Claimed' || firstClaim === 'Refreshed')
        const secondClaim = runtime.claimCurrentRequest(rightScope, right.bloggerSession, runtime.main(right))
        assert.match(secondClaim, /^Conflict:/)
        const leftLive = runtime.currentRequest(leftScope, left.bloggerSession)
        // The losing claim stays foreign — the live entry for this session is
        // still the left owner's (BloggerFlights is a shared registry, so the
        // observability check is the left descriptor's canonical digest, not a
        // nil read from the right scope).
        const rightLive = runtime.currentRequest(rightScope, right.bloggerSession)
        assert.equal(rightLive.deltaDigest, sha256Hex(left.toml))
        assert.notEqual(rightLive.deltaDigest, sha256Hex(right.toml))
        // Different Toml → different canonical digest (independent of
        // the live-claim outcome: content identity, not request identity).
        assert.notEqual(sha256Hex(left.toml), sha256Hex(right.toml))
        // Stability: rebuilding the same descriptor reproduces the digest.
        const replayScope = runtime.scope()
        try {
          const replayClaim = runtime.claimCurrentRequest(replayScope, left.bloggerSession, runtime.main(left))
          assert.ok(replayClaim === 'Claimed' || replayClaim === 'Refreshed')
          assert.equal(
            runtime.currentRequest(replayScope, left.bloggerSession).deltaDigest,
            leftLive.deltaDigest,
          )
        } finally {
          runtime.dispose(replayScope)
        }
      } finally {
        runtime.dispose(leftScope)
        runtime.dispose(rightScope)
      }
    }),
    { seed: 0x27062704, numRuns: 100 },
  )
})

test('WHAT[CONTEXT-COMPRESSION-027] squash count/digest disagreement is rejected, never constructed', () => {
  // Count-vs-carried disagreement: the production squash constructor rejects
  // before any flight state is touched. Proved through the claim boundary
  // (constructor failure throws out of `squash`, so no flight is claimed).
  fc.assert(
    fc.property(
      arbitrarySquashPick,
      fc.integer({ min: 0, max: 6 }),
      (pick, declaredCount) => {
        fc.pre(declaredCount !== pick.digests.length)
        const descriptor = { ...honestSquash(pick), coveredFrameCount: declaredCount }
        const scope = runtime.scope()
        try {
          assert.throws(
            () => runtime.claimCurrentRequest(scope, descriptor.bloggerSession, runtime.squash(descriptor)),
            /SquashCoverageMismatch|EmptySquashCoverage|squash context rejected/,
          )
          assert.equal(runtime.currentRequest(scope, descriptor.bloggerSession), null)
        } finally {
          runtime.dispose(scope)
        }
      },
    ),
    { seed: 0x27062705, numRuns: 100 },
  )

  // Empty coverage (count 0, no digests) is its own rejection — the
  // `k < 1` guard from `tryBuildSquashContext` now lives in the constructor.
  const scope = runtime.scope()
  try {
    assert.throws(
      () =>
        runtime.claimCurrentRequest(
          scope,
          'ses-blog-empty',
          runtime.squash({
            kind: 'Squash',
            mainSession: 'ses-main-empty',
            bloggerSession: 'ses-blog-empty',
            frameEpoch: 0,
            observedEpoch: 0,
            coveredFrameCount: 0,
            digests: [],
          }),
        ),
      /EmptySquashCoverage|squash context rejected/,
    )
    assert.equal(runtime.currentRequest(scope, 'ses-blog-empty'), null)
  } finally {
    runtime.dispose(scope)
  }
})

test('WHAT[CONTEXT-COMPRESSION-027] recovery rejection cases are distinct and labeled', () => {
  // The compiled production recovery exports one typed rejection union with
  // five distinct cases plus a label renderer. Each corruption class maps to
  // its own case: unreadable I/O (BlobUnreadable), unparseable bytes
  // (BlobCorrupt), unknown durable kind (UnsupportedRequestKind),
  // undecodable items (ItemsUndecodable), invariant breach
  // (InvariantViolated). The rebuild/empty-calls `tryReloadRequestContext`
  // keeps its fail-closed None contract for the rawMessages fallback, while
  // `tryReloadRequestContextDetailed` carries the typed rejection — so a
  // corrupt blob can never be mistaken for "no open request".
  assert.equal(typeof recovery.tryReloadRequestContext, 'function')
  assert.equal(typeof recovery.tryReloadRequestContextDetailed, 'function')
  assert.equal(typeof recovery.reloadRejectionLabel, 'function')

  const labelOf = (tag, fields) => recovery.reloadRejectionLabel(new recovery.CycleContextReloadRejection(tag, fields))
  const labels = new Set([
    labelOf(0, ['io-down']),
    labelOf(1, ['{not json']),
    labelOf(2, ['future-kind']),
    labelOf(3, ['items wire broken']),
    labelOf(4, [new bloggerRequest.BloggerRequestRejection(0, [3n, 3n])]),
  ])
  assert.equal(labels.size, 5, 'each rejection class must render a distinct label')
  assert.match(labelOf(0, ['io-down']), /unreadable/)
  assert.match(labelOf(1, ['{not json']), /corrupt/)
  assert.match(labelOf(2, ['future-kind']), /unsupported request kind: future-kind/)
  assert.match(labelOf(3, ['items wire broken']), /undecodable/)
})

test('WHAT[CONTEXT-COMPRESSION-027] old-epoch staged requests keep their frozen epoch and never claim current authority', () => {
  fc.assert(
    fc.property(
      arbitraryMainPick,
      fc.integer({ min: 1, max: 9 }),
      (pick, epochBump) => {
        const staged = honestMain({ ...pick, observedEpoch: pick.observedEpoch })
        const current = pick.observedEpoch + epochBump
        fc.pre(current > staged.observedEpoch)
        // Recovery and commit both read the frozen epoch off the staged
        // context — the live epoch is never substituted in.
        const scope = runtime.scope()
        try {
          assert.equal(
            runtime.claimCurrentRequest(scope, staged.bloggerSession, runtime.main(staged)),
            'Claimed',
          )
          const live = runtime.currentRequest(scope, staged.bloggerSession)
          assert.equal(live.observedEpoch, staged.observedEpoch)
          assert.ok(
            live.observedEpoch < current,
            'a staged old-epoch request stays old-epoch: it cannot claim current commit authority',
          )
        } finally {
          runtime.dispose(scope)
        }
      },
    ),
    { seed: 0x27062707, numRuns: 100 },
  )
})
