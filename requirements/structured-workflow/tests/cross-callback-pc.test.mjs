import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  EXEMPTION_CATEGORIES,
  REGISTERED_DECLARATIONS,
  auditFiles,
  evaluateViolations,
  isRegistrationLive,
  scanText,
  scanFiles,
} from '../../../scripts/checks/cross-callback-pc.mjs'

const readFixture = (name) => readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

// ── Pattern 1: DU await state (CounterfactualAwait shape) ──────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_du_await_state_without_proof_is_RED', () => {
  const source = readFixture('cross-callback-pc-illegal.fs')
  const hits = scanText(source, 'src/Wanxiangshu/New/CounterfactualCollector.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'DU await state without proof annotation must be RED')
  assert.ok(
    regressions.some((v) => v.pattern === 'du-await-state' || v.pattern === 'trytake-continuation'),
    'must detect DU await or TryTake pattern',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_private_du_await_state_is_detected', () => {
  const source = [
    'module Sample',
    'type private AwaitState =',
    '    | AwaitFirst of string',
    '    | AwaitSecond of string',
    'let awaits = Dictionary<string, AwaitState>()',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/PrivateAwait.fs')
  assert.ok(hits.some((v) => v.name === 'awaits' && v.pattern === 'du-await-state'))
})

// ── Pattern 2: TryTake continuation consumption ─────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_trytake_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — recovery arming',
    'let recoveryArming = Dictionary<string, SlotArming>()',
    'type Scope() =',
    '    member _.TryTakeRecoveryPermit(sessionId: string) =',
    '        match recoveryArming.TryGetValue(sessionId) with',
    '        | true, value -> recoveryArming.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs')
  // A synthetic source without the required physical proof is RED.
  const { regressions, ok } = evaluateViolations(hits)
  assert.ok(hits.some((v) => v.name === 'recoveryArming'))
  assert.equal(ok, false, 'recoveryArming without proof annotation must be RED after baseline removal')
  assert.ok(regressions.some((v) => v.name === 'recoveryArming'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_new_trytake_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — new continuation',
    'let newContinuation = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeContinuation(sessionId: string) =',
    '        match newContinuation.TryGetValue(sessionId) with',
    '        | true, value -> newContinuation.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Module.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'new TryTake pattern without proof must be RED')
  assert.ok(regressions.some((v) => v.name === 'newContinuation'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_trytake_only_marks_the_registry_it_consumes', () => {
  const source = [
    'module Sample',
    'let continuation = Dictionary<string, string>()',
    'let unrelatedCache = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeContinuation(sessionId: string) =',
    '        match continuation.TryGetValue(sessionId) with',
    '        | true, value -> continuation.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
    '    member _.ReadCache(sessionId: string) = unrelatedCache.TryGetValue(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Module.fs')
  assert.deepEqual(hits.map((v) => v.name), ['continuation'])
})

// ── Pattern 3: Armed presence probe ─────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_armed_probe_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — one-shot armed mark',
    'let armed = HashSet<string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = armed.Contains(sessionId)',
    '    member _.TryArmForeign(sessionId: string) = armed.Add(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'an unauthorized caller of the registered armed cell stays RED')
  assert.ok(regressions.some((v) => v.name === 'armed' && v.pattern === 'armed-presence-probe'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_new_armed_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — new armed mark',
    'let newArmed = HashSet<string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = newArmed.Contains(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Sensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'new armed probe without proof must be RED')
  assert.ok(regressions.some((v) => v.name === 'newArmed'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_armed_probe_only_marks_the_registry_it_reads', () => {
  const source = [
    'module Sample',
    'let armed = HashSet<string>()',
    'let unrelatedCache = Dictionary<string, string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = armed.Contains(sessionId)',
    '    member _.ReadCache(sessionId: string) = unrelatedCache.TryGetValue(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Sensor.fs')
  assert.deepEqual(hits.map((v) => v.name), ['armed'])
})

// ── Green: physical proof annotation whitelists ─────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_narrow_single_flight_proof_stays_green', () => {
  const source = [
    'module Sample',
    '/// DSL-cross-callback-proof: physical single-flight — one-shot permit channel',
    'let permits = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakePermit(sessionId: string) =',
    '        match permits.TryGetValue(sessionId) with',
    '        | true, value -> permits.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Scope.fs')
  assert.deepEqual(hits, [], 'narrow single-flight proof must whitelist the pattern')
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_narrow_waiter_proof_stays_green', () => {
  const source = [
    'module Sample',
    '/// DSL-cross-callback-proof: physical waiter — parked transform rendezvous',
    'let parked = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.IsArmed(sessionId: string) = parked.ContainsKey(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Scope.fs')
  assert.deepEqual(hits, [], 'narrow waiter proof must whitelist the pattern')
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_narrow_quiescence_permit_proof_stays_green', () => {
  const source = [
    'module Sample',
    '/// DSL-cross-callback-proof: physical quiescence-permit — idle admission fence',
    'let permits = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakePermit(sessionId: string) =',
    '        match permits.TryGetValue(sessionId) with',
    '        | true, value -> permits.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Scope.fs')
  assert.deepEqual(hits, [], 'narrow quiescence-permit proof must whitelist the pattern')
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_bare_physical_without_narrow_category_is_RED', () => {
  // The bare `physical` fixture no longer whitelists: no narrow category and
  // no live exact registration (the phantom PtyManager.fs:ptyHandles entry
  // is deleted), so the TryTake continuation must be RED.
  const source = readFixture('cross-callback-pc-physical.fs')
  const hits = scanText(source, 'src/Wanxiangshu/Process/PtyManager.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'bare physical without narrow category or live registration must be RED')
  assert.ok(regressions.some((v) => v.name === 'ptyHandles' && v.pattern === 'trytake-continuation'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_generic_resource_proof_is_RED', () => {
  const source = [
    'module Sample',
    '/// DSL-cross-callback-proof: physical resource — generic resource handle',
    '// DSL-MUTABLE: resource',
    'let genericHandles = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeHandle(sessionId: string) =',
    '        match genericHandles.TryGetValue(sessionId) with',
    '        | true, value -> genericHandles.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/GenericScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'generic resource proof must not suppress a finding')
  assert.equal(hits.length, 1)
  assert.equal(hits[0].name, 'genericHandles')
  assert.ok(regressions.some((v) => v.name === 'genericHandles' && v.pattern === 'trytake-continuation'))
})

// ── Green: no pattern (plain resource without TryTake/IsArmed/DU-await) ─────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_plain_resource_without_pattern_stays_green', () => {
  const source = readFixture('cross-callback-pc-clean.fs')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  assert.deepEqual(hits, [], 'plain resource without TryTake/IsArmed/DU-await must stay green')
})

// ── scanFiles aggregates ────────────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_scanFiles_aggregates_entries', () => {
  const illegal = readFixture('cross-callback-pc-illegal.fs')
  const clean = readFixture('cross-callback-pc-clean.fs')
  const hits = scanFiles([
    { file: 'src/Wanxiangshu/New/Evil.fs', text: illegal },
    { file: 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs', text: clean },
  ])
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'new illegal fixture must produce regression')
  assert.ok(regressions.some((v) => v.file === 'src/Wanxiangshu/New/Evil.fs'))
  assert.ok(!hits.some((v) => v.file === 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs'))
})

// ── Pattern 4: Clear/Drop presence-clearing probe ───────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_clear_presence_without_proof_is_detected', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — armed mark',
    'let armed = HashSet<string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = armed.Contains(sessionId)',
    '    member _.ClearArmed(sessionId: string) = armed.Remove(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false)
  assert.ok(regressions.some((v) => v.name === 'armed'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_new_clear_presence_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — new clear mark',
    'let newArmed = HashSet<string>()',
    'type Sensor() =',
    '    member _.ClearArmed(sessionId: string) = newArmed.Remove(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Sensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'new clear-presence probe without proof must be RED')
  assert.ok(regressions.some((v) => v.name === 'newArmed' && v.pattern === 'clear-presence-probe'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_drop_attempt_presence_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — attempt tracking',
    'let attempts = Dictionary<string, Attempt>()',
    'type Scope() =',
    '    member _.DropAttempt(sessionId: string) = attempts.Remove(sessionId) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/AttemptTracker.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'DropAttempt presence-clearing without proof must be RED')
  assert.ok(regressions.some((v) => v.name === 'attempts' && v.pattern === 'clear-presence-probe'))
})

// ── EXEMPTION_CATEGORIES completeness ───────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_exemption_categories_contains_physical_capability_types', () => {
  for (const category of [
    'pty', 'timer', 'waiter', 'single-flight', 'quiescence-permit',
    'process-handle', 'socket', 'cancellation-token',
  ]) {
    assert.ok(EXEMPTION_CATEGORIES.has(category), `EXEMPTION_CATEGORIES must contain '${category}'`)
  }
  assert.equal(EXEMPTION_CATEGORIES.has('resource'), false, 'broad resource must not auto-pass')
  assert.equal(EXEMPTION_CATEGORIES.size, 8, 'EXEMPTION_CATEGORIES must contain exactly 8 narrow categories')
})

// ── SessionQuiescenceGate as legal reference (green) ────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_session_quiescence_gate_stays_green', () => {
  // SessionQuiescenceGate is the legal reference for quiescence-permit exemption.
  // It uses mutable Map (not Dictionary/HashSet), so it does not match the
  // REGISTRY_DECLARATION pattern. This is correct: the gate's TryConsume
  // receives a typed QuiescencePermit, not a key-based TryGetValue.
  const source = [
    'module Sample',
    'type SessionQuiescenceGate() =',
    '    let mutable activities = Map.empty<string, int>',
    '    member _.TryConsume(permit: QuiescencePermit) : Result<unit, QuiescencePermitFailure> =',
    '        lock gate (fun () -> Ok())',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/SessionQuiescenceGate.fs')
  assert.deepEqual(hits, [], 'SessionQuiescenceGate must stay green — typed permit, not registry presence')
})

// ── R18: Bare physical annotation fails open without category or registration ──

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_bare_physical_without_category_is_RED', () => {
  const source = [
    'module Sample',
    '/// DSL-cross-callback-proof: physical',
    '// DSL-MUTABLE: resource',
    'let unexemptedQueue = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeContinuation(sessionId: string) =',
    '        match unexemptedQueue.TryGetValue(sessionId) with',
    '        | true, value -> unexemptedQueue.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/UnregisteredScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'bare physical without category or registration must be RED')
  assert.equal(hits.length, 1)
  assert.equal(hits[0].name, 'unexemptedQueue')
})

// ── R18: Registered narrow declaration symbol is exempted ───────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_registered_declaration_symbol_is_exempted', () => {
  assert.ok(REGISTERED_DECLARATIONS.size > 0, 'REGISTERED_DECLARATIONS must contain registered declaration symbols')
  const [firstKey, firstEntry] = [...REGISTERED_DECLARATIONS.entries()][0]
  assert.ok(firstEntry.owner, 'registered entry must have owner')
  assert.ok(firstEntry.issuer, 'registered entry must have issuer')
  assert.ok(firstEntry.key, 'registered entry must have key')
  assert.ok(firstEntry.rules, 'registered entry must have rules')
  assert.ok(firstEntry.testAnchor, 'registered entry must have testAnchor')
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_registered_declarations_carry_exact_metadata', () => {
  assert.ok(REGISTERED_DECLARATIONS.size > 0, 'REGISTERED_DECLARATIONS must not be empty')
  for (const [regKey, entry] of REGISTERED_DECLARATIONS) {
    assert.ok(entry.owner, `${regKey} must have a real owner`)
    assert.ok(entry.issuer, `${regKey} must have a real issuer`)
    assert.ok(entry.key, `${regKey} must have a real key identity`)
    assert.ok(entry.rules, `${regKey} must have consumption rules`)
    assert.ok(entry.revoke, `${regKey} must name its revoke/cleanup entry point`)
    assert.ok(Array.isArray(entry.allowedConsumers) && entry.allowedConsumers.length > 0, `${regKey} must list real allowed consumers`)
    assert.ok(entry.testAnchor, `${regKey} must have a real test anchor`)
  }
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_phantom_pty_handles_registration_is_deleted', () => {
  assert.equal(
    REGISTERED_DECLARATIONS.has('src/Wanxiangshu/Process/PtyManager.fs:ptyHandles'),
    false,
    'phantom PtyManager.fs:ptyHandles registration must stay deleted: the source file never existed',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_all_registered_declarations_are_live', () => {
  for (const [regKey, entry] of REGISTERED_DECLARATIONS) {
    assert.equal(isRegistrationLive(regKey, entry), true, `registration ${regKey} must resolve to a real source declaration and a real test anchor`)
  }
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_phantom_declaration_registration_is_RED', () => {
  const regKey = 'src/Wanxiangshu/Phantom/Missing.fs:ghostQueue'
  REGISTERED_DECLARATIONS.set(regKey, {
    owner: 'phantom-owner',
    issuer: 'Phantom.Issue',
    key: 'SessionId',
    rules: 'phantom rules',
    allowedConsumers: ['Phantom.TryTakeGhost'],
    revoke: 'Phantom.Clear',
    testAnchor: 'requirements/structured-workflow/tests/cross-callback-pc.test.mjs',
  })
  try {
    const source = [
      'module Sample',
      'let ghostQueue = Dictionary<string, string>()',
      'type Scope() =',
      '    member _.TryTakeGhost(sessionId: string) =',
      '        match ghostQueue.TryGetValue(sessionId) with',
      '        | true, value -> ghostQueue.Remove(sessionId) |> ignore; Some value',
      '        | _ -> None',
    ].join('\n')
    const hits = scanText(source, 'src/Wanxiangshu/Phantom/Missing.fs')
    const { regressions, ok } = evaluateViolations(hits)
    assert.equal(ok, false, 'registration pointing at a phantom source file must not exempt')
    assert.ok(regressions.some((v) => v.name === 'ghostQueue' && v.reason === 'dead-registration'))
  } finally {
    REGISTERED_DECLARATIONS.delete(regKey)
  }
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_phantom_test_anchor_registration_is_RED', () => {
  const regKey = 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:callsByDelegate'
  REGISTERED_DECLARATIONS.set(regKey, {
    owner: 'sync-delegate-execution',
    issuer: 'SyncDelegateCallStore.Observe',
    key: 'SessionId',
    rules: 'phantom anchor rules',
    allowedConsumers: ['SyncDelegateCallStore.TryTakeCalls'],
    revoke: 'SyncDelegateCallStore.ClearAll',
    testAnchor: 'requirements/structured-workflow/tests/no-such-anchor.test.mjs',
  })
  try {
    const source = [
      'module Sample',
      'let callsByDelegate = Dictionary<string, string>()',
      'type Scope() =',
      '    member _.TryTakeCalls(sessionId: string) =',
      '        match callsByDelegate.TryGetValue(sessionId) with',
      '        | true, value -> callsByDelegate.Remove(sessionId) |> ignore; Some value',
      '        | _ -> None',
    ].join('\n')
    const hits = scanText(source, 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs')
    const { regressions, ok } = evaluateViolations(hits)
    assert.equal(ok, false, 'registration pointing at a phantom test anchor must not exempt')
    assert.ok(regressions.some((v) => v.name === 'callsByDelegate' && v.reason === 'dead-registration'))
  } finally {
    REGISTERED_DECLARATIONS.delete(regKey)
  }
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_module_let_trytake_consumption_is_RED', () => {
  const source = [
    'module Sample',
    'let continuations = Dictionary<string, string>()',
    'let tryTakeContinuation(sessionId: string) =',
    '    match continuations.TryGetValue(sessionId) with',
    '    | true, value -> continuations.Remove(sessionId) |> ignore; Some value',
    '    | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/ModuleLet.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'module-let TryTake consumption without proof must be RED')
  assert.ok(regressions.some((v) => v.name === 'continuations' && v.pattern === 'trytake-continuation'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_unauthorized_consumer_of_registered_declaration_is_RED', () => {
  const source = [
    'module Sample',
    'let pendingAttemptPlans = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeUnowned(sessionId: string) =',
    '        match pendingAttemptPlans.TryGetValue(sessionId) with',
    '        | true, value -> pendingAttemptPlans.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'a caller outside allowedConsumers must not inherit the registration')
  assert.ok(regressions.some((v) => v.name === 'pendingAttemptPlans' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_authorized_consumer_of_registered_declaration_stays_green', () => {
  const source = [
    'module Sample',
    'let pendingAttemptPlans = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.ConsumeAttemptPlan(sessionId: string, providerRun: string) =',
    '        match pendingAttemptPlans.TryGetValue(sessionId) with',
    '        | true, value -> pendingAttemptPlans.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs')
  assert.deepEqual(hits, [], 'the real ConsumeAttemptPlan terminal consumer of a live registration must stay green')
})

// ── Exact physical protocol registrations: metadata liveness and unauthorized-consumer rejection ──

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_exact_physical_registrations_are_live_with_exact_anchors', () => {
  const expected = [
    {
      key: 'src/Wanxiangshu/Execution/Session/Attachment/AttachedRuntime.fs:bindings',
      owner: 'managed-session-lifecycle',
      anchor: 'requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs',
    },
    {
      key: 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:callsByOwnerScope',
      owner: 'sync-delegate-execution',
      anchor: 'requirements/delegation/tests/sync-delegate-runtime.test.mjs',
    },
    {
      key: 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:observedProviderRuns',
      owner: 'sync-delegate-execution',
      anchor: 'requirements/delegation/tests/sync-delegate-host-observation.test.mjs',
    },
  ]

  for (const exp of expected) {
    assert.ok(REGISTERED_DECLARATIONS.has(exp.key), `REGISTERED_DECLARATIONS must contain ${exp.key}`)
    const entry = REGISTERED_DECLARATIONS.get(exp.key)
    assert.equal(entry.owner, exp.owner, `${exp.key} must have owner ${exp.owner}`)
    assert.equal(entry.testAnchor, exp.anchor, `${exp.key} must point to behavior test anchor ${exp.anchor}`)
    assert.ok(entry.issuer, `${exp.key} must have issuer`)
    assert.ok(entry.key, `${exp.key} must have key identity`)
    assert.ok(entry.rules, `${exp.key} must have rules`)
    assert.ok(entry.revoke, `${exp.key} must have revoke method`)
    assert.ok(Array.isArray(entry.allowedConsumers) && entry.allowedConsumers.length > 0, `${exp.key} must list allowed consumers`)
    assert.equal(isRegistrationLive(exp.key, entry), true, `${exp.key} must be live`)
  }

  // Verify ModelCapacity and PluginSessionScope caches are NOT registered
  for (const regKey of REGISTERED_DECLARATIONS.keys()) {
    if (regKey !== 'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Borrowing.fs:creditSourceByExecution') {
      assert.doesNotMatch(regKey, /ModelCapacity/, 'ModelCapacity parents/companion topology must not be registered')
    }
    assert.doesNotMatch(regKey, /PluginSessionScope/, 'PluginSessionScope caches must not be registered')
  }
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_attached_runtime_bindings_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let bindings = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.ClearForeignBindings() = bindings.Clear()',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Execution/Session/Attachment/AttachedRuntime.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of AttachedRuntime.bindings must be RED')
  assert.ok(regressions.some((v) => v.name === 'bindings' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_sync_delegate_calls_by_owner_scope_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let callsByOwnerScope = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.DropForeignCalls(scope: string) = callsByOwnerScope.Remove(scope) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of SyncDelegateCallStore.callsByOwnerScope must be RED')
  assert.ok(regressions.some((v) => v.name === 'callsByOwnerScope' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_sync_delegate_observed_provider_runs_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let observedProviderRuns = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.DropForeignRuns(run: string) = observedProviderRuns.Remove(run) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of SyncDelegateCallStore.observedProviderRuns must be RED')
  assert.ok(regressions.some((v) => v.name === 'observedProviderRuns' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_strength_episodes_registration_is_live_with_dist_backed_anchor', () => {
  const regKey = 'src/Wanxiangshu/Strength/OpenCode/PluginScope.fs:episodes'
  assert.ok(REGISTERED_DECLARATIONS.has(regKey), 'episodes must be exactly registered: one fold, not three maps')
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.equal(entry.owner, 'speculative-investigation')
  assert.equal(entry.issuer, 'PluginStrengthScope.ArmStrengthCounterfactual')
  assert.equal(entry.key, 'SessionId')
  assert.ok(entry.rules, `${regKey} must state the one-episode fold law`)
  assert.equal(entry.revoke, 'PluginStrengthScope.ClearSession')
  assert.ok(Array.isArray(entry.allowedConsumers) && entry.allowedConsumers.length > 0, `${regKey} must list real allowed consumers`)
  assert.equal(entry.testAnchor, 'requirements/speculative-investigation/tests/predictor-rollout.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true, 'episodes registration must resolve to a real declaration and a real dist-backed anchor')
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_strength_episodes_authorized_consumer_stays_green', () => {
  const source = [
    'module Sample',
    'let episodes = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.ClearAll() = episodes.Clear()',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Strength/OpenCode/PluginScope.fs')
  assert.deepEqual(hits, [], 'an allowlisted consumer of the live episodes registration must stay green')
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_strength_episodes_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let episodes = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.DropForeignEpisodes(sessionId: string) = episodes.Remove(sessionId) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Strength/OpenCode/PluginScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the episodes fold must be RED')
  assert.ok(regressions.some((v) => v.name === 'episodes' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_fake_basename_or_category_variants_stay_RED', () => {
  // Fake basename variant in a different folder
  const fakeFile = 'src/Wanxiangshu/OpenCode/Host/AttachedRuntime.fs'
  const source = [
    'module Sample',
    'let bindings = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.Clear() = bindings.Clear()',
  ].join('\n')
  const hitsFakePath = scanText(source, fakeFile)
  const resFakePath = evaluateViolations(hitsFakePath)
  assert.equal(resFakePath.ok, false, 'fake path with same basename must stay RED')
  assert.ok(resFakePath.regressions.some((v) => v.name === 'bindings' && !v.reason))

  // Broad physical resource proof on an unregistered cell stays RED
  const broadSource = [
    'module Sample',
    '/// DSL-cross-callback-proof: physical resource — arbitrary cell',
    'let customRegistry = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.ClearCustom() = customRegistry.Clear()',
  ].join('\n')
  const hitsBroad = scanText(broadSource, 'src/Wanxiangshu/New/Arbitrary.fs')
  const resBroad = evaluateViolations(hitsBroad)
  assert.equal(resBroad.ok, false, 'broad resource proof must not auto-pass')
  assert.ok(resBroad.regressions.some((v) => v.name === 'customRegistry'))
})

// ── R03/R05/R09/R14 cross-file shapes as permanent negative fixtures ────────

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_R03_arm_recovery_shape_is_RED', () => {
  const source = readFixture('cross-callback-pc-r03-arm-recovery.fs')
  const hits = scanText(source, 'src/Wanxiangshu/Participant/Provider/Attempt/R03ArmRecovery.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'R03 arm recovery shape must be RED')
  assert.ok(regressions.some((v) => v.name === 'recoveryArmingMap' && v.pattern === 'trytake-continuation'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_R05_drain_window_shape_is_RED', () => {
  const source = readFixture('cross-callback-pc-r05-drain-window.fs')
  const hits = scanText(source, 'src/Wanxiangshu/Context/Companion/Blogger/Runtime/R05DrainWindow.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'R05 drain window presence latch shape must be RED')
  assert.ok(regressions.some((v) => v.name === 'drainWindows' && v.pattern === 'armed-presence-probe'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_R09_loop_sensor_shape_is_RED', () => {
  const source = readFixture('cross-callback-pc-r09-loop-sensor.fs')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/R09LoopSensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'R09 session-only loop sensor anomaly armed shape must be RED')
  assert.ok(regressions.some((v) => v.name === 'armedAnomalies' && v.pattern === 'clear-presence-probe'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_R14_counterfactual_shape_is_RED', () => {
  const source = readFixture('cross-callback-pc-r14-counterfactual.fs')
  const hits = scanText(source, 'src/Wanxiangshu/Strength/OpenCode/R14Counterfactual.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'R14 multi-phase observation shape without unified fold must be RED')
  assert.ok(regressions.some((v) => v.name === 'observedFirsts' && v.pattern === 'trytake-continuation'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_auditFiles_counts_exemptions_and_coverage_gaps', () => {
  const res = auditFiles([{ file: 'src/Wanxiangshu/OpenCode/Host/SessionQuiescenceGate.fs', text: readFixture('cross-callback-pc-r03-arm-recovery.fs') }])
  assert.ok(res.coverageGaps > 0, 'coverage gaps must be counted from Map/ref cells')
})

// ── Exact registry coverage: freeze-retained plans, blogger mailbox/episodes, routing credit, loop guard ──

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_pending_attempt_plans_freeze_metadata_is_live', () => {
  const regKey = 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs:pendingAttemptPlans'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'pendingAttemptPlans must be exactly registered')
  assert.equal(entry.owner, 'managed-chat-execution')
  assert.equal(entry.issuer, 'PluginRecoveryScope.FreezePendingAttemptPlan')
  assert.equal(entry.key, 'SessionId * PhysicalUserMessageId')
  assert.match(entry.rules, /retained through bind until terminal/)
  for (const consumer of [
    'FreezePendingAttemptPlan', 'TryPeekPendingAttemptPlan', 'BindPendingAttemptPlan',
    'TryBindAttemptPlan', 'ConsumeAttemptPlan', 'ClearAttemptPlansFor',
  ]) {
    assert.ok(
      entry.allowedConsumers.includes(`PluginRecoveryScope.${consumer}`),
      `${consumer} must be an allowed consumer`,
    )
  }
  assert.ok(
    !entry.allowedConsumers.some((c) => c.includes('TryTakePendingAttemptPlan')),
    'stale TryTakePendingAttemptPlan must stay deleted: the method never existed',
  )
  assert.equal(entry.revoke, 'PluginRecoveryScope.ClearSession')
  assert.equal(entry.testAnchor, 'requirements/provider-attempt-recovery/tests/freeze-admission.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_attempt_plans_exact_run_registry_is_live', () => {
  const regKey = 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs:attemptPlans'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'attemptPlans must be exactly registered as the provider-run channel')
  assert.equal(entry.owner, 'managed-chat-execution')
  assert.equal(entry.issuer, 'PluginRecoveryScope.BindPendingAttemptPlan')
  assert.equal(entry.key, 'SessionId * ProviderRunIdentity')
  assert.match(entry.rules, /consumed once on terminal/)
  assert.ok(entry.allowedConsumers.includes('PluginRecoveryScope.TryBindAttemptPlan'))
  assert.ok(entry.allowedConsumers.includes('PluginRecoveryScope.ConsumeAttemptPlan'))
  assert.equal(entry.revoke, 'PluginRecoveryScope.ClearSession')
  assert.equal(entry.testAnchor, 'requirements/provider-attempt-recovery/tests/freeze-admission.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_attempt_plans_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let attemptPlans = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeForeignPlan(sessionId: string) =',
    '        match attemptPlans.TryGetValue(sessionId) with',
    '        | true, value -> attemptPlans.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the exact run registry must be RED')
  assert.ok(regressions.some((v) => v.name === 'attemptPlans' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_pending_offer_mailbox_metadata_is_live', () => {
  const regKey = 'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs:pendingOffer'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'pendingOffer must be exactly registered as the inbound mailbox')
  assert.equal(entry.owner, 'blogger-companion')
  assert.equal(entry.issuer, 'PluginBloggerScope.OfferMaterial')
  assert.equal(entry.key, 'SessionId')
  assert.match(entry.rules, /newest covers/)
  for (const consumer of ['ParkTransform', 'OfferMaterial', 'CancelParked']) {
    assert.ok(
      entry.allowedConsumers.includes(`PluginBloggerScope.${consumer}`),
      `${consumer} must be an allowed consumer`,
    )
  }
  assert.equal(entry.revoke, 'PluginBloggerScope.CancelParked')
  assert.equal(entry.testAnchor, 'requirements/context-compression/tests/blogger-boundary-cleanbreak.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_pending_offer_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let pendingOffer = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.DropForeignOffer(sessionId: string) = pendingOffer.Remove(sessionId) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the pendingOffer mailbox must be RED')
  assert.ok(regressions.some((v) => v.name === 'pendingOffer' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_blogger_repair_episodes_exact_request_root_is_live', () => {
  const regKey = 'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs:episodes'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'Blogger repair episodes must be exactly registered by request/root')
  assert.equal(entry.owner, 'blogger-companion')
  assert.equal(entry.issuer, 'PluginBloggerScope.ClaimRepairEpisode')
  assert.match(entry.key, /RequestId/)
  assert.match(entry.key, /AuthorityRoot/)
  assert.match(entry.rules, /exact request id/)
  for (const consumer of ['ClaimRepairEpisode', 'TryGetRepairEpisode', 'CancelRepairEpisode']) {
    assert.ok(
      entry.allowedConsumers.includes(`PluginBloggerScope.${consumer}`),
      `${consumer} must be an allowed consumer`,
    )
  }
  assert.equal(entry.revoke, 'PluginBloggerScope.CancelRepairEpisode')
  assert.equal(entry.testAnchor, 'requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
  assert.equal(
    REGISTERED_DECLARATIONS.has('src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs:episodeCompletions'),
    false,
    'episodeCompletions drain tasks must not be registered as a program-counter cell',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_blogger_repair_episodes_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let episodes = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.DropForeignEpisodes(sessionId: string) = episodes.Remove(sessionId) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the repair episode registry must be RED')
  assert.ok(regressions.some((v) => v.name === 'episodes' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_borrowing_credit_source_exact_lender_is_live', () => {
  const regKey = 'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Borrowing.fs:creditSourceByExecution'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'creditSourceByExecution must be exactly registered by physical execution and explicit lender')
  assert.equal(entry.owner, 'execution-model-routing')
  assert.equal(entry.issuer, 'BorrowingCapacity.RouteFresh')
  assert.match(entry.key, /PhysicalUserMessageId/)
  assert.match(entry.key, /LenderSessionId/)
  assert.match(entry.rules, /exactly one lender token/)
  for (const consumer of ['RouteFresh', 'ReleaseSession', 'ReleasePhysical', 'ExactCredit']) {
    assert.ok(
      entry.allowedConsumers.includes(`BorrowingCapacity.${consumer}`),
      `${consumer} must be an allowed consumer`,
    )
  }
  assert.equal(entry.revoke, 'BorrowingCapacity.ReleaseSession')
  assert.equal(entry.testAnchor, 'requirements/execution-model-routing/tests/model-routing-runtime.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_borrowing_credit_source_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let creditSourceByExecution = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.DropForeignSources(key: string) = creditSourceByExecution.Remove(key) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Borrowing.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the borrowing credit map must be RED')
  assert.ok(regressions.some((v) => v.name === 'creditSourceByExecution' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_loop_sensor_armed_exact_run_is_live', () => {
  const regKey = 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs:armed'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'LoopSensor.armed must be exactly registered by session and exact run')
  assert.equal(entry.owner, 'degeneration-guard')
  assert.equal(entry.issuer, 'LoopSensor.TryArm')
  assert.match(entry.key, /ProviderRunIdentity/)
  assert.match(entry.rules, /only the exact reconciled run consumes/)
  for (const consumer of ['TryArm', 'Interrupt', 'ConsumeAbortCause']) {
    assert.ok(
      entry.allowedConsumers.includes(`LoopSensor.${consumer}`),
      `${consumer} must be an allowed consumer`,
    )
  }
  assert.equal(entry.revoke, 'LoopSensor.DropSession')
  assert.equal(entry.testAnchor, 'requirements/degeneration-guard/tests/loop-sensor.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_loop_sensor_armed_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let armed = HashSet<string>()',
    'type Sensor() =',
    '    member _.ClearForeignArmed(sessionId: string) = armed.Remove(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the armed anomaly cell must be RED')
  assert.ok(regressions.some((v) => v.name === 'armed' && v.reason === 'unauthorized-consumer'))
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_loop_sensor_active_interrupts_exact_run_is_live', () => {
  const regKey = 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs:activeInterrupts'
  const entry = REGISTERED_DECLARATIONS.get(regKey)
  assert.ok(entry, 'LoopSensor.activeInterrupts must be exactly registered by session and exact run')
  assert.equal(entry.owner, 'degeneration-guard')
  assert.equal(entry.issuer, 'LoopSensor.Interrupt')
  assert.match(entry.key, /ProviderRunIdentity/)
  assert.match(entry.rules, /matching task identity and run/)
  for (const consumer of ['Interrupt', 'ConsumeAbortCause', 'ActiveInterruptTask']) {
    assert.ok(
      entry.allowedConsumers.includes(`LoopSensor.${consumer}`),
      `${consumer} must be an allowed consumer`,
    )
  }
  assert.equal(entry.revoke, 'LoopSensor.DropSession')
  assert.equal(entry.testAnchor, 'requirements/degeneration-guard/tests/loop-sensor.test.mjs')
  assert.equal(isRegistrationLive(regKey, entry), true)
})

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_loop_sensor_active_interrupts_rejects_unauthorized_consumer', () => {
  const source = [
    'module Sample',
    'let activeInterrupts = Dictionary<string, string>()',
    'type Scope() =',
    '    member _.TryTakeForeignInterrupt(sessionId: string) =',
    '        match activeInterrupts.TryGetValue(sessionId) with',
    '        | true, value -> activeInterrupts.Remove(sessionId) |> ignore; Some value',
    '        | _ -> None',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'unauthorized consumer of the active interrupt cell must be RED')
  assert.ok(regressions.some((v) => v.name === 'activeInterrupts' && v.reason === 'unauthorized-consumer'))
})
