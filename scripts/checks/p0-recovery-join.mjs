#!/usr/bin/env node
// P0-RECOVERY-JOIN-001 §10: production source patterns that reintroduce false finality.
//
// Modes:
//   node scripts/checks/p0-recovery-join.mjs           scan production tree
//   import { scanText, scanFiles, RULES } from ...      pure synthetic tests

import { readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'

/** @typedef {{ id: string, fileHint?: string | null, pathHint?: string | null, allowedPathPrefix?: string, pattern: RegExp, label: string, positive?: boolean }} Rule */

/** Pure rules: each id is one CI-checked invariant. */
export const RULES = [
  // —— EXEC-020 negative: agent ABORTED finality reintroduction ——
  {
    id: 'agent-aborted-type',
    fileHint: null,
    pattern: /\bAgentAborted\b/,
    label: 'production must not reintroduce AgentAborted type/case (EXEC-020)',
  },
  {
    id: 'agent-completion-aborted-factory',
    fileHint: null,
    pattern: /AgentCompletion\.aborted\b/,
    label: 'production must not call AgentCompletion.aborted (EXEC-020)',
  },
  {
    id: 'child-run-make-aborted',
    fileHint: null,
    pattern: /ChildRun\.makeAborted\b|\bmakeAborted\b/,
    label: 'production must not call ChildRun.makeAborted / makeAborted (EXEC-020)',
  },
  {
    id: 'aborted-run-factory',
    fileHint: null,
    pattern: /\babortedRun\b/,
    label: 'production must not define/use abortedRun factory (EXEC-020)',
  },
  {
    id: 'join-renderer-agent-status-aborted',
    fileHint: 'JoinResultRenderer.fs',
    // Agent join wire must never render status="aborted" (PTY PtyAborted may).
    // Match only agent-kind resultEntry with aborted status; do not flag PtyAborted path.
    pattern: /resultEntry[\s\S]{0,60}"agent"[\s\S]{0,40}"aborted"|"agent"[\s\S]{0,20}"aborted"/,
    label: 'JoinResultRenderer agent path must not render status="aborted" (EXEC-020)',
  },
  {
    id: 'codec-encode-finality-aborted',
    fileHint: 'HandleCompletionCodec.fs|CompletionCodec.fs',
    // Encode path must never write finality/status aborted as a Current blob.
    pattern: /finality["']\s*,\s*str\s+"aborted"|str\s+"aborted"[\s\S]{0,80}schemaVersion|"finality",\s*str\s+"aborted"/,
    label: 'HandleCompletionCodec must not encode finality=aborted as joinable blob (EXEC-021)',
  },
  {
    id: 'try-from-durable-completed',
    fileHint: null,
    pattern: /\btryFromDurableCompleted\b/,
    label: 'tryFromDurableCompleted deleted; use fromDecoded after DurableCompletionDecode (EXEC-021)',
  },
  {
    id: 'publish-completion-agent',
    fileHint: null,
    pattern: /\.PublishCompletion\b|\bPublishCompletion\b/,
    label: 'PublishCompletion(RunCompletion) banned; agent uses PulseAgentHandle + Journal (EXEC-024)',
  },
  {
    id: 'awaiting-evidence-case',
    fileHint: null,
    pattern: /\|\s*AwaitingEvidence\b|\bAwaitingEvidence\b\s*of\b/,
    label: 'AwaitingEvidence deleted; use RecoveryIncomplete | RecoveryBlocked (EXEC-023)',
  },
  {
    id: 'lifecycle-aborted-completion',
    fileHint: 'HostForkRunLifecycle.fs|RunLifecycle.fs',
    pattern: /AgentCompletion\.aborted\b/,
    label: 'HostForkRunLifecycle must not mint AgentCompletion.aborted',
  },
  {
    id: 'lifecycle-aborted-record',
    fileHint: 'HostForkRunLifecycle.fs|RunLifecycle.fs',
    // Aborted branch that still calls recordCompletion (comment-stripped line)
    pattern: /\brecordCompletion\b[\s\S]{0,80}\bAborted\b|\bAborted\b[\s\S]{0,120}\brecordCompletion\b/,
    label: 'TerminalOutcome.Aborted must not call recordCompletion',
  },
  {
    id: 'lifecycle-aborted-setresult',
    fileHint: 'HostForkRunLifecycle.fs|RunLifecycle.fs',
    pattern: /\bAborted\b[\s\S]{0,200}\.SetResult\b|\.SetResult\b[\s\S]{0,80}\bAborted\b/,
    label: 'Aborted path must not SetResult on completion cell',
  },
  {
    id: 'fork-recovery-synthetic-restored',
    fileHint: 'ForkRecovery.fs|Recovery.fs',
    pattern: /ofSimpleText[\s\S]{0,160}?restored|Completion\.TrySet|\.TrySetResult\b/,
    label: 'ForkRecovery must not synthesize restored completions',
  },
  {
    id: 'fork-recovery-interrupted-finality',
    fileHint: 'ForkRecovery.fs|Recovery.fs',
    pattern: /RunCompletion|makeAborted|AgentCompletion\.(?:aborted|failed|completed)|INTERRUPTED/,
    label: 'ForkRecovery.markInterrupted must not construct RunCompletion / INTERRUPTED finality',
  },
  {
    id: 'ensure-recovery-unit',
    fileHint: 'PluginRuntimeScope.fs',
    // Collapse FamilyRecovery → Task unit (old fail-open shape)
    pattern: /EnsureRecoveryDone[^\n]*:\s*Task\s*<\s*unit\s*>/,
    label: 'EnsureRecoveryDone must not return Task<unit>',
  },
  {
    id: 'missing-ports-family-ready',
    fileHint: 'PluginRuntimeScope.fs',
    // Synthetic FamilyReady when ports missing
    pattern:
      /familyRecoveryPorts[\s\S]{0,200}None[\s\S]{0,120}FamilyReady|None\s*->\s*[\s\S]{0,80}FamilyReady/,
    label: 'missing ports must not synthesize FamilyReady',
  },
  {
    // GREEN-4: option RestoreHandles / RecoverJob must not collapse to NoRecoveryRequired.
    id: 'restore-handles-none-no-recovery',
    fileHint: 'SessionRecoveryWorkflow.fs|Workflow.fs',
    pattern:
      /RestoreHandles[\s\S]{0,120}None[\s\S]{0,80}NoRecoveryRequired|None\s*->\s*[\s\S]{0,60}NoRecoveryRequired[\s\S]{0,80}RestoreHandles|match ports\.RestoreHandles/,
    label: 'RestoreHandles must be mandatory; missing port must not map to NoRecoveryRequired',
  },
  {
    id: 'recover-job-none-no-recovery',
    fileHint: 'SessionRecoveryWorkflow.fs|Workflow.fs',
    pattern:
      /RecoverJob[\s\S]{0,120}None[\s\S]{0,80}NoRecoveryRequired|match ports\.RecoverJob|RecoverJob:\s*\([^)]*\)\s*option/,
    label: 'RecoverJobs must be mandatory; RecoverJob option → NoRecoveryRequired is forbidden',
  },
  {
    id: 'spike-restore-handles-none',
    fileHint: 'SpikePlugin.fs',
    pattern: /RestoreHandles\s*=\s*None|RecoverJob\s*=\s*None/,
    label: 'SpikePlugin must inject real RestoreHandles/RecoverJobs (not None)',
  },
  {
    id: 'host-fork-runtime-recovery-task',
    fileHint: 'HostForkRuntime.fs|Runtime.fs',
    pattern: /\brecoveryTask\b|EnsureChildRestoreStarted|member [^\n]*AwaitRecovery|member [^\n]*RestoreLinkedHandles\s*\(\s*\)/,
    label: 'HostForkRuntime must not own recoveryTask / AwaitRecovery / EnsureChildRestoreStarted',
  },
  {
    id: 'host-fork-runtime-await-recovery-call',
    fileHint: null,
    pattern: /do!\s*this\.AwaitRecovery\s*\(\s*\)/,
    label: 'production must not call AwaitRecovery (recovery ownership is SessionRecoveryWorkflow)',
  },
  {
    id: 'join-tool-family-recovery',
    fileHint: 'JoinTool.fs',
    pattern: /RequireFamilyRecovery|EnsureRecoveryDone|FamilyReady/,
    label: 'JoinTool must RequireFamilyRecovery / match FamilyReady',
    positive: true,
  },
  {
    id: 'join-tool-family-blocked',
    fileHint: 'JoinTool.fs',
    pattern: /FamilyBlocked/,
    label: 'JoinTool must match FamilyBlocked',
    positive: true,
  },
  {
    id: 'join-tool-join-program',
    fileHint: 'JoinTool.fs',
    // EXEC-018 / PR5: JoinTool production path is direct Join.joinAvailable (no AST).
    pattern: /Join\.joinAvailable/,
    label: 'JoinTool must call Join.joinAvailable',
    positive: true,
  },
  {
    id: 'join-tool-no-bare-runtime-join',
    fileHint: 'JoinTool.fs',
    // P0 §五 / §十: JoinTool must not bare-call runtime.Join (JoinWithPermit / Join(permit ok elsewhere).
    // Bare = runtime.Join( without leading permit argument.
    pattern: /runtime\.Join\s*\(\s*(?!permit\b)/,
    label: 'JoinTool must not call runtime.Join; use Join.joinAvailable',
  },
  {
    // P0 REVISE: production Tools agent-join must not bare-call runtime.Join(
    // (JoinTool, Distillation*, ExecutorTool). No production path currently
    // bare-calls runtime.Join( — every join routes through Join.joinAvailable /
    // JoinWithPermit / AwaitAgentWithPermit. The exact-path allowlist
    // (BARE_RUNTIME_JOIN_ALLOW_PATHS) is empty, so any reintroduction fails
    // closed until an exact owner path is added with a source reference.
    // Allow runtime.Join(permit, ...) (permit-gated IExecutorRuntime); forbid Join() / Join(timeoutMs=...).
    id: 'tools-no-bare-runtime-join',
    fileHint: null,
    pattern: /runtime\.Join\s*\(\s*(?!permit\b)/,
    label:
      'production Tools agent-join must not bare-call runtime.Join(; use Join(permit) / JoinWithPermit / Join.joinAvailable',
  },
  {
    id: 'executor-tool-require-permit',
    fileHint: 'ExecutorTool.fs',
    pattern: /requirePermit|RequireFamilyRecovery|FamilyReady\s+permit|asExecutorRuntime/,
    label: 'ExecutorTool must RequireFamilyRecovery / requirePermit / asExecutorRuntime',
    positive: true,
  },
  {
    id: 'executor-tool-empty-session-fail-closed',
    fileHint: 'ExecutorTool.fs',
    // Empty SessionId must not return true / skip recovery.
    pattern: /IsNullOrWhiteSpace\s+context\.SessionId[\s\S]{0,80}return\s+true/,
    label: 'ExecutorTool empty SessionId must fail closed (not return true)',
  },
  {
    id: 'distillation-join-with-permit',
    fileHint: 'Distillation.fs',
    pattern: /JoinWithPermit|AwaitAgentWithPermit/,
    label: 'Distillation must call JoinWithPermit / AwaitAgentWithPermit',
    positive: true,
  },
  {
    id: 'distillation-runtime-join-with-permit',
    fileHint: 'DistillationRuntime.fs',
    pattern: /JoinWithPermit|requirePermit/,
    label: 'DistillationRuntime must wire JoinWithPermit + requirePermit',
    positive: true,
  },
  {
    id: 'join-with-permit-closure-digest',
    fileHint: 'Host/Join.fs|HostForkJoin.fs',
    pattern: /closureDigest|permitDigest|RecoveryClosureProjection\.discover/,
    label: 'JoinWithPermit must re-check closureDigest via RecoveryClosureProjection.discover',
    positive: true,
  },
  {
    id: 'host-fork-restart-false-finality',
    fileHint: 'HostForkRestart.fs|Restart.fs',
    // Synthetic aborted / restored finality must not be published on restart.
    pattern: /AgentCompletion\.aborted|makeAborted|ofSimpleText[\s\S]{0,100}?restored/,
    label: 'HostForkRestart must not mint aborted or synthetic restored finality',
  },
  {
    id: 'host-fork-restart-proof-structure',
    fileHint: 'HostForkRestart.fs|Restart.fs',
    // Restart recovery must walk interpreter / JoinableCompletion path.
    pattern:
      /ChildRecoveryWorkflow|tryFromProvenTerminal|JoinableCompletion|recordCompletion|HandleCompletionCodec\.(tryRead|tryReadBody|decodeBody)|fromDecoded|LegacyFalseAbort/,
    label: 'HostForkRestart must use proven terminal or durable completion structure',
    positive: true,
  },
  {
    id: 'host-fork-restart-bare-publish',
    fileHint: 'HostForkRestart.fs|Restart.fs',
    pattern: /AgentCompletion\.completed[\s\S]{0,400}PublishCompletion/,
    label: 'HostForkRestart must not PublishCompletion from bare AgentCompletion.completed',
  },
  {
    id: 'fork-runtime-parent-cancelled-aborted',
    fileHint: 'ForkRuntime.fs|Runtime.fs',
    pattern: /ParentCancelled[\s\S]{0,120}makeAborted|makeAborted[\s\S]{0,80}parent cancelled/,
    label: 'ParentCancelled must not mint makeAborted completion cell',
  },
  {
    // P0 §十: production recordCompletion call sites must be definition or ChildRecoveryWorkflow.
    // Scanned across all src/Wanxiangshu/**/*.fs (no fileHint). Comments stripped before match.
    id: 'record-completion-single-owner',
    fileHint: null,
    pattern: /\brecordCompletion\b/,
    label:
      'HandleController.recordCompletion production caller must be only ChildRecoveryWorkflow (or definition)',
  },
  // —— EXEC-020..024 positive: required shapes must remain ——
  {
    id: 'agent-outcome-completed-case',
    fileHint: 'AgentCompletion.fs',
    pattern: /\|\s*AgentCompleted\b/,
    label: 'AgentCompletionOutcome must include AgentCompleted (EXEC-020)',
    positive: true,
  },
  {
    id: 'agent-outcome-failed-case',
    fileHint: 'AgentCompletion.fs',
    pattern: /\|\s*AgentFailed\b/,
    label: 'AgentCompletionOutcome must include AgentFailed (EXEC-020)',
    positive: true,
  },
  {
    id: 'agent-outcome-abandoned-case',
    fileHint: 'AgentCompletion.fs',
    pattern: /\|\s*AgentAbandoned\b/,
    label: 'AgentCompletionOutcome must include AgentAbandoned (EXEC-020)',
    positive: true,
  },
  {
    id: 'agent-join-item-three-cases',
    fileHint: 'AgentCompletion.fs',
    pattern:
      /type AgentJoinItem\s*=\s*\n\s*\|\s*AgentCompletedItem[\s\S]{0,200}\|\s*AgentFailedItem[\s\S]{0,200}\|\s*AgentAbandonedItem/,
    label: 'AgentJoinItem must be Completed|Failed|Abandoned only (EXEC-020)',
    positive: true,
  },
  {
    id: 'pty-aborted-retained',
    fileHint: 'AgentCompletion.fs',
    pattern: /\|\s*PtyAborted\b/,
    label: 'PtyJoinItem must retain PtyAborted for physical PTY interrupt (EXEC-020)',
    positive: true,
  },
  {
    id: 'completion-blob-schema-v2',
    fileHint: 'HandleCompletionCodec.fs',
    pattern: /"schemaVersion",\s*box\s*2/,
    label: 'HandleCompletionCodec encode must write schemaVersion=2 (EXEC-021)',
    positive: true,
  },
  {
    id: 'legacy-false-abort-decode',
    fileHint: 'HandleCompletionCodec.fs',
    pattern: /LegacyFalseAbort/,
    label: 'HandleCompletionCodec must decode legacy abort as LegacyFalseAbort (EXEC-021)',
    positive: true,
  },
  {
    id: 'joinable-from-decoded',
    fileHint: 'ChildRecovery.fs',
    pattern: /let fromDecoded\b|fromDecoded/,
    label: 'JoinableCompletion.fromDecoded must exist as sole Current constructor (EXEC-021)',
    positive: true,
  },
  {
    id: 'session-ports-restore-handles-mandatory',
    fileHint: 'SessionRecoveryWorkflow.fs',
    pattern: /RestoreHandles:\s*SessionId\s*->\s*Task</,
    label: 'SessionRecoveryPorts.RestoreHandles must be mandatory (not option) (EXEC-023)',
    positive: true,
  },
  {
    id: 'session-ports-recover-jobs-mandatory',
    fileHint: 'SessionRecoveryWorkflow.fs',
    pattern: /RecoverJobs:\s*SessionId\s*->\s*Task</,
    label: 'SessionRecoveryPorts.RecoverJobs must be mandatory (not option) (EXEC-023)',
    positive: true,
  },
  {
    id: 'child-recovery-result-five-cases',
    fileHint: 'ChildRecovery.fs',
    pattern:
      /type ChildRecoveryResult\s*=[\s\S]{0,400}RecoveredActive[\s\S]{0,200}RecoveredTerminal[\s\S]{0,200}RecoveredAbandoned[\s\S]{0,200}RecoveryIncomplete[\s\S]{0,200}RecoveryBlocked/,
    label: 'ChildRecoveryResult must be five-case algebra without AwaitingEvidence (EXEC-023)',
    positive: true,
  },
  {
    id: 'join-program-requires-permit',
    fileHint: 'Join.fs',
    pattern: /joinAvailable[\s\S]{0,160}FamilyRecoveryPermit/,
    label: 'Join ops must take FamilyRecoveryPermit (EXEC-023)',
    positive: true,
  },
  {
    id: 'mailbox-pulse-agent-handle',
    fileHint: 'CompletionMailbox.fs',
    pattern: /PulseAgentHandle/,
    label: 'CompletionMailbox must expose PulseAgentHandle wake channel (EXEC-024)',
    positive: true,
  },
  {
    id: 'mailbox-publish-pty-completion',
    fileHint: 'CompletionMailbox.fs',
    pattern: /PublishPtyCompletion/,
    label: 'CompletionMailbox must expose PublishPtyCompletion PTY channel (EXEC-024)',
    positive: true,
  },
  {
    // EXEC-022: compensation fact must remain in Delegation fact algebra (codec permanently replayable).
    // After Fact ownership rotation (2026-08-17), constructors live in Execution/Delegation/Fact.fs and
    // cases live in Execution/Delegation/Facts.fs — not in every Fact.fs.
    id: 'false-completion-rejected-fact',
    fileHint: 'Delegation/Facts.fs|Delegation/Fact.fs',
    pattern: /HandleFalseCompletionRejected/,
    label: 'AgentFact must retain HandleFalseCompletionRejected for legacy abort compensation (EXEC-022)',
    positive: true,
  },
  {
    id: 'parent-join-correction-fact',
    fileHint: 'Delegation/Facts.fs|Delegation/Fact.fs',
    pattern: /ParentJoinCorrectionRequested/,
    label: 'AgentFact must retain ParentJoinCorrectionRequested for legacy false-abort compensation replay (EXEC-022)',
    positive: true,
  },
  // —— Task 10: provider-attempt recovery ownership and proof closure ——
  {
    id: 'provider-request-kind-owner',
    pathHint: 'src/Wanxiangshu/',
    allowedPathPrefix: 'src/Wanxiangshu/Participant/Provider/Attempt/',
    pattern: /\btype\s+ProviderRequestKind\b/,
    label: 'ProviderRequestKind may only be defined under Participant/Provider/Attempt',
  },
  {
    id: 'provider-recovery-role-classification',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    fileHint: 'Workflow.fs|Ledger.fs|ConfirmedFailurePort.fs',
    pattern: /\b(?:Provider|Participant|Persona)?Role\b|\.Role\b/,
    label: 'provider recovery must use typed attempt identity, not role classification',
  },
  {
    id: 'provider-recovery-error-string-classification',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    fileHint: 'Workflow.fs|Ledger.fs|ConfirmedFailurePort.fs',
    pattern:
      /\b(?:error|reason)\b\s*\.\s*(?:Contains|StartsWith|EndsWith|IndexOf)\b|\b(?:Regex\.)?IsMatch\s*\(\s*(?:error|reason)\b|\bmatch\s+(?:error|reason)\s+with\b|\bif\s+(?:error|reason)\s*=\s*["']/i,
    label: 'provider recovery must not classify attempts from error strings',
  },
  {
    id: 'no-active-run-continues-recovery',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    fileHint: 'Workflow.fs|Ledger.fs|ConfirmedFailurePort.fs',
    pattern:
      /\|\s*(?:Ok\s+)?(?:ConfirmedFailureOutcome\.)?NoActiveRun\b(?:(?!\n\s*\|)[\s\S]){0,300}->(?:(?!\n\s*\|)[\s\S]){0,300}\bContinueRecovery\b/,
    label: 'ConfirmedFailureOutcome.NoActiveRun must never continue recovery',
  },
  {
    id: 'provider-recovery-time-control',
    pathHint: 'Participant/Provider/Attempt/',
    pattern:
      /\b(?:DateTime|DateTimeOffset|TimeSpan|PeriodicTimer|Timer|time|deadline|sleep|poll(?:ing)?)\b|\bTask\.Delay\b|\bThread\.Sleep\b/i,
    label: 'provider recovery must be durable-fact-driven, never time/timer/deadline/sleep/poll driven',
  },
  {
    id: 'provider-recovery-process-local-success',
    pathHint: 'Participant/Provider/Attempt/',
    pattern:
      /\b\w*(?:registry|waiter|flight)\w*\b[\s\S]{0,180}\b(?:FallbackSucceeded|recordConfirmedSuccess|RecoveryAdvanced|ContinueRecovery|success|succeeded)\b|\b(?:FallbackSucceeded|recordConfirmedSuccess|RecoveryAdvanced|ContinueRecovery|success|succeeded)\b[\s\S]{0,180}\b\w*(?:registry|waiter|flight)\w*\b/i,
    label: 'process-local registry/waiter/flight state must not prove provider recovery success',
  },
  {
    id: 'old-fallback-surface-import',
    pathHint: 'src/Wanxiangshu/',
    pattern:
      /(?:\bopen\s+|\bimport\s+.*?from\s+["']|\brequire\s*\(\s*["'])[^\n"']*Participant[./\\]Provider[./\\]Attempt[./\\]Fallback[./\\](?:HandleSurface|Surface)\b/,
    label: 'old Fallback HandleSurface/Surface imports are forbidden; use CursorSurface',
  },
  {
    id: 'old-fallback-surface-compile-entry',
    fileHint: 'Wanxiangshu.fsproj',
    pattern:
      /<Compile\s+Include=["'][^"']*Participant[\\/]Provider[\\/]Attempt[\\/]Fallback[\\/](?:HandleSurface|Surface)\.fs["']/,
    label: 'old Fallback HandleSurface/Surface compile entries are forbidden; compile CursorSurface only',
  },
  {
    id: 'confirmed-failure-outcome-contract',
    fileHint: 'ConfirmedFailurePort.fs',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    pattern:
      /type\s+ConfirmedFailureOutcome\s*=\s*\r?\n\s*\|\s*RecoveryAdvanced\s+of\s+RecoveryOpportunity\s*\r?\n\s*\|\s*RecoveryExhausted\s*\r?\n\s*\|\s*AlreadyRecorded\s*\r?\n\s*\|\s*NoActiveRun\s*\r?\n(?:\s*\r?\n|\s*\/\/\/[^\n]*\r?\n)*\s*type\s+ConfirmedFailurePort\s*=[^\n]*Task\s*<\s*Result\s*<\s*ConfirmedFailureOutcome\s*,\s*string\s*>\s*>/,
    label: 'ConfirmedFailurePort must own the exact typed four-case ConfirmedFailureOutcome contract',
    positive: true,
  },
  {
    id: 'workflow-confirmed-failure-exhaustive',
    fileHint: 'Workflow.fs',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    pattern:
      /\bmatch\s+\w+\s+with\b[\s\S]{0,1200}\bConfirmedFailureOutcome\.RecoveryExhausted\b[\s\S]{0,500}\bConfirmedFailureOutcome\.AlreadyRecorded\b[\s\S]{0,500}\bConfirmedFailureOutcome\.NoActiveRun\b[\s\S]{0,500}\bConfirmedFailureOutcome\.RecoveryAdvanced\b/,
    label: 'Fallback Workflow must exhaustively handle every ConfirmedFailureOutcome case',
    positive: true,
  },
  {
    id: 'workflow-main-session-failure-owner',
    fileHint: 'Workflow.fs',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    pattern:
      /\blet\s+ownerSessionId\s*=\s*mainSessionOfBloggerProjection\s+projection\s+turn\.SessionId\s*\|>\s*Option\.orElseWith\s*\(fun\s*\(\)\s*->\s*FallbackEvidence\.tryCurrentState\s+turn\.SessionId\s+projection\s*\|>\s*Option\.map\s*\(fun\s+_\s*->\s*turn\.SessionId\)\)[\s\S]{0,700}\blet\s+ownerState\s*=\s*ownerSessionId\s*\|>\s*Option\.bind\s*\(fun\s+owner\s*->\s*FallbackEvidence\.tryCurrentState\s+owner\s+projection\s*\|>\s*Option\.map\s*\(fun\s+current\s*->\s*owner\s*,\s*current\)\)[\s\S]{0,500}\bmatch\s+ownerState\s+with\s*\|\s*None\s*->\s*Task\.FromResult\s*\(Ok\s+ConfirmedFailureOutcome\.NoActiveRun\)[\s\S]{0,300}\|\s*Some\s*\(owner\s*,\s*current\)\s*->\s*admitCurrentFailure\s+durable\s+owner\b/,
    label: 'Fallback Workflow must resolve one Blogger-main or durable WorkMain owner and fail closed without it',
    positive: true,
  },
  {
    id: 'workflow-failure-ledger-owner-flow',
    fileHint: 'Workflow.fs',
    pathHint: 'Participant/Provider/Attempt/Fallback/',
    pattern:
      /\blet\s+private\s+admitAuthorizedFailure[\s\S]{0,400}\(ownerSessionId\s*:\s*SessionId\)[\s\S]{0,700}\bFallbackLedger\.recordAuthorizedFailure\s+durable\s+ownerSessionId\s+authorization\s+error[\s\S]{0,300}\breconcileFailureAdmission\s+durable\s+ownerSessionId\s+admission\b/,
    label: 'Fallback Workflow must pass the same resolved owner through the sole ledger write and reconciliation',
    positive: true,
  },
  {
    id: 'interaction-repair-main-session-failure-owner',
    fileHint: 'InteractionRepair.fs',
    pathHint: 'Interaction/Repair/',
    pattern:
      /\bProviderRecoveryWorkflow\.admitPolicyAuthorizedFailure\s+journal\s+turn\s+ExecutionFailure\.ProviderTransient\s+requestKind\s+reason\b/,
    label: 'InteractionRepair must delegate exact owner resolution and failure admission to ProviderRecoveryWorkflow',
    positive: true,
  },
  {
    id: 'interaction-repair-no-fallback-writer',
    fileHint: 'InteractionRepair.fs',
    pathHint: 'Interaction/Repair/',
    pattern: /\bFallbackLedger\b/,
    label: 'InteractionRepair must not bypass ProviderRecoveryWorkflow and write the fallback ledger directly',
  },
]

export const RULE_IDS = RULES.map((r) => r.id)

const norm = (p) => p.replace(/\\/g, '/')

const stripComments = (line) => line.replace(/\/\/.*/g, '')

/**
 * Exact normalized repo-relative paths permitted to reference recordCompletion:
 * the HandleController definition and its sole production caller ChildRecoveryWorkflow.
 * Basename matching is forbidden — an unrelated `Surface.fs` / `Controller.fs` must not
 * inherit this authority. Fail-closed for any renamed/moved/new file.
 */
const RECORD_COMPLETION_OWNER_PATHS = new Set([
  'src/Wanxiangshu/Execution/Delegation/Handle/Controller.fs',
  'src/Wanxiangshu/Execution/Delegation/ChildRecoveryWorkflow.fs',
])

/**
 * Exact normalized repo-relative paths permitted to bare-call runtime.Join(.
 * No production path currently does — every join routes through Join.joinAvailable /
 * JoinWithPermit / AwaitAgentWithPermit. The set is empty so any reintroduction of
 * runtime.Join( fails closed until an exact owner path is added here with a source
 * reference proving it is a physical caller.
 */
const BARE_RUNTIME_JOIN_ALLOW_PATHS = new Set([])

/**
 * Scan one file body. Multi-line rules see joined non-comment text; single-line
 * rules still report the first matching line number when possible.
 * @returns {{ id: string, file: string, line: number, label: string, text: string }[]}
 */
export const scanText = (text, file = '<synthetic>') => {
  const base = file.split(/[/\\]/).pop() || file
  const lines = text.split('\n')
  const codeLines = lines.map((line) => stripComments(line))
  const joined = codeLines.join('\n')
  const hits = []

  for (const rule of RULES) {
    if (rule.pathHint && file !== '<synthetic>' && !norm(file).includes(rule.pathHint)) continue
    if (rule.fileHint && file !== '<synthetic>') {
      const hints = rule.fileHint.split('|')
      if (!hints.some((h) => base === h || file.endsWith(h))) continue
    }

    if (rule.positive) {
      if (!rule.pattern.test(joined) && !rule.pattern.test(text)) {
        hits.push({
          id: rule.id,
          file,
          line: 1,
          label: rule.label,
          text: 'missing required pattern',
        })
      }
      continue
    }

    // Prefer line-local match for simple patterns; fall back to multi-line search.
    let found = false
    for (let i = 0; i < codeLines.length; i++) {
      if (rule.pattern.test(codeLines[i])) {
        if (rule.allowedPathPrefix && norm(file).startsWith(rule.allowedPathPrefix)) {
          found = true
          break
        }
        // Sole-owner rule: exact owner paths only (definition + ChildRecoveryWorkflow).
        if (
          rule.id === 'record-completion-single-owner' &&
          RECORD_COMPLETION_OWNER_PATHS.has(norm(file))
        ) {
          found = true
          break
        }
        // Bare Join allowlist: exact physical caller paths only (currently none).
        if (
          rule.id === 'tools-no-bare-runtime-join' &&
          BARE_RUNTIME_JOIN_ALLOW_PATHS.has(norm(file))
        ) {
          found = true
          break
        }
        // JoinWithPermit( is not bare Join(; line-level pattern is runtime.Join(
        // which already excludes JoinWithPermit. Keep for clarity.
        hits.push({
          id: rule.id,
          file,
          line: i + 1,
          label: rule.label,
          text: lines[i].trim(),
        })
        found = true
        break
      }
    }
    if (!found && rule.pattern.test(joined)) {
      if (rule.allowedPathPrefix && norm(file).startsWith(rule.allowedPathPrefix)) {
        continue
      }
      if (
        rule.id === 'record-completion-single-owner' &&
        RECORD_COMPLETION_OWNER_PATHS.has(norm(file))
      ) {
        continue
      }
      if (
        rule.id === 'tools-no-bare-runtime-join' &&
        BARE_RUNTIME_JOIN_ALLOW_PATHS.has(norm(file))
      ) {
        continue
      }
      // Multi-line hit: approximate first line of match.
      const m = joined.match(rule.pattern)
      let line = 1
      if (m && typeof m.index === 'number') {
        line = joined.slice(0, m.index).split('\n').length
      }
      hits.push({
        id: rule.id,
        file,
        line,
        label: rule.label,
        text: (m && m[0] ? m[0].replace(/\s+/g, ' ').slice(0, 120) : rule.label),
      })
    }
  }
  return hits
}

/** @param {{ file: string, text: string }[]} entries */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    for (const hit of scanText(entry.text, entry.file)) violations.push(hit)
  }
  return violations
}

const runCli = () => {
  const productionFiles = walk(PRODUCTION_ROOT, ['.fs', '.fsproj']).map(norm)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const violations = scanFiles(entries)

  if (violations.length === 0) {
    console.log(`p0-recovery-join: OK — ${productionFiles.length} files, ${RULES.length} rules`)
    process.exit(0)
  }

  console.error(`p0-recovery-join: ${violations.length} violation(s)\n`)
  for (const v of violations) {
    console.error(`  [${v.id}] ${v.file}:${v.line}  ${v.label}`)
    console.error(`    ${v.text}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
