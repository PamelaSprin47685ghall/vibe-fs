/**
 * C0 red inventory for the Blogger vertical-slice convergence (spec/15).
 *
 * These tests assert the Definition of Done for the current slice — not the
 * present implementation. They MUST fail until C1–C7 close each gap. Do not
 * weaken an assertion to go green; fix the production path instead.
 *
 * Scope (exactly this chain):
 *   main material → single coordinator → typed Normal/Squash request
 *   → single projection → blog tool cycle → Entry/Squash commit → Park
 *   → resume on new material → crash/retry exactly-once
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

const ROOT = new URL('../../', import.meta.url).pathname
const PROD = join(ROOT, 'src/Wanxiangshu.Next')

const walkFs = (dir, acc = []) => {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    const st = statSync(path)
    if (st.isDirectory()) walkFs(path, acc)
    else if (path.endsWith('.fs')) acc.push(path)
  }
  return acc
}

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
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

test('C0_BloggerRuntime_onMaterial_has_production_call_site', () => {
  // Pure module + pure tests exist; production lifecycle must call it.
  const callers = filesContaining(/BloggerRuntime\.onMaterial\b/)
    .map(rel)
    .filter((path) => !path.endsWith('Session/BloggerRuntimeState.fs'))
  assert.ok(
    callers.length > 0,
    'BloggerRuntime.onMaterial has zero production call sites — runtime state is not the lifecycle authority',
  )
})

test('C0_BloggerRuntimeState_is_the_only_busy_definition', () => {
  const companion = prodText('src/Wanxiangshu.Next/Session/Companion.fs')
  assert.equal(
    /mutable inFlightTask/.test(companion),
    false,
    'Companion.inFlightTask must not decide Blogger busy; BloggerRuntimeState.InFlight is the sole busy definition',
  )
  assert.equal(
    /mutable inFlightCompleted/.test(companion),
    false,
    'Companion.inFlightCompleted must not decide Blogger busy',
  )
})

test('C0_CurrentRequest_and_PendingOffer_are_separate_slots', () => {
  // Dual slots without dual storage: PendingOffer is a dictionary;
  // CurrentRequest is the InFlight payload. A parallel currentRequest dict
  // dual-wrote and drifted into "missing CurrentRequest".
  const scope = prodText('src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs')
  assert.equal(/parkedOffer/.test(scope), false, 'parkedOffer single-slot is forbidden')
  assert.match(scope, /pendingOffer/, 'PendingOffer dictionary required')
  assert.equal(
    /\blet currentRequest\b/.test(scope),
    false,
    'CurrentRequest must not be a second dictionary; InFlight payload is sole authority',
  )
  assert.match(
    scope,
    /BloggerRuntime\.inFlightContext|inFlightContext \(this\.GetBloggerRuntimeUnlocked/,
    'TryPeekCurrentRequest must read InFlight payload',
  )
})

test('C0_commit_uses_live_InFlight_only_not_open_heal', () => {
  // Host transform msgs end on the historical last assistant (new outbound shell
  // is not in the list). Commit must peek InFlight only — healing open here
  // rebinds a new RequestId onto an old provider run (stale-cycle race).
  // Durable open reload stays for rebuild / crash recovery, not cycle commit.
  const host = prodText('src/Wanxiangshu.Next/Session/EnforcerHost.fs')
  assert.match(host, /tryLiveCycleContext/, 'commit authority is live InFlight peek')
  assert.match(host, /let liveCtx = tryLiveCycleContext/, 'completed-blog arm peeks live only')
  assert.match(host, /resolveCycleContext/, 'rebuild/empty-calls still resolve typed context')
  assert.match(host, /tryReloadRequestContext/, 'durable open materialization must reload full typed context')
  assert.equal(
    /SetCurrentRequest\(key, ctx\)[\s\S]{0,80}Some ctx[\s\S]{0,40}resolveCycleContext|resolveCycleContext[\s\S]{0,200}SetCurrentRequest\(key, ctx\)/.test(
      host,
    ),
    false,
    'resolveCycleContext must not heal InFlight via SetCurrentRequest',
  )
  assert.equal(
    /PreviousCoverableTurnCutoffExclusive = 0\s*\n\s*NextCoverableTurnCutoffExclusive = 0/.test(
      prodText('src/Wanxiangshu.Next/Application/Reconciliation/BloggerCrashRecovery.fs'),
    ),
    false,
    'crash recovery must not zero cutoff/digest when reloading Main context',
  )
})

test('C0_single_main_material_coordinator_entry', () => {
  const hasCoordinator = filesContaining(/BloggerCoordinator\.onMainMaterial\b/).map(rel)
  assert.ok(
    hasCoordinator.length > 0,
    'missing BloggerCoordinator.onMainMaterial production call',
  )
  const offerSites = filesContaining(/offerToBlogger\b/).map(rel)
  assert.equal(
    offerSites.length,
    0,
    `parallel offerToBlogger sites remain: ${offerSites.join(', ')}`,
  )
})

// ── projection / reset ──────────────────────────────────────────────────────

test('C0_no_BloggerNeedsReset_full_X_replay', () => {
  const hits = filesContaining(/BloggerNeedsReset/).map(rel)
  assert.equal(
    hits.length,
    0,
    `BloggerNeedsReset still present: ${hits.join(', ')} — restart must reuse durable frames + X gap`,
  )
})

test('C0_first_request_does_not_extract_raw_user_toml', () => {
  const host = prodText('src/Wanxiangshu.Next/Session/EnforcerHost.fs')
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

test('C0_squash_path_does_not_SubscribeTerminal', () => {
  const blogger = prodText('src/Wanxiangshu.Next/Session/CompanionHostBlogger.fs')
  assert.equal(
    /SubscribeTerminal/.test(blogger),
    false,
    'Squash still waits on SubscribeTerminal; must share blog-tool continuation with Normal',
  )
})

test('C0_squash_constructs_typed_BloggerRequestContext_Squash_in_production', () => {
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

test('C0_park_only_after_KnownCommitted', () => {
  const host = prodText('src/Wanxiangshu.Next/Session/EnforcerHost.fs')
  assert.match(host, /ParkTransform/,
    'probe: ParkTransform must exist to assert the KnownCommitted gate')
  // Gate: invalid/failed/unknown paths return before park. The drain branch sits
  // between `if not committed then` and ParkTransform, so distance is large.
  const notCommitted = host.indexOf('if not committed then')
  const park = host.indexOf('ParkTransform', notCommitted)
  assert.ok(notCommitted >= 0 && park > notCommitted, 'ParkTransform must follow not-committed gate')
  const between = host.slice(notCommitted, park)
  assert.match(between, /return rawMessages|return \[\]/,
    'not-committed path must return before ParkTransform')
  assert.match(host, /KnownCommitted/, 'KnownCommitted is the only park-enabling commit outcome')
})

test('C0_commit_drains_via_tryRefresh_before_park', () => {
  // One external wake may need many ≤200 KiB cycles. After BlogEntryCommitted the
  // continuation must re-chunk from durable coverage (tryRefresh) and continue
  // without waiting for a new main-session wake. Stale PendingOffer is not enough.
  const host = prodText('src/Wanxiangshu.Next/Session/EnforcerHost.fs')
  const refresh = host.lastIndexOf('tryRefreshMainContextFromJournal', host.indexOf('ParkTransform'))
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
    /TryTakePendingOffer key \|> ignore[\s\S]{0,400}tryRefreshMainContextFromJournal/,
    'PendingOffer is discarded; next window always re-chunks from coverage',
  )
})

test('C0_no_EnforcementCycleCommitted_fact', () => {
  const fact = prodText('src/Wanxiangshu.Next/Kernel/Fact.fs')
  assert.equal(
    /\| EnforcementCycleCommitted\b/.test(fact),
    false,
    'EnforcementCycleCommitted must stay deleted; BlogEntryCommitted is the atomic fact',
  )
  // FactCodec may list it only as a pre-0.5.0 refuse marker (escaped JSON case name).
  const codec = prodText('src/Wanxiangshu.Next/Journal/FactCodec.fs')
  assert.ok(
    codec.includes('EnforcementCycleCommitted') && /pre050Markers|pre-0\.5\.0/.test(codec),
    'FactCodec must keep the legacy refuse marker for old journals',
  )
})

// ── status / pending ────────────────────────────────────────────────────────

test('C0_adopted_blogger_motion_is_not_active_PENDING', () => {
  assert.equal(
    existsSync(join(ROOT, 'PENDING/blogger-prompt-shape-and-parking.md')),
    false,
    'ADOPTED motion must leave active PENDING/',
  )
  assert.equal(
    existsSync(join(ROOT, 'docs/archive/shock-anneal-2026/blogger-prompt-shape-and-parking.md')),
    true,
    'ADOPTED motion must live under docs/archive/',
  )
})

test('C7_blogger_slice_conformance_rows_are_CONFORMANT', () => {
  const conf = read('STATUS/conformance.md')
  for (const clause of [
    'COMPANION-005',
    'COMPANION-008',
    'CTX-006',
    'CTX-007',
    'CTX-012',
  ]) {
    const line = conf.split('\n').find((l) => l.includes(`${clause}:`) || l.startsWith(`| ${clause}`))
    assert.ok(line, `missing conformance row for ${clause}`)
    assert.match(
      line,
      /\| CONFORMANT \|/,
      `${clause} must be CONFORMANT after layer-4 evidence:\n${line}`,
    )
  }
  const enforcer = conf.split('\n').find((l) => l.startsWith('| ENFORCER-010'))
  assert.ok(enforcer, 'missing ENFORCER-010 row')
  assert.match(enforcer, /\| CONFORMANT \|/, `ENFORCER-010 must be CONFORMANT:\n${enforcer}`)
})
