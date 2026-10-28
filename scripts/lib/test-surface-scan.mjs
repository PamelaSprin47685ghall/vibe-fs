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
import { parse } from 'acorn'
import { walk } from './walk.mjs'

export const REQUIREMENTS_ROOT = join(
  join(join(join(fileURLToPath(import.meta.url), '..'), '..'), '..'),
  'requirements',
)

/** Compiler/build verification: subject is the emitted artifact, not semantics. */
export const BUILD_VERIFICATION_FILES = new Set([
  'requirements/verification-system/tests/011.test.mjs',
  'requirements/distribution/tests/001.test.mjs',
  'requirements/distribution/tests/002.test.mjs',
  'requirements/distribution/tests/005.test.mjs',
  'requirements/distribution/tests/006.test.mjs',
  'requirements/distribution/tests/007.test.mjs',
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
  'requirements/host-boundary/tests/006.test.mjs',
  'requirements/host-boundary/tests/009.test.mjs',
  'requirements/host-boundary/tests/010.test.mjs',
  'requirements/host-boundary/tests/011.test.mjs',
  'requirements/host-boundary/tests/012.test.mjs',
  'requirements/host-boundary/tests/host-message-projection.test.mjs',
  'requirements/host-boundary/tests/host-session-context.test.mjs',
  'requirements/host-boundary/tests/host010-run-id-equivalence.test.mjs',
  'requirements/host-boundary/tests/magic-todo-host-canaries.test.mjs',
  'requirements/host-boundary/tests/session-snapshot-locality.test.mjs',
  'requirements/host-boundary/tests/omp-scope-seam.test.mjs',
  'requirements/host-boundary/tests/omp-negotiate-physical.test.mjs',
  // Scope HNI physical canary: Fable SessionMessage unions and actual OpenCode provider-request mutation.
  'requirements/host-boundary/tests/scope-hni-physical.test.mjs',
  'requirements/context-compression/tests/039.test.mjs',
  'requirements/context-compression/tests/040.test.mjs',
  'requirements/context-compression/tests/041.test.mjs',
  'requirements/prefix-stability/tests/016.test.mjs',
  // Physical-exit canary: the child must reach the real Diagnostic.fatal and
  // FatalProcess.trip links — a wire-level invariant, not a test seam.
  'requirements/host-boundary/tests/fixtures/fatal-process-child.fixture.mjs',
])

/**
 * Registered semantic-surface manifest (JS-SEMANTIC-SURFACE-002/003).
 *
 * A semantic test may import a registered surface directly: the surface IS the
 * legal entry point (owner boundary translation, JSON-shaped in/out). Deep
 * imports of any other dist module remain debt. Register a surface here when
 * a semantic component exposes a contract; `source` names the production file
 * the build-side manifest gate checks against the compile inventory.
 */
export const SURFACE_MANIFEST = [
  {
    module: 'Context/Scope/Surface.js',
    source: 'src/Wanxiangshu/Context/Scope/Surface.fs',
  },
  {
    module: 'Composition/Omp/OmpExtension.js',
    source: 'src/Wanxiangshu/Composition/Omp/OmpExtension.fs',
  },
  {
    module: 'Interaction/Authority/LifeClassification.js',
    source: 'src/Wanxiangshu/Interaction/Authority/LifeClassification.fs',
  },
  {
    module: 'Interaction/Authority/Surface.js',
    source: 'src/Wanxiangshu/Interaction/Authority/Surface.fs',
  },
  {
    module: 'Participant/Persona/Surface.js',
    source: 'src/Wanxiangshu/Participant/Persona/Surface.fs',
  },
  {
    module: 'OpenCode/Plugin/Plugin.js',
    source: 'src/Wanxiangshu/OpenCode/Plugin/Plugin.fs',
  },
  {
    module: 'OpenCode/Host/PluginTransformSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/PluginTransformSurface.fs',
  },
  {
    module: 'OpenCode/Host/LoadRecoverySurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/LoadRecoverySurface.fs',
  },
  {
    module: 'Interaction/Admission/ChatAdmissionIntentSurface.js',
    source: 'src/Wanxiangshu/Interaction/Admission/ChatAdmissionIntentSurface.fs',
  },
  {
    module: 'Interaction/Admission/ChatAdmissionTransactionSurface.js',
    source: 'src/Wanxiangshu/Interaction/Admission/ChatAdmissionTransactionSurface.fs',
  },
  {
    module: 'OpenCode/Host/ModelRoutingSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/ModelRoutingSurface.fs',
  },
  {
    module: 'OpenCode/Host/ReliabilityDiagnosticsSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/ReliabilityDiagnosticsSurface.fs',
  },
  {
    module: 'OpenCode/Host/HookPolicySurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/HookPolicySurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/ChildRecoverySurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecoverySurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/JoinSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/JoinSurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/CleanBreakSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/CleanBreakSurface.fs',
  },
  {
    module: 'Execution/Session/Recovery/Surface.js',
    source: 'src/Wanxiangshu/Execution/Session/Recovery/Surface.fs',
  },
  {
    module: 'Execution/Session/RecoveryClosureSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/RecoveryClosureSurface.fs',
  },
  {
    module: 'Execution/Session/OpenCode/HorizonSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/OpenCode/HorizonSurface.fs',
  },
  {
    module: 'Execution/Session/OpenCode/HorizonCalibrationSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/OpenCode/HorizonCalibrationSurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/LifecycleSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/LifecycleSurface.fs',
  },
  {
    module: 'Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js',
    source: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.fs',
  },
  {
    module: 'Execution/Delegation/SyncDelegate/Surface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs',
  },
  {
    module: 'Execution/Delegation/DelegatedToolEstimateSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/DelegatedToolEstimateSurface.fs',
  },
  {
    module: 'OpenCode/Host/PairProgrammingCalibrationSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/PairProgrammingCalibrationSurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/Surface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Surface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/OpenCode/ToolSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/ToolSurface.fs',
  },
  {
    module: 'Execution/Fission/Surface.js',
    source: 'src/Wanxiangshu/Execution/Fission/Surface.fs',
  },
  {
    module: 'OpenCode/Host/FissionHostSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/FissionHostSurface.fs',
  },
  {
    module: 'Execution/Fission/Canaries.js',
    source: 'src/Wanxiangshu/Execution/Fission/Canaries.fs',
  },
  {
    module: 'Foundation/RolesSurface.js',
    source: 'src/Wanxiangshu/Foundation/RolesSurface.fs',
  },
  {
    module: 'Foundation/SyntheticTomlSurface.js',
    source: 'src/Wanxiangshu/Foundation/SyntheticTomlSurface.fs',
  },
  {
    module: 'Host/Contract/ToolResultBound.js',
    source: 'src/Wanxiangshu/Host/Contract/ToolResultBound.fs',
  },
  {
    module: 'Host/Contract/CompactionPolicySurface.js',
    source: 'src/Wanxiangshu/Host/Contract/CompactionPolicySurface.fs',
  },
  {
    module: 'OpenCode/Host/QuiescenceSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/QuiescenceSurface.fs',
  },
  {
    module: 'OpenCode/Host/SessionRecoveryHostSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/SessionRecoveryHostSurface.fs',
  },
  {
    module: 'Process/LargeGateSurface.js',
    source: 'src/Wanxiangshu/Process/LargeGateSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/Surface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/Surface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/IndexSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/IndexSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/BookkeeperSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/OpenCode/BookkeeperToolSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/OpenCode/BookkeeperToolSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/LifecycleSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/LifecycleSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/SettlementSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/SettlementSurface.fs',
  },
  {
    module: 'Repository/Knowledge/Casebook/FetchSurface.js',
    source: 'src/Wanxiangshu/Repository/Knowledge/Casebook/FetchSurface.fs',
  },
  {
    module: 'Participant/Provider/LanguageSurface.js',
    source: 'src/Wanxiangshu/Participant/Provider/LanguageSurface.fs',
  },
  {
    module: 'Sphinx/V2/Runtime/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Runtime/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Persistence/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Persistence/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Hosts/Mcp/Tool.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Hosts/Mcp/Tool.fs',
  },
  {
    module: 'Sphinx/V2/Hosts/Mcp/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Hosts/Mcp/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Hosts/OpenCode/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Hosts/OpenCode/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Composition/SettlementSurface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Composition/SettlementSurface.fs',
  },
  {
    module: 'Sphinx/V2/Wire/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Wire/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Plugins/Bayes/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Plugins/Bayes/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Plugins/Ordinal/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Plugins/Ordinal/Surface.fs',
  },
  {
    module: 'Sphinx/V2/Core/Surface.js',
    source: 'src/Wanxiangshu/Sphinx/V2/Core/Surface.fs',
  },
  {
    module: 'Context/Prefix/Surface.js',
    source: 'src/Wanxiangshu/Context/Prefix/Surface.fs',
  },
  {
    module: 'Context/Prefix/XWireSurface.js',
    source: 'src/Wanxiangshu/Context/Prefix/XWireSurface.fs',
  },
  {
    module: 'Context/Prefix/OpenCode/XWireHostSurface.js',
    source: 'src/Wanxiangshu/Context/Prefix/OpenCode/XWireHostSurface.fs',
  },
  {
    module: 'OpenCode/Host/PluginRecoveryScopeSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScopeSurface.fs',
  },
  {
    module: 'Repository/Investigation/WarmStartSurface.js',
    source: 'src/Wanxiangshu/Repository/Investigation/WarmStartSurface.fs',
  },
  {
    module: 'Repository/Investigation/SembleSurface.js',
    source: 'src/Wanxiangshu/Repository/Investigation/SembleSurface.fs',
  },
  {
    module: 'Composition/Turn/ReconcileSurface.js',
    source: 'src/Wanxiangshu/Composition/Turn/ReconcileSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/GeneratorSurface.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/GeneratorSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/RuntimeSurface.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/RuntimeSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/FilesystemSurface.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/FilesystemSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/TransactionSurface.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/TransactionSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/WorkflowSurface.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/WorkflowSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/OpenCode/ToolHostSurface.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolHostSurface.fs',
  },
  {
    module: 'Repository/Programming/Js/Programs.js',
    source: 'src/Wanxiangshu/Repository/Programming/Js/Programs.fs',
  },
  {
    module: 'Context/Companion/CompressionSurface.js',
    source: 'src/Wanxiangshu/Context/Companion/CompressionSurface.fs',
  },
  {
    module: 'Persistence/Journal/ObligationJournalSurface.js',
    source: 'src/Wanxiangshu/Persistence/Journal/ObligationJournalSurface.fs',
  },
  {
    module: 'Persistence/Journal/ObligationEnvelopeSurface.js',
    source: 'src/Wanxiangshu/Persistence/Journal/ObligationEnvelopeSurface.fs',
  },
  {
    module: 'Foundation/HostSendContract.js',
    source: 'src/Wanxiangshu/Foundation/HostSendContract.fs',
  },
  {
    module: 'Persistence/EventStore/CodecSurface.js',
    source: 'src/Wanxiangshu/Persistence/EventStore/CodecSurface.fs',
  },
  {
    module: 'Persistence/Journal/CodecSurface.js',
    source: 'src/Wanxiangshu/Persistence/Journal/CodecSurface.fs',
  },
  {
    module: 'Persistence/Journal/FactCodecSurface.js',
    source: 'src/Wanxiangshu/Persistence/Journal/FactCodecSurface.fs',
  },
  {
    module: 'Persistence/Journal/Surface.js',
    source: 'src/Wanxiangshu/Persistence/Journal/Surface.fs',
  },
  {
    module: 'Persistence/EventStore/Surface.js',
    source: 'src/Wanxiangshu/Persistence/EventStore/Surface.fs',
  },
  {
    module: 'Persistence/EventStore/MergeSurface.js',
    source: 'src/Wanxiangshu/Persistence/EventStore/MergeSurface.fs',
  },
  {
    module: 'Persistence/EventStore/RetentionSurface.js',
    source: 'src/Wanxiangshu/Persistence/EventStore/RetentionSurface.fs',
  },
  {
    module: 'Process/DeadlineSurface.js',
    source: 'src/Wanxiangshu/Process/DeadlineSurface.fs',
  },
  {
    module: 'Interaction/Authority/RuntimeSurface.js',
    source: 'src/Wanxiangshu/Interaction/Authority/RuntimeSurface.fs',
  },
  {
    module: 'Interaction/Repair/CompletedTurnSurface.js',
    source: 'src/Wanxiangshu/Interaction/Repair/CompletedTurnSurface.fs',
  },
  {
    module: 'OpenCode/Host/ChatParamsSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/ChatParamsSurface.fs',
  },
  {
    module: 'OpenCode/Host/SessionsSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/SessionsSurface.fs',
  },
  {
    module: 'OpenCode/Host/TerminalPolicySurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/TerminalPolicySurface.fs',
  },
  {
    module: 'Participant/Provider/Projection/Surface.js',
    source: 'src/Wanxiangshu/Participant/Provider/Projection/Surface.fs',
  },
  {
    module: 'Persistence/Journal/RevisionSurface.js',
    source: 'src/Wanxiangshu/Persistence/Journal/RevisionSurface.fs',
  },
  {
    module: 'Execution/Delegation/HostTurnObservedSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/HostTurnObservedSurface.fs',
  },
  {
    module: 'Context/Companion/FoldSurface.js',
    source: 'src/Wanxiangshu/Context/Companion/FoldSurface.fs',
  },
  {
    module: 'Execution/Session/AssociationSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/AssociationSurface.fs',
  },
  {
    module: 'OpenCode/Host/WorkspaceSharedJournal.js',
    source: 'src/Wanxiangshu/OpenCode/Host/WorkspaceSharedJournal.fs',
  },
  {
    module: 'OpenCode/Codec/CanonicalJsonSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Codec/CanonicalJsonSurface.fs',
  },
  {
    module: 'OpenCode/Codec/ProviderProjectionSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Codec/ProviderProjectionSurface.fs',
  },
  {
    module: 'OpenCode/Codec/ToolHostSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Codec/ToolHostSurface.fs',
  },
  {
    module: 'OpenCode/Host/PairProgrammingThoughtSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtSurface.fs',
  },
  {
    module: 'Participant/Provider/Attempt/FailureSurface.js',
    source: 'src/Wanxiangshu/Participant/Provider/Attempt/FailureSurface.fs',
  },
  {
    module: 'Participant/Provider/Attempt/PlannerSurface.js',
    source: 'src/Wanxiangshu/Participant/Provider/Attempt/PlannerSurface.fs',
  },
  {
    module: 'OpenCode/Host/ManagedAgentConfigSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/ManagedAgentConfigSurface.fs',
  },
  {
    module: 'OpenCode/Tools/ToolRegistrySurface.js',
    source: 'src/Wanxiangshu/OpenCode/Tools/ToolRegistrySurface.fs',
  },
  {
    module: 'OpenCode/Tools/ToolSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Tools/ToolSurface.fs',
  },
  {
    module: 'Ablation/Surface.js',
    source: 'src/Wanxiangshu/Ablation/Surface.fs',
  },
  {
    module: 'Execution/Session/Wait/Surface.js',
    source: 'src/Wanxiangshu/Execution/Session/Wait/Surface.fs',
  },
  {
    module: 'Change/Surface.js',
    source: 'src/Wanxiangshu/Change/Surface.fs',
  },
  {
    module: 'Process/Surface.js',
    source: 'src/Wanxiangshu/Process/Surface.fs',
  },
  {
    module: 'OpenCode/Host/EventsSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/EventsSurface.fs',
  },
  {
    module: 'OpenCode/Host/WorkspaceEventStoreSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/WorkspaceEventStoreSurface.fs',
  },
  {
    module: 'OpenCode/Host/HostMessageProjection.js',
    source: 'src/Wanxiangshu/OpenCode/Host/HostMessageProjection.fs',
  },
  {
    module: 'OpenCode/Host/HostSessionContextSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/HostSessionContextSurface.fs',
  },
  {
    module: 'OpenCode/Host/SharedStateSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/SharedStateSurface.fs',
  },
  {
    module: 'OpenCode/Host/ProviderRunBindingSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/ProviderRunBindingSurface.fs',
  },
  {
    module: 'OpenCode/Host/MessageVisibilitySurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/MessageVisibilitySurface.fs',
  },
  {
    module: 'OpenCode/Plugin/PluginLifecycleSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Plugin/PluginLifecycleSurface.fs',
  },
  {
    module: 'OpenCode/Plugin/PluginHostWiringSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Plugin/PluginHostWiringSurface.fs',
  },
  {
    module: 'OpenCode/Plugin/PlanWorkOpenWiring.js',
    source: 'src/Wanxiangshu/OpenCode/Plugin/PlanWorkOpenWiring.fs',
  },
  {
    module: 'OpenCode/Host/PluginHooksSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/PluginHooksSurface.fs',
  },
  {
    module: 'OpenCode/Host/SessionSnapshotSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/SessionSnapshotSurface.fs',
  },
  {
    module: 'OpenCode/Host/HostSignalSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/HostSignalSurface.fs',
  },
  {
    module: 'OpenCode/Host/HostSignalSubscribeSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Host/HostSignalSubscribeSurface.fs',
  },
  {
    module: 'Execution/Failure/Surface.js',
    source: 'src/Wanxiangshu/Execution/Failure/Surface.fs',
  },
  {
    module: 'Execution/Session/ChatExecution/Surface.js',
    source: 'src/Wanxiangshu/Execution/Session/ChatExecution/Surface.fs',
  },
  {
    module: 'Execution/Session/ChatExecution/RecoverySurface.js',
    source: 'src/Wanxiangshu/Execution/Session/ChatExecution/RecoverySurface.fs',
  },
  {
    module: 'Execution/Session/ChatExecution/RecoveryRuntimeSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/ChatExecution/RecoveryRuntimeSurface.fs',
  },
  {
    module: 'Execution/Session/ChatExecution/StatusSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/ChatExecution/StatusSurface.fs',
  },
  {
    module: 'Execution/Delegation/Handle/Surface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Handle/Surface.fs',
  },
  {
    module: 'Execution/Delegation/Handle/FoldSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Handle/FoldSurface.fs',
  },
  {
    module: 'Execution/Delegation/Handle/JournalSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Handle/JournalSurface.fs',
  },
  {
    module: 'Execution/Session/Attachment/AttachmentSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/Attachment/AttachmentSurface.fs',
  },
  {
    module: 'Execution/Session/Attachment/SatelliteSurface.js',
    source: 'src/Wanxiangshu/Execution/Session/Attachment/SatelliteSurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/JoinResultRendererSurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/JoinResultRendererSurface.fs',
  },
  {
    module: 'Participant/Persona/OfficeCapabilitySurface.js',
    source: 'src/Wanxiangshu/Participant/Persona/OfficeCapabilitySurface.fs',
  },
  {
    module: 'Context/Trace/SemanticTraceSurface.js',
    source: 'src/Wanxiangshu/Context/Trace/SemanticTraceSurface.fs',
  },
  {
    module: 'Execution/Delegation/Fork/Host/HostForkPtySurface.js',
    source: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/HostForkPtySurface.fs',
  },
  {
    module: 'Interaction/Dispatch/DispatchSurface.js',
    source: 'src/Wanxiangshu/Interaction/Dispatch/DispatchSurface.fs',
  },
  {
    module: 'Mission/Manager/WorkflowSurface.js',
    source: 'src/Wanxiangshu/Mission/Manager/WorkflowSurface.fs',
  },
  {
    module: 'Interaction/Dispatch/JoinGuardSurface.js',
    source: 'src/Wanxiangshu/Interaction/Dispatch/JoinGuardSurface.fs',
  },
  {
    module: 'Interaction/Dispatch/RecoverySurface.js',
    source: 'src/Wanxiangshu/Interaction/Dispatch/RecoverySurface.fs',
  },
  {
    module: 'Change/Host/Surface.js',
    source: 'src/Wanxiangshu/Change/Host/Surface.fs',
  },
  {
    module: 'Foundation/ParallelSurface.js',
    source: 'src/Wanxiangshu/Foundation/ParallelSurface.fs',
  },
  {
    module: 'Foundation/FsToolkitFableCompat.js',
    source: 'src/Wanxiangshu/Foundation/FsToolkitFableCompat.fs',
  },
  {
    module: 'Foundation/OutcomeSurface.js',
    source: 'src/Wanxiangshu/Foundation/OutcomeSurface.fs',
  },
  {
    module: 'Resources/PromptSurface.js',
    source: 'src/Wanxiangshu/Resources/PromptSurface.fs',
  },
  {
    module: 'Verification/TemporalSurface.js',
    source: 'src/Wanxiangshu/Verification/TemporalSurface.fs',
  },
  {
    module: 'Verification/JournalPortObservationSurface.js',
    source: 'src/Wanxiangshu/Verification/JournalPortObservationSurface.fs',
  },
  {
    module: 'Mission/Planning/Surface.js',
    source: 'src/Wanxiangshu/Mission/Planning/Surface.fs',
  },
  {
    module: 'OpenCode/Tools/ToolRuntimeScopeSurface.js',
    source: 'src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScopeSurface.fs',
  },
  {
    module: 'Mission/Lifecycle/Surface.js',
    source: 'src/Wanxiangshu/Mission/Lifecycle/Surface.fs',
  },
  {
    module: 'Mission/Lifecycle/DurableWorkHost.js',
    source: 'src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs',
  },
  {
    module: 'OpenCode/Host/ProviderFailurePresentation.js',
    source: 'src/Wanxiangshu/OpenCode/Host/ProviderFailurePresentation.fs',
  },
]

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

const isEnvironmentKeys = (node) => node?.type === 'CallExpression'
  && node.callee?.object?.name === 'Object' && node.callee?.property?.name === 'keys'
  && node.arguments[0]?.object?.name === 'process' && node.arguments[0]?.property?.name === 'env'

const mangledLookupLines = (source, syntax) => {
  const lines = new Set()
  const visit = (node, environmentNames = new Set()) => {
    if (!node || typeof node !== 'object') return
    if (node.type === 'ForOfStatement' && isEnvironmentKeys(node.right)) {
      const name = node.left.declarations?.[0]?.id?.name
      visit(node.body, new Set([...environmentNames, name]))
      return
    }
    if (node.type === 'CallExpression') {
      const callee = node.callee
      if (callee?.property?.name === 'filter' && isEnvironmentKeys(callee.object)) {
        const callback = node.arguments[0]
        visit(callback?.body, new Set([...environmentNames, callback?.params?.[0]?.name]))
        return
      }
      if (['startsWith', 'endsWith'].includes(callee?.property?.name)
        && typeof node.arguments[0]?.value === 'string' && /__|_[A-Z]/.test(node.arguments[0].value)
        && !(callee.object?.type === 'Identifier' && environmentNames.has(callee.object.name))) {
        lines.add(node.loc.start.line)
      }
    }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach((child) => visit(child, environmentNames))
      else if (value && typeof value === 'object') visit(value, environmentNames)
    }
  }
  if (syntax) visit(syntax)
  else {
    source.split('\n').forEach((line, index) => { if (B_MANGLED_LOOKUP.test(line)) lines.add(index + 1) })
  }
  return lines
}

const syntaxChildren = node => Object.values(node).flatMap(value =>
  Array.isArray(value) ? value.filter(child => child?.type) : value?.type ? [value] : [])

const bindingNames = pattern => {
  if (!pattern) return []
  if (pattern.type === 'Identifier') return [pattern.name]
  if (pattern.type === 'Property') return bindingNames(pattern.value)
  if (pattern.type === 'AssignmentPattern') return bindingNames(pattern.left)
  return syntaxChildren(pattern).flatMap(bindingNames)
}

const receiverName = node => {
  if (node?.type === 'Identifier') return node.name
  if (node?.type === 'MemberExpression') return receiverName(node.object)
  return null
}

// A literal container ceases to be a proof once it can be changed or escape
// through an alias/call. Name collisions conservatively retain the rejection.
const escapedDataNames = syntax => {
  const names = new Set()
  const mark = node => {
    const name = receiverName(node)
    if (name) names.add(name)
  }
  const visit = node => {
    if (node.type === 'AssignmentExpression') {
      mark(node.left)
      mark(node.right)
    }
    if (node.type === 'UpdateExpression') mark(node.argument)
    if (node.type === 'VariableDeclarator' && (node.init?.type === 'Identifier' || node.init?.computed)) mark(node.init)
    if (node.type === 'ReturnStatement' && node.argument?.type === 'Identifier') mark(node.argument)
    if (node.type === 'ArrowFunctionExpression') mark(node.body)
    if (node.type === 'Property') mark(node.value)
    if (node.type === 'ArrayExpression') node.elements.forEach(mark)
    if (node.type === 'CallExpression' || node.type === 'NewExpression') {
      if (node.callee.type === 'MemberExpression') mark(node.callee.object)
      node.arguments.forEach(argument => { if (argument.type === 'Identifier') mark(argument) })
    }
    if (node.type === 'ExportSpecifier') mark(node.local)
    syntaxChildren(node).forEach(visit)
  }
  visit(syntax)
  return names
}

const isLiteralData = node => {
  if (!node) return false
  if (node.type === 'Literal') return true
  if (node.type === 'ArrayExpression') return node.elements.every(isLiteralData)
  if (node.type !== 'ObjectExpression') return false
  return node.properties.every(property => property.type === 'Property'
    && property.kind === 'init' && !property.method && !property.computed
    && isLiteralData(property.value))
}

const fieldsAccessLines = (source, syntax) => {
  const lines = new Set()
  if (!syntax) {
    source.split('\n').forEach((line, index) => { if (/\.fields\b/.test(line)) lines.add(index + 1) })
    return lines
  }
  const escaped = escapedDataNames(syntax)
  const values = (node, bindings) => node?.type === 'Identifier'
    ? escaped.has(node.name) ? [] : bindings.get(node.name) ?? []
    : node ? [node] : []
  const nativeObject = (node, bindings) => {
    const candidates = values(node, bindings)
    return candidates.length > 0 && candidates.every(value => value.type === 'ObjectExpression' && isLiteralData(value))
  }
  const bindUnknown = (bindings, pattern) => bindingNames(pattern).forEach(name => bindings.set(name, []))
  const visit = (node, bindings) => {
    if (node.type === 'Program' || node.type === 'BlockStatement') {
      const local = new Map(bindings)
      for (const statement of node.body) {
        if (statement.type === 'VariableDeclaration') statement.declarations.forEach(declaration => bindUnknown(local, declaration.id))
        if (statement.type === 'FunctionDeclaration' || statement.type === 'ClassDeclaration') bindUnknown(local, statement.id)
      }
      node.body.forEach(statement => visit(statement, local))
      return
    }
    if (['FunctionDeclaration', 'FunctionExpression', 'ArrowFunctionExpression'].includes(node.type)) {
      const local = new Map(bindings)
      node.params.forEach(parameter => bindUnknown(local, parameter))
      bindUnknown(local, node.id)
      syntaxChildren(node).forEach(child => visit(child, local))
      return
    }
    if (node.type === 'VariableDeclaration') {
      for (const declaration of node.declarations) {
        visit(declaration.id, bindings)
        if (declaration.init) visit(declaration.init, bindings)
        bindUnknown(bindings, declaration.id)
        if (node.kind === 'const' && declaration.id.type === 'Identifier' && isLiteralData(declaration.init)) {
          bindings.set(declaration.id.name, [declaration.init])
        }
      }
      return
    }
    if (node.type === 'ForOfStatement') {
      visit(node.right, bindings)
      const local = new Map(bindings)
      const declaration = node.left.declarations?.[0]
      bindUnknown(local, declaration?.id ?? node.left)
      visit(declaration?.id ?? node.left, local)
      const candidates = values(node.right, bindings)
      const array = candidates.length === 1 ? candidates[0] : null
      if (node.left.kind === 'const' && declaration?.id.type === 'Identifier'
        && array?.type === 'ArrayExpression' && isLiteralData(array)) {
        local.set(declaration.id.name, array.elements)
      }
      visit(node.body, local)
      return
    }
    if (node.type === 'CatchClause') {
      const local = new Map(bindings)
      bindUnknown(local, node.param)
      visit(node.body, local)
      return
    }
    if (node.type === 'MemberExpression'
      && (node.computed ? node.property.value === 'fields' : node.property.name === 'fields')
      && !nativeObject(node.object, bindings)) lines.add(node.loc.start.line)
    syntaxChildren(node).forEach(child => visit(child, bindings))
  }
  visit(syntax, new Map())
  return lines
}

const C1_DU_SHAPE = /\.cases\(\)|\.tag\b/
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
  let syntax
  try {
    syntax = parse(source, { ecmaVersion: 'latest', sourceType: 'module', locations: true })
  } catch {
    syntax = null
  }
  const mangledLines = mangledLookupLines(source, syntax)
  const fieldsLines = fieldsAccessLines(source, syntax)
  const hits = []
  for (let i = 0; i < lines.length; i++) {
    const text = lines[i]
    for (const [rule, re] of RULES) {
      if (rule === 'export-discovery') {
        if (isModuleDiscovery(text, moduleNames)) hits.push({ file: relPath, line: i + 1, rule, text: text.trim() })
      } else if (rule === 'mangled-lookup' ? mangledLines.has(i + 1)
        : rule === 'du-shape' ? fieldsLines.has(i + 1) || re.test(text) : re.test(text)) {
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
