// Test-surface debt scanner (P1/P2 shared core).
//
// Scans the complete requirements/**/tests executable dependency zone (.mjs
// and .js), including support, fixtures, helpers, e2e, and integration files.
// It reports the debt
// classes a semantic test must not carry (TASK.md §1):
//
//   A. deep production import   import '...dist/<internal>.js' (not fable_modules)
//   B. Fable export discovery   Object.keys / Object.entries on F# modules and
//                              startsWith('Foo__') / endsWith('_Bar') lookup
//   C. Fable representation     .tag / .fields / .cases() / FSharp* names / fable_modules
//   D. legacy interop authority member( / bind( / fableInstanceMethod( / prod( /
//                              toList( / caseOf( / payloadOf( / resultOf( / unwrapOption(
//
// Compiler/build verification files (emitted-surface pins, domain.meta's artifact self-contract,
// distribution artifact tests, the representation validator, and the coverage runner) are exempt
// only by explicit path allowlist: their subject is the compiled/representation artifact, not
// product semantics. Product-package tests/support, fixtures, e2e, integration, and contract
// adapters remain in the scanned zone.

import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from './walk.mjs'

export const REQUIREMENTS_ROOT = join(
  join(join(join(fileURLToPath(import.meta.url), '..'), '..'), '..'),
  'requirements',
)

/** Compiler/build verification: subject is the emitted artifact, not semantics. */
export const BUILD_VERIFICATION_FILES = new Set([
  'requirements/verification-system/tests/guide-contract.test.mjs',
  'requirements/verification-system/tests/domain.meta.test.mjs',
  // Its subject is the coverage/build runner itself, including the literal
  // fable_modules exclusion that keeps the coverage denominator honest.
  'requirements/verification-system/tests/coverage-gate.test.mjs',
  'requirements/distribution/tests/pack-closure.test.mjs',
  'requirements/distribution/tests/cwd-independent-resources.test.mjs',
  // Representation validator: its subject is the JS-native boundary rules,
  // so it must be able to spell out the forbidden Fable shapes.
  'requirements/verification-system/tests/support/js-contract.mjs',
  'requirements/verification-system/tests/support/run-inner.mjs',
  'requirements/verification-system/tests/support/coverage-policy.mjs',
])

/** Physical host-shape canaries (HOST-BOUNDARY-019): their subject is the raw
 *  Host SDK snapshot / Fable representation itself — locating a ToolPart,
 *  proving run-id equivalence, or observing a projection requires reading the
 *  exact raw shape. Routing these through a semantic surface would hide
 *  precisely what the canary exists to prove. */
export const HOST_PHYSICAL_CANARY_FILES = new Set([
  'requirements/host-boundary/tests/host-message-projection.test.mjs',
  'requirements/host-boundary/tests/host-session-context.test.mjs',
  'requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs',
  'requirements/host-boundary/tests/magic-todo-host-canaries.test.mjs',
  'requirements/host-boundary/tests/session-snapshot-locality.test.mjs',
])

/**
 * Registered semantic-surface manifest (JS-SEMANTIC-SURFACE-002/003).
 *
 * A semantic test may import a registered surface directly: the surface IS the
 * legal entry point (owner boundary translation, JSON-shaped in/out). Deep
 * imports of any other dist module remain debt. Register here when a surface
 * is established — registration requires the full manifest below (owner
 * package, governing laws, production source, representation, kind), so a
 * surface exists because a semantic component owns a contract, never because
 * a test wants access (TASK.md §9/§10, PR 4).
 */
export const SURFACE_MANIFEST = [
  {
    module: 'Interaction/Authority/Surface.js',
    owner: 'participant-identity',
    laws: ['PID-005', 'PID-006'],
    source: 'src/Wanxiangshu/Interaction/Authority/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Participant/Persona/SessionSurface.js',
    owner: 'participant-identity',
    laws: ['PID-003', 'PID-010'],
    source: 'src/Wanxiangshu/Participant/Persona/SessionSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Participant/Persona/Surface.js',
    owner: 'participant-identity',
    laws: ['PID-001', 'PID-002', 'PID-007', 'PID-009'],
    source: 'src/Wanxiangshu/Participant/Persona/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Plugin/Plugin.js',
    owner: 'execution-model-routing',
    laws: ['EMR-003'],
    source: 'src/Wanxiangshu/OpenCode/Plugin/Plugin.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/ModelRoutingSurface.js',
    owner: 'execution-model-routing',
    laws: ['EMR-001', 'EMR-002', 'EMR-003', 'EMR-004', 'EMR-005', 'EMR-006', 'EMR-007', 'EMR-008', 'EMR-009'],
    source: 'src/Wanxiangshu/OpenCode/Host/ModelRoutingSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/SessionBindingSurface.js',
    owner: 'participant-identity',
    laws: ['PID-008'],
    source: 'src/Wanxiangshu/OpenCode/Host/SessionBindingSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/Blogger/TomlSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-009'],
    source: 'src/Wanxiangshu/Context/Companion/Blogger/TomlSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/ChildRecoverySurface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-009', 'CRASH-010', 'CRASH-011', 'CRASH-012'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecoverySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/OpenCode/JoinSurface.js',
    owner: 'delegation',
    laws: ['DELEG-013', 'DELEG-015'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/CleanBreakSurface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-009', 'CRASH-012', 'EFFECT-ACCOUNTING-007'],
    lawOwners: { 'EFFECT-ACCOUNTING-007': 'effect-accounting' },
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/CleanBreakSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/Host/JoinSurface.js',
    owner: 'delegation',
    laws: ['DELEG-013', 'DELEG-015', 'CRASH-011'],
    lawOwners: { 'CRASH-011': 'crash-reconciliation' },
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/JoinSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/Host/Runtime.js',
    owner: 'delegation',
    laws: ['DELEG-019'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Session/Recovery/Surface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-005', 'CRASH-010', 'CRASH-013', 'CRASH-014'],
    source: 'src/Wanxiangshu/Execution/Session/Recovery/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Session/OpenCode/HorizonSurface.js',
    owner: 'delegation',
    laws: ['DELEG-016', 'PARTICIPANT-HORIZON-004', 'PARTICIPANT-HORIZON-011'],
    lawOwners: {
      'PARTICIPANT-HORIZON-004': 'participant-horizon',
      'PARTICIPANT-HORIZON-011': 'participant-horizon',
    },
    source: 'src/Wanxiangshu/Execution/Session/OpenCode/HorizonSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/Blogger/BloggerCrashSurface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-016'],
    source: 'src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/Blogger/Runtime/CycleSurface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-016', 'EFFECT-ACCOUNTING-004', 'EFFECT-ACCOUNTING-008'],
    lawOwners: {
      'EFFECT-ACCOUNTING-004': 'effect-accounting',
      'EFFECT-ACCOUNTING-008': 'effect-accounting',
    },
    source: 'src/Wanxiangshu/Context/Companion/Blogger/Runtime/CycleSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/LifecycleSurface.js',
    owner: 'managed-session-lifecycle',
    laws: ['MANAGED-SESSION-012'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/LifecycleSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Enforcer/RepairSurface.js',
    owner: 'provider-attempt-recovery',
    laws: ['PAR-012'],
    source: 'src/Wanxiangshu/Enforcer/RepairSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Participant/Provider/Attempt/Fallback/CursorSurface.js',
    owner: 'provider-attempt-recovery',
    laws: [
      'PAR-001',
      'PAR-002',
      'PAR-003',
      'PAR-004',
      'PAR-005',
      'PAR-006',
      'PAR-007',
      'PAR-008',
      'PAR-009',
      'PAR-010',
      'PAR-011',
      'PAR-013',
      'PAR-014',
      'PAR-016',
      'EFFECT-ACCOUNTING-004',
      'VERIFICATION-SYSTEM-008',
    ],
    lawOwners: {
      'EFFECT-ACCOUNTING-004': 'effect-accounting',
      'VERIFICATION-SYSTEM-008': 'verification-system',
    },
    source: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/CursorSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Delegation/SyncDelegate/Surface.js',
    owner: 'delegation',
    laws: ['DELEG-005', 'DELEG-010', 'DELEG-015', 'DELEG-019', 'DELEG-021', 'DELEG-022', 'MANAGED-SESSION-001'],
    lawOwners: { 'MANAGED-SESSION-001': 'managed-session-lifecycle' },
    source: 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Delegation/DelegatedToolEstimateSurface.js',
    owner: 'delegation',
    laws: ['DELEG-022'],
    source: 'src/Wanxiangshu/Execution/Delegation/DelegatedToolEstimateSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/HandoffSurface.js',
    owner: 'delegation',
    laws: ['DELEG-024'],
    source: 'src/Wanxiangshu/Execution/Delegation/HandoffSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/Surface.js',
    owner: 'delegation',
    laws: ['DELEG-019'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Fork/OpenCode/ToolSurface.js',
    owner: 'delegation',
    laws: ['DELEG-024', 'DELEG-025', 'DELEG-026'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/ToolSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Fission/Surface.js',
    owner: 'intra-participant-parallelism',
    laws: [
      'INTRA-PARTICIPANT-PARALLELISM-001',
      'INTRA-PARTICIPANT-PARALLELISM-002',
      'INTRA-PARTICIPANT-PARALLELISM-003',
      'INTRA-PARTICIPANT-PARALLELISM-004',
      'INTRA-PARTICIPANT-PARALLELISM-005',
      'INTRA-PARTICIPANT-PARALLELISM-006',
      'INTRA-PARTICIPANT-PARALLELISM-007',
      'INTRA-PARTICIPANT-PARALLELISM-008',
      'INTRA-PARTICIPANT-PARALLELISM-011',
      'INTRA-PARTICIPANT-PARALLELISM-013',
    ],
    source: 'src/Wanxiangshu/Execution/Fission/Surface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/FissionHostSurface.js',
    owner: 'intra-participant-parallelism',
    laws: ['INTRA-PARTICIPANT-PARALLELISM-009'],
    source: 'src/Wanxiangshu/OpenCode/Host/FissionHostSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Foundation/RolesSurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-002'],
    source: 'src/Wanxiangshu/Foundation/RolesSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Foundation/SyntheticTomlSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-008', 'PROVIDER-PROJECTION-010', 'PROVIDER-PROJECTION-012'],
    source: 'src/Wanxiangshu/Foundation/SyntheticTomlSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Host/Contract/ToolResultBound.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-015'],
    source: 'src/Wanxiangshu/Host/Contract/ToolResultBound.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Host/Contract/CompactionPolicySurface.js',
    owner: 'context-compression',
    laws: ['CONTEXT-COMPRESSION-002', 'CONTEXT-COMPRESSION-005', 'HOST-BOUNDARY-007'],
    lawOwners: { 'HOST-BOUNDARY-007': 'host-boundary' },
    source: 'src/Wanxiangshu/Host/Contract/CompactionPolicySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/QuiescenceSurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-018', 'ENF-019', 'CRASH-001', 'CRASH-006', 'CRASH-008'],
    lawOwners: {
      'CRASH-001': 'crash-reconciliation',
      'CRASH-006': 'crash-reconciliation',
      'CRASH-008': 'crash-reconciliation',
    },
    source: 'src/Wanxiangshu/OpenCode/Host/QuiescenceSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/ExplicitResumeSurface.js',
    owner: 'crash-reconciliation',
    laws: ['CRASH-018'],
    source: 'src/Wanxiangshu/OpenCode/Host/ExplicitResumeSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Tools/ExecutorToolSurface.js',
    owner: 'process-execution',
    laws: ['PROC-011'],
    source: 'src/Wanxiangshu/OpenCode/Tools/ExecutorToolSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Process/LargeGateSurface.js',
    owner: 'output-distillation',
    laws: ['DISTILL-011'],
    source: 'src/Wanxiangshu/Process/LargeGateSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Mission/Manager/FinalitySurface.js',
    owner: 'finality',
    laws: [
      'FINALITY-001',
      'FINALITY-002',
      'FINALITY-003',
      'FINALITY-004',
      'FINALITY-005',
      'FINALITY-006',
      'FINALITY-009',
      'FINALITY-016',
      'FINALITY-017',
      'FINALITY-019',
      'FINALITY-022',
      'FINALITY-026',
      'FINALITY-027',
      'FINALITY-028',
    ],
    source: 'src/Wanxiangshu/Mission/Manager/FinalitySurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'Mission/Finality/PromptSurface.js',
    owner: 'finality',
    laws: [
      'FINALITY-004', 'FINALITY-012', 'FINALITY-013', 'FINALITY-019',
      'FINALITY-020', 'FINALITY-022', 'FINALITY-024', 'FINALITY-026',
    ],
    source: 'src/Wanxiangshu/Mission/Finality/PromptSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Repository/Knowledge/Casebook/Surface.js',
    owner: 'knowledge-reuse',
    laws: ['KNOWLEDGE-REUSE-002', 'KNOWLEDGE-REUSE-003', 'KNOWLEDGE-REUSE-004', 'KNOWLEDGE-REUSE-008', 'KNOWLEDGE-REUSE-010'],
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Repository/Knowledge/Casebook/IndexSurface.js',
    owner: 'knowledge-reuse',
    laws: ['KNOWLEDGE-REUSE-012'],
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/IndexSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Repository/Knowledge/Casebook/BookkeeperSurface.js',
    owner: 'knowledge-reuse',
    laws: ['KNOWLEDGE-REUSE-006', 'KNOWLEDGE-REUSE-010'],
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Repository/Knowledge/Casebook/LifecycleSurface.js',
    owner: 'knowledge-reuse',
    laws: ['KNOWLEDGE-REUSE-006', 'KNOWLEDGE-REUSE-010'],
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/LifecycleSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Repository/Knowledge/Casebook/FetchSurface.js',
    owner: 'knowledge-reuse',
    laws: ['KNOWLEDGE-REUSE-004', 'KNOWLEDGE-REUSE-005', 'KNOWLEDGE-REUSE-009', 'KNOWLEDGE-REUSE-011'],
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/FetchSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Participant/Provider/LanguageSurface.js',
    owner: 'provider-language',
    laws: [
      'PROVIDER-LANGUAGE-001',
      'PROVIDER-LANGUAGE-002',
      'PROVIDER-LANGUAGE-003',
      'PROVIDER-LANGUAGE-004',
      'PROVIDER-LANGUAGE-005',
      'PROVIDER-LANGUAGE-006',
      'PROVIDER-LANGUAGE-007',
      'PROVIDER-LANGUAGE-008',
      'PROVIDER-LANGUAGE-009',
      'PROVIDER-LANGUAGE-010',
      'PROVIDER-LANGUAGE-011',
    ],
    source: 'src/Wanxiangshu/Participant/Provider/LanguageSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Sphinx/Surface.js',
    owner: 'epistemic-reasoning',
    laws: [
      'EPI-001',
      'EPI-002',
      'EPI-003',
      'EPI-004',
      'EPI-005',
      'EPI-006',
      'EPI-007',
      'EPI-008',
      'EPI-009',
      'EPI-010',
      'EPI-011',
      'EPI-012',
      'EPI-013',
      'EPI-014',
    ],
    source: 'src/Wanxiangshu/Sphinx/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Context/Prefix/Surface.js',
    owner: 'prefix-stability',
    laws: [
      'PREFIX-STABILITY-001',
      'PREFIX-STABILITY-002',
      'PREFIX-STABILITY-003',
      'PREFIX-STABILITY-004',
      'PREFIX-STABILITY-006',
      'PREFIX-STABILITY-008',
      'PREFIX-STABILITY-011',
      'PREFIX-STABILITY-013',
      'PREFIX-STABILITY-015',
    ],
    source: 'src/Wanxiangshu/Context/Prefix/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Prefix/XWireSurface.js',
    owner: 'prefix-stability',
    laws: [
      'PREFIX-STABILITY-001',
      'PREFIX-STABILITY-003',
      'PREFIX-STABILITY-005',
      'PREFIX-STABILITY-009',
    ],
    source: 'src/Wanxiangshu/Context/Prefix/XWireSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Repository/Investigation/WarmStartSurface.js',
    owner: 'repository-investigation',
    laws: [
      'REPOSITORY-INVESTIGATION-001',
      'REPOSITORY-INVESTIGATION-006',
      'REPOSITORY-INVESTIGATION-007',
      'REPOSITORY-INVESTIGATION-008',
      'REPOSITORY-INVESTIGATION-009',
    ],
    source: 'src/Wanxiangshu/Repository/Investigation/WarmStartSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Repository/Investigation/SembleSurface.js',
    owner: 'repository-investigation',
    laws: ['REPOSITORY-INVESTIGATION-001', 'REPOSITORY-INVESTIGATION-002', 'REPOSITORY-INVESTIGATION-006'],
    source: 'src/Wanxiangshu/Repository/Investigation/SembleSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/LoopSensorSurface.js',
    owner: 'degeneration-guard',
    laws: ['DG-002', 'DG-006', 'DG-007', 'DG-008'],
    source: 'src/Wanxiangshu/OpenCode/Host/LoopSensorSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Composition/Turn/ReconcileSurface.js',
    owner: 'structured-workflow',
    laws: ['STRUCTURED-WORKFLOW-007'],
    source: 'src/Wanxiangshu/Composition/Turn/ReconcileSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Repository/Programming/Js/GeneratorSurface.js',
    owner: 'repository-programming',
    laws: [
      'REPOSITORY-PROGRAMMING-001',
      'REPOSITORY-PROGRAMMING-002',
      'REPOSITORY-PROGRAMMING-003',
      'REPOSITORY-PROGRAMMING-004',
      'REPOSITORY-PROGRAMMING-005',
    ],
    source: 'src/Wanxiangshu/Repository/Programming/Js/GeneratorSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Repository/Programming/Js/RuntimeSurface.js',
    owner: 'repository-programming',
    laws: ['REPOSITORY-PROGRAMMING-006', 'REPOSITORY-PROGRAMMING-012'],
    source: 'src/Wanxiangshu/Repository/Programming/Js/RuntimeSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Repository/Programming/Js/FilesystemSurface.js',
    owner: 'repository-programming',
    laws: [
      'REPOSITORY-PROGRAMMING-007',
      'REPOSITORY-PROGRAMMING-008',
      'REPOSITORY-PROGRAMMING-009',
      'REPOSITORY-PROGRAMMING-013',
      'REPOSITORY-PROGRAMMING-015',
    ],
    source: 'src/Wanxiangshu/Repository/Programming/Js/FilesystemSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Repository/Programming/Js/TransactionSurface.js',
    owner: 'repository-programming',
    laws: [
      'REPOSITORY-PROGRAMMING-007',
      'REPOSITORY-PROGRAMMING-010',
      'REPOSITORY-PROGRAMMING-013',
      'REPOSITORY-PROGRAMMING-014',
      'REPOSITORY-PROGRAMMING-015',
      'REPOSITORY-PROGRAMMING-018',
    ],
    source: 'src/Wanxiangshu/Repository/Programming/Js/TransactionSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Repository/Programming/Js/WorkflowSurface.js',
    owner: 'repository-programming',
    laws: [
      'REPOSITORY-PROGRAMMING-011',
      'REPOSITORY-PROGRAMMING-012',
      'REPOSITORY-PROGRAMMING-013',
      'REPOSITORY-PROGRAMMING-014',
      'REPOSITORY-PROGRAMMING-015',
      'REPOSITORY-PROGRAMMING-016',
      'REPOSITORY-PROGRAMMING-018',
      'REPOSITORY-PROGRAMMING-019',
    ],
    source: 'src/Wanxiangshu/Repository/Programming/Js/WorkflowSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Repository/Programming/Js/OpenCode/ToolHostSurface.js',
    owner: 'repository-programming',
    laws: ['REPOSITORY-PROGRAMMING-005', 'REPOSITORY-PROGRAMMING-016'],
    source: 'src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolHostSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Tools/FileToolsSurface.js',
    owner: 'repository-programming',
    laws: ['REPOSITORY-PROGRAMMING-007', 'REPOSITORY-PROGRAMMING-010'],
    source: 'src/Wanxiangshu/OpenCode/Tools/FileToolsSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Tools/FileMutationSurface.js',
    owner: 'repository-programming',
    laws: ['REPOSITORY-PROGRAMMING-020'],
    source: 'src/Wanxiangshu/OpenCode/Tools/FileMutationSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Context/Companion/Blogger/FrameSurface.js',
    owner: 'context-compression',
    laws: ['CONTEXT-COMPRESSION-011', 'CONTEXT-COMPRESSION-012', 'CONTEXT-COMPRESSION-015', 'CONTEXT-COMPRESSION-016'],
    source: 'src/Wanxiangshu/Context/Companion/Blogger/FrameSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/Blogger/DeltaSurface.js',
    owner: 'context-compression',
    laws: ['CONTEXT-COMPRESSION-003', 'CONTEXT-COMPRESSION-012', 'CONTEXT-COMPRESSION-016'],
    source: 'src/Wanxiangshu/Context/Companion/Blogger/DeltaSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/ProjectionSurface.js',
    owner: 'context-compression',
    laws: ['CONTEXT-COMPRESSION-011', 'CONTEXT-COMPRESSION-012'],
    source: 'src/Wanxiangshu/Context/Companion/ProjectionSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/CompressionSurface.js',
    owner: 'context-compression',
    laws: [
      'CONTEXT-COMPRESSION-002',
      'CONTEXT-COMPRESSION-005',
      'CONTEXT-COMPRESSION-006',
      'CONTEXT-COMPRESSION-007',
      'CONTEXT-COMPRESSION-008',
      'CONTEXT-COMPRESSION-009',
      'CONTEXT-COMPRESSION-010',
      'CONTEXT-COMPRESSION-013',
    ],
    source: 'src/Wanxiangshu/Context/Companion/CompressionSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/RuntimeSurface.js',
    owner: 'context-compression',
    laws: ['CONTEXT-COMPRESSION-006', 'CONTEXT-COMPRESSION-018'],
    source: 'src/Wanxiangshu/Context/Companion/RuntimeSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Mission/Obligation/Todo/MagicTodoSemanticSurface.js',
    owner: 'obligation-ledger',
    laws: [
      'OBLIGATION-LEDGER-001',
      'OBLIGATION-LEDGER-002',
      'OBLIGATION-LEDGER-006',
      'OBLIGATION-LEDGER-007',
      'OBLIGATION-LEDGER-008',
      'OBLIGATION-LEDGER-010',
      'OBLIGATION-LEDGER-012',
      'OBLIGATION-LEDGER-016',
      'OBLIGATION-LEDGER-017',
      'OBLIGATION-LEDGER-020',
      'OBLIGATION-LEDGER-021',
      'OBLIGATION-LEDGER-022',
      'OBLIGATION-LEDGER-026',
    ],
    source: 'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoSemanticSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js',
    owner: 'obligation-ledger',
    laws: ['OBLIGATION-LEDGER-002', 'OBLIGATION-LEDGER-009', 'OBLIGATION-LEDGER-015', 'OBLIGATION-LEDGER-024', 'EFFECT-ACCOUNTING-011'],
    lawOwners: { 'EFFECT-ACCOUNTING-011': 'effect-accounting' },
    source: 'src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Obligation/Todo/MagicTodoProjectionSurface.js',
    owner: 'obligation-ledger',
    laws: [
      'OBLIGATION-LEDGER-008',
      'OBLIGATION-LEDGER-010',
      'OBLIGATION-LEDGER-011',
      'OBLIGATION-LEDGER-012',
      'OBLIGATION-LEDGER-013',
      'OBLIGATION-LEDGER-014',
      'OBLIGATION-LEDGER-016',
      'OBLIGATION-LEDGER-018',
      'OBLIGATION-LEDGER-019',
      'OBLIGATION-LEDGER-020',
    ],
    source: 'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoProjectionSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js',
    owner: 'obligation-ledger',
    laws: ['OBLIGATION-LEDGER-018'],
    source: 'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Obligation/Todo/MagicTodoLocalitySurface.js',
    owner: 'obligation-ledger',
    laws: ['OBLIGATION-LEDGER-025'],
    source: 'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoLocalitySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Obligation/Todo/MagicTodoMembraneSurface.js',
    owner: 'obligation-ledger',
    laws: [
      'OBLIGATION-LEDGER-009',
      'OBLIGATION-LEDGER-010',
      'OBLIGATION-LEDGER-011',
      'OBLIGATION-LEDGER-012',
      'OBLIGATION-LEDGER-013',
      'OBLIGATION-LEDGER-014',
      'OBLIGATION-LEDGER-016',
      'OBLIGATION-LEDGER-017',
      'OBLIGATION-LEDGER-026',
      'EFFECT-ACCOUNTING-011',
    ],
    lawOwners: { 'EFFECT-ACCOUNTING-011': 'effect-accounting' },
    source: 'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembraneSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Persistence/Journal/ObligationJournalSurface.js',
    owner: 'obligation-ledger',
    laws: [
      'OBLIGATION-LEDGER-010',
      'OBLIGATION-LEDGER-011',
      'OBLIGATION-LEDGER-012',
      'OBLIGATION-LEDGER-013',
      'OBLIGATION-LEDGER-014',
      'OBLIGATION-LEDGER-018',
    ],
    source: 'src/Wanxiangshu/Persistence/Journal/ObligationJournalSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Persistence/Journal/ObligationEnvelopeSurface.js',
    owner: 'obligation-ledger',
    laws: ['OBLIGATION-LEDGER-017', 'OBLIGATION-LEDGER-018'],
    source: 'src/Wanxiangshu/Persistence/Journal/ObligationEnvelopeSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/WorkRecord/OpeningSemanticSurface.js',
    owner: 'obligation-ledger',
    laws: ['OBLIGATION-LEDGER-016', 'OBLIGATION-LEDGER-017'],
    source: 'src/Wanxiangshu/Mission/WorkRecord/OpeningSemanticSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Persistence/EventStore/CodecSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-003', 'DURABLE-EVENTS-005'],
    source: 'src/Wanxiangshu/Persistence/EventStore/CodecSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Persistence/Journal/CodecSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-001', 'DURABLE-EVENTS-002', 'DURABLE-EVENTS-003', 'DURABLE-EVENTS-014'],
    source: 'src/Wanxiangshu/Persistence/Journal/CodecSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Persistence/Journal/FactCodecSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-002', 'DURABLE-EVENTS-003', 'DURABLE-EVENTS-005'],
    source: 'src/Wanxiangshu/Persistence/Journal/FactCodecSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Persistence/Journal/Surface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-009', 'DURABLE-EVENTS-010', 'DURABLE-EVENTS-013', 'EFFECT-ACCOUNTING-008', 'EFFECT-ACCOUNTING-011'],
    lawOwners: {
      'EFFECT-ACCOUNTING-008': 'effect-accounting',
      'EFFECT-ACCOUNTING-011': 'effect-accounting',
    },
    source: 'src/Wanxiangshu/Persistence/Journal/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Persistence/EventStore/Surface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-001', 'DURABLE-EVENTS-004', 'DURABLE-EVENTS-005', 'DURABLE-EVENTS-006'],
    source: 'src/Wanxiangshu/Persistence/EventStore/Surface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Persistence/EventStore/MergeSurface.js',
    owner: 'durable-convergence',
    laws: ['DURABLE-CONVERGENCE-001', 'DURABLE-CONVERGENCE-002', 'DURABLE-CONVERGENCE-003', 'DURABLE-CONVERGENCE-006'],
    source: 'src/Wanxiangshu/Persistence/EventStore/MergeSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Persistence/EventStore/RetentionSurface.js',
    owner: 'durable-convergence',
    laws: ['DURABLE-CONVERGENCE-009', 'DURABLE-CONVERGENCE-010', 'DURABLE-CONVERGENCE-011'],
    source: 'src/Wanxiangshu/Persistence/EventStore/RetentionSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Process/DeadlineSurface.js',
    owner: 'time-capability',
    laws: ['TIME-002', 'TIME-005'],
    source: 'src/Wanxiangshu/Process/DeadlineSurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Tools/DistillationSurface.js',
    owner: 'output-distillation',
    laws: [
      'DISTILL-001',
      'DISTILL-002',
      'DISTILL-003',
      'DISTILL-004',
      'DISTILL-005',
      'DISTILL-006',
      'DISTILL-007',
      'DISTILL-008',
      'DISTILL-009',
      'DISTILL-010',
      'DISTILL-013',
    ],
    source: 'src/Wanxiangshu/OpenCode/Tools/DistillationSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Interaction/Authority/RuntimeSurface.js',
    owner: 'interaction-authority',
    laws: [
      'INTERACTION-AUTHORITY-001',
      'INTERACTION-AUTHORITY-002',
      'INTERACTION-AUTHORITY-003',
      'INTERACTION-AUTHORITY-004',
      'INTERACTION-AUTHORITY-005',
      'INTERACTION-AUTHORITY-006',
      'INTERACTION-AUTHORITY-007',
      'INTERACTION-AUTHORITY-008',
      'INTERACTION-AUTHORITY-009',
      'INTERACTION-AUTHORITY-010',
      'INTERACTION-AUTHORITY-011',
      'INTERACTION-AUTHORITY-012',
      'INTERACTION-AUTHORITY-013',
      'INTERACTION-AUTHORITY-014',
      'INTERACTION-AUTHORITY-015',
      'INTERACTION-AUTHORITY-016',
      'INTERACTION-AUTHORITY-017',
      'INTERACTION-AUTHORITY-018',
    ],
    source: 'src/Wanxiangshu/Interaction/Authority/RuntimeSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Interaction/Attention/Surface.js',
    owner: 'attention-regulation',
    laws: [
      'ATTENTION-REGULATION-001',
      'ATTENTION-REGULATION-002',
      'ATTENTION-REGULATION-003',
      'ATTENTION-REGULATION-004',
      'ATTENTION-REGULATION-005',
      'ATTENTION-REGULATION-006',
    ],
    source: 'src/Wanxiangshu/Interaction/Attention/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Interaction/Concern/Surface.js',
    owner: 'concern-routing',
    laws: [
      'CONCERN-ROUTING-001',
      'CONCERN-ROUTING-002',
      'CONCERN-ROUTING-003',
      'CONCERN-ROUTING-004',
      'CONCERN-ROUTING-005',
      'CONCERN-ROUTING-006',
      'CONCERN-ROUTING-007',
    ],
    source: 'src/Wanxiangshu/Interaction/Concern/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Enforcer/InstitutionalLearning/Surface.js',
    owner: 'institutional-learning',
    laws: [
      'INSTITUTIONAL-LEARNING-001',
      'INSTITUTIONAL-LEARNING-002',
      'INSTITUTIONAL-LEARNING-003',
      'INSTITUTIONAL-LEARNING-004',
      'INSTITUTIONAL-LEARNING-005',
      'INSTITUTIONAL-LEARNING-006',
      'INSTITUTIONAL-LEARNING-007',
      'INSTITUTIONAL-LEARNING-008',
    ],
    source: 'src/Wanxiangshu/Enforcer/InstitutionalLearning/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Interaction/Repair/CompletedTurnSurface.js',
    owner: 'interaction-authority',
    laws: ['INTERACTION-AUTHORITY-004', 'INTERACTION-AUTHORITY-019'],
    source: 'src/Wanxiangshu/Interaction/Repair/CompletedTurnSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/ChatParamsSurface.js',
    owner: 'interaction-authority',
    laws: ['INTERACTION-AUTHORITY-011'],
    source: 'src/Wanxiangshu/OpenCode/Host/ChatParamsSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/SessionsSurface.js',
    owner: 'session-ontology',
    laws: ['SESSION-ONTOLOGY-006', 'MANAGED-SESSION-016', 'MANAGED-SESSION-017'],
    lawOwners: {
      'MANAGED-SESSION-016': 'managed-session-lifecycle',
      'MANAGED-SESSION-017': 'managed-session-lifecycle',
    },
    source: 'src/Wanxiangshu/OpenCode/Host/SessionsSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Participant/Provider/Projection/Surface.js',
    owner: 'provider-projection',
    laws: [
      'PROVIDER-PROJECTION-001',
      'PROVIDER-PROJECTION-002',
      'PROVIDER-PROJECTION-003',
      'PROVIDER-PROJECTION-004',
      'PROVIDER-PROJECTION-005',
      'PROVIDER-PROJECTION-006',
      'PROVIDER-PROJECTION-007',
      'PROVIDER-PROJECTION-011',
      'PROVIDER-PROJECTION-012',
    ],
    source: 'src/Wanxiangshu/Participant/Provider/Projection/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Persistence/Journal/RevisionSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-013'],
    source: 'src/Wanxiangshu/Persistence/Journal/RevisionSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Delegation/HostTurnObservedSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-002'],
    source: 'src/Wanxiangshu/Execution/Delegation/HostTurnObservedSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Context/Companion/FoldSurface.js',
    owner: 'context-compression',
    laws: ['DURABLE-EVENTS-015'],
    lawOwners: { 'DURABLE-EVENTS-015': 'durable-events' },
    source: 'src/Wanxiangshu/Context/Companion/FoldSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Session/AssociationSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-013'],
    source: 'src/Wanxiangshu/Execution/Session/AssociationSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Git/Hook/Surface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-018'],
    source: 'src/Wanxiangshu/Git/Hook/Surface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/WorkspaceEventStoreSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-009', 'DURABLE-EVENTS-010'],
    source: 'src/Wanxiangshu/OpenCode/Host/WorkspaceEventStoreSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Codec/CanonicalJsonSurface.js',
    owner: 'durable-events',
    laws: ['DURABLE-EVENTS-003'],
    source: 'src/Wanxiangshu/OpenCode/Codec/CanonicalJsonSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Codec/ProviderProjectionSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-003', 'HOST-BOUNDARY-020'],
    lawOwners: { 'HOST-BOUNDARY-020': 'host-boundary' },
    source: 'src/Wanxiangshu/OpenCode/Codec/ProviderProjectionSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Codec/ToolHostSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-003', 'PROVIDER-PROJECTION-005', 'PROVIDER-PROJECTION-008', 'PROVIDER-PROJECTION-009', 'HOST-BOUNDARY-009'],
    lawOwners: { 'HOST-BOUNDARY-009': 'host-boundary' },
    source: 'src/Wanxiangshu/OpenCode/Codec/ToolHostSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/PairProgrammingThoughtSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-010'],
    source: 'src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtSurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'Requirement/Grounding/Surface.js',
    owner: 'requirement-grounding',
    laws: [
      'REQUIREMENT-GROUNDING-001',
      'REQUIREMENT-GROUNDING-002',
      'REQUIREMENT-GROUNDING-003',
      'REQUIREMENT-GROUNDING-004',
      'REQUIREMENT-GROUNDING-005',
      'REQUIREMENT-GROUNDING-006',
    ],
    source: 'src/Wanxiangshu/Requirement/Grounding/Surface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/RequirementGroundingSurface.js',
    owner: 'requirement-grounding',
    laws: [
      'REQUIREMENT-GROUNDING-006',
      'REQUIREMENT-GROUNDING-007',
      'REQUIREMENT-GROUNDING-008',
      'REQUIREMENT-GROUNDING-011',
      'REQUIREMENT-GROUNDING-012',
    ],
    source: 'src/Wanxiangshu/OpenCode/Host/RequirementGroundingSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/RequirementGroundingRepositorySurface.js',
    owner: 'requirement-grounding',
    laws: ['REQUIREMENT-GROUNDING-009', 'REQUIREMENT-GROUNDING-010'],
    source: 'src/Wanxiangshu/OpenCode/Host/RequirementGroundingRepositorySurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Enforcer/Surface.js',
    owner: 'behavior-diagnosis',
    laws: [
      'BD-001',
      'BD-002',
      'BD-003',
      'BD-004',
      'BD-005',
      'BD-006',
      'BD-007',
      'BD-008',
      'BD-009',
      'BD-010',
      'BD-011',
    ],
    source: 'src/Wanxiangshu/Enforcer/Surface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Enforcer/ObservationSurface.js',
    owner: 'behavior-diagnosis',
    laws: ['BD-012', 'BD-014', 'BD-015', 'BD-016'],
    source: 'src/Wanxiangshu/Enforcer/ObservationSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Enforcer/BlogSurface.js',
    owner: 'behavior-diagnosis',
    laws: ['BD-006', 'BD-008', 'BD-009', 'BD-010', 'BD-011', 'BD-013', 'BD-017'],
    source: 'src/Wanxiangshu/Enforcer/BlogSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Participant/Provider/Attempt/PlannerSurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-001', 'ENF-003', 'ENF-004'],
    source: 'src/Wanxiangshu/Participant/Provider/Attempt/PlannerSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/ManagedAgentConfigSurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-010', 'ENF-011'],
    source: 'src/Wanxiangshu/OpenCode/Host/ManagedAgentConfigSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Tools/ToolRegistrySurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-001', 'ENF-006', 'ENF-010'],
    source: 'src/Wanxiangshu/OpenCode/Tools/ToolRegistrySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Tools/ToolSurface.js',
    owner: 'capability-enforcement',
    laws: ['ENF-006', 'ENF-010'],
    source: 'src/Wanxiangshu/OpenCode/Tools/ToolSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Strength/Surface.js',
    owner: 'speculative-investigation',
    laws: [
      'SPEC-INV-001',
      'SPEC-INV-002',
      'SPEC-INV-003',
      'SPEC-INV-004',
      'SPEC-INV-005',
      'SPEC-INV-006',
      'SPEC-INV-007',
      'SPEC-INV-008',
      'SPEC-INV-009',
      'SPEC-INV-010',
      'SPEC-INV-011',
      'SPEC-INV-012',
      'SPEC-INV-013',
    ],
    source: 'src/Wanxiangshu/Strength/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Session/Wait/Surface.js',
    owner: 'causal-wait',
    laws: ['CAUSAL-001', 'CAUSAL-002', 'CAUSAL-003', 'CAUSAL-004', 'CAUSAL-005', 'CAUSAL-006', 'CAUSAL-007', 'CAUSAL-008'],
    source: 'src/Wanxiangshu/Execution/Session/Wait/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Change/Surface.js',
    owner: 'change-integration',
    laws: ['CHGINT-001', 'CHGINT-002', 'CHGINT-003', 'CHGINT-004', 'CHGINT-005', 'CHGINT-006', 'CHGINT-007', 'CHGINT-008', 'CHGINT-009', 'CHGINT-010', 'CHGINT-011', 'CHGINT-012', 'CHGINT-013', 'CRASH-019'],
    lawOwners: { 'CRASH-019': 'crash-reconciliation' },
    source: 'src/Wanxiangshu/Change/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Process/Surface.js',
    owner: 'time-capability',
    laws: ['TIME-001', 'TIME-002', 'TIME-003', 'TIME-004', 'TIME-005', 'TIME-006', 'TIME-007'],
    source: 'src/Wanxiangshu/Process/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/EventsSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-016'],
    source: 'src/Wanxiangshu/OpenCode/Host/EventsSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/HostMessageProjection.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-011'],
    source: 'src/Wanxiangshu/OpenCode/Host/HostMessageProjection.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/HostSessionContextSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-017'],
    source: 'src/Wanxiangshu/OpenCode/Host/HostSessionContextSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/SharedStateSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-010'],
    source: 'src/Wanxiangshu/OpenCode/Host/SharedStateSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/ProviderRunBindingSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-008'],
    source: 'src/Wanxiangshu/OpenCode/Host/ProviderRunBindingSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/MessageVisibilitySurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-008'],
    source: 'src/Wanxiangshu/OpenCode/Host/MessageVisibilitySurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/PluginHooksSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-014', 'EFFECT-ACCOUNTING-008'],
    lawOwners: { 'EFFECT-ACCOUNTING-008': 'effect-accounting' },
    source: 'src/Wanxiangshu/OpenCode/Host/PluginHooksSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/SessionSnapshotSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-006', 'HOST-BOUNDARY-009', 'HOST-BOUNDARY-012', 'HOST-BOUNDARY-019', 'HOST-BOUNDARY-020'],
    source: 'src/Wanxiangshu/OpenCode/Host/SessionSnapshotSurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/HostSignalSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-001', 'HOST-BOUNDARY-002', 'HOST-BOUNDARY-003'],
    source: 'src/Wanxiangshu/OpenCode/Host/HostSignalSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/HostSignalSubscribeSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-003'],
    source: 'src/Wanxiangshu/OpenCode/Host/HostSignalSubscribeSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/SphinxMcpConfigSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-017'],
    source: 'src/Wanxiangshu/OpenCode/Host/SphinxMcpConfigSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'OpenCode/Host/StealthBrowserMcpConfigSurface.js',
    owner: 'host-boundary',
    laws: ['HOST-BOUNDARY-017'],
    source: 'src/Wanxiangshu/OpenCode/Host/StealthBrowserMcpConfigSurface.fs',
    representation: 'json',
    kind: 'resource',
  },
  {
    module: 'Execution/Delegation/Handle/Surface.js',
    owner: 'managed-session-lifecycle',
    laws: ['MANAGED-SESSION-006', 'MANAGED-SESSION-007', 'MANAGED-SESSION-008', 'MANAGED-SESSION-009', 'MANAGED-SESSION-013', 'MANAGED-SESSION-015'],
    source: 'src/Wanxiangshu/Execution/Delegation/Handle/Surface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'Execution/Delegation/Handle/FoldSurface.js',
    owner: 'managed-session-lifecycle',
    laws: ['MANAGED-SESSION-006', 'MANAGED-SESSION-008', 'MANAGED-SESSION-015'],
    source: 'src/Wanxiangshu/Execution/Delegation/Handle/FoldSurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'Execution/Session/Attachment/AttachmentSurface.js',
    owner: 'managed-session-lifecycle',
    laws: ['MANAGED-SESSION-001', 'MANAGED-SESSION-005', 'CRASH-019'],
    lawOwners: { 'CRASH-019': 'crash-reconciliation' },
    source: 'src/Wanxiangshu/Execution/Session/Attachment/AttachmentSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/JoinResultRendererSurface.js',
    owner: 'provider-projection',
    laws: ['PROVIDER-PROJECTION-009'],
    source: 'src/Wanxiangshu/OpenCode/JoinResultRendererSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Participant/Persona/OfficeCapabilitySurface.js',
    owner: 'office-capability',
    laws: ['OFF-002'],
    source: 'src/Wanxiangshu/Participant/Persona/OfficeCapabilitySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/WorkRecord/Surface.js',
    owner: 'work-record',
    laws: ['WORK-RECORD-001', 'WORK-RECORD-002', 'WORK-RECORD-003', 'WORK-RECORD-004', 'WORK-RECORD-005', 'WORK-RECORD-006', 'WORK-RECORD-007', 'WORK-RECORD-008', 'WORK-RECORD-009', 'WORK-RECORD-010', 'WORK-RECORD-011', 'WORK-RECORD-012', 'WORK-RECORD-013', 'WORK-RECORD-014', 'WORK-RECORD-015', 'WORK-RECORD-016'],
    source: 'src/Wanxiangshu/Mission/WorkRecord/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Context/Trace/SemanticTraceSurface.js',
    owner: 'semantic-trace',
    laws: ['SEMANTIC-TRACE-001', 'SEMANTIC-TRACE-002', 'SEMANTIC-TRACE-003', 'SEMANTIC-TRACE-004', 'SEMANTIC-TRACE-005', 'SEMANTIC-TRACE-006', 'SEMANTIC-TRACE-007', 'SEMANTIC-TRACE-008', 'SEMANTIC-TRACE-009', 'SEMANTIC-TRACE-010'],
    source: 'src/Wanxiangshu/Context/Trace/SemanticTraceSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Execution/Delegation/Fork/Host/HostForkRunLifecycleSurface.js',
    owner: 'effect-accounting',
    laws: ['EFFECT-ACCOUNTING-002'],
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/HostForkRunLifecycleSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Interaction/Dispatch/DispatchSurface.js',
    owner: 'dispatch-protocol',
    laws: ['DISPATCH-PROTOCOL-002', 'DISPATCH-PROTOCOL-005', 'DISPATCH-PROTOCOL-007', 'DISPATCH-PROTOCOL-009', 'EFFECT-ACCOUNTING-008'],
    lawOwners: { 'EFFECT-ACCOUNTING-008': 'effect-accounting' },
    source: 'src/Wanxiangshu/Interaction/Dispatch/DispatchSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Interaction/Dispatch/JoinGuardSurface.js',
    owner: 'dispatch-protocol',
    laws: ['DISPATCH-PROTOCOL-007'],
    source: 'src/Wanxiangshu/Interaction/Dispatch/JoinGuardSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Interaction/Dispatch/RecoverySurface.js',
    owner: 'dispatch-protocol',
    laws: ['DISPATCH-PROTOCOL-007', 'DISPATCH-PROTOCOL-009', 'EFFECT-ACCOUNTING-008'],
    lawOwners: { 'EFFECT-ACCOUNTING-008': 'effect-accounting' },
    source: 'src/Wanxiangshu/Interaction/Dispatch/RecoverySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Execution/Session/LoopDetectorSurface.js',
    owner: 'degeneration-guard',
    laws: ['DG-001', 'DG-003', 'DG-004', 'DG-005', 'DG-006'],
    source: 'src/Wanxiangshu/Execution/Session/LoopDetectorSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Change/Host/Surface.js',
    owner: 'review-assurance',
    laws: ['REVIEW-ASSURANCE-006', 'CHGINT-003'],
    lawOwners: { 'CHGINT-003': 'change-integration' },
    source: 'src/Wanxiangshu/Change/Host/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Foundation/ParallelSurface.js',
    owner: 'structured-workflow',
    laws: ['STRUCTURED-WORKFLOW-010'],
    source: 'src/Wanxiangshu/Foundation/ParallelSurface.fs',
    representation: 'opaque-capability',
    kind: 'pure',
  },
  {
    module: 'Foundation/FsToolkitFableCompat.js',
    owner: 'intra-participant-parallelism',
    laws: ['INTRA-PARTICIPANT-PARALLELISM-016'],
    source: 'src/Wanxiangshu/Foundation/FsToolkitFableCompat.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Foundation/OutcomeSurface.js',
    owner: 'structured-workflow',
    laws: ['STRUCTURED-WORKFLOW-003'],
    source: 'src/Wanxiangshu/Foundation/OutcomeSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Review/Assurance/Surface.js',
    owner: 'review-assurance',
    laws: [
      'REVIEW-ASSURANCE-001',
      'REVIEW-ASSURANCE-002',
      'REVIEW-ASSURANCE-003',
      'REVIEW-ASSURANCE-004',
      'REVIEW-ASSURANCE-005',
      'REVIEW-ASSURANCE-006',
      'REVIEW-ASSURANCE-007',
      'REVIEW-ASSURANCE-008',
      'REVIEW-ASSURANCE-009',
      'REVIEW-ASSURANCE-010',
      'REVIEW-ASSURANCE-011',
      'REVIEW-ASSURANCE-012',
      'REVIEW-ASSURANCE-013',
    ],
    source: 'src/Wanxiangshu/Mission/Review/Assurance/Surface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Mission/Review/OpenCode/ReviewHostSurface.js',
    owner: 'review-assurance',
    laws: ['REVIEW-ASSURANCE-006', 'REVIEW-ASSURANCE-010'],
    source: 'src/Wanxiangshu/Mission/Review/OpenCode/ReviewHostSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Mission/Review/OpenCode/JudgeSurface.js',
    owner: 'review-judgement',
    laws: ['REVIEW-JUDGEMENT-001', 'REVIEW-JUDGEMENT-008'],
    source: 'src/Wanxiangshu/Mission/Review/OpenCode/JudgeSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Mission/Review/ReviewTodoSurface.js',
    owner: 'review-assurance',
    laws: ['REVIEW-ASSURANCE-008', 'REVIEW-ASSURANCE-009', 'REVIEW-ASSURANCE-010', 'REVIEW-ASSURANCE-012'],
    source: 'src/Wanxiangshu/Mission/Review/ReviewTodoSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Persistence/Journal/ReviewJournalSurface.js',
    owner: 'review-assurance',
    laws: ['REVIEW-ASSURANCE-008', 'REVIEW-ASSURANCE-009', 'REVIEW-ASSURANCE-011', 'REVIEW-ASSURANCE-012'],
    source: 'src/Wanxiangshu/Persistence/Journal/ReviewJournalSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Enforcer/Guidance/TipSurface.js',
    owner: 'guidance-delivery',
    laws: ['GD-002', 'GD-003', 'GD-004', 'GD-005', 'GD-006', 'GD-007'],
    source: 'src/Wanxiangshu/Enforcer/Guidance/TipSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
  {
    module: 'Enforcer/Guidance/DeliverySurface.js',
    owner: 'guidance-delivery',
    laws: ['GD-001', 'GD-003', 'GD-005'],
    source: 'src/Wanxiangshu/Enforcer/Guidance/DeliverySurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/PairProgramming/GuidelineSurface.js',
    owner: 'guidance-delivery',
    laws: ['GD-011'],
    source: 'src/Wanxiangshu/OpenCode/Host/PairProgramming/GuidelineSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'OpenCode/Host/PairProgrammingCalibrationSurface.js',
    owner: 'guidance-delivery',
    laws: ['GD-012'],
    source: 'src/Wanxiangshu/OpenCode/Host/PairProgrammingCalibrationSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Resources/PromptSurface.js',
    owner: 'cognitive-environment',
    laws: ['COGNITIVE-ENVIRONMENT-001', 'COGNITIVE-ENVIRONMENT-003', 'COGNITIVE-ENVIRONMENT-004', 'COGNITIVE-ENVIRONMENT-005'],
    source: 'src/Wanxiangshu/Resources/PromptSurface.fs',
    representation: 'json',
    kind: 'pure',
  },
  {
    module: 'Verification/TemporalSurface.js',
    owner: 'verification-system',
    laws: ['VERIFICATION-SYSTEM-007'],
    source: 'src/Wanxiangshu/Verification/TemporalSurface.fs',
    representation: 'opaque-capability',
    kind: 'resource',
  },
]

/**
 * Explicit cross-owner consumer authorization (JS-SEMANTIC-SURFACE-003).
 *
 * A test that actively uses a registered surface must either carry a WHAT tag
 * from the surface's `laws` and live under the corresponding law owner's
 * tests directory, or be declared here as an authorized consumer package.
 * Registration grants no blanket import authority: every cross-owner
 * dependency is explicit so an unrelated test importing a surface is a hard
 * failure, not a false green.
 */
export const SURFACE_CONSUMERS = {
  'Change/Surface.js': ['crash-reconciliation', 'effect-accounting'],
  'Change/Host/Surface.js': ['change-integration'],
  'Execution/Session/Attachment/AttachmentSurface.js': ['crash-reconciliation'],
  'Composition/Turn/ReconcileSurface.js': ['crash-reconciliation', 'host-boundary', 'structured-workflow'],
  'Context/Companion/Blogger/BloggerCrashSurface.js': ['context-compression'],
  'Context/Companion/Blogger/FrameSurface.js': ['context-compression'],
  'Context/Companion/Blogger/TomlSurface.js': ['context-compression', 'guidance-delivery'],
  'Context/Companion/CompressionSurface.js': ['context-compression', 'prefix-stability', 'provider-attempt-recovery'],
  'Context/Companion/FoldSurface.js': ['durable-events', 'verification-system'],
  'Context/Companion/ProjectionSurface.js': ['guidance-delivery', 'prefix-stability'],
  'Context/Prefix/Surface.js': ['context-compression', 'provider-attempt-recovery'],
  'Context/Prefix/XWireSurface.js': ['context-compression', 'host-boundary', 'participant-horizon'],
  'Context/Trace/SemanticTraceSurface.js': ['obligation-ledger', 'work-record'],
  'Enforcer/BlogSurface.js': ['behavior-diagnosis'],
  'Enforcer/Surface.js': ['behavior-diagnosis', 'guidance-delivery'],
  'Execution/Delegation/Fork/ChildRecoverySurface.js': ['crash-reconciliation', 'effect-accounting'],
  'Execution/Delegation/Fork/OpenCode/JoinSurface.js': ['crash-reconciliation', 'delegation', 'effect-accounting', 'participant-horizon'],
  'Execution/Delegation/Fork/Surface.js': ['delegation', 'participant-horizon'],
  'Execution/Delegation/Handle/Surface.js': ['context-compression', 'crash-reconciliation', 'delegation', 'effect-accounting'],
  'Execution/Delegation/SyncDelegate/Surface.js': ['crash-reconciliation', 'knowledge-reuse', 'prefix-stability'],
  'Execution/Session/AssociationSurface.js': ['session-ontology'],
  'Execution/Session/Recovery/Surface.js': ['managed-session-lifecycle'],
  'Execution/Session/Wait/Surface.js': ['time-capability'],
  'Foundation/RolesSurface.js': ['capability-enforcement', 'cognitive-environment', 'repository-programming'],
  'Git/Hook/Surface.js': ['durable-convergence'],
  'Host/Contract/CompactionPolicySurface.js': ['host-boundary'],
  'Interaction/Authority/RuntimeSurface.js': ['delegation', 'dispatch-protocol'],
  'Interaction/Authority/Surface.js': ['prefix-stability'],
  'Interaction/Dispatch/DispatchSurface.js': ['degeneration-guard', 'dispatch-protocol', 'effect-accounting'],
  'Interaction/Dispatch/RecoverySurface.js': ['effect-accounting'],
  'Mission/Manager/FinalitySurface.js': ['interaction-authority'],
  'Mission/Obligation/Todo/MagicTodoLocalitySurface.js': ['host-boundary'],
  'Mission/Obligation/Todo/MagicTodoMembraneSurface.js': ['effect-accounting', 'host-boundary'],
  'Mission/Obligation/Todo/MagicTodoSemanticSurface.js': ['context-compression', 'host-boundary', 'obligation-ledger', 'prefix-stability', 'work-record'],
  'Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js': ['effect-accounting', 'host-boundary'],
  'Mission/Review/Assurance/Surface.js': ['review-judgement'],
  'Mission/Review/ReviewTodoSurface.js': ['effect-accounting', 'review-judgement'],
  'Mission/WorkRecord/OpeningSemanticSurface.js': ['work-record'],
  'OpenCode/Codec/CanonicalJsonSurface.js': ['prefix-stability'],
  'OpenCode/Codec/ProviderProjectionSurface.js': ['prefix-stability', 'speculative-investigation'],
  'OpenCode/Codec/ToolHostSurface.js': ['host-boundary', 'output-distillation', 'process-execution'],
  'OpenCode/Host/ManagedAgentConfigSurface.js': ['capability-enforcement', 'external-investigation', 'prefix-stability', 'repository-investigation', 'speculative-investigation'],
  'OpenCode/Host/ModelRoutingSurface.js': ['host-boundary', 'participant-identity'],
  'OpenCode/Host/PairProgrammingThoughtSurface.js': ['capability-enforcement', 'context-compression', 'guidance-delivery', 'prefix-stability', 'requirement-grounding'],
  'OpenCode/Host/RequirementGroundingSurface.js': ['context-compression'],
  'OpenCode/Host/PluginHooksSurface.js': ['effect-accounting', 'requirement-grounding'],
  'OpenCode/Host/QuiescenceSurface.js': ['crash-reconciliation', 'managed-session-lifecycle'],
  'OpenCode/Host/SessionBindingSurface.js': ['host-boundary', 'interaction-authority'],
  'OpenCode/Host/SessionSnapshotSurface.js': ['host-boundary'],
  'OpenCode/Tools/ExecutorToolSurface.js': ['output-distillation'],
  'OpenCode/Tools/ToolRegistrySurface.js': ['capability-enforcement'],
  'Participant/Persona/Surface.js': ['participant-identity', 'session-ontology'],
  'Participant/Provider/Attempt/Fallback/CursorSurface.js': ['effect-accounting', 'provider-attempt-recovery', 'verification-system'],
  'Participant/Provider/Attempt/PlannerSurface.js': ['prefix-stability'],
  'Participant/Provider/LanguageSurface.js': ['cognitive-environment', 'degeneration-guard', 'guidance-delivery', 'review-judgement'],
  'Participant/Provider/Projection/Surface.js': ['context-compression', 'interaction-authority', 'prefix-stability', 'speculative-investigation'],
  'Persistence/EventStore/MergeSurface.js': ['durable-events'],
  'Persistence/EventStore/Surface.js': ['durable-convergence', 'durable-events', 'effect-accounting', 'knowledge-reuse', 'repository-programming'],
  'Persistence/Journal/CodecSurface.js': ['verification-system'],
  'Persistence/Journal/FactCodecSurface.js': ['verification-system'],
  'Persistence/Journal/ObligationJournalSurface.js': ['host-boundary'],
  'Persistence/Journal/ReviewJournalSurface.js': ['review-assurance', 'review-judgement'],
  'Persistence/Journal/Surface.js': ['degeneration-guard', 'dispatch-protocol', 'durable-events', 'effect-accounting', 'host-boundary', 'obligation-ledger', 'provider-attempt-recovery', 'review-assurance', 'review-judgement', 'semantic-trace', 'work-record'],
  'Process/DeadlineSurface.js': ['process-execution', 'verification-system'],
  'Process/Surface.js': ['causal-wait', 'process-execution'],
  'Repository/Knowledge/Casebook/BookkeeperSurface.js': ['knowledge-reuse'],
  'Repository/Knowledge/Casebook/IndexSurface.js': ['knowledge-reuse', 'verification-system'],
  'Repository/Knowledge/Casebook/Surface.js': ['knowledge-reuse'],
  'Repository/Programming/Js/GeneratorSurface.js': ['repository-programming'],
  'Repository/Programming/Js/WorkflowSurface.js': ['repository-programming'],
  'Resources/PromptSurface.js': ['guidance-delivery'],
  'Strength/Surface.js': ['capability-enforcement', 'prefix-stability'],
  'Verification/TemporalSurface.js': ['provider-attempt-recovery'],
}

/** Flat module-path allowlist derived from the manifest (scanner regex input). */
export const SURFACE_MODULES = SURFACE_MANIFEST.map((entry) => entry.module)

const SURFACE_ALT = SURFACE_MODULES.map((m) => m.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')

const A_DEEP_IMPORT = new RegExp(
  `(?:from\\s*|import\\s*\\(\\s*|import\\s+)['"][^'"]*dist/(?!fable_modules)(?!(?:${SURFACE_ALT})['"])[^'"]+\\.js['"]`,
)
// Dynamic template import: `new URL(`...dist/${...}.js`, ...)` bypasses the
// static-import regex. A template literal with interpolation that contains
// `dist/` and ends in `.js` is a deep import — the variable means the scanner
// cannot prove it loads only registered surfaces, so it is always debt.
const A_TEMPLATE_IMPORT = /new URL\(\s*[`'"][^`'"]*dist\/[^`'"]*\$\{[^}]+\}[^`'"]*\.js[`'"]/
const B_EXPORT_DISCOVERY = /Object\.(?:keys|entries|values)\(\s*([A-Za-z_$][\w$]*)/
const B_MANGLED_LOOKUP = /(?:\.startsWith|\.endsWith)\(\s*['"`][^'"`]*(?:__|_[A-Z])/

const C1_DU_SHAPE = /\.cases\(\)|\.fields\b|\.tag\b/
const C2_FSHARP = /\bFSharp(?:List|Map|Set|Option|Result)\b/
const C3_FABLE_MODULES = /fable_modules/
// Ordinary JavaScript `.bind(...)` is not the legacy Fable helper; bare calls remain forbidden.
const D_HELPERS = /(?<![.$])\b(?:member|bind|fableInstanceMethod|prod|toList|caseOf|payloadOf|resultOf|unwrapOption)\(/

const RULES = [
  ['deep-dist-import', A_DEEP_IMPORT],
  ['template-dist-import', A_TEMPLATE_IMPORT],
  ['export-discovery', B_EXPORT_DISCOVERY],
  ['mangled-lookup', B_MANGLED_LOOKUP],
  ['du-shape', C1_DU_SHAPE],
  ['fsharp-type', C2_FSHARP],
  ['fable-modules', C3_FABLE_MODULES],
  ['interop-helper', D_HELPERS],
]

const moduleBindingNames = (source) => {
  const names = new Set(['mod', 'module', 'hostModule', 'productionModule'])
  const patterns = [
    /\b(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(?:await\s+)?(?:import|prod|bind)\s*\(/g,
    /\bimport\s+\*\s+as\s+([A-Za-z_$][\w$]*)\b/g,
  ]
  for (const pattern of patterns) {
    pattern.lastIndex = 0
    let match
    while ((match = pattern.exec(source)) !== null) names.add(match[1])
  }
  return names
}

const isModuleDiscovery = (text, moduleNames) => {
  const keys = B_EXPORT_DISCOVERY.exec(text)
  B_EXPORT_DISCOVERY.lastIndex = 0
  if (keys && moduleNames.has(keys[1])) return true
  return false
}

/** Scan one file; return [{ line, rule, text }]. */
export const scanFile = (absPath, relPath) => {
  const source = readFileSync(absPath, 'utf8')
  const lines = source.split('\n')
  const moduleNames = moduleBindingNames(source)
  const hits = []
  for (let i = 0; i < lines.length; i++) {
    const text = lines[i]
    for (const [rule, re] of RULES) {
      if (rule === 'export-discovery') {
        if (isModuleDiscovery(text, moduleNames)) hits.push({ file: relPath, line: i + 1, rule, text: text.trim() })
      } else if (re.test(text)) {
        hits.push({ file: relPath, line: i + 1, rule, text: text.trim() })
      }
    }
  }
  return hits
}

const IMPORT_SPECIFIER = /(?:\bfrom\s*|\bimport\s*\(\s*|\bimport\s+)['"]([^'"]+)['"]/g

/** Resolve relative local imports from semantic tests into the scanned zone.
 *
 * Traverses transitively from `.test.mjs` roots through all semantic-zone
 * files (support, fixtures, helpers): a support→support edge that carries
 * debt is just as much a test dependency as a direct test→support edge.
 */
export const semanticImportEdges = (root = REQUIREMENTS_ROOT) => {
  const files = semanticTestFiles(root)
  const known = new Set(files.map((file) => resolve(file)))
  const edges = []
  const visited = new Set()
  const queue = files.filter((file) => file.endsWith('.test.mjs'))
  while (queue.length > 0) {
    const importer = queue.shift()
    const importerKey = resolve(importer)
    if (visited.has(importerKey)) continue
    visited.add(importerKey)
    const source = readFileSync(importer, 'utf8')
    IMPORT_SPECIFIER.lastIndex = 0
    let match
    while ((match = IMPORT_SPECIFIER.exec(source)) !== null) {
      if (!match[1].startsWith('.')) continue
      const target = resolve(dirname(importer), match[1])
      if (known.has(target)) {
        edges.push({ importer, target })
        if (!visited.has(target)) queue.push(target)
      }
    }
  }
  return edges
}

/**
 * Semantic-test zone files under requirements: every executable .mjs or .js
 * under a package's tests directory — *.test.*, support files, fixtures,
 * helpers, *-contract.*, e2e, and integration.
 *
 * TASK.md §7/§21: forbidden knowledge moved from a test file into test
 * support does not reduce debt; the whole semantic-test dependency zone is
 * scanned. The transition facade (support/domain.mjs and its sublayers) and
 * package-local contract adapters remain debt only while they are being
 * removed; neither path is a quarantine.
 */
export const semanticTestFiles = (root = REQUIREMENTS_ROOT) =>
  walk(root, ['.mjs', '.js']).filter((abs) => {
    const segments = relative(root, abs).replace(/\\/g, '/').split('/')
    const testsIndex = segments.indexOf('tests')
    return testsIndex > 0
  })

/** Full inventory: { <rel-file>: [ {line, rule, text}, ... ] } minus allowlist. */
export const scanAll = (root = REQUIREMENTS_ROOT) => {
  const out = {}
  for (const abs of semanticTestFiles(root)) {
    const rel = relative(process.cwd(), abs).replace(/\\/g, '/')
    if (BUILD_VERIFICATION_FILES.has(rel)) continue
    if (HOST_PHYSICAL_CANARY_FILES.has(rel)) continue
    const hits = scanFile(abs, rel)
    if (hits.length > 0) out[rel] = hits
  }
  return out
}
