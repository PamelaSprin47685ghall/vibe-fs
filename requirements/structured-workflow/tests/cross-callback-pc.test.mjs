import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  KNOWN_DEBT_BASELINE,
  EXEMPTION_CATEGORIES,
  BASELINE_MAX_SIZE,
  evaluateViolations,
  scanText,
  scanFiles,
} from '../../../scripts/checks/cross-callback-pc.mjs'

const readFixture = (name) => readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

// ── Pattern 1: DU await state (CounterfactualAwait shape) ──────────────────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_du_await_state_without_proof_is_RED', () => {
  const source = readFixture('cross-callback-pc-illegal.fs')
  const hits = scanText(source, 'src/Wanxiangshu/New/CounterfactualCollector.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'DU await state without proof annotation must be RED')
  assert.ok(
    regressions.some((v) => v.pattern === 'du-await-state' || v.pattern === 'trytake-continuation'),
    'must detect DU await or TryTake pattern',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_private_du_await_state_is_detected', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_trytake_without_proof_is_RED', () => {
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
  // recoveryArming was removed from the known debt baseline after being proven
  // as a physical single-flight channel (DSL-cross-callback-proof: physical).
  // A synthetic source WITHOUT the proof annotation must now be RED.
  const { regressions, ok } = evaluateViolations(hits)
  assert.ok(hits.some((v) => v.name === 'recoveryArming'))
  assert.equal(ok, false, 'recoveryArming without proof annotation must be RED after baseline removal')
  assert.ok(regressions.some((v) => v.name === 'recoveryArming'))
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_new_trytake_not_in_baseline_is_RED', () => {
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
  assert.equal(ok, false, 'new TryTake pattern not in baseline must be RED')
  assert.ok(regressions.some((v) => v.name === 'newContinuation'))
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_trytake_only_marks_the_registry_it_consumes', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_armed_probe_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — one-shot armed mark',
    'let armed = HashSet<string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = armed.Contains(sessionId)',
    '    member _.TryArm(sessionId: string) = armed.Add(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  // armed in LoopSensor is in the known debt baseline
  assert.ok(hits.some((v) => v.name === 'armed' && v.pattern === 'armed-presence-probe'))
  assert.ok(hits.every((v) => v.knownDebt === true), 'LoopSensor.armed must be recognized as known debt')
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_new_armed_not_in_baseline_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — new armed mark',
    'let newArmed = HashSet<string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = newArmed.Contains(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Sensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'new armed probe not in baseline must be RED')
  assert.ok(regressions.some((v) => v.name === 'newArmed'))
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_armed_probe_only_marks_the_registry_it_reads', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_physical_proof_annotation_stays_green', () => {
  const source = readFixture('cross-callback-pc-physical.fs')
  const hits = scanText(source, 'src/Wanxiangshu/Process/PtyManager.fs')
  assert.deepEqual(hits, [], 'physical proof annotation must whitelist the pattern')
})

// ── Green: no pattern (plain resource without TryTake/IsArmed/DU-await) ─────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_plain_resource_without_pattern_stays_green', () => {
  const source = readFixture('cross-callback-pc-clean.fs')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  assert.deepEqual(hits, [], 'plain resource without TryTake/IsArmed/DU-await must stay green')
})

// ── Baseline completeness ───────────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_baseline_covers_known_debt', () => {
  assert.ok(KNOWN_DEBT_BASELINE.has('src/Wanxiangshu/OpenCode/Host/LoopSensor.fs::armed'))
  assert.equal(KNOWN_DEBT_BASELINE.size, 1, 'baseline must contain exactly 1 known debt entry')
  assert.ok(KNOWN_DEBT_BASELINE.size <= BASELINE_MAX_SIZE, 'baseline size must not exceed BASELINE_MAX_SIZE (ratchet)')
})

// ── scanFiles aggregates ────────────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_scanFiles_aggregates_entries', () => {
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

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_clear_presence_without_proof_is_detected', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — armed mark',
    'let armed = HashSet<string>()',
    'type Sensor() =',
    '    member _.IsArmed(sessionId: string) = armed.Contains(sessionId)',
    '    member _.ClearArmed(sessionId: string) = armed.Remove(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  // armed in LoopSensor is in the known debt baseline; ClearArmed triggers clear-presence-probe
  assert.ok(hits.some((v) => v.name === 'armed'))
  assert.ok(hits.every((v) => v.knownDebt === true), 'LoopSensor.armed must be recognized as known debt')
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_new_clear_presence_not_in_baseline_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — new clear mark',
    'let newArmed = HashSet<string>()',
    'type Sensor() =',
    '    member _.ClearArmed(sessionId: string) = newArmed.Remove(sessionId)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/Sensor.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'new clear-presence probe not in baseline must be RED')
  assert.ok(regressions.some((v) => v.name === 'newArmed' && v.pattern === 'clear-presence-probe'))
})

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_drop_attempt_presence_without_proof_is_RED', () => {
  const source = [
    'module Sample',
    '// DSL-MUTABLE: single-flight — attempt tracking',
    'let attempts = Dictionary<string, Attempt>()',
    'type Scope() =',
    '    member _.DropAttempt(sessionId: string) = attempts.Remove(sessionId) |> ignore',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/New/AttemptTracker.fs')
  const { regressions, ok } = evaluateViolations(hits)
  assert.equal(ok, false, 'DropAttempt presence-clearing not in baseline must be RED')
  assert.ok(regressions.some((v) => v.name === 'attempts' && v.pattern === 'clear-presence-probe'))
})

// ── EXEMPTION_CATEGORIES completeness ───────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_exemption_categories_contains_physical_capability_types', () => {
  for (const category of [
    'pty', 'timer', 'waiter', 'single-flight', 'quiescence-permit',
    'process-handle', 'socket', 'cancellation-token', 'resource',
  ]) {
    assert.ok(EXEMPTION_CATEGORIES.has(category), `EXEMPTION_CATEGORIES must contain '${category}'`)
  }
  assert.equal(EXEMPTION_CATEGORIES.size, 9, 'EXEMPTION_CATEGORIES must contain exactly 9 categories')
})

// ── BASELINE_MAX_SIZE ratchet ───────────────────────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_baseline_max_size_is_a_ratchet_ceiling', () => {
  assert.equal(typeof BASELINE_MAX_SIZE, 'number')
  assert.ok(BASELINE_MAX_SIZE >= 0, 'BASELINE_MAX_SIZE must be a non-negative integer')
  assert.ok(KNOWN_DEBT_BASELINE.size <= BASELINE_MAX_SIZE,
    `KNOWN_DEBT_BASELINE.size (${KNOWN_DEBT_BASELINE.size}) must not exceed BASELINE_MAX_SIZE (${BASELINE_MAX_SIZE})`)
})

// ── SessionQuiescenceGate as legal reference (green) ────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-017] CROSS_CALLBACK_PC_session_quiescence_gate_stays_green', () => {
  // SessionQuiescenceGate is the legal reference for quiescence-permit exemption.
  // It uses mutable Map (not Dictionary/HashSet), so it does not match the
  // REGISTRY_DECLARATION pattern. This is correct: the gate's TryConsume
  // receives a typed QuiescencePermit, not a key-based TryGetValue.
  const source = [
    'module Sample',
    'type SessionQuiescenceGate() =',
    '    let mutable activities = Map.empty<string, int>',
    '    member _.TryConsume(permit: QuiescencePermit) : bool =',
    '        lock gate (fun () -> true)',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/OpenCode/Host/SessionQuiescenceGate.fs')
  assert.deepEqual(hits, [], 'SessionQuiescenceGate must stay green — typed permit, not registry presence')
})
