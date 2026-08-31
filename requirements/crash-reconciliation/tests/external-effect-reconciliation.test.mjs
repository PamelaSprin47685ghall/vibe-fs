import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import {
  loadValidationContext,
  scanRecoveryProgramCounters,
  validateExternalEffectRegistry,
} from '../../../scripts/checks/external-effect-reconciliation.mjs'
import { validateProofLevelRegistry } from '../../../scripts/lib/requirement-trace.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const REGISTRY_PATH = join(ROOT, 'scripts/checks/external-effect-contracts.json')
const registry = JSON.parse(readFileSync(REGISTRY_PATH, 'utf8'))
const context = loadValidationContext(ROOT, registry)
const copy = () => structuredClone(registry)
const row = (document, id) => document.effects.find((effect) => effect.id === id)
const finding = (document, code, supplied = context) => validateExternalEffectRegistry(document, supplied).find((item) => item.code === code)

const assertOwnedDiagnostic = (item, effect, owner, law) => {
  assert.ok(item, `missing diagnostic for ${effect}`)
  assert.equal(item.effect, effect)
  assert.equal(item.owner, owner)
  assert.match(item.message, new RegExp(`^${effect} owner=${owner} law=${law.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}:`))
}

test('WHAT[CRASH-019] external_effect_registry_accepts_the_closed_12_row_contract', () => {
  assert.deepEqual(validateExternalEffectRegistry(registry, context), [])
  assert.deepEqual(registry.effects.map((effect) => effect.id), [
    'canonical-append',
    'writer-sync',
    'worktree-create',
    'branch-fast-forward',
    'prompt-dispatch',
    'blogger-request',
    'todo-write',
    'js-transaction',
    'provider-execution',
    'managed-child',
    'managed-attempt-interrupt',
    'bounded-process',
  ])
})

test('WHAT[CRASH-019] effect phases name the actual admission and physical operation boundaries', () => {
  assert.deepEqual(row(registry, 'worktree-create').admission, {
    kind: 'not-applicable',
    reason: 'The durable request and resolved physical query directly decide adoption or creation; there is no separate process-local admission capability.',
  })
  assert.deepEqual(row(registry, 'worktree-create').physical_receipt, {
    kind: 'physical-identity',
    path: 'src/Wanxiangshu/Change/Runtime.fs',
    symbols: ['WorktreeResource.Create', 'WorktreeResource.Adopt'],
  })
  assert.deepEqual(row(registry, 'branch-fast-forward').admission.symbols, ['publishUnderGate', 'AcquirePublishGate'])
  assert.deepEqual(row(registry, 'branch-fast-forward').physical_receipt.symbols, ['ffMerge'])
  assert.deepEqual(row(registry, 'todo-write').admission.symbols, ['runBeforeTodo', 'PreparedBridge'])
  assert.deepEqual(row(registry, 'todo-write').physical_receipt.symbols, ['runAfterTodo', 'PhysicalSuccessEvidence.LiveAfterSuccess'])
  assert.ok(row(registry, 'blogger-request').proof_portfolio.some((proof) =>
    proof.title === 'WHAT[EFFECT-ACCOUNTING-008] C5_entry_commit_records_receipt_and_clears_open_request'))
})

test('WHAT[CRASH-019] duplicate_and_malformed_effect_rows_fail_closed', () => {
  const duplicate = copy()
  duplicate.effects.push(structuredClone(duplicate.effects[0]))
  assertOwnedDiagnostic(finding(duplicate, 'EFFECT_ROW_DUPLICATE'), 'canonical-append', 'durable-events', 'row.id')

  const malformed = copy()
  delete row(malformed, 'canonical-append').intent.kind
  assertOwnedDiagnostic(finding(malformed, 'EFFECT_PHASE_KIND'), 'canonical-append', 'durable-events', 'phase.intent')
})

test('WHAT[CRASH-019] unknown_and_foreign_WHAT_ownership_is_rejected', () => {
  const unknown = copy()
  row(unknown, 'canonical-append').what_ids = ['DURABLE-EVENTS-999']
  assertOwnedDiagnostic(finding(unknown, 'EFFECT_WHAT_UNKNOWN'), 'canonical-append', 'durable-events', 'WHAT[DURABLE-EVENTS-999]')

  const foreign = copy()
  row(foreign, 'canonical-append').what_ids = ['CRASH-019']
  assertOwnedDiagnostic(finding(foreign, 'EFFECT_WHAT_FOREIGN'), 'canonical-append', 'durable-events', 'WHAT[CRASH-019]')
})

test('WHAT[CRASH-019] stale_source_symbol_and_proof_title_are_exact_failures', () => {
  const staleSource = copy()
  row(staleSource, 'bounded-process').reentry.symbol = 'runProgramRenamed'
  assertOwnedDiagnostic(finding(staleSource, 'EFFECT_SOURCE_SYMBOL'), 'bounded-process', 'process-execution', 'reentry')

  const staleProof = copy()
  row(staleProof, 'bounded-process').proof_portfolio[0].title += '_renamed'
  assertOwnedDiagnostic(finding(staleProof, 'EFFECT_PROOF_STALE'), 'bounded-process', 'process-execution', 'proof.PROC-004')
})

test('WHAT[CRASH-019] physical_receipts_must_anchor_effect_calls_not_symbol_existing_types', () => {
  const staleWorktree = copy()
  row(staleWorktree, 'worktree-create').physical_receipt = {
    kind: 'physical-identity',
    path: 'src/Wanxiangshu/Git/WorktreeResource.fs',
    symbols: ['WorktreeResource', 'Identity', 'Adopt'],
  }
  assertOwnedDiagnostic(finding(staleWorktree, 'EFFECT_PHASE_TRUTH'), 'worktree-create', 'change-integration', 'phase.physical_receipt')

  const staleTodo = copy()
  row(staleTodo, 'todo-write').physical_receipt = {
    kind: 'physical-observation',
    path: 'src/Wanxiangshu/Mission/Obligation/Todo/Facts.fs',
    symbols: ['PhysicalSuccessEvidence'],
  }
  assertOwnedDiagnostic(finding(staleTodo, 'EFFECT_PHASE_TRUTH'), 'todo-write', 'effect-accounting', 'phase.physical_receipt')
})

test('WHAT[CRASH-019] acknowledged_effect_without_restart_law_is_rejected', () => {
  const document = copy()
  delete row(document, 'prompt-dispatch').ambiguity
  assertOwnedDiagnostic(finding(document, 'EFFECT_RESTART_LAW'), 'prompt-dispatch', 'dispatch-protocol', 'ambiguity')
})

test('WHAT[CRASH-019] ambiguity_must_be_finite_and_retry_must_be_proven_safe', () => {
  const unbounded = copy()
  row(unbounded, 'worktree-create').ambiguity.states.push('*')
  assertOwnedDiagnostic(finding(unbounded, 'EFFECT_AMBIGUITY_UNBOUNDED'), 'worktree-create', 'change-integration', 'ambiguity.states')

  const unsafe = copy()
  row(unsafe, 'branch-fast-forward').ambiguity.mechanisms = ['finite-fail-closed']
  assertOwnedDiagnostic(finding(unsafe, 'EFFECT_RETRY_UNSAFE'), 'branch-fast-forward', 'change-integration', 'ambiguity.retry_when')
})

test('WHAT[CRASH-019] external_boundaries_require_adapter_and_deterministic_ambiguity_proofs', () => {
  const document = copy()
  row(document, 'branch-fast-forward').proof_portfolio = row(document, 'branch-fast-forward').proof_portfolio.filter((proof) => proof.level !== 'adapter')
  assertOwnedDiagnostic(finding(document, 'EFFECT_ADAPTER_PROOF'), 'branch-fast-forward', 'change-integration', 'proof_portfolio')
})

test('WHAT[CRASH-019] proof_levels_are_independent_exact_and_cannot_be_self_relabelled', () => {
  const relabelled = copy()
  row(relabelled, 'bounded-process').proof_portfolio[0].level = 'adapter'
  assertOwnedDiagnostic(finding(relabelled, 'EFFECT_PROOF_LEVEL_MISMATCH'), 'bounded-process', 'process-execution', 'proof.PROC-004')

  const unknownContext = {
    ...context,
    proofLevelRegistry: {
      ...structuredClone(context.proofLevelRegistry),
      proofs: context.proofLevelRegistry.proofs.filter((proof) => !(
        proof.path === row(registry, 'bounded-process').proof_portfolio[0].path
        && proof.title === row(registry, 'bounded-process').proof_portfolio[0].title
        && proof.what_id === row(registry, 'bounded-process').proof_portfolio[0].what_id
      )),
    },
  }
  assertOwnedDiagnostic(
    finding(copy(), 'EFFECT_PROOF_CLASSIFICATION_UNKNOWN', unknownContext),
    'bounded-process',
    'process-execution',
    'proof.PROC-004',
  )

  const invalidRegistry = structuredClone(context.proofLevelRegistry)
  invalidRegistry.proofs[0].level = 'self-declared-adapter'
  const invalidContext = {
    ...context,
    proofLevelRegistry: invalidRegistry,
    proofLevelRegistryFindings: validateProofLevelRegistry(invalidRegistry),
  }
  const invalidFinding = finding(copy(), 'EFFECT_PROOF_REGISTRY_INVALID', invalidContext)
  assert.equal(invalidFinding.effect, '<registry>')
  assert.equal(invalidFinding.owner, '<unknown>')
  assert.match(invalidFinding.message, /^registry owner=<unknown> law=proof-level-registry:/)
})

test('WHAT[CRASH-019] recovery_program_counters_are_rejected_without_scanning_comments_or_strings', () => {
  assert.deepEqual(scanRecoveryProgramCounters('// RecoveryStage\nlet label = "ResumeAt"\n', 'safe.fs'), [])
  assert.deepEqual(scanRecoveryProgramCounters('let RecoveryStep = 1\n', 'bad.fs'), [
    { path: 'bad.fs', line: 1, symbol: 'RecoveryStep' },
  ])

  const document = copy()
  const effect = row(document, 'canonical-append')
  const sources = new Map(context.sources)
  sources.set(effect.reentry.path, `${sources.get(effect.reentry.path)}\nlet NextAction = 1\n`)
  const supplied = { ...context, sources }
  assertOwnedDiagnostic(finding(document, 'EFFECT_RECOVERY_PC', supplied), 'canonical-append', 'durable-events', 'reentry')
})
