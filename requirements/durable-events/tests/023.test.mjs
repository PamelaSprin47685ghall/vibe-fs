import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, readdirSync, readFileSync } = await import("node:fs");
const { join, resolve } = await import("node:path");
const eventCodec = await import("../../../dist/Persistence/EventStore/CodecSurface.js");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const event = ({
  id = '1111111111111111111111111111111111111111',
  payload = { state: 'open' },
} = {}) => ({
  id,
  stream: 'proof/canonical-codec-slice',
  type: 'JobRequested',
  parents: [],
  payload,
  payloadRefs: [],
})
function collectSourceFiles(directory) {
  const found = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) found.push(...collectSourceFiles(path))
    else if (/\.[fs]i?$/.test(entry.name) || entry.name.endsWith('.fs')) found.push(path)
  }
  return found
}

test('WHAT[durable-events-023] canonical codec surface keeps encode decode UTF-8 identity and merge in one fail-closed protocol', () => {
  const left = event()
  const same = event()
  const collision = event({ payload: { state: 'closed' } })
  const distinct = event({ id: '2222222222222222222222222222222222222222' })
  const canonical = eventCodec.encode(left)

  assert.deepEqual(eventCodec.decode(canonical), { ok: true, event: left })
  assert.deepEqual(eventCodec.decodeUtf8Text(Buffer.from(canonical)), { ok: true, text: canonical })
  assert.deepEqual(eventCodec.decodeUtf8(Buffer.from(canonical)), { ok: true, event: left })

  assert.deepEqual(eventCodec.checkIdentity(left, same), { ok: true })
  assert.deepEqual(eventCodec.checkIdentity(left, collision), {
    ok: false,
    error: { code: 'IdentityCollision', eventId: left.id },
  })
  assert.deepEqual(eventCodec.checkIdentity(left, distinct), { ok: true })

  assert.deepEqual(eventCodec.mergeByIdentity([left, same, distinct]), {
    ok: true,
    events: [left, distinct],
  })
  assert.deepEqual(eventCodec.mergeByIdentity([left, collision, distinct]), {
    ok: false,
    error: { code: 'IdentityCollision', eventId: left.id },
  })

  const nonCanonical = canonical.replace('"event_id"', '"stream_id"').replace(/,"stream_id":"[^"]+"/, `,"event_id":"${left.id}"`)
  assert.deepEqual(eventCodec.decode(nonCanonical), {
    ok: false,
    error: { code: 'NonCanonical', reason: 'event bytes are not §5.0 canonical' },
  })
  const invalidUtf8 = Buffer.from([0xc3, 0x20])
  const invalidUtf8Error = {
    ok: false,
    error: { code: 'NonCanonical', reason: 'event bytes are not valid UTF-8' },
  }
  assert.deepEqual(eventCodec.decodeUtf8Text(invalidUtf8), invalidUtf8Error)
  assert.deepEqual(eventCodec.decodeUtf8(invalidUtf8), invalidUtf8Error)
})
test('WHAT[durable-events-023] single-field family folds own their slice and declare no aggregate dependency', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = [...subsystemInventory.projects.values()]

  // These four families only ever wrote their own top-level field, so their
  // `AgentProjectionSet -> ... -> AgentProjectionSet` wrappers are gone and the
  // slice write lives in composition (`ProjectionUpdate.apply*`).
  for (const source of [
    'Execution/Fission/Fold.fs',
    'Interaction/Concern/Fold.fs',
    'Interaction/Attention/Fold.fs',
    'Enforcer/InstitutionalLearning/Fold.fs',
  ])
    assert.equal(
      existsSync(join(SOURCE_ROOT, source)),
      false,
      `${source} must not come back as an aggregate-typed wrapper`,
    )

  // The Change family keeps the fold but reads its own slice: the shard declares
  // no reference to the aggregate projection and the fold names neither the
  // aggregate nor the durable fail-closed report.
  const changeFold = projects.filter((project) => project.shard === 'change-fold')
  assert.equal(changeFold.length, 1, 'change-fold must resolve to exactly one compile shard')
  assert.ok(
    !changeFold[0].references.some((reference) => reference.endsWith('composition-durable-projection.fsproj')),
    'change-fold must not declare composition-durable-projection',
  )
  const changeFoldSources = ['Change/Fold.fs', 'Change/Fold.fsi']
    .map((source) => readFileSync(join(SOURCE_ROOT, source), 'utf8'))
    .join('\n')
  assert.doesNotMatch(changeFoldSources, /\bAgentProjectionSet\b|\bFoldRejection\b/)
})
test('WHAT[durable-events-023] prompt provider companion and context folds decide on their own slices while composition owns the aggregate write', () => {
  // These families span more than one slice, so the fold stays and returns a
  // change list over the slices it owns; the bridge writes it back.
  for (const source of [
    'Interaction/Authority/Fold.fs',
    'Participant/Provider/Attempt/Fallback/ProviderFailureFactFold.fs',
    'Context/Companion/CompanionFactFold.fs',
    // The Context (Blogger) fold writes six slices; it decides which ones move and
    // leaves the aggregate write and the refusal rendering to the bridge.
    'Context/Companion/Blogger/ContextFactFold.fs',
  ]) {
    const text = readFileSync(join(SOURCE_ROOT, source), 'utf8')
    assert.doesNotMatch(
      text,
      /\bAgentProjection(?:Set|s)?\b|\bAgentProjection\.|\bFoldRejection\b|\bProjectionUpdate\b|\bComposition\.Durable\b/,
      `${source} is a domain fold and must not name the aggregate projection, the write algebra, or the spine's rejection`,
    )
  }

  // The session-scoped write helpers are composition's; a domain fold that calls
  // them again would re-invert the dependency this boundary exists to prevent.
  const writeHelper =
    /ProjectionUpdate\.(?:updateSession|updateAuthority|updateCompanion|retireAuxiliaryInjectionVisibility)\b/
  for (const file of collectSourceFiles(SOURCE_ROOT)) {
    const relative = file.slice(SOURCE_ROOT.length + 1)
    if (relative.startsWith('Composition/')) continue
    assert.doesNotMatch(
      readFileSync(file, 'utf8'),
      writeHelper,
      `${relative} is outside composition and must not write the aggregate through ProjectionUpdate`,
    )
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const surface = await import("../../../dist/Verification/JournalPortObservationSurface.js");

const withJournalDir = async (tag, scenario) => {
  const dir = mkdtempSync(join(tmpdir(), `wxs-portobs-${tag}-`))
  execFileSync('git', ['init', '--quiet', dir])
  try {
    return await scenario(join(dir, '.git'), tag)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('WHAT[durable-events-023] EXEC_port_members_read_journal_at_call_time', () =>
  withJournalDir('live-read', (commonDir, tag) =>
    surface.liveReadScenario(commonDir, tag).then((result) => {
      assert.equal(result.folded, true, 'the seeded fact must fold')
      assert.equal(result.openedOk, true, 'the session-opening fact must fold')
      assert.equal(result.pendingBefore, 0, 'no deferred work before the append')
      assert.equal(result.pendingAfter, 1, 'a port built before the append must still read live')
      assert.equal(result.freshAfter, 1, 'a port built after the append reads the same live state')
      assert.equal(result.poisonedBefore, false)
      assert.equal(result.poisonedAfter, false, 'a healthy journal is never poisoned')
      assert.equal(result.stateBefore, false, 'no session state before the opening append')
      assert.equal(result.stateAfter, true, 'ReadView must observe the XTrace slice the opening wrote')
    }),
  ))
test('WHAT[durable-events-023] EXEC_one_commit_moves_every_related_view_together', () =>
  withJournalDir('same-commit', (commonDir, tag) =>
    surface.sameCommitViewScenario(commonDir, tag).then((result) => {
      assert.equal(result.outcome, 'Ok', `commit must succeed, got ${result.outcome}`)
      assert.equal(result.preMember, false)
      assert.equal(result.preLinked, false)
      assert.equal(result.preState, false)
      // While the physical append is parked mid-commit, every affected member
      // must still read the pre-commit projection — a view that tears would
      // already show one slice advanced.
      assert.equal(result.midMember, false, 'mid-commit member must not see the parked handle')
      assert.equal(result.midLinked, false, 'mid-commit view must not see the parked link')
      assert.equal(result.midState, false, 'mid-commit canonical handle state must stay absent')
      assert.equal(result.midCompanion, false)
      assert.equal(result.postMember, true, 'after release both handle slices are visible')
      assert.equal(result.postLinked, true)
      assert.equal(result.postState, true, 'the canonical parent handle state must appear with both derived views')
      assert.equal(result.advancedOnce, true, 'only the parked handle commit advances the accepted setup revision')
    }),
  ))
test('WHAT[durable-events-023] EXEC_revision_waiter_wakes_on_next_commit', { timeout: 8000 }, () =>
  withJournalDir('wait', (commonDir, tag) =>
    surface.revisionWaitScenario(commonDir, tag).then((result) => {
      assert.equal(result.committed, 'Ok')
      assert.equal(result.resolved, true, 'the registered waiter must resolve through the commit, not a poll')
      assert.ok(result.changeRevision > 0, 'the woken waiter must carry the new revision')
      assert.equal(result.changeRevision, result.currentRevision, 'the wake revision is the live revision')
      assert.equal(result.observedHandle, true, 'the member reads the committed state after the wake')
      assert.equal(result.advancedOnce, true, 'the waiter observes exactly the next commit after physical admission setup')
    }),
  ))
test('WHAT[durable-events-023] EXEC_cancelled_waiter_releases_without_stealing_a_commit', () =>
  withJournalDir('cancel', (commonDir, tag) =>
    surface.cancelWaiterScenario(commonDir, tag).then((result) => {
      assert.equal(result.cancelledToNone, true, 'a cancelled waiter resolves to None')
      assert.equal(result.committed, 'Ok')
      assert.equal(result.revisionAdvanced, true, 'the commit still lands and advances revision')
    }),
  ))
test('WHAT[durable-events-023] EXEC_unknown_append_poisons_and_is_never_confirmed', () =>
  withJournalDir('poison', (commonDir, tag) =>
    surface.poisonedUnknownAppendScenario(commonDir, tag).then((result) => {
      assert.equal(result.seededOk, true)
      assert.ok(result.failedOutcome.startsWith('Unknown:'), `uncertain append must report unknown, got ${result.failedOutcome}`)
      assert.equal(result.poisoned, true, 'the port must observe the poisoned writer')
      assert.ok(result.afterOutcome.startsWith('Poisoned:'), `a poisoned writer must refuse later appends, got ${result.afterOutcome}`)
    }),
  ))
integrationTest('WHAT[durable-events-023] isolated compilation rejects physical-store authority in the codec closure', async () => {
  const { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } = await import('node:fs')
  const { createHash } = await import('node:crypto')
  const { dirname: dirnameOf, join: joinPath, relative: relativeOf, resolve: resolveRoot } = await import('node:path')
  const { tmpdir } = await import('node:os')
  const { compileOwnerProject, planOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')
  const ROOT = resolveRoot(import.meta.dirname, '../../..')
  const SOURCE_ROOT = joinPath(ROOT, 'src/Wanxiangshu')
  const CODEC_SHARD = joinPath(SOURCE_ROOT, 'Wanxiangshu.Owner.durable-events.persistence-eventstore-canonicalcodec.fsproj')
  const STORE_SHARD = joinPath(SOURCE_ROOT, 'Wanxiangshu.Owner.durable-convergence.persistence-eventstore-processeventlog.fsproj')

  // The probe references the real physical-store factory
  // (Wanxiangshu.Persistence.EventStore.EventStore.createLocal, a
  // RequireQualifiedAccess module member). The same probe source is used in
  // both closures so the only variable is which dependencies are in scope.
  const PROBE_SOURCE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Persistence.EventStore',
    '',
    'module ProbeStoreUsage =',
    '    let factory = EventStore.createLocal',
    '',
  ].join('\n')

  // Source isolation: every compile input resolves to a copy under a temp
  // root, so the real workspace is never written and a crash mid-test cannot
  // leave the tree mutated. compileOwnerProject's scratchRoot only isolates
  // outputs; the compile items themselves are remapped here.
  const isolate = (plan) => {
    const iso = mkdtempSync(joinPath(tmpdir(), 'wxs-023-iso-'))
    const items = plan.compileItems.map((item) => {
      const dest = joinPath(iso, 'src', relativeOf(SOURCE_ROOT, item))
      mkdirSync(dirnameOf(dest), { recursive: true })
      cpSync(item, dest)
      return dest
    })
    const probe = joinPath(iso, 'probe-store-usage.fs')
    writeFileSync(probe, PROBE_SOURCE)
    return { plan: { ...plan, compileItems: [...items, probe] }, iso }
  }

  // Isolation evidence: the real codec source keeps its bytes and mtime, and
  // the generated project references only isolated copies.
  const realCodec = joinPath(SOURCE_ROOT, 'Persistence/EventStore/CanonicalEventCodec.fs')
  const before = { hash: createHash('sha256').update(readFileSync(realCodec)).digest('hex'), mtime: statSync(realCodec).mtimeMs }

  const scratch = mkdtempSync(joinPath(tmpdir(), 'wxs-023-compile-'))
  const positiveIso = isolate(planOwnerCompile({ projectPath: STORE_SHARD }))
  const negativeIso = isolate(planOwnerCompile({ projectPath: CODEC_SHARD }))
  try {
    // Positive: the store closure legitimately contains Store.fs, so the
    // probe compiles — the symbol and its qualified usage are valid.
    const positive = await compileOwnerProject({
      projectPath: STORE_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: positiveIso.plan,
    })
    assert.equal(positive.ok, true, 'probe compiles in the store closure (symbol and usage are valid): ' + String(positive.stdout ?? '').slice(-200))

    // Negative: the codec closure has no Store.fs, so the same probe fails.
    // The diagnostic must name the target symbol, not just any error.
    const negative = await compileOwnerProject({
      projectPath: CODEC_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: negativeIso.plan,
    })
    assert.equal(negative.ok, false, 'the same probe must fail in the codec closure')
    assert.match(
      String(negative.stdout ?? '') + String(negative.stderr ?? ''),
      /'EventStore' is not defined/,
      'the diagnostic names the physical-store module',
    )

    // Isolation evidence: the real source was never touched.
    const after = { hash: createHash('sha256').update(readFileSync(realCodec)).digest('hex'), mtime: statSync(realCodec).mtimeMs }
    assert.deepEqual(after, before, 'the real codec source is untouched (bytes and mtime)')
  } finally {
    rmSync(scratch, { recursive: true, force: true })
    rmSync(positiveIso.iso, { recursive: true, force: true })
    rmSync(negativeIso.iso, { recursive: true, force: true })
  }

  // Static boundary supplement (read-only): the codec shard declares no
  // reference to the physical store family.
  const { readCompileShardInventory } = await import('../../../scripts/lib/compile-shards.mjs')
  const inventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const codec = [...inventory.projects.values()].find((p) => p.explicitCompileShard === 'eventstore-canonical-codec')
  assert.ok(codec, 'codec shard resolves in the inventory')
  const forbidden = codec.references.filter((reference) =>
    /eventstore-(store|merge-runtime|integrator-engine|process-log|git|writer|handle)\b/i.test(reference))
  assert.deepEqual(forbidden, [], 'codec shard declares no physical-store reference')
})

test.todo('WHAT[durable-events-023] isolated compilation rejects aggregate authority in domain folds (GAP-149: domain-fold compile proof pending — the codec closure proof above does not cover domain folds)')
}
