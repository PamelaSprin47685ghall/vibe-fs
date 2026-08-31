// Split from tests/unit/enforcer/blogger-convergence-gaps.test.mjs (cutover Wave 2a); owner: context-compression.
//
// C0 red inventory for the Blogger vertical-slice convergence (docs/what/enforcer.md).
//
// These tests assert the Definition of Done for the current slice — not the
// present implementation. They MUST fail until C1–C7 close each gap. Do not
// weaken an assertion to go green; fix the production path instead.
//
// Scope (exactly this chain):
//   main material → single coordinator → typed Normal/Squash request
//   → single projection → blog tool cycle → Entry/Squash commit → Park
//   → resume on new material → crash/retry exactly-once
//
// The atomic-fact assertion (C0_no_EnforcementCycleCommitted_fact, BD-012) moved
// to behavior-diagnosis (blogger-cycle-atomic-fact.test.mjs).
import test from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

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

// ── production authority ────────────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-018] C0_blogger_lifecycle_authority_is_physical_ownership', () => {
  // PR7 (Blogger runtime) migration is complete: the lifecycle authority is the
  // pure router decideMaterial (parked waiter + physical flight ownership), NOT
  // the transition cell. onMaterial having zero production callers is the
  // correct, expected direction — it locks the deletion of the transition DU.
  // 1. The pure router decideMaterial MUST be the production lifecycle authority.
  const routerCallers = filesContaining(/BloggerRuntime\.decideMaterial\b/)
    .map(rel)
    .filter((path) => !path.endsWith('Session/BloggerRuntimeState.fs'))
  assert.ok(
    routerCallers.length > 0,
    'BloggerRuntime.decideMaterial has zero production call sites — the pure router is not the lifecycle authority',
  )
  // 2. The transition API onMaterial must have ZERO production callers outside
  //    BloggerRuntimeState.fs (its own definition + comment). This is the
  //    correct assertion now that the migration is done; it locks the deletion
  //    direction for the transition module.
  const transitionCallers = filesContaining(/BloggerRuntime\.onMaterial\b/)
    .map(rel)
    .filter((path) => !path.endsWith('Session/BloggerRuntimeState.fs'))
  assert.equal(
    transitionCallers.length,
    0,
    `BloggerRuntime.onMaterial still has production callers: ${transitionCallers.join(', ')} — lifecycle authority must be physical ownership, not the transition cell`,
  )
  // 3. The coordinator must route via decideMaterial and must not reference the
  //    shadow state (Get/SetBloggerRuntime, BloggerRuntimeState, cell .State).
  const coordinator = prodText('src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs')
  assert.match(coordinator, /BloggerRuntime\.decideMaterial/,
    'BloggerCoordinator must route via decideMaterial')
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
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_physical_HasFlight_is_the_only_busy_definition', () => {
  // Companion send Task must not decide busy. Production busy is host HasFlight only.
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
    /scope\.HasFlight key/,
    'onMainMaterial busy must use HasFlight',
  )
  const runtimeSrc = prodText('src/Wanxiangshu/Context/Companion/Blogger/Runtime/State.fs')
  assert.doesNotMatch(runtimeSrc, /BloggerRuntimeState\b/, 'BloggerRuntimeState DU must be deleted')
  assert.doesNotMatch(runtimeSrc, /BloggerRuntimeCell\b/, 'BloggerRuntimeCell must be deleted')
  const scope = prodText('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  assert.doesNotMatch(scope, /GetBloggerRuntime|SetBloggerRuntime/, 'scope must not expose cell Get/Set')
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_CurrentRequest_and_PendingOffer_are_separate_slots', () => {
  // Dual slots: PendingOffer dictionary + flight ownership registry.
  // Forbidden: a second `currentRequest` dict or InFlight shadow fallback.
  // Blogger parking/flight/drain state moved to PluginBloggerScope (Wave 2).
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
  assert.match(scope, /HasFlight/, 'HasFlight ownership API required')
  assert.match(
    scope,
    /SharedState\.BloggerFlights\.TryGetValue/,
    'TryPeekCurrentRequest / TryGetFlight must read SharedState.BloggerFlights only',
  )
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_commit_uses_live_InFlight_only_not_open_heal', () => {
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
  assert.equal(
    /PreviousCoverableTurnCutoffExclusive = 0\s*\n\s*NextCoverableTurnCutoffExclusive = 0/.test(
      prodText('src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs'),
    ),
    false,
    'crash recovery must not zero cutoff/digest when reloading Main context',
  )
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_single_main_material_coordinator_entry', () => {
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

// ── projection / reset ──────────────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-018] C0_no_BloggerNeedsReset_full_X_replay', () => {
  const hits = filesContaining(/BloggerNeedsReset/).map(rel)
  assert.equal(
    hits.length,
    0,
    `BloggerNeedsReset still present: ${hits.join(', ')} — restart must reuse durable frames + X gap`,
  )
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_first_request_does_not_extract_raw_user_toml', () => {
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

// ── squash tool loop ────────────────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-018] C0_squash_path_does_not_SubscribeTerminal', () => {
  const blogger = prodText('src/Wanxiangshu/Context/Companion/HostBlogger.fs')
  assert.equal(
    /SubscribeTerminal/.test(blogger),
    false,
    'Squash still waits on SubscribeTerminal; must share blog-tool continuation with Normal',
  )
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_squash_constructs_typed_BloggerRequestContext_Squash_in_production', () => {
  // Domain type + match arms exist; production must CONSTRUCT Squash context for send/commit.
  // Pattern match (`| BloggerRequestContext.Squash _`) is not construction.
  const constructors = prodFiles.filter((file) => {
    const text = readFileSync(file, 'utf8')
    return /BloggerRequestContext\.Squash\s*\{/.test(text) || /BloggerRequestContext\.Squash\s*\n\s*\{/.test(text)
  }).map(rel)
  assert.ok(
    constructors.length > 0,
    'no production construction of BloggerRequestContext.Squash { ... } — typed squash context is domain-only',
  )
})

// ── commit / park ───────────────────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-018] C0_park_only_after_KnownCommitted', () => {
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  assert.match(host, /ParkTransform/,
    'probe: ParkTransform must exist to assert the KnownCommitted gate')
  // P1-3: not-committed paths are CycleDisposition arms (Working/InjectRepair/
  // CommitUnknown/AbandonThenCatchUp); park lives only under Committed.
  assert.match(host, /type CycleDisposition/,
    'commit outcomes must collapse into CycleDisposition before park')
  // Committed → finishCommitted → drain → finishCaughtUpAfterCommit →
  // parkAfterCatchUpClear → ParkTransform. Helpers are defined above the
  // disposition match, so source-order "ParkTransform after Committed arm"
  // is the wrong probe.
  assert.match(
    host,
    /CycleDisposition\.Committed afterSquashMain -> finishCommitted/,
    'Committed arm must enter finishCommitted',
  )
  assert.match(host, /return! drainAfterCommitMaterial/, 'finishCommitted drains before park')
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

test('WHAT[CONTEXT-COMPRESSION-018] C0_commit_drains_via_tryRefresh_before_park', () => {
  // One external wake may need many ≤200 KiB cycles. After BlogObservationCommitted the
  // continuation must re-chunk from durable coverage (tryRefresh) and continue
  // without waiting for a new main-session wake. Stale PendingOffer is not enough.
  const host = prodText('src/Wanxiangshu/Enforcer/Continuation.fs')
  // EnforcerHost injects the re-chunk through ctx.RefreshMainContext; the
  // commit branch must use it before parking.
  const refresh = host.lastIndexOf('RefreshMainContext', host.indexOf('ParkTransform'))
  const park = host.indexOf('ParkTransform')
  assert.ok(refresh >= 0 && park > refresh,
    'post-commit path must tryRefresh (catch-up drain) before ParkTransform')
  assert.match(
    host,
    /resumeCatchUp|Catch-up drain/,
    'already-committed / catch-up arm must re-chunk from coverage',
  )
  // Stale PendingOffer must not be preferred over re-chunk.
  assert.match(
    host,
    /TryTakePendingOffer \w+ \|> ignore[\s\S]{0,400}RefreshMainContext/,
    'PendingOffer is discarded; next window always re-chunks from coverage',
  )
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_caught_up_is_parked_not_completed_and_wake_rechecks_live_Current', () => {
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

// ── status / pending ────────────────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-018] C0_adopted_blogger_motion_is_not_active_PENDING', () => {
  // Adopted motion is git history only; no active PENDING/ parking file.
  assert.equal(
    existsSync(join(ROOT, 'PENDING/blogger-prompt-shape-and-parking.md')),
    false,
    'ADOPTED motion must leave active PENDING/',
  )
})
