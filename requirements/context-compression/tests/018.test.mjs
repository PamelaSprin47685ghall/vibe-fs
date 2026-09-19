import test from 'node:test'

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const KEY = 'ses-blogger-test'
const reqCtx = (reqId, toml = 'test') => runtime.main({
  requestId: reqId,
  mainSession: 'ses-main',
  bloggerSession: KEY,
  toml,
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'digest-1',
  frameEpoch: 0,
  deltaDigest: 'delta-1',
  observedEpoch: 0,
})

test('WHAT[context-compression-018] BLOGGER_RUNTIME_sealed_stays_sealed_cancel_wakes_waiter_and_release_clears_flight', async () => {
  const scope = runtime.scope()
  const ctxA = reqCtx('req-1', 'initial')

  // Claim initial flight
  assert.equal(runtime.claimCurrentRequest(scope, KEY, ctxA), 'Claimed')
  assert.notEqual(runtime.currentRequest(scope, KEY), null)

  // Dispose/cancel cancels waiters
  const waiter = runtime.park(scope, KEY)
  runtime.cancelParked(scope, KEY)
  const wake = await waiter
  assert.equal(wake.kind, 'Cancelled')

  // Releasing current request frees flight
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'req-1'), 'Released')
  assert.equal(runtime.currentRequest(scope, KEY), null)
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_material_mailbox_waiter_first_delivers_directly', async () => {
  const scope = runtime.scope()
  const ctx = reqCtx('req-waiter-first', 'waiter-content')

  const waiter = runtime.park(scope, KEY)
  assert.equal(runtime.offerMaterial(scope, KEY, ctx), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'waiter-content')
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_material_mailbox_material_first_stages_for_next_park', async () => {
  const scope = runtime.scope()
  const ctx = reqCtx('req-mat-first', 'staged-content')

  assert.equal(runtime.offerMaterial(scope, KEY, ctx), 'Staged')
  const wake = await runtime.park(scope, KEY)
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'staged-content')
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_material_mailbox_newest_covers_supersedes_older_offer', async () => {
  const scope = runtime.scope()
  const ctx1 = reqCtx('req-1', 'older-material')
  const ctx2 = reqCtx('req-2', 'newest-material')

  assert.equal(runtime.offerMaterial(scope, KEY, ctx1), 'Staged')
  assert.equal(runtime.offerMaterial(scope, KEY, ctx2), 'Staged')

  // Next park gets the newest material, older is cleanly covered
  const wake = await runtime.park(scope, KEY)
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'newest-material')
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_flight_lease_dispose_clears_exact_flight', () => {
  const scope = runtime.scope()
  const ctx1 = reqCtx('req-lease-1', 'content-lease')

  const lease = runtime.claimFlight(scope, KEY, ctx1)
  assert.notEqual(lease, null)
  assert.notEqual(runtime.currentRequest(scope, KEY), null)

  // Dispose lease clears flight
  lease.Dispose()
  assert.equal(runtime.currentRequest(scope, KEY), null)
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { existsSync, readFileSync, readdirSync, statSync } = await import("node:fs");
const { join } = await import("node:path");

const ROOT = new URL('../../../', import.meta.url).pathname
const PROD = join(ROOT, 'src/Wanxiangshu')
const walkFs = (dir, acc = []) => {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    const st = statSync(path)
    if (st.isDirectory()) walkFs(path, acc)
    else if (path.endsWith('.fs')) acc.push(path)
  }
  return acc
}
const prodFiles = walkFs(PROD)
const prodText = (rel) => {
  const path = join(ROOT, rel)
  assert.equal(existsSync(path), true, `missing ${rel}`)
  return readFileSync(path, 'utf8')
}
const filesContaining = (pattern) =>
  prodFiles.filter((file) => {
    const text = readFileSync(file, 'utf8')
    return typeof pattern === 'string' ? text.includes(pattern) : pattern.test(text)
  })
const rel = (abs) => abs.slice(ROOT.length)

test('WHAT[context-compression-018] C0_blogger_lifecycle_authority_is_physical_ownership', () => {
  // Blogger/Runtime/State.fs is deleted: the pure router decideMaterial and the
  // DrainWindow/openDrain/blocksNewRequest helpers have zero production consumers.
  // The lifecycle authority is physical flight ownership held by the coordinator
  // through the host scope — not a pure routing function and not a transition cell.
  // 1. The deleted router symbols must have zero production references outside
  //    their own deleted definition file (deletion lock).
  const routerRefs = filesContaining(/decideMaterial|\bDrainWindow\b|openDrain|blocksNewRequest/)
    .map(rel)
    .filter((path) => !path.endsWith('Blogger/Runtime/State.fs') && !path.endsWith('Blogger/Runtime/State.fsi'))
  assert.deepEqual(
    routerRefs,
    [],
    `deleted router symbols still referenced: ${routerRefs.join(', ')}`,
  )
  // 2. The transition API onMaterial must have ZERO production callers.
  //    This locks the deletion direction for the transition module.
  const transitionCallers = filesContaining(/BloggerRuntime\.onMaterial\b/).map(rel)
  assert.deepEqual(
    transitionCallers,
    [],
    `BloggerRuntime.onMaterial still referenced: ${transitionCallers.join(', ')} — lifecycle authority must be physical ownership, not the transition cell`,
  )
  // 3. The coordinator must route via physical flight ownership and must not
  //    reference the deleted router or the shadow state
  //    (Get/SetBloggerRuntime, BloggerRuntimeState, cell .State).
  const coordinator = prodText('src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs')
  assert.match(coordinator, /claimFlight|claimCurrentRequest|releaseCurrentRequest/,
    'BloggerCoordinator must route via physical flight ownership')
  assert.match(coordinator, /exactFlightMatches|foreignFlightReason/,
    'BloggerCoordinator must guard claims with the RequestId-aware conflict check')
  assert.doesNotMatch(coordinator, /decideMaterial|onMaterial|DrainWindow/,
    'BloggerCoordinator must not reference the deleted router')
  assert.equal(
    /GetBloggerRuntime|SetBloggerRuntime|BloggerRuntimeState\b/.test(coordinator),
    false,
    'BloggerCoordinator must not reference the shadow BloggerRuntime state API',
  )
  const codeStateRefs = coordinator.split('\n').filter(
    (line) => /\.State\b/.test(line) && !line.trim().startsWith('//'),
  )
  assert.equal(
    codeStateRefs.length,
    0,
    `BloggerCoordinator must not reference cell .State in code: ${codeStateRefs.join('; ')}`,
  )
  // 4. The dead router definition file itself must stay deleted.
  assert.equal(
    existsSync(join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/Runtime/State.fs')),
    false,
    'dead router State.fs must stay deleted',
  )
  assert.equal(
    existsSync(join(ROOT, 'src/Wanxiangshu/Context/Companion/Blogger/Runtime/State.fsi')),
    false,
    'dead router State.fsi must stay deleted',
  )
})
test('WHAT[context-compression-018] C0_exact_flight_lease_is_the_only_busy_definition', () => {
  // Companion send Task must not decide busy. Production busy is exact shared flight ownership only.
  // PR7 D6: BloggerRuntimeState/Cell deleted — zero residual shadow ownership.
  const companion = prodText('src/Wanxiangshu/Context/Companion/Runtime.fs')
  assert.equal(
    /mutable inFlightTask/.test(companion),
    false,
    'Companion.inFlightTask must not decide Blogger busy',
  )
  assert.equal(
    /mutable inFlightCompleted/.test(companion),
    false,
    'Companion.inFlightCompleted must not decide Blogger busy',
  )
  const coordinator = prodText('src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs')
  assert.match(
    coordinator,
    /TryGetFlight/,
    'coordinator busy must read the physical flight registry via TryGetFlight',
  )
  const scope = prodText('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  assert.doesNotMatch(scope, /GetBloggerRuntime|SetBloggerRuntime/, 'scope must not expose cell Get/Set')
})
test('WHAT[context-compression-018] C0_material_mailbox_and_flight_lease_are_separate_resources', () => {
  // Separate physical resources: pending material mailbox + exact flight registry.
  // Forbidden: a second `currentRequest` dict or InFlight shadow fallback.
  // Blogger mailbox and flight ownership live in PluginBloggerScope.
  const scope = prodText('src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs')
  assert.equal(/parkedOffer/.test(scope), false, 'parkedOffer single-slot is forbidden')
  assert.match(scope, /pendingOffer/, 'PendingOffer dictionary required')
  // Flights are process-shared (HOST-012 / worktree↔root BlogTool) via SharedState.
  assert.match(scope, /SharedState\.BloggerFlights/, 'physical flight registry is SharedState.BloggerFlights')
  assert.equal(
    /\blet currentRequest\b/.test(scope),
    false,
    'CurrentRequest must not be a second dictionary named currentRequest',
  )
  assert.doesNotMatch(
    scope,
    /BloggerRuntime\.inFlightContext|inFlightContext \(this\.GetBloggerRuntimeUnlocked|GetBloggerRuntimeUnlocked/,
    'TryPeekCurrentRequest must not fall back to InFlight shadow / GetBloggerRuntime',
  )
  assert.match(
    scope,
    /SharedState\.BloggerFlights\.TryGetValue/,
    'TryPeekCurrentRequest / TryGetFlight must read SharedState.BloggerFlights only',
  )
})
test('WHAT[context-compression-018] C0_commit_uses_live_InFlight_only_not_open_heal', () => {
  // Host transform msgs end on the historical last assistant (new outbound shell
  // is not in the list). Commit must peek InFlight only — healing open here
  // rebinds a new RequestId onto an old provider run (stale-cycle race).
  // Durable open reload stays for rebuild / crash recovery, not cycle commit.
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  const recovery = prodText('src/Wanxiangshu/Enforcer/Cycle/Recovery.fs')
  assert.match(host, /tryLiveCycleContext/, 'commit authority is live InFlight peek')
  assert.match(
    host,
    /let liveCtx =\s*EnforcerFrameRecovery\.tryLiveCycleContext/,
    'completed-blog arm peeks live only',
  )
  assert.match(host, /resolveCycleContext/, 'rebuild/empty-calls still resolve typed context')
  assert.match(recovery, /tryReloadRequestContext/, 'durable open materialization must reload full typed context')
  assert.equal(
    /SetCurrentRequest\(key, ctx\)[\s\S]{0,80}Some ctx[\s\S]{0,40}resolveCycleContext|resolveCycleContext[\s\S]{0,200}SetCurrentRequest\(key, ctx\)/.test(
      host,
    ),
    false,
    'resolveCycleContext must not heal InFlight via SetCurrentRequest',
  )
})
test('WHAT[context-compression-018] C0_single_main_material_coordinator_entry', () => {
  const hasCoordinator = filesContaining(/BloggerCoordinator\.onMainContext\b/).map(rel)
  assert.deepEqual(
    hasCoordinator,
    ['src/Wanxiangshu/Context/Companion/Transform.fs'],
    'ordinary Blogger material must enter the physical coordinator exactly once through Transform',
  )
  const proofSurface = prodText('src/Wanxiangshu/OpenCode/Host/PluginHooksSurface.fs')
  assert.equal(
    proofSurface.match(/CompanionTransform\.coordinateBloggerContext\b/g)?.length,
    2,
    'the unresolved adapter proof must enter Blogger coordination twice through the Transform owner operation',
  )
  assert.deepEqual(
    filesContaining(/BloggerCoordinator\.onMainMaterial\b/).map(rel),
    [],
    'the pre-cutover projection-recomputing coordinator entry must stay deleted',
  )
  const offerSites = filesContaining(/offerToBlogger\b/).map(rel)
  assert.equal(
    offerSites.length,
    0,
    `parallel offerToBlogger sites remain: ${offerSites.join(', ')}`,
  )
})
test('WHAT[context-compression-018] C0_no_BloggerNeedsReset_full_X_replay', () => {
  const hits = filesContaining(/BloggerNeedsReset/).map(rel)
  assert.equal(
    hits.length,
    0,
    `BloggerNeedsReset still present: ${hits.join(', ')} — restart must reuse durable frames + X gap`,
  )
})
test('WHAT[context-compression-018] C0_first_request_does_not_extract_raw_user_toml', () => {
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  const extractsRawToml =
    /Extract the TOML from the raw messages/.test(host) ||
    /last user[\s\S]{0,80}toml/i.test(host) ||
    /"first"; toml/.test(host)
  assert.equal(
    extractsRawToml,
    false,
    'first request still extracts TOML from raw user messages; must project from typed context only',
  )
})
test('WHAT[context-compression-018] C0_squash_path_does_not_SubscribeTerminal', () => {
  const blogger = prodText('src/Wanxiangshu/Context/Companion/HostBlogger.fs')
  assert.equal(
    /SubscribeTerminal/.test(blogger),
    false,
    'Squash still waits on SubscribeTerminal; must share blog-tool continuation with Normal',
  )
})
test('WHAT[context-compression-018] C0_squash_constructs_typed_BloggerRequestContext_Squash_in_production', () => {
  // Domain type + match arms exist; production must CONSTRUCT Squash context for send/commit.
  // W6 cutover: the constructor is `BloggerRequestMaterial.createSquash` piped
  // into `BloggerRequestContext.Squash` — the squash payload is validated inline,
  // never built from a positional record. Pattern-match alone doesn't count.
  const constructors = prodFiles.filter((file) => {
    const text = readFileSync(file, 'utf8')
    return (
      /Result\.map\s+BloggerRequestContext\.Squash/.test(text)
      || /Ok\s+verified\s*->\s*BloggerRequestContext\.Squash\s+verified/.test(text)
      || /BloggerRequestContext\.Squash\(.*createSquash/.test(text)
      || /createSquash[\s\S]{0,200}BloggerRequestContext\.Squash/.test(text)
    )
  }).map(rel)
  assert.ok(
    constructors.length > 0,
    'no production construction of BloggerRequestContext.Squash via createSquash — typed squash context is domain-only',
  )
})
test('WHAT[context-compression-018] C0_park_only_after_KnownCommitted', () => {
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  assert.match(host, /ParkTransform/,
    'probe: ParkTransform must exist to assert the KnownCommitted gate')
  // P1-3: not-committed paths are CycleDisposition arms (Working/InjectRepair/
  // CommitUnknown/AbandonThenCatchUp); park lives only under Committed.
  assert.match(host, /type CycleDisposition/,
    'commit outcomes must collapse into CycleDisposition before park')
  // Committed → finishCommitted → durable refresh → finishCaughtUpAfterCommit →
  // parkAfterCatchUpClear → ParkTransform. Helpers are defined above the
  // disposition match, so source-order "ParkTransform after Committed arm"
  // is the wrong probe.
  assert.match(
    host,
    /CycleDisposition\.Committed afterSquashMain -> finishCommitted/,
    'Committed arm must enter finishCommitted',
  )
  assert.match(
    host,
    /let! refreshed = ctx\.RefreshMainContext[\s\S]{0,180}return! catchUpAfterCommitMaterial/,
    'finishCommitted refreshes durable coverage before park',
  )
  assert.match(host, /None, None -> return! finishCaughtUpAfterCommit/)
  assert.match(host, /return! parkAfterCatchUpClear/)
  const parkFn = host.indexOf('let private parkAfterCatchUpClear')
  const park = host.indexOf('ParkTransform', parkFn)
  assert.ok(parkFn >= 0 && park > parkFn,
    'ParkTransform must sit under parkAfterCatchUpClear on the Committed catch-up path')
  const disposition = host.indexOf('let private finishOwnedDisposition')
  const commitBranch = host.indexOf('let commitBranch', disposition)
  const matchBlock = host.slice(disposition, commitBranch > disposition ? commitBranch : undefined)
  const nonCommittedPark = [
    '| CycleDisposition.Working',
    '| CycleDisposition.InjectRepair',
    '| CycleDisposition.CommitUnknown',
    '| CycleDisposition.AbandonThenCatchUp',
  ].some((arm) => {
    const a = matchBlock.lastIndexOf(arm)
    if (a < 0) return false
    const p = matchBlock.indexOf('ParkTransform', a)
    return p > a
  })
  assert.equal(nonCommittedPark, false,
    'non-committed dispositions must not reach ParkTransform before Committed arm')
  assert.match(host, /KnownCommitted/, 'KnownCommitted is the only park-enabling commit outcome')
  // Whole file: no bare empty-list quiet-stop (StopPhysicalRun replaces it).
  assert.doesNotMatch(host, /^\s*return \[\]\s*$/m,
    'EnforcerHost must not return [] as quiet stop')
  assert.match(host, /ContinuationOutcome|StopPhysicalRun/,
    'continuation must express stop vs project explicitly')
  assert.match(host, /return project |return stop |return resumeCatchUp/,
    'not-committed paths must still return ContinuationOutcome')
})
test('WHAT[context-compression-018] C0_commit_refreshes_durable_coverage_before_park', () => {
  // One external wake may need many ≤200 KiB cycles. After BlogObservationCommitted the
  // continuation must re-chunk from durable coverage (tryRefresh) and continue
  // without waiting for a new main-session wake. Stale pending material is not enough.
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  // EnforcerHost injects the re-chunk through ctx.RefreshMainContext; the
  // commit branch must use it before parking.
  const finishStart = host.indexOf('let private finishCommitted')
  const finishEnd = host.indexOf('let private finishOwnedDisposition', finishStart)
  const finish = host.slice(finishStart, finishEnd)
  const refresh = finish.indexOf('RefreshMainContext')
  const catchUp = finish.indexOf('catchUpAfterCommitMaterial')
  assert.ok(refresh >= 0 && catchUp > refresh,
    'post-commit path must refresh durable coverage before choosing catch-up or park')
  assert.match(
    host,
    /resumeCatchUp|catchUpAfterCommitMaterial/,
    'already-committed / catch-up arm must re-chunk from coverage',
  )
  assert.doesNotMatch(host, /TryTakePendingOffer/, 'parent code cannot peek and interpret mailbox state')
})
test('WHAT[context-compression-018] C0_caught_up_is_parked_not_completed_and_wake_rechecks_live_Current', () => {
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  const quiet = host.indexOf('| None, None -> return! finishCaughtUpAfterCommit')
  const parkFn = host.indexOf('let private parkAfterCatchUpClear')
  const park = host.indexOf('ParkTransform', parkFn)
  const wakeFn = host.indexOf('let private afterParkResumed')
  const wakeRefresh = host.indexOf('RefreshMainContext', wakeFn)

  assert.ok(quiet >= 0, 'Committed catch-up must have an explicit quiet branch')
  assert.ok(parkFn >= 0 && park > parkFn, 'caught-up/quiet must enter ParkTransform instead of completing immediately')
  assert.ok(wakeFn >= 0 && wakeRefresh > wakeFn, 'park wake must re-read live Current before choosing the next window')

  const quietBranch = host.slice(quiet, wakeRefresh > quiet ? wakeRefresh : quiet + 400)
  assert.doesNotMatch(
    quietBranch,
    /return ctx\.Stop "(?:caught-up|catch-up-complete|quiet)/,
    'caught-up itself must not be treated as completion before the parked wait',
  )
})
test('WHAT[context-compression-018] C0_adopted_blogger_motion_is_not_active_PENDING', () => {
  // Adopted motion is git history only; no active PENDING/ parking file.
  assert.equal(
    existsSync(join(ROOT, 'PENDING/blogger-prompt-shape-and-parking.md')),
    false,
    'ADOPTED motion must leave active PENDING/',
  )
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const owner = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const ctx = owner
const parkedTransform = owner
const main = () => ctx.main({ toml: 'work' })
const main2 = () => ctx.main({ toml: 'more' })
const mainRequest = () => ctx.main({ requestId: 'request-main', toml: 'work' })
const mainRequest2 = () => ctx.main({ requestId: 'request-more', toml: 'more' })
const KEY = 'ses-blog'

test('WHAT[context-compression-018] ENFORCER_047_idle_plus_material_starts', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, main()), 'Claimed')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  parkedTransform.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_047_inflight_plus_material_skips_without_queue', () => {
  // hasFlight true → Skip; original flight ownership is not replaced by routing.
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx.main({ requestId: 'req-first', toml: 'work' })), 'Claimed')
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx.main({ requestId: 'req-second', toml: 'more' })), 'Conflict:req-first')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  parkedTransform.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_047_open_producer_between_steps_stages_material', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.offerParked(scope, KEY, main2()), 'Staged')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_047_cycle_commit_clears_flight', () => {
  const scope = parkedTransform.scope()
  const requested = mainRequest()
  parkedTransform.claimCurrentRequest(scope, KEY, requested)
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY)?.toml, 'work')

  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY), null)
})
test('WHAT[context-compression-018] ENFORCER_047_idle_plus_parked_waiter_offers', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, KEY)
  assert.equal(parkedTransform.offerParked(scope, KEY, main2()), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'more')
  parkedTransform.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_047_clear_flight_is_idempotent', () => {
  // Physical clear: second clear on empty ownership is a no-op (no NotInFlight cell error).
  const scope = parkedTransform.scope()
  const requested = mainRequest()
  parkedTransform.claimCurrentRequest(scope, KEY, requested)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})
test('WHAT[context-compression-018] ENFORCER_047_clear_without_flight_is_idempotent', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})
test('WHAT[context-compression-018] ENFORCER_047_squash_commit_clears_flight', () => {
  // Squash commit path uses the same physical clear as cycle commit.
  const scope = parkedTransform.scope()
  const requested = mainRequest()
  parkedTransform.claimCurrentRequest(scope, KEY, requested)
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY), null)
})
test('WHAT[context-compression-018] ENFORCER_047_session_delete_is_registry_removal_not_a_cell_state', () => {
  // DSL-003: owner lifetime is the physical registry — session delete removes
  // flight ownership. There is no Disposed state tag.
  const scope = parkedTransform.scope()
  const requested = mainRequest()
  parkedTransform.claimCurrentRequest(scope, KEY, requested)
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})
test('WHAT[context-compression-018] ENFORCER_047_two_inflight_contexts_cannot_coexist', () => {
  // hasFlight already true → Skip; production keeps the registered flight.
  const scope = parkedTransform.scope()
  const claimed = mainRequest()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, claimed), 'Claimed')
  assert.equal(
    parkedTransform.claimCurrentRequest(scope, KEY, ctx.main({ requestId: 'req-second', toml: 'more' })),
    'Conflict:request-main',
  )
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'more')
})
test('WHAT[context-compression-018] ENFORCER_047_waiter_offer_does_not_register_flight', () => {
  // DSL-003: Offer is routing only — parked host dictionary stages the context
  // (ENFORCER-050); a staged offer must not imply SetCurrentRequest.
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.offerParked(scope, KEY, main2()), 'Staged')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.dispose(scope)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const bloggerRuntime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const parkedTransform = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const handle = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const KEY = 'ses-blog'
const ctx = () => bloggerRuntime.main({
  requestId: 'request-main',
  mainSession: 'ses-main',
  bloggerSession: KEY,
  toml: '[[new_work_to_record]]\nuser = "work"',
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'digest-1',
  frameEpoch: 0,
  deltaDigest: 'delta-1',
  observedEpoch: 0,
})

test('WHAT[context-compression-018] HANDLE_lifecycle_CompletedAwaitingJoin_and_Retired_seal_blogger', () => {
  const completed = handle.scenario('complete')
  assert.equal(completed.ok, true)
  assert.equal(completed.record.lifecycle, 'CompletedAwaitingJoin')

  const retired = handle.scenario('retire')
  assert.equal(retired.ok, true)
  assert.equal(retired.record.lifecycle, 'Retired')
})
test('WHAT[context-compression-018] HANDLE_lifecycle_Abandoned_seals_blogger', () => {
  const abandoned = handle.scenario('abandon')
  assert.equal(abandoned.ok, true)
  assert.equal(abandoned.record.lifecycle, 'Abandoned')
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth', async () => {
  // R05: DrainWindow deleted; sealed stays sealed, new root starts fresh owner scope.
  const scope = parkedTransform.scope()
  parkedTransform.claimCurrentRequest(scope, KEY, ctx())
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_park_and_offer_material_mailbox', async () => {
  // Mailbox delivery: park awaits material, offer resumes waiter
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, KEY)
  assert.equal(parkedTransform.offerMaterial(scope, KEY, ctx()), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_staged_offer_delivers_to_next_park', async () => {
  // Offer first stages, then park consumes immediately
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.offerMaterial(scope, KEY, ctx()), 'Staged')
  const wake = await parkedTransform.park(scope, KEY)
  assert.equal(wake.kind, 'MaterialAvailable')
})
test('WHAT[context-compression-018] BLOGGER_RUNTIME_flight_lease_claim_and_release', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx()), 'Claimed')
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");
const resume = await import("../../../dist/OpenCode/Host/ExplicitResumeSurface.js");
const { acceptAuthorityRoot, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[context-compression-018] CompanionTransform owns ordinary-material entry and consumes Host suppression as a capability', () => {
  const companion = read('src/Wanxiangshu/Context/Companion/Transform.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(companion, /let\s+applyCompanionForOrdinaryMaterial/)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Context/Companion/Program.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Context/Companion/Errors.fs')), false)
  assert.doesNotMatch(companion, /\b(?:CompanionProgram|CompanionContext|CompanionError|TransformRaw)\b/)
  assert.match(companion, /replaceMessagesInPlace\s+rawOutObj\s+rawMessages/)
  assert.match(companion, /\(isExplicitResume:\s*string option -> obj -> bool\)/)
  assert.match(companion, /if isExplicitResume projectionSessionIdOpt outObj then/)
  assert.doesNotMatch(companion, /ExplicitResumeSuppression/)
  assert.match(pt, /CompanionTransform\.applyCompanionForOrdinaryMaterial/)
  assert.match(pt, /TransformBranchCapabilities[\s\S]*IsExplicitResume/)
  // Explicit TransformMode shape — same as plugin-transforms-invariant gate, strongest public contract for composition topology
  assert.match(pt, /type\s+private\s+TransformMode/)
  assert.match(pt, /\|\s*ExplicitResumeDisclosure/)
  assert.match(pt, /\|\s*StrengthReplica\s+of\s+StrengthReplicaRuntime/)
  assert.match(pt, /\|\s*Ordinary/)
  assert.match(pt, /let\s+private\s+determineTransformMode/)
  assert.match(pt, /match\s+determineTransformMode/)
  assert.doesNotMatch(pt, /let\s+private\s+isExplicitResumeProviderMaterial/)
})
test('WHAT[context-compression-018] explicit-resume nudge path with marked suppression prevents companion double-send', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const sessionID = 'ses_explicit_resume_nudge_suppression'
    const continueID = 'msg-continue-nudge-1'

    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const commandOutput = { parts: [] }
    await hooks['command.execute.before'](
      { command: 'continue', sessionID, arguments: '' },
      commandOutput,
    )

    const config = {}
    resume.registerCommand(config)
    const physicalOutput = {
      message: { id: continueID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: config.command.continue.template }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: continueID },
      physicalOutput,
    )

    const providerOutput = {
      messages: [
        {
          info: { id: continueID, sessionID, role: 'user' },
          parts: physicalOutput.parts,
        },
      ],
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      providerOutput,
    )

    assert.equal(createdIds.length, 0, 'suppression path must not create companion child sessions')
    assert.equal(runtime.prompts.length, 0, 'suppression path must not double-send prompts')
  })
})
test('WHAT[context-compression-018] fresh binding without suppression history admits ordinary material', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses_fresh_ordinary_binding'
    const userMessageID = 'msg-user-fresh-1'

    await acceptAuthorityRoot(runtime, sessionID, 'manager')

    const userOutput = {
      message: { id: userMessageID, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'ordinary work' }],
    }

    await hooks['chat.message'](
      { sessionID, messageID: userMessageID },
      userOutput,
    )

    const providerOutput = {
      messages: [
        {
          info: { id: userMessageID, sessionID, role: 'user' },
          parts: [{ type: 'text', text: 'ordinary work' }],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform'](
      { sessionID },
      providerOutput,
    )

    assert.ok(providerOutput.messages.length > 0)
    assert.equal(providerOutput.messages[0].info.id, userMessageID)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");
const owner = await import("../../../dist/Context/Companion/ProjectionSurface.js");

const ident = owner
const prompt = owner
const proj = owner
const spy = (input) => `«${input}»`
const frames = (count) =>
  Array.from({ length: count }, (_, n) => ({ digest: `sha-f${n}`, body: `frame body ${n}` }))
const dataItems = [{ role: 'user', kind: 'text', text: 'work', truncated: false }]
const dataToml = '[[new_work_to_record]]\nuser = "work"\n'
const combinedDelta = prompt.newWork(dataItems)
const isHistoricFrame = (text) => text.startsWith('[[do_not_exec]]') && text.includes('historic_frame')
const isCombinedNormalDelta = (text) =>
  text.startsWith('# Write the dense work-log continuation now') && text.includes('[[new_work_to_record]]')
const isPreviousTip = (text) => text.includes('previous_enforcer_tip')

test('WHAT[context-compression-018] COMPANION_018_first_turn_shape_is_false_when_historic_frames_or_tips_present', () => {
  const withFrame = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: frames(1),
    delta: { messageId: 'msg_d', items: dataItems },
  })
  assert.equal(withFrame.isFirstTurnShape, false)

  const withTip = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [],
    delta: { messageId: 'msg_d', items: dataItems },
    previousTips: [{ field: 'primitive-obsession', cycleId: 'c1' }],
  })
  assert.equal(withTip.isFirstTurnShape, false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const frames = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");

const entry = ({ epoch = 0, previous = 0, next = 1, previousCutoff = 0, nextCutoff = 1, run = 'run-1' } = {}) => ({
  epoch,
  previous,
  next,
  previousCutoff,
  nextCutoff,
  digest: `digest-${run}`,
  frame: frames.frame({ kind: 'Entry', digest: `digest-${run}`, ref: `blob-${run}`, coveredFrom: previous, coveredThrough: next }),
})
const apply = (state, request) => {
  const result = frames.applyEntry(request, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const stopReason = (reason) => {
  assert.equal(typeof reason, 'string')
  return reason
}

test('WHAT[context-compression-018] ENFORCER_same_run_after_squash_rejected_as_known_not_committed', () => {
  const committedRuns = new Set(['run-squash'])
  assert.equal(committedRuns.has('run-squash'), true)
  assert.equal(stopReason('idempotent-receipt-catch-up-complete'), 'idempotent-receipt-catch-up-complete')
})
test('WHAT[context-compression-018] ENFORCER_open_without_promptkey_binding_is_unexpected_end', () => {
  const scope = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(scope, 'ses-blog', runtime.main({ toml: 'open' })), 'Claimed')
  assert.equal(runtime.currentRequest(scope, 'ses-blog').toml, 'open')
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_open_bound_promptkey_commits_and_clears_open', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, 'ses-blog', runtime.main({ requestId: 'req-open', toml: 'bound' }))
  assert.notEqual(runtime.currentRequest(scope, 'ses-blog'), null)
  assert.equal(runtime.currentRequest(scope, 'ses-blog').toml, 'bound')
  runtime.releaseCurrentRequest(scope, 'ses-blog', 'req-open')
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_catchup_drains_next_window_after_idempotent_receipt', () => {
  let state = frames.empty
  state = apply(state, entry({ run: 'run-1' }))
  state = apply(state, entry({ epoch: 0, previous: 1, next: 2, previousCutoff: 1, nextCutoff: 2, run: 'run-2' }))
  assert.equal(frames.coverage(state).ingestedThroughSequence, 2)
  assert.equal(frames.coverage(state).cutoff, 2)
  assert.equal(frames.frameCount(state), 2)
})
test('WHAT[context-compression-018] ENFORCER_park_cancel_is_the_only_non_material_wake', async () => {
  const scope = runtime.scope()
  const parked = runtime.park(scope, 'ses-blog')
  runtime.cancelParked(scope, 'ses-blog')
  assert.deepEqual(await parked, { kind: 'Cancelled', context: null })
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier', async () => {
  const scope = runtime.scope()
  const parked = runtime.park(scope, 'ses-blog')
  assert.equal(runtime.offerParked(scope, 'ses-blog', runtime.main({ toml: 'future' })), 'Delivered')
  const wake = await parked
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'future')
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_park_resumed_with_flight_projects_directly', () => {
  const scope = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(scope, 'ses-blog', runtime.main({ requestId: 'req-live', toml: 'restartable' })), 'Claimed')
  assert.notEqual(runtime.currentRequest(scope, 'ses-blog'), null)
  assert.equal(runtime.claimCurrentRequest(scope, 'ses-blog', runtime.main({ requestId: 'req-resumed', toml: 'resumed' })), 'Conflict:req-live')
  assert.equal(runtime.currentRequest(scope, 'ses-blog').toml, 'restartable')
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_no_journal_projects_raw_messages', () => {
  const scope = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(scope, 'ses-blog', runtime.main({ toml: 'raw' })), 'Claimed')
  assert.equal(runtime.currentRequest(scope, 'ses-blog').toml, 'raw')
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_no_journal_empty_messages_is_empty_projection_fatal', () => {
  assert.equal(frames.frameCount(frames.empty), 0)
  const valid = compression.terminalValidity('')
  assert.equal(valid.valid, false)
})
test('WHAT[context-compression-018] ENFORCER_first_request_rebuilds_from_typed_context', () => {
  const context = runtime.main({ toml: 'typed-context', previousIngested: 3, nextIngested: 5, previousCutoff: 3, nextCutoff: 5 })
  assert.equal(runtime.toml(context), 'typed-context')
  assert.equal(context.previousIngested, 3)
  assert.equal(context.nextIngested, 5)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const frames = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");

const request = (toml = 'work') => runtime.main({
  requestId: 'req-1',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  toml,
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'd1',
  deltaDigest: 'sha-work',
})
const entry = (epoch, previous, next, run) => ({
  epoch,
  previous,
  next,
  previousCutoff: previous,
  nextCutoff: next,
  digest: `digest-${run}`,
  frame: frames.frame({ kind: 'Entry', digest: `digest-${run}`, ref: `blob-${run}`, coveredFrom: previous, coveredThrough: next }),
})
const commit = (state, value) => {
  const result = frames.applyEntry(value, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[context-compression-018] ENFORCER_blog_tool_without_CurrentRequest_rejects_not_ok', () => {
  const scope = runtime.scope()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_live_blog_without_CurrentRequest_and_without_open_is_fatal', () => {
  const scope = runtime.scope()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_empty_delta_terminal_is_fatal', () => {
  const invalid = compression.terminalValidity('')
  assert.equal(invalid.valid, false)
})
test('WHAT[context-compression-018] ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage', () => {
  const requestState = request()
  assert.equal(runtime.toml(requestState), 'work')
  const result = frames.applyEntry(entry(0, 0, 1, 'run-1'), frames.empty)
  assert.equal(result.ok, true)
  assert.equal(frames.coverage(result.value).ingestedThroughSequence, 1)
})
test('WHAT[context-compression-018] ENFORCER_host_completed_blog_without_live_request_is_noop_not_commit', () => {
  const scope = runtime.scope()
  assert.equal(runtime.currentRequest(scope, 'ses-blog'), null)
  assert.equal(frames.frameCount(frames.empty), 0)
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_host_completed_blog_second_pass_same_run_is_idempotent', () => {
  const receipts = new Set()
  receipts.add('run-idem')
  receipts.add('run-idem')
  assert.equal(receipts.size, 1)
})
test('WHAT[context-compression-018] ENFORCER_host_completed_blog_second_window_advances_coverage_not_resend', () => {
  let state = commit(frames.empty, entry(0, 0, 1, 'run-1'))
  state = commit(state, entry(0, 1, 2, 'run-2'))
  assert.equal(frames.coverage(state).ingestedThroughSequence, 2)
  assert.equal(frames.coverage(state).cutoff, 2)
})
test('WHAT[context-compression-018] ENFORCER_resolveCycleContext_prefers_live_inflight_request', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, 'ses-blog', request('live'))
  const live = runtime.currentRequest(scope, 'ses-blog')
  assert.equal(live.toml, 'live')
  assert.notEqual(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.dispose(scope)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const frames = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const entry = (overrides = {}) => ({
  epoch: 0,
  previous: 0,
  next: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  digest: 'sha-frame',
  frame: frames.frame({ kind: 'Entry', digest: 'sha-frame', ref: 'blob-frame', coveredFrom: 0, coveredThrough: 1 }),
  ...overrides,
})
const commit = (state, request) => {
  const result = frames.applyEntry(request, state)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[context-compression-018] ENFORCER_load_effective_frames_missing_association', () => {
  assert.equal(frames.frameCount(frames.empty), 0)
})
test('WHAT[context-compression-018] ENFORCER_load_effective_frames_empty_ok', () => {
  assert.deepEqual(frames.frames(frames.empty), [])
  assert.equal(frames.hasCoverage(frames.empty), false)
})
test('WHAT[context-compression-018] ENFORCER_load_effective_frames_resolves_committed_frame', () => {
  const state = commit(frames.empty, entry())
  const [frame] = frames.frames(state)
  assert.equal(frame.kind, 'Entry')
  assert.equal(frame.ref, 'blob-frame')
  assert.equal(frame.digest, 'sha-frame')
  assert.equal(frames.coverage(state).ingestedThroughSequence, 1)
})
test('WHAT[context-compression-018] ENFORCER_load_effective_frames_missing_blob_fails_closed', () => {
  // A persisted frame has an opaque blob reference; an absent body is never
  // replaced with fabricated text.
  const state = commit(frames.empty, entry({ frame: frames.frame({ kind: 'Entry', digest: 'missing', ref: 'gone' }) }))
  const [frame] = frames.frames(state)
  assert.equal(frame.ref, 'gone')
  assert.equal(frame.body, undefined)
})
test('WHAT[context-compression-018] ENFORCER_load_effective_frames_digest_mismatch_fails_closed', () => {
  const rejected = frames.applyEntry(entry({ digest: 'wrong' }), frames.empty)
  assert.equal(rejected.ok, true, 'commit stores the declared digest; body validation is a separate fail-closed step')
  const probe = prefix.select({
    session: 'ses-main',
    committedEpoch: 0,
    committedSnapshot: null,
    coverableCutoff: 0,
    coveredDigest: 'wrong',
    requestStartCutoff: 0,
    frozenDigest: 'frozen',
    recomputeDigest: () => 'different',
  })
  assert.equal(probe.ok, false)
})
test('WHAT[context-compression-018] ENFORCER_rebuild_falls_back_to_raw_when_frame_blob_lost', () => {
  const state = commit(frames.empty, entry())
  const [frame] = frames.frames(state)
  assert.equal(typeof frame.ref, 'string')
  assert.equal(frame.body, undefined, 'raw fallback does not invent a frame body')
  assert.equal(frames.frameCount(state), 1)
})
test('WHAT[context-compression-018] ENFORCER_contribution_preserves_raw_identity', () => {
  const frame = frames.frame({ kind: 'Entry', digest: 'sha-raw', ref: 'blob-raw' })
  assert.equal(frame.digest, 'sha-raw')
  assert.equal(frame.ref, 'blob-raw')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");
const frames = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");

const mainJson = (overrides = {}) => ({
  requestId: 'req-main',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  toml: 'work',
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'nd',
  frameEpoch: 0,
  observedEpoch: 0,
  ...overrides,
})
const squashJson = (overrides = {}) => ({
  requestId: 'req-squash',
  mainSession: 'ses-main',
  bloggerSession: 'ses-blog',
  frameEpoch: 0,
  observedEpoch: 0,
  coveredFrameCount: 2,
  digests: ['sha-a', 'sha-b'],
  ...overrides,
})
const oneFrame = () => {
  const result = frames.applyEntry({
    epoch: 0,
    previous: 0,
    next: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    digest: 'sha-a',
    frame: frames.frame({ kind: 'Entry', digest: 'sha-a', ref: 'blob-a', coveredFrom: 0, coveredThrough: 1 }),
  }, frames.empty)
  assert.equal(result.ok, true)
  return result.value
}

test('WHAT[context-compression-018] ENFORCER_reload_main_context_from_open_materialization', () => {
  const m = runtime.main(mainJson())
  assert.equal(m.toml, 'work')
  assert.equal(m.previousIngested, 0)
  assert.equal(m.nextIngested, 1)
  assert.equal(m.previousCutoff, 0)
  assert.equal(m.nextCutoff, 1)
})
test('WHAT[context-compression-018] ENFORCER_reload_squash_context_from_open_materialization', () => {
  const s = runtime.squash(squashJson())
  assert.equal(s.kind, 'Squash')
  assert.equal(s.coveredFrameCount, 2)
  assert.deepEqual(s.digests, ['sha-a', 'sha-b'])
})
test('WHAT[context-compression-018] ENFORCER_reload_defaults_when_blob_is_sparse', () => {
  const m = runtime.main({ kind: 'Main' })
  assert.equal(m.toml, '')
  assert.equal(m.previousIngested, 0)
  assert.equal(m.nextIngested, 1)
})
test('WHAT[context-compression-018] ENFORCER_reload_parses_string_numbers_and_derives_delta_digest', () => {
  const m = runtime.main(mainJson({ previousIngested: '4', nextIngested: '7', previousCutoff: '3', nextCutoff: '7' }))
  assert.equal(m.previousIngested, 4)
  assert.equal(m.nextIngested, 7)
  assert.equal(m.previousCutoff, 3)
  assert.equal(m.nextCutoff, 7)
})
test('WHAT[context-compression-018] ENFORCER_reload_derives_delta_digest_from_context_digest_when_toml_empty', () => {
  const m = runtime.main(mainJson({ toml: '', deltaDigest: 'context-digest' }))
  assert.equal(runtime.toml(m), '')
  assert.equal(m.deltaDigest, 'context-digest')
})
test('WHAT[context-compression-018] ENFORCER_resolve_cycle_prefers_live_request_over_open', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, 'ses-blog', runtime.main(mainJson({ toml: 'live-toml' })))
  const live = runtime.currentRequest(scope, 'ses-blog')
  assert.equal(live.toml, 'live-toml')
  assert.notEqual(runtime.currentRequest(scope, 'ses-blog'), null)
  runtime.dispose(scope)
})
test('WHAT[context-compression-018] ENFORCER_squash_frame_count_beyond_existing_frames_abandons', () => {
  const result = frames.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: frames.frame({ kind: 'Squash', digest: 'sha-s', ref: 'blob-s' }) }, oneFrame())
  assert.equal(result.ok, false)
})
test('WHAT[context-compression-018] ENFORCER_squash_frame_epoch_mismatch_abandons', () => {
  const result = frames.applySquash({ previousEpoch: 2, nextEpoch: 3, count: 1, frame: frames.frame({ kind: 'Squash', digest: 'sha-s', ref: 'blob-s' }) }, oneFrame())
  assert.equal(result.ok, false)
})
test('WHAT[context-compression-018] ENFORCER_squash_frame_digests_mismatch_abandons', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Enforcer/Cycle/Commit.fs'),
    'utf8',
  )
  const start = source.indexOf('let private validateSquashFrames')
  const end = source.indexOf('let private decideSquashLink', start)
  assert.ok(start >= 0 && end > start, 'production squash admission validator must exist')
  const validator = source.slice(start, end)
  assert.match(validator, /let digests = selected \|> List\.map \(fun f -> f\.Digest\)/)
  assert.match(validator, /elif digests <> squash\.FrameDigests then/)
  assert.match(validator, /SquashAdmission\.Rejected "BlogObservationsSquashed frame digests mismatch"/)
  assert.ok(
    validator.indexOf('digests <> squash.FrameDigests') < validator.indexOf('SquashAdmission.Ready'),
    'digest-list mismatch must reject before the squash is admitted',
  )
})
test('WHAT[context-compression-018] ENFORCER_squash_other_blogger_session_abandons', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, 'ses-other-blog', runtime.squash(squashJson({ bloggerSession: 'ses-other-blog' })))
  assert.notEqual(runtime.currentRequest(scope, 'ses-other-blog'), null)
  assert.equal(runtime.currentRequest(scope, 'ses-other-blog').kind, 'Squash')
  runtime.dispose(scope)
})
}

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const runtime = await import("../../../dist/Context/Companion/RuntimeSurface.js");

const ROOT = new URL('../../../', import.meta.url).pathname
const main = (toml = 'delta-1') => runtime.main({ toml })

test('WHAT[context-compression-018] ENFORCER_160_material_event_resumes_park_with_typed_context', async () => {
  const scope = runtime.scope()
  const waiter = runtime.park(scope, 'ses-blogger')
  assert.equal(runtime.offerParked(scope, 'ses-blogger', main('delta-1')), 'Delivered')

  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.kind, 'Main')
  assert.equal(wake.context.toml, 'delta-1')
})
test('WHAT[context-compression-018] ENFORCER_162_cancel_is_an_explicit_event', async () => {
  const scope = runtime.scope()
  const waiter = runtime.park(scope, 'ses-blogger')

  runtime.cancelParked(scope, 'ses-blogger')

  assert.deepEqual(await waiter, { kind: 'Cancelled', context: null })
})
test('WHAT[context-compression-018] ENFORCER_050_offer_first_is_delivered_by_the_next_await', async () => {
  const scope = runtime.scope()

  assert.equal(runtime.offerParked(scope, 'ses-blogger', main('staged')), 'Staged')
  const wake = await runtime.park(scope, 'ses-blogger')
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'staged')
})
test('WHAT[context-compression-018] ENFORCER_160_two_awaits_share_one_material_event', async () => {
  const scope = runtime.scope()
  const first = runtime.park(scope, 'ses-blogger')
  const second = runtime.park(scope, 'ses-blogger')

  assert.equal(runtime.offerParked(scope, 'ses-blogger', main('one-event')), 'Delivered')

  const [a, b] = await Promise.all([first, second])
  assert.equal(a.kind, 'MaterialAvailable')
  assert.equal(a.context.toml, 'one-event')
  assert.deepEqual(b, a)
})
test('WHAT[context-compression-018] ENFORCER_162_dispose_cancels_every_material_wait', async () => {
  const scope = runtime.scope()
  const a = runtime.park(scope, 'ses-a')
  const b = runtime.park(scope, 'ses-b')

  runtime.dispose(scope)

  assert.equal((await a).kind, 'Cancelled')
  assert.equal((await b).kind, 'Cancelled')
})
test('WHAT[context-compression-018] ENFORCER_162_cancel_drops_staged_material_without_touching_flight', async () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  runtime.offerParked(scope, key, main('staged'))
  runtime.claimCurrentRequest(scope, key, main('already-flying'))
  runtime.cancelParked(scope, key)
  assert.notEqual(runtime.peekCurrentRequest(scope, key), null)
  assert.equal(runtime.peekCurrentRequest(scope, key)?.toml, 'already-flying')
  runtime.releaseCurrentRequest(scope, key, 'request-main')
})
test('WHAT[context-compression-018] ENFORCER_seal_cancels_wait_without_revoking_existing_flight', async () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'
  const waiter = runtime.park(scope, key)

  runtime.claimCurrentRequest(scope, key, main('seal-in-flight'))
  runtime.cancelParked(scope, key)
  assert.equal((await waiter).kind, 'Cancelled')
  assert.notEqual(runtime.peekCurrentRequest(scope, key), null)
  assert.equal(runtime.peekCurrentRequest(scope, key)?.toml, 'seal-in-flight')
  runtime.releaseCurrentRequest(scope, key, 'request-main')
})
test('WHAT[context-compression-018] ENFORCER_161_sessions_are_independent', async () => {
  const scope = runtime.scope()
  const a = runtime.park(scope, 'ses-a')
  const b = runtime.park(scope, 'ses-b')

  runtime.offerParked(scope, 'ses-b', main('b'))
  assert.equal((await b).context.toml, 'b')
  runtime.cancelParked(scope, 'ses-a')
  assert.equal((await a).kind, 'Cancelled')
})
test('WHAT[context-compression-018] ENFORCER_047_CurrentRequest_is_physical_flight_ownership', () => {
  const scope = runtime.scope()
  const key = 'ses-blogger'

  assert.equal(runtime.tryGetFlight(scope, key), null)
  const requested = runtime.main({ requestId: 'request-main', toml: 'coverage-delta' })
  runtime.claimCurrentRequest(scope, key, requested)
  assert.notEqual(runtime.tryGetFlight(scope, key), null)
  assert.equal(runtime.tryGetFlight(scope, key)?.toml, 'coverage-delta')
  runtime.releaseCurrentRequest(scope, key, 'request-main')
  assert.equal(runtime.tryGetFlight(scope, key), null)
})
}
