import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, readdirSync, readFileSync } = await import("node:fs");
const { join, resolve } = await import("node:path");
const eventCodec = await import("../../../dist/Persistence/EventStore/CodecSurface.js");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");
const { assertEffectIsInjected, assertFatalBoundary, assertPureContract } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");

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
test('WHAT[durable-events-023] canonical codec and owner folds reject physical store and outer-union authority', () => {
  assertPureContract()
  assertEffectIsInjected('file-system')
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
      assert.equal(result.midState, false, 'mid-commit state must stay absent')
      assert.equal(result.midCompanion, false)
      assert.equal(result.postMember, true, 'after release both handle slices are visible')
      assert.equal(result.postLinked, true)
      assert.equal(result.postState, true, 'ReadView must carry the session state the commit created')
    }),
  ))
test('WHAT[durable-events-023] EXEC_revision_waiter_wakes_on_next_commit', () =>
  withJournalDir('wait', (commonDir, tag) =>
    Promise.race([
      surface.revisionWaitScenario(commonDir, tag),
      new Promise((_, reject) => setTimeout(() => reject(new Error('revision waiter hung past 8s')), 8000)),
    ]).then((result) => {
      assert.equal(result.committed, 'Ok')
      assert.equal(result.resolved, true, 'the registered waiter must resolve through the commit, not a poll')
      assert.ok(result.changeRevision > 0, 'the woken waiter must carry the new revision')
      assert.equal(result.changeRevision, result.currentRevision, 'the wake revision is the live revision')
      assert.equal(result.observedHandle, true, 'the member reads the committed state after the wake')
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
}
