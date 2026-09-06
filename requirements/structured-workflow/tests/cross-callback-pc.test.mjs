import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  EXEMPTION_CATEGORIES,
  REGISTERED_DECLARATIONS,
  auditFiles,
  evaluateViolations,
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
    '    member _.TryArm(sessionId: string) = armed.Add(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'an exact production-looking path grants no exemption')
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

test('WHAT[STRUCTURED-WORKFLOW-006] CROSS_CALLBACK_PC_physical_proof_annotation_stays_green', () => {
  const source = readFixture('cross-callback-pc-physical.fs')
  const hits = scanText(source, 'src/Wanxiangshu/Process/PtyManager.fs')
  assert.deepEqual(hits, [], 'physical proof annotation must whitelist the pattern')
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
    'process-handle', 'socket', 'cancellation-token', 'resource',
  ]) {
    assert.ok(EXEMPTION_CATEGORIES.has(category), `EXEMPTION_CATEGORIES must contain '${category}'`)
  }
  assert.equal(EXEMPTION_CATEGORIES.size, 9, 'EXEMPTION_CATEGORIES must contain exactly 9 categories')
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
