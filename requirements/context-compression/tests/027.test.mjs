import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import { createHash, randomUUID } from 'node:crypto'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import { cpSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

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

test('WHAT[context-compression-027] Main construction derives its digest and preserves validated coverage and epochs', () => {
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

test('WHAT[context-compression-027] every successful Squash construction binds count and digests', () => {
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

test('WHAT[context-compression-027] non-advancing coverage is rejected, never constructed', () => {
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

test('WHAT[context-compression-027] different content yields different digests; same content is stable', () => {
  // Live construction derives the digest; stored-digest tampering is covered
  // separately through the actual recovery decoder below.
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

test('WHAT[context-compression-027] squash count/digest disagreement is rejected, never constructed', () => {
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

const withJournal = async (run) => {
  const directory = mkdtempSync(join(tmpdir(), 'blogger-reload-'))
  const opened = await journal.JournalSurface_boot(directory, 'reload-runtime', 4242, '9999-01-01T00:00:00Z')
  assert.equal(opened.ok, true)
  try { return await run(opened.journal) }
  finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}
const validStoredMain = {
  toml: 'raw work', delta_digest: sha256Hex('raw work'), items: [],
  prev_ingest: 1, next_ingest: 3, prev_cutoff: 1, next_cutoff: 2, next_prefix_digest: 'prefix',
}
const storedRequest = {
  requestId: 'frozen-request-id', mainSession: 'ses-main', bloggerSession: 'ses-blog',
  requestKind: 'main', observedEpoch: 2, frameEpoch: 3,
  previousIngested: 1, nextIngested: 3, digests: [],
}
const reloadBlob = async (handle, raw, overrides = {}) => {
  const written = await journal.JournalSurface_writePayload(handle, typeof raw === 'string' ? raw : JSON.stringify(raw))
  assert.equal(written.ok, true)
  return blog.reloadRequest(handle.journal, {
    ...storedRequest, contextRef: written.blobRef, contextDigest: written.blobDigest, ...overrides,
  })
}

test('WHAT[context-compression-027] actual stored Main and Squash reload preserve frozen identity and epochs', async () => {
  await withJournal(async (handle) => {
    assert.deepEqual(await reloadBlob(handle, validStoredMain), {
      ok: true, kind: 'Main', requestId: 'frozen-request-id', toml: 'raw work',
      deltaDigest: sha256Hex('raw work'), previousIngested: 1, nextIngested: 3,
      frameEpoch: 3, observedEpoch: 2,
    })
    assert.deepEqual(await reloadBlob(handle, { covered_frame_count: 2 }, { requestKind: 'squash', digests: ['a', 'b'] }), {
      ok: true, kind: 'Squash', requestId: 'frozen-request-id', coveredFrameCount: 2,
      digests: ['a', 'b'], frameEpoch: 3, observedEpoch: 2,
    })
  })
})

test('WHAT[context-compression-027] actual recovery distinguishes unreadable, malformed, unsupported, undecodable and invalid input', async () => {
  await withJournal(async (handle) => {
    const outcomes = [
      [await blog.reloadRequest(handle.journal, { ...storedRequest, contextRef: `blobs/${'0'.repeat(64)}`, contextDigest: 'missing' }), /unreadable/],
      [await reloadBlob(handle, '{broken'), /corrupt/],
      [await reloadBlob(handle, validStoredMain, { requestKind: 'future-kind' }), /unsupported request kind/],
      [await reloadBlob(handle, { ...validStoredMain, items: 'not-an-array' }), /undecodable/],
      [await reloadBlob(handle, { ...validStoredMain, delta_digest: 'wrong-digest' }), /delta digest mismatch/],
      [await reloadBlob(handle, { ...validStoredMain, next_ingest: 1 }), /coverage did not advance/],
      [await reloadBlob(handle, { covered_frame_count: 2 }, { requestKind: 'squash', digests: ['only-one'] }), /squash coverage mismatch/],
    ]
    for (const [outcome, expected] of outcomes) {
      assert.equal(outcome.ok, false)
      assert.match(outcome.error, expected)
    }
  })
})

test('WHAT[context-compression-027] live request construction preserves its supplied epoch', () => {
  fc.assert(
    fc.property(
      arbitraryMainPick,
      fc.integer({ min: 1, max: 9 }),
      (pick, epochBump) => {
        const staged = honestMain({ ...pick, observedEpoch: pick.observedEpoch })
        const current = pick.observedEpoch + epochBump
        fc.pre(current > staged.observedEpoch)
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
            'construction preserves its supplied epoch rather than the unrelated newer number',
          )
        } finally {
          runtime.dispose(scope)
        }
      },
    ),
    { seed: 0x27062707, numRuns: 100 },
  )
})

test.todo('WHAT[context-compression-027] actual commit refuses a restored old-epoch request against a newer live projection; preserving the number alone does not prove authority; GAP-104')

test('WHAT[context-compression-027] wire descriptors cannot bypass the validating constructor at runtime', () => {
  const scope = runtime.createScope()
  try {
    // A wire descriptor is not a context: the only path to a live request is
    // claimCurrentRequest, which routes through the validating constructor.
    // A forged DeltaDigest on the wire never survives: the constructor
    // recomputes it as SHA256(Toml) (WHAT 027: DeltaDigest = SHA256(Toml)).
    const forged = runtime.main({
      kind: 'Main',
      mainSession: 'ses-main-forged',
      bloggerSession: 'ses-blog-forged',
      toml: 'raw work',
      items: [],
      previousIngested: 1,
      nextIngested: 3,
      previousCutoff: 1,
      nextCutoff: 2,
      nextDigest: 'nd-forged',
      frameEpoch: 0,
      observedEpoch: 0,
      deltaDigest: 'FORGED-DIGEST',
    })
    runtime.claimCurrentRequest(scope, 'ses-blog-forged', forged)
    const live = runtime.currentRequest(scope, 'ses-blog-forged')
    assert.equal(live.deltaDigest, sha256Hex('raw work'))
    assert.notEqual(live.deltaDigest, 'FORGED-DIGEST')

    // nextIngested must strictly advance previousIngested; the constructor
    // rejects the record and no partial state is claimed.
    const noAdvance = runtime.main({
      kind: 'Main',
      mainSession: 'ses-main-no-advance',
      bloggerSession: 'ses-blog-no-advance',
      toml: 'raw work',
      items: [],
      previousIngested: 3,
      nextIngested: 3,
      previousCutoff: 1,
      nextCutoff: 2,
      nextDigest: 'nd-no-advance',
      frameEpoch: 0,
      observedEpoch: 0,
    })
    assert.throws(
      () => runtime.claimCurrentRequest(scope, 'ses-blog-no-advance', noAdvance),
      /main context rejected: CoverageDidNotAdvance/,
    )
    assert.equal(runtime.currentRequest(scope, 'ses-blog-no-advance'), null)

    // Squash coverage must be non-empty and match the digest list length.
    const emptySquash = runtime.squash({
      kind: 'Squash',
      mainSession: 'ses-main-empty-squash',
      bloggerSession: 'ses-blog-empty-squash',
      frameEpoch: 0,
      observedEpoch: 0,
      coveredFrameCount: 0,
      digests: [],
    })
    assert.throws(
      () => runtime.claimCurrentRequest(scope, 'ses-blog-empty-squash', emptySquash),
      /squash context rejected/,
    )
    assert.equal(runtime.currentRequest(scope, 'ses-blog-empty-squash'), null)

    // Without a wire requestId the owner identity is the canonical hash of
    // the record contents, frozen and deterministic across claims.
    const honest = runtime.main({
      kind: 'Main',
      mainSession: 'ses-main-honest',
      bloggerSession: 'ses-blog-honest',
      toml: 'raw work',
      items: [],
      previousIngested: 1,
      nextIngested: 3,
      previousCutoff: 1,
      nextCutoff: 2,
      nextDigest: 'nd-honest',
      frameEpoch: 0,
      observedEpoch: 0,
    })
    runtime.claimCurrentRequest(scope, 'ses-blog-honest', honest)
    const first = runtime.currentRequest(scope, 'ses-blog-honest').requestId
    assert.match(first, /^[0-9a-f]{64}$/)
    const secondScope = runtime.createScope()
    try {
      runtime.claimCurrentRequest(secondScope, 'ses-blog-honest', honest)
      assert.equal(runtime.currentRequest(secondScope, 'ses-blog-honest').requestId, first)
    } finally {
      runtime.dispose(secondScope)
    }
  } finally {
    runtime.dispose(scope)
  }
})

async function compileRequestConsumer(consumer) {
  const { compileOwnerProject, planOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')
  const root = resolve(import.meta.dirname, '../../..')
  const sourceRoot = join(root, 'src/Wanxiangshu')
  const projectPath = join(sourceRoot, 'Wanxiangshu.Owner.context-compression.context-companion-fact.fsproj')
  const scratch = mkdtempSync(join(tmpdir(), 'wxs-request-construction-'))
  try {
    const plan = planOwnerCompile({ projectPath })
    const isolatedItems = plan.compileItems.map(source => {
      const destination = join(scratch, 'src', relative(sourceRoot, source))
      mkdirSync(dirname(destination), { recursive: true })
      cpSync(source, destination)
      return destination
    })
    const consumerPath = join(scratch, consumer)
    cpSync(join(import.meta.dirname, 'fixtures/request-construction', consumer), consumerPath)
    return await compileOwnerProject({
      projectPath,
      scratchRoot: join(scratch, 'build'),
      rootPropsPath: join(root, 'Directory.Build.props'),
      compilePlan: { ...plan, compileItems: [...isolatedItems, consumerPath] },
      stdio: 'pipe',
    })
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
}

integrationTest('WHAT[context-compression-027] Fable compiles an external consumer of both validating factories and readonly context members', async () => {
  const result = await compileRequestConsumer('FactoryConsumer.fs')
  assert.equal(result.ok, true, `the real declared request closure must compile with its public factories and readers\n${result.stdout}\n${result.stderr}`)
  assert.equal(result.code, 0)
  assert.equal(result.signal, null)
})

const assertPrivateConstructionRejected = async (consumer, typeName) => {
  const result = await compileRequestConsumer(consumer)
  const diagnostics = `${result.stdout}\n${result.stderr}`
  assert.equal(result.ok, false, `${consumer} must not construct a private context`)
  assert.notEqual(result.code, 0)
  assert.equal(result.signal, null, 'a terminated compiler does not prove a private construction boundary')
  assert.ok(
    diagnostics.split('\n').some(line => line.includes(consumer) && line.includes(typeName) && /not accessible/i.test(line)),
    `the compiler must reject access to ${typeName} at the external construction site\n${diagnostics}`,
  )
}

integrationTest('WHAT[context-compression-027] Fable rejects external direct construction of the private Main record', () =>
  assertPrivateConstructionRejected('DirectMainConsumer.fs', 'BloggerMainRequestContext'))

integrationTest('WHAT[context-compression-027] Fable rejects external direct construction of the private Squash record', () =>
  assertPrivateConstructionRejected('DirectSquashConsumer.fs', 'BloggerSquashRequestContext'))
