import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  KNOWN_DEBT_BASELINE,
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
  // recoveryArming is in the known debt baseline
  assert.ok(hits.some((v) => v.name === 'recoveryArming'))
  assert.ok(hits.every((v) => v.knownDebt === true), 'recoveryArming must be recognized as known debt')
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
  assert.ok(KNOWN_DEBT_BASELINE.has('src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs::recoveryArming'))
  assert.ok(KNOWN_DEBT_BASELINE.has('src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs::attemptPlans'))
  assert.ok(KNOWN_DEBT_BASELINE.has('src/Wanxiangshu/Strength/OpenCode/PluginScope.fs::counterfactualAwait'))
  assert.ok(KNOWN_DEBT_BASELINE.has('src/Wanxiangshu/OpenCode/Host/LoopSensor.fs::armed'))
  assert.ok(KNOWN_DEBT_BASELINE.has('src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs::armed'))
  assert.equal(KNOWN_DEBT_BASELINE.size, 5, 'baseline must contain exactly 5 known debt entries')
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
