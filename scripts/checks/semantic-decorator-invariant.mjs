#!/usr/bin/env node
// Semantic higher-order composition invariant. A passed business operation may
// be invoked once transparently, including on one selected failure branch.
// Equivalent repeated direct calls, recursion, or loop-driven re-invocation changes
// trace and therefore requires an owner law, trace relation and executable
// policy proof at the declaration.

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { buildTraceGraph } from '../lib/requirement-trace.mjs'
import { walk } from '../lib/walk.mjs'
import { scanProjectSymbolUses } from './owner-dependencies.mjs'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const PRODUCTION_ROOT = 'src/Wanxiangshu'
const FCS_SCRATCH = join(ROOT, '.fable-build/semantic-decorator-fcs')
const FCS_RESULT = join(FCS_SCRATCH, 'symbol-uses.json')

const GENERIC_FRAMEWORK_PATTERNS = Object.freeze([
  /\bMiddlewarePipeline\b/,
  /\bDecoratorBase\b/,
  /\bIWorkflowDecorator\b/,
  /\bITransformMiddleware\b/,
  /\b(?:type|and)\s+I\w*(?:Middleware|Decorator)\b/,
  /\b(?:register|add|insert)(?:Middleware|Decorator)\b/i,
  /\b(?:middleware|decorator|pipeline)s?\s*\.\s*(?:Add|Insert|Register|Remove)\b/i,
  /\b(?:ResizeArray|List|Dictionary)<[^>]*(?:Middleware|Decorator)[^>]*>/,
  /\babstract\s+[A-Za-z_]\w*\s*:\s*\([^\n)]*->[^\n)]*\)\s*->/,
])

const DECLARATION = /^(\s*)let\s+(?:rec\s+)?(?:(?:private|internal|inline|mutable)\s+)*([A-Za-z_]\w*)\b/
const REQUIRED_METADATA = Object.freeze([
  ['owner', 'owner'],
  ['WHAT', 'WHAT'],
  ['trace relation', 'trace-relation'],
  ['proof', 'proof'],
  ['failure policy', 'failure-policy'],
  ['cancel policy', 'cancel-policy'],
  ['deadline policy', 'deadline-policy'],
])

export const PHYSICAL_LISTENER_CONTRACTS = Object.freeze([
  Object.freeze({
    kind: 'invocation',
    file: 'src/Wanxiangshu/OpenCode/Host/Events.fs',
    declaration: 'replayStickyTerminals',
    parameter: 'listener',
    site: 'listener (SessionId.create sessionKey) outcome',
    owner: 'host-boundary',
    what: 'HOST-BOUNDARY-016',
    traceRelation: 'R_physical_terminal_fanout=one-notification-per-live-listener',
    proof: 'requirements/host-boundary/tests/events-port.test.mjs::WHAT[HOST-BOUNDARY-016] EVT_terminal_notification_fans_out_once_to_each_live_physical_listener',
    failurePolicy: 'listener failures propagate synchronously and stop the remaining physical fanout',
    cancelPolicy: 'notification fanout introduces no cancellation boundary',
    deadlinePolicy: 'notification fanout introduces no deadline',
  }),
  Object.freeze({
    kind: 'collection',
    file: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs',
    binding: 'ptyCompletionObservers',
    sites: ['let ptyCompletionObservers = ResizeArray<PtyJoinItem -> unit>()', 'ptyCompletionObservers.Add listener'],
    owner: 'delegation',
    what: 'DELEG-019',
    traceRelation: 'R_physical_pty_observation=each-completion-once-per-live-observer',
    proof: 'requirements/delegation/tests/pty-observer-fanout.test.mjs::WHAT[DELEG-019] HOST_PTY_completion_observers_receive_each_physical_completion_once_until_disposed',
    failurePolicy: 'observer exceptions are isolated so remaining physical observers receive the completion',
    cancelPolicy: 'disposing one registration removes only that observer',
    deadlinePolicy: 'synchronous physical observation introduces no deadline',
  }),
  Object.freeze({
    kind: 'collection',
    file: 'src/Wanxiangshu/Process/Pty.fs',
    binding: 'mailboxSenders',
    sites: ['let mailboxSenders = ResizeArray<PtyJoinItem -> unit>()', 'mailboxSenders.Add sender'],
    owner: 'process-execution',
    what: 'PROC-003',
    traceRelation: 'R_physical_pty_mailbox=each-first-close-once-per-registered-sender',
    proof: 'requirements/process-execution/tests/pty-port.test.mjs::WHAT[PROC-003] PORT_AddMailboxSender_reaches_every_registered_sender',
    failurePolicy: 'sender failures are isolated so remaining physical senders receive the close item',
    cancelPolicy: 'PTY abort is delivered as a typed close item without changing sender registration',
    deadlinePolicy: 'synchronous physical delivery introduces no deadline',
  }),
])

export const TRACE_CHANGE_CONTRACTS = Object.freeze([
  Object.freeze({
    file: 'src/Wanxiangshu/Process/NodeProcessHost.fs', declaration: 'notifyExitedList', parameter: 'callbacks',
    sites: ['let cbs = callbacks |> Seq.toList', 'callbacks.Clear()', 'for cb in cbs do', 'cb ()'], owner: 'process-execution', what: 'PROC-003',
    traceRelation: 'R_physical_exit=registration-order-once-per-snapshotted-callback',
    proof: 'requirements/process-execution/tests/process-wait.test.mjs::WHAT[PROC-003] NODE_EXIT_registered_callbacks_run_once_in_registration_order_and_fail_fast',
    maxPathInvocationsPerListener: 1,
    listenerCardinality: 'callbacks.Count at exit receipt',
    failurePolicy: 'callbacks run synchronously in registration order and the first callback failure stops the remaining snapshot',
    cancelPolicy: 'wait cancellation removes its callback before physical exit notification',
    deadlinePolicy: 'exit notification introduces no deadline and does not extend a waiter deadline',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Strength/Lifecycle.fs', declaration: 'replayPlans', parameter: 'loadBundle',
    sites: [
      'let rec loop (remaining: StrengthCandidateView list) (acc: StrengthReplayPlan list) =',
      'let! bundle = loadBundle view.Prepared',
    ], owner: 'speculative-investigation', what: 'SPEC-INV-008',
    traceRelation: 'loadBundle is invoked inside the deterministic decision-id-sorted candidate traversal, after anchor resolution and before plan construction',
    proof: 'requirements/speculative-investigation/tests/lifecycle-recovery.test.mjs::WHAT[SPEC-INV-008] STRENGTH_008_replay_loads_each_selected_plan_once_in_decision_order_and_stops_on_load_failure',
    iterationCardinality: 'candidates.Length after owner-session, Promoted, and non-Abandoned filtering',
    maxInvocationsPerItem: 1,
    failurePolicy: 'the first loadBundle Error propagates unchanged and stops traversal before any later candidate load',
    cancelPolicy: 'loadBundle task cancellation propagates and stops traversal before any later candidate load',
    deadlinePolicy: 'replay traversal introduces no deadline and does not extend the loadBundle deadline',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Foundation/FsToolkitFableCompat.fs', declaration: 'traverseM', parameter: 'mapper',
    sites: [
      'let rec collect reversed remaining =',
      'let! value = mapper item',
    ], owner: 'intra-participant-parallelism', what: 'INTRA-PARTICIPANT-PARALLELISM-016',
    traceRelation: 'mapper is invoked exactly once per reached item in strict input order; success reaches every item, the first Error stops before the tail, empty input invokes zero times, and no item is retried',
    proof: 'requirements/intra-participant-parallelism/tests/task-result-list-traversal.test.mjs::WHAT[INTRA-PARTICIPANT-PARALLELISM-016] TASK_RESULT_LIST_traverseM_calls_mapper_once_per_input_in_order_stops_at_first_Error_and_skips_empty',
    iterationCardinality: 'items.Length',
    maxInvocationsPerItem: 1,
    failurePolicy: 'the first mapper Error propagates unchanged and stops traversal before any later item',
    cancelPolicy: 'mapper task cancellation propagates and stops traversal before any later item',
    deadlinePolicy: 'traversal introduces no deadline and does not extend the mapper deadline',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Change/Host/GitAdapter.fs', declaration: 'hasRebaseHead', parameter: 'runner',
    sites: [
      'let! mergeCode, mergePath, _ = runner (command worktree [ "rev-parse"; "--git-path"; "rebase-merge" ])',
      'let! applyCode, applyPath, _ = runner (command worktree [ "rev-parse"; "--git-path"; "rebase-apply" ])',
    ], owner: 'change-integration', what: 'CHGINT-003',
    traceRelation: 'invoke rev-parse --git-path rebase-merge exactly once, then rev-parse --git-path rebase-apply exactly once; the second command is not short-circuited when the first directory exists, and the result is the OR of the two successful nonblank existing-directory observations',
    proof: 'requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-003] GIT_has_rebase_head_true_only_when_git_path_dir_exists',
    invocationBound: 2,
    failurePolicy: 'a nonzero exit or blank/nonexistent path contributes false; an injected runner task failure propagates and prevents later commands',
    cancelPolicy: 'cancellation is owned by the injected command runner and stops before the next command',
    deadlinePolicy: 'each command uses the runner deadline; no sequence-level deadline extension',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Git/Operations.fs', declaration: 'continueRebase', parameter: 'runner',
    sites: [
      'let! addCode, _, addErr = runner (command dir [ "add"; "-A" ])',
      'let! code, stdout, stderr = runner (command dir [ "-c"; "core.editor=true"; "rebase"; "--continue" ])',
    ], owner: 'change-integration', what: 'CHGINT-003',
    traceRelation: 'invoke git add -A exactly once and, only after a zero exit, invoke git -c core.editor=true rebase --continue exactly once',
    proof: 'requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-003] GIT_rebase_in_progress_stages_and_continues',
    invocationBound: 2,
    failurePolicy: 'a failed add returns its Git failure and suppresses rebase --continue; a failed continue returns its Git failure without another command',
    cancelPolicy: 'cancellation is owned by the injected command runner and stops before the next command',
    deadlinePolicy: 'each command uses the runner deadline; no sequence-level deadline extension',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Git/Operations.fs', declaration: 'freshRebase', parameter: 'runner',
    sites: [
      'let! _ = runner (command dir [ "update-ref"; "-d"; "REBASE_HEAD" ])',
      'let! code, stdout, stderr = runner (command dir [ "rebase"; TargetRef.value target ])',
    ], owner: 'change-integration', what: 'CHGINT-003',
    traceRelation: 'invoke update-ref -d REBASE_HEAD exactly once, then invoke rebase <frozen target> exactly once regardless of the stale-ref deletion exit tuple',
    proof: 'requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-003] GIT_rebase_stale_rebase_head_is_cleared_before_fresh_rebase',
    invocationBound: 2,
    failurePolicy: 'ignore the stale REBASE_HEAD deletion outcome; return the fresh rebase Git failure on nonzero exit',
    cancelPolicy: 'cancellation is owned by the injected command runner and stops before the next command',
    deadlinePolicy: 'each command uses the runner deadline; no sequence-level deadline extension',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Git/Operations.fs', declaration: 'rebaseInProgress', parameter: 'runner',
    sites: [
      'let! mergeCode, mergePath, _ = runner (command dir [ "rev-parse"; "--git-path"; "rebase-merge" ])',
      'let! applyCode, applyPath, _ = runner (command dir [ "rev-parse"; "--git-path"; "rebase-apply" ])',
    ], owner: 'change-integration', what: 'CHGINT-003',
    traceRelation: 'invoke rev-parse --git-path rebase-merge exactly once, then rev-parse --git-path rebase-apply exactly once; the second command is not short-circuited when the first directory exists, and the result is the OR of the two successful nonblank existing-directory observations',
    proof: 'requirements/change-integration/tests/git-operations.test.mjs::WHAT[CHGINT-003] GIT_rebase_in_progress_stages_and_continues',
    invocationBound: 2,
    failurePolicy: 'a nonzero exit or blank/nonexistent path contributes false; an injected runner task failure propagates and prevents later commands',
    cancelPolicy: 'cancellation is owned by the injected command runner and stops before the next command',
    deadlinePolicy: 'each command uses the runner deadline; no sequence-level deadline extension',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/ChildDispatch.fs', declaration: 'settlePendingAbandoned', parameter: 'settleAbandoned',
    sites: ['settleAbandoned run'], owner: 'delegation', what: 'DELEG-019',
    traceRelation: 'R_shutdown_abandon=once-per-pending-run',
    proof: 'requirements/delegation/tests/join-tool.test.mjs::WHAT[DELEG-019] JOIN_TOOL_abandoned_agent_is_not_completed',
    failurePolicy: 'the first settlement failure stops shutdown settlement', cancelPolicy: 'shutdown cancellation does not skip the pending snapshot', deadlinePolicy: 'no deadline is introduced',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs', declaration: 'failCalls', parameter: 'failCall',
    sites: ['failCall call error'], owner: 'delegation', what: 'DELEG-025',
    traceRelation: 'R_sync_failure=once-per-selected-call',
    proof: 'requirements/delegation/tests/sync-delegate-runtime.test.mjs::WHAT[DELEG-025] SYNC_RUNTIME_late_failure_from_previous_authority_root_cannot_fail_reused_call',
    failurePolicy: 'the selected call set receives the same typed failure', cancelPolicy: 'no cancellation boundary is introduced', deadlinePolicy: 'no deadline is introduced',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Execution/Session/RecoveryClosureProjection.fs', declaration: 'addBloggerPair', parameter: 'add',
    sites: ['add (RecoveryNode.Companion(owner, blogger))', 'add (RecoveryNode.Blogger(owner, blogger))'], owner: 'managed-session-lifecycle', what: 'MANAGED-SESSION-013',
    traceRelation: 'R_recovery_pair=companion-then-blogger',
    proof: 'requirements/managed-session-lifecycle/tests/session-recovery.test.mjs::WHAT[MANAGED-SESSION-013] session_recovery_contract_authorizes_family_without_physical_handle_leaks',
    failurePolicy: 'missing association adds neither node and add failure stops projection', cancelPolicy: 'pure projection introduces no cancellation', deadlinePolicy: 'pure projection introduces no deadline',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Execution/Session/RecoveryClosureProjection.fs', declaration: 'addManagerJob', parameter: 'add',
    sites: ['add (RecoveryNode.ManagerJob(job.ManagerJobId, job.ManagerSessionId))', 'add ('], owner: 'managed-session-lifecycle', what: 'MANAGED-SESSION-013',
    traceRelation: 'R_recovery_manager_job=job-plus-linked-manager-family',
    proof: 'requirements/managed-session-lifecycle/tests/session-recovery.test.mjs::WHAT[MANAGED-SESSION-013] session_recovery_contract_authorizes_family_without_physical_handle_leaks',
    failurePolicy: 'unrelated jobs add no node and add failure stops projection', cancelPolicy: 'pure projection introduces no cancellation', deadlinePolicy: 'pure projection introduces no deadline',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/OpenCode/Codec/ToolHostCodec.fs', declaration: 'listenAbort', parameter: 'callback',
    sites: ['callback ()', 'let listener = fun (_: obj) -> callback ()'], owner: 'host-boundary', what: 'HOST-BOUNDARY-009',
    traceRelation: 'R_abort_listener=immediate-or-once-event',
    proof: 'requirements/host-boundary/tests/tool-host-abort.test.mjs::WHAT[HOST-BOUNDARY-009] HOST_abort_callback_fires_once_immediately_or_from_the_registered_unit_listener',
    failurePolicy: 'callback exceptions propagate from the physical abort notification', cancelPolicy: 'unsubscribe removes the registered abort listener', deadlinePolicy: 'abort observation introduces no deadline',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Process/ProcessRunner.fs', declaration: 'emitOutputChunks', parameter: 'onStdout',
    sites: ['onStdout chunk'], owner: 'process-execution', what: 'PROC-010',
    traceRelation: 'R_stdout_chunks=ordered-8192-byte-physical-output',
    proof: 'requirements/process-execution/tests/process-runner.test.mjs::WHAT[PROC-010] EXEC_011_successful_run_collects_stdout_and_exit_code',
    failurePolicy: 'consumer failure stops output delivery and fails execution', cancelPolicy: 'parent cancellation precedes launcher completion delivery', deadlinePolicy: 'the caller process deadline is unchanged',
  }),
  Object.freeze({
    file: 'src/Wanxiangshu/Process/ProcessRunner.fs', declaration: 'emitOutputChunks', parameter: 'onStderr',
    sites: ['onStderr chunk'], owner: 'process-execution', what: 'PROC-010',
    traceRelation: 'R_stderr_chunks=ordered-8192-byte-physical-output',
    proof: 'requirements/process-execution/tests/process-runner.test.mjs::WHAT[PROC-010] EXEC_011_successful_run_collects_stdout_and_exit_code',
    failurePolicy: 'consumer failure stops output delivery and fails execution', cancelPolicy: 'parent cancellation precedes launcher completion delivery', deadlinePolicy: 'the caller process deadline is unchanged',
  }),
])

let traceGraph
const requirementGraph = () => {
  traceGraph ??= buildTraceGraph(join(ROOT, 'requirements'))
  return traceGraph
}

const annotationValue = (docs, field) => {
  const escaped = field.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return new RegExp(`semantic-decorator-${escaped}:\\s*(.+)$`, 'im').exec(docs)?.[1].trim() ?? ''
}

const validateAuthority = (docs) => {
  const missing = REQUIRED_METADATA
    .filter(([, field]) => annotationValue(docs, field) === '')
    .map(([name]) => name)
  const owner = annotationValue(docs, 'owner')
  const whatId = annotationValue(docs, 'WHAT')
  const proof = annotationValue(docs, 'proof')
  const graph = requirementGraph()
  const what = graph.whats.get(whatId)

  if (owner && ![...graph.whats.values()].some((candidate) => candidate.package === owner)) missing.push('existing owner')
  if (whatId && (!what || graph.whatDefinitions.get(whatId)?.length !== 1)) missing.push('unique WHAT')
  if (owner && what && what.package !== owner) missing.push('WHAT owned by declared owner')

  if (proof) {
    const separator = proof.indexOf('::')
    if (separator <= 0 || separator === proof.length - 2) missing.push('proof path and exact title')
    else {
      const proofPath = proof.slice(0, separator).trim().replace(/\\/g, '/')
      const title = proof.slice(separator + 2).trim()
      const absolute = resolve(ROOT, proofPath)
      const matches = graph.tests.filter((test) => resolve(test.file) === absolute && test.title === title)
      if (matches.length !== 1 || matches[0].state !== 'active' || matches[0].whatIds.length !== 1 || matches[0].whatIds[0] !== whatId) {
        missing.push('exact WHAT-owned proof')
      }
    }
  }
  return [...new Set(missing)]
}

const physicalListenerContractDocs = (contract) => [
  `// semantic-decorator-owner: ${contract.owner}`,
  `// semantic-decorator-WHAT: ${contract.what}`,
  `// semantic-decorator-trace-relation: ${contract.traceRelation}`,
  `// semantic-decorator-proof: ${contract.proof}`,
  `// semantic-decorator-failure-policy: ${contract.failurePolicy}`,
  `// semantic-decorator-cancel-policy: ${contract.cancelPolicy}`,
  `// semantic-decorator-deadline-policy: ${contract.deadlinePolicy}`,
].join('\n')

const resolvedPhysicalListenerContract = (file, declaration, parameter, bodyLines, calls) =>
  PHYSICAL_LISTENER_CONTRACTS.find((contract) =>
    contract.kind === 'invocation' &&
    contract.file === file &&
    contract.declaration === declaration &&
    contract.parameter === parameter &&
    calls.length === 1 &&
    bodyLines[calls[0].line]?.trim() === contract.site &&
    validateAuthority(physicalListenerContractDocs(contract)).length === 0)

const resolvedTraceChangeContract = (file, declaration, parameter, bodyLines, calls) =>
  TRACE_CHANGE_CONTRACTS.find((contract) => {
    if (contract.file !== file || contract.declaration !== declaration || contract.parameter !== parameter) return false
    const callSites = calls.map(({ line }) => bodyLines[line]?.trim())
    if (contract.listenerCardinality || contract.iterationCardinality) {
      return callSites.length > 0 &&
        callSites.every((site) => contract.sites.includes(site)) &&
        contract.sites.every((site) => bodyLines.some((line) => line.trim() === site)) &&
        validateAuthority(physicalListenerContractDocs(contract)).length === 0
    }
    return callSites.length > 0 &&
      callSites.every((site) => contract.sites.includes(site)) &&
      contract.sites.every((site) => callSites.includes(site)) &&
      validateAuthority(physicalListenerContractDocs(contract)).length === 0
  })

const declarationBlock = (lines, start) => {
  const parts = []
  for (let i = start; i < Math.min(lines.length, start + 16); i++) {
    parts.push(lines[i])
    if (/=\s*(?:task\s*\{|async\s*\{|result\s*\{|taskResult\s*\{|$)/.test(lines[i])) {
      return { text: parts.join('\n'), end: i }
    }
  }
  return null
}

const operationParameters = (signature) => {
  const names = new Set()
  const eq = signature.indexOf('=')
  const declarationHead = (eq >= 0 ? signature.slice(0, eq) : signature)
    .replace(/^\s*let\s+(?:rec\s+)?(?:(?:private|internal|inline|mutable)\s+)*[A-Za-z_]\w*/, '')
    .replace(/\s+/g, ' ')

  for (const match of declarationHead.matchAll(/\(\s*([A-Za-z_]\w*)\b([^)]*)\)/g)) {
    const annotation = /:\s*(.*)$/.exec(match[2])?.[1] ?? ''
    if (!annotation || annotation.includes('->')) names.add(match[1])
  }

  const bareHead = declarationHead.replace(/\([^)]*\)/g, ' ').split(/\s*:\s*/, 1)[0]
  for (const match of bareHead.matchAll(/(?:^|\s)([a-z_][A-Za-z0-9_']*)\b(?=\s|:|$)/g)) {
    names.add(match[1])
  }
  return [...names]
}

const bodyEnd = (lines, start, declarationIndent) => {
  for (let i = start + 1; i < lines.length; i++) {
    if (lines[i].trim() === '') continue
    const match = DECLARATION.exec(lines[i])
    if (match && match[1].length <= declarationIndent) return i
    const member = /^(\s*)(?:(?:static|abstract|override|default)\s+)*member\b/.exec(lines[i])
    if (member && member[1].length <= declarationIndent) return i
    if (/^\s*(?:type|and|module|namespace)\s+/.test(lines[i])) {
      const indent = lines[i].length - lines[i].trimStart().length
      if (indent <= declarationIndent) return i
    }
  }
  return lines.length
}

const precedingDocBlock = (lines, start) => {
  const docs = []
  for (let i = start - 1; i >= 0; i--) {
    const trimmed = lines[i].trim()
    if (trimmed === '') break
    if (!trimmed.startsWith('//')) break
    docs.unshift(trimmed)
  }
  return docs.join('\n')
}

const directInvocations = (bodyLines, parameter) => {
  const escaped = parameter.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const direct = new RegExp(`^${escaped}\\b(.*)$`)
  const results = []

  for (let i = 0; i < bodyLines.length; i++) {
    let code = bodyLines[i].replace(/\/\/.*$/, '').trim()
    if (!code) continue
    const awaited = /^(?:return!|do!|yield!|match!)\s+/.test(code) || /^(?:let!|use!)\s+[^=]+\s*=/.test(code)

    // Binding/control prefixes do not distinguish invocation semantics.
    // Removing them also ensures a callback merely forwarded as an argument
    // (`register callback scope`) is not mistaken for a direct call.
    if (code.includes('->')) code = code.slice(code.lastIndexOf('->') + 2).trim()
    code = code.replace(/^else\s+/, '')
    code = code.replace(/^if\b.*\bthen\s+/, '')
    code = code.replace(/^(?:return!|do!|yield!|match!|return|yield)\s+/, '')
    code = code.replace(/^(?:let!|let|use!|use)\s+[A-Za-z_]\w*(?:\s*:[^=]+)?\s*=\s*/, '')
    code = code.replace(/^(?:return!|do!|yield!|match!|return|yield)\s+/, '')

    const match = direct.exec(code)
    const suffix = match?.[1].trim().replace(/\s+/g, ' ')
    // A trace-policy record dependency selected through `.Member` / `?.Member`
    // is a port value, not invocation of the dependency binder itself.
    if (suffix && /^(?:[A-Za-z0-9_'"([{])/.test(suffix) && !suffix.startsWith('|>')) {
      results.push({ line: i, suffix, awaited })
    }
  }
  return results
}

const sourceOffsets = (text) => {
  const offsets = [0]
  for (let index = 0; index < text.length; index++) if (text[index] === '\n') offsets.push(index + 1)
  return offsets
}

const position = (line, column) => line * 1_000_000 + column

const applicationSource = (text, offsets, application) => {
  const start = (offsets[application.startLine - 1] ?? 0) + application.startColumn
  const end = (offsets[application.endLine - 1] ?? offsets[application.startLine - 1] ?? 0) + application.endColumn
  return text.slice(start, end).trim().replace(/\s+/g, ' ')
}

const resolvedParameterInvocations = (text, offsets, file, bodyLines, bodyStartLine, bodyEndLine, parameter, applicationUses) => {
  return applicationUses
    .filter((application) => application.consumerPath === file)
    .filter((application) => application.startLine >= bodyStartLine && application.startLine <= bodyEndLine)
    .filter((application) => application.resolvedTarget === parameter && application.declarationPaths?.includes(file))
    .filter((application) => /->/.test(application.inferredType ?? ''))
    .map((application) => {
      const line = application.startLine - bodyStartLine
      const code = bodyLines[line]?.replace(/\/\/.*$/, '').trim() ?? ''
      return {
        line,
        suffix: applicationSource(text, offsets, application),
        awaited: /^(?:return!|do!|yield!|match!)\s+/.test(code) || /^(?:let!|use!)\s+[^=]+\s*=/.test(code),
        application,
      }
    })
}

const rangeContains = (range, application) =>
  position(range.startLine, range.startColumn) <= position(application.startLine, application.startColumn)
  && position(application.endLine, application.endColumn) <= position(range.endLine, range.endColumn)

const executableDeclarationCalls = (calls, file, declarationLine, flowEvidence) => {
  const closures = [
    ...(flowEvidence?.lambdaExpressions?.filter((lambda) => lambda.consumerPath === file)
      .map((lambda) => ({ ...lambda, invokedBy: lambda.invokedBy ?? [], range: lambda })) ?? []),
    ...(flowEvidence?.localFunctionBindings?.filter((binding) =>
      binding.consumerPath === file && binding.startLine !== declarationLine)
      .map((binding) => ({ ...binding, invokedBy: binding.invokedBy ?? [], range: binding })) ?? []),
  ].sort((left, right) =>
    (position(left.range.endLine, left.range.endColumn) - position(left.range.startLine, left.range.startColumn))
    - (position(right.range.endLine, right.range.endColumn) - position(right.range.startLine, right.range.startColumn)))

  return calls.flatMap((call) => {
    const invokedWrapper = closures.find((closure) => closure.invokedBy.length > 0
      && closure.invokedBy.some((invocation) => rangeContains(invocation, call.application))
      && rangeContains({
        startLine: call.application.targetStartLine,
        startColumn: call.application.targetStartColumn,
        endLine: call.application.targetEndLine,
        endColumn: call.application.targetEndColumn,
      }, closure.range))
    if (invokedWrapper) {
      return invokedWrapper.invokedBy.map((invocation) => ({ ...call, pathApplication: invocation }))
    }

    const closure = closures.find((candidate) => rangeContains(candidate.body, call.application))
    if (!closure) return [{ ...call, pathApplication: call.application }]
    return closure.invokedBy.map((invocation) => ({ ...call, pathApplication: invocation }))
  })
}

const callsCanSharePath = (left, right, file, flowEvidence) => {
  const branchGroups = [
    ...(flowEvidence?.matchExpressions?.filter((match) => match.consumerPath === file).map((match) => match.clauses) ?? []),
    ...(flowEvidence?.conditionalExpressions?.filter((conditional) => conditional.consumerPath === file)
      .map((conditional) => conditional.branches) ?? []),
    ...(flowEvidence?.tryExpressions?.filter((expression) => expression.consumerPath === file)
      .map((expression) => expression.continuations) ?? []),
  ]
  return !branchGroups.some((branches) => {
    const leftBranch = branches.findIndex((branch) => rangeContains(branch, left.pathApplication ?? left.application))
    const rightBranch = branches.findIndex((branch) => rangeContains(branch, right.pathApplication ?? right.application))
    return leftBranch >= 0 && rightBranch >= 0 && leftBranch !== rightBranch
  })
}

const isInsideResolvedLoop = (call, file, flowEvidence) =>
  flowEvidence?.loopExpressions?.some((loop) => loop.consumerPath === file
    && (rangeContains(loop.body, call.application) || rangeContains(loop.body, call.pathApplication ?? call.application))) ?? false

const maximumPathCalls = (calls, file, flowEvidence) => {
  let best = []
  const visit = (index, selected) => {
    if (selected.length + calls.length - index <= best.length) return
    if (index === calls.length) {
      best = selected
      return
    }
    const call = calls[index]
    if (selected.every((candidate) => callsCanSharePath(candidate, call, file, flowEvidence))) {
      visit(index + 1, [...selected, call])
    }
    visit(index + 1, selected)
  }
  visit(0, [])
  return best
}

const hasCompatiblePair = (calls, file, flowEvidence, predicate) => {
  for (let left = 0; left < calls.length; left++) {
    for (let right = left + 1; right < calls.length; right++) {
      if (callsCanSharePath(calls[left], calls[right], file, flowEvidence) && predicate(calls[left], calls[right])) return true
    }
  }
  return false
}

const dynamicFunctionCollections = (text, file) => {
  const bindings = []
  const collection = /\b(?:ResizeArray|List|Dictionary|IList|ICollection)\s*</g

  for (const line of text.split('\n')) {
    const declaration = /^\s*let\s+(?:(?:private|internal|mutable)\s+)*([A-Za-z_]\w*)\b/.exec(line)
    if (!declaration) continue

    collection.lastIndex = 0
    const match = collection.exec(line)
    if (!match) continue

    const typeStart = collection.lastIndex
    let depth = 1
    let typeEnd = typeStart
    while (typeEnd < line.length && depth > 0) {
      if (line[typeEnd] === '<') depth++
      else if (line[typeEnd] === '>' && line[typeEnd - 1] !== '-') depth--
      typeEnd++
    }
    if (depth !== 0) continue

    const elementType = line.slice(typeStart, typeEnd - 1).trim()
    if (elementType.includes('->')) bindings.push(declaration[1])
  }

  return bindings.filter((binding) => {
    const escaped = binding.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    if (!new RegExp(`\\b${escaped}\\s*\\.\\s*(?:Add|Insert|Register)\\b`).test(text)) return false
    const contract = PHYSICAL_LISTENER_CONTRACTS.find((candidate) =>
      candidate.kind === 'collection' &&
      candidate.file === file &&
      candidate.binding === binding &&
      candidate.sites.every((site) => text.includes(site)) &&
      validateAuthority(physicalListenerContractDocs(candidate)).length === 0)
    return contract === undefined
  })
}

const isInsideRepeatingLoop = (bodyLines, at) => {
  const callLine = bodyLines[at] ?? ''
  if (/\b(?:while|for)\b.*\bdo\b.*$/.test(callLine)) return true

  const callIndent = callLine.length - callLine.trimStart().length
  for (let i = at - 1; i >= 0; i--) {
    const line = bodyLines[i]
    if (line.trim() === '' || /^\s*\/\//.test(line)) continue
    const indent = line.length - line.trimStart().length
    if (indent >= callIndent) continue
    return /^\s*(?:while\b.*\bdo\b|for\b.*\bdo\b)/.test(line)
  }
  return false
}

const recursivelyReinvokes = (declarationLine, functionName, bodyLines, calls) =>
  /^\s*let\s+rec\b/.test(declarationLine) &&
  calls.length > 0 &&
  directInvocations(bodyLines, functionName).length > 0

const caseBranchAt = (bodyLines, at) => {
  const callIndent = bodyLines[at].length - bodyLines[at].trimStart().length
  for (let i = at; i >= 0; i--) {
    const line = bodyLines[i]
    const indent = line.length - line.trimStart().length
    if (/^\s*\|[^>]*->/.test(line) && indent <= callIndent) return i
    if (/^\s*match!?\b/.test(line) && indent < callIndent) return null
  }
  return null
}

const hasPossibleRepeatedCall = (bodyLines, calls) => {
  if (calls.length < 2) return false
  const branches = calls.map(({ line }) => caseBranchAt(bodyLines, line))
  return !(branches.every((branch) => branch !== null) && new Set(branches).size === branches.length)
}

const repeatedAttemptShape = (calls) => {
  const shapes = new Set()
  for (const call of calls) {
    if (shapes.has(call.suffix)) return true
    shapes.add(call.suffix)
  }
  return false
}

/** @returns {{file:string,line:number,kind:string,message:string}[]} */
export const scanSemanticDecorators = (text, file = '<synthetic>', applicationUses, flowEvidence) => {
  const violations = []
  const lines = text.split('\n')
  const offsets = applicationUses === undefined ? undefined : sourceOffsets(text)

  for (const pattern of GENERIC_FRAMEWORK_PATTERNS) {
    if (pattern.test(text)) violations.push({ file, line: 1, kind: 'generic-framework', message: `generic or dynamic decorator framework: ${pattern}` })
  }
  if (dynamicFunctionCollections(text, file).length > 0) {
    violations.push({ file, line: 1, kind: 'generic-framework', message: 'dynamically mutated or invoked collection of function handlers' })
  }

  for (let start = 0; start < lines.length; start++) {
    const declaration = DECLARATION.exec(lines[start])
    if (!declaration) continue
    const signature = declarationBlock(lines, start)
    if (signature === null) continue
    const parameters = operationParameters(signature.text)
    if (parameters.length === 0) continue

    const end = bodyEnd(lines, signature.end, declaration[1].length)
    const body = lines.slice(signature.end + 1, end)
    for (const parameter of parameters) {
      const calls = applicationUses === undefined
        ? directInvocations(body, parameter)
        : resolvedParameterInvocations(text, offsets, file, body, signature.end + 2, end, parameter, applicationUses)
      const declarationCalls = applicationUses === undefined ? calls : executableDeclarationCalls(calls, file, start + 1, flowEvidence)
      const maximumCalls = applicationUses === undefined ? calls : maximumPathCalls(declarationCalls, file, flowEvidence)
      const physicalListener = resolvedPhysicalListenerContract(file, declaration[2], parameter, body, declarationCalls)
      const traceContract = resolvedTraceChangeContract(file, declaration[2], parameter, body, declarationCalls)
      const repeatedCalls = applicationUses === undefined
        ? hasPossibleRepeatedCall(body, calls)
        : maximumCalls.length > 1
      const looped = applicationUses === undefined
        ? declarationCalls.some(({ line }) => isInsideRepeatingLoop(body, line))
        : declarationCalls.some((call) => isInsideResolvedLoop(call, file, flowEvidence))
      const recursive = recursivelyReinvokes(lines[start], declaration[2], body, declarationCalls)
      const traceChanging =
        repeatedCalls ||
        looped ||
        recursive
      if (!traceChanging) continue

      if (physicalListener || traceContract) continue

      const docs = precedingDocBlock(lines, start)
      const missing = validateAuthority(docs)
      const retrying = looped || recursive || (applicationUses === undefined
        ? repeatedCalls && repeatedAttemptShape(calls)
        : hasCompatiblePair(declarationCalls, file, flowEvidence, (left, right) => left.suffix === right.suffix))
      const distinctSequence = applicationUses === undefined
        ? repeatedCalls && new Set(calls.map(({ suffix }) => suffix)).size > 1
        : hasCompatiblePair(declarationCalls, file, flowEvidence, (left, right) => left.suffix !== right.suffix)
      if (retrying && !/semantic-decorator-retry-bound:\s*[1-9]\d*\s*$/im.test(docs)) missing.push('finite retry bound')
      if (distinctSequence) {
        const invocationBound = Number(annotationValue(docs, 'invocation-bound'))
        if (!Number.isInteger(invocationBound) || invocationBound < maximumCalls.length) {
          missing.push(`invocation bound covering ${maximumCalls.length} calls`)
        }
      }
      if (missing.length > 0) {
        violations.push({
          file,
          line: start + 1,
          kind: 'unowned-trace-change',
          message: `${declaration[2]} can re-invoke passed operation '${parameter}' through repetition, recursion, or a loop; missing ${missing.join(', ')}`,
        })
      }
    }
  }

  return violations
}

/** @param {{file:string,text:string}[]} entries */
export const scanEntries = (entries, applicationUses, flowEvidence) =>
  entries.flatMap((entry) => scanSemanticDecorators(entry.text, entry.file, applicationUses, flowEvidence))

export const scanSemanticDecoratorEvidence = ({
  projectFile = join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj'),
  productionRoot = join(ROOT, PRODUCTION_ROOT),
  scratchRoot = FCS_SCRATCH,
  resultPath = FCS_RESULT,
  applicationConsumerPaths,
} = {}) => scanProjectSymbolUses({
  projectFile,
  productionRoot,
  scratchRoot,
  resultPath,
  applicationConsumerPaths,
})

export const scanSemanticDecoratorApplications = (options) =>
  scanSemanticDecoratorEvidence(options).applicationUses

export const scanRepo = (root = ROOT, compilerEvidence) => {
  const base = resolve(root, PRODUCTION_ROOT)
  const entries = walk(base, ['.fs']).map((file) => ({
    file: file.replace(`${resolve(root)}/`, '').replace(/\\/g, '/'),
    text: readFileSync(file, 'utf8'),
  }))
  const evidence = compilerEvidence ?? scanSemanticDecoratorEvidence({
    projectFile: join(root, 'src/Wanxiangshu/Wanxiangshu.fsproj'),
    productionRoot: base,
    scratchRoot: join(root, '.fable-build/semantic-decorator-fcs'),
    resultPath: join(root, '.fable-build/semantic-decorator-fcs/symbol-uses.json'),
  })
  const applicationUses = Array.isArray(evidence) ? evidence : evidence.applicationUses
  const flowEvidence = Array.isArray(evidence) ? undefined : evidence
  return scanEntries(entries, applicationUses, flowEvidence)
}

const runCli = () => {
  const violations = scanRepo()
  if (violations.length > 0) {
    console.error(`semantic-decorator-invariant: ${violations.length} violation(s)`)
    for (const violation of violations) console.error(`  ${violation.file}:${violation.line} [${violation.kind}] ${violation.message}`)
    process.exit(1)
  }
  console.log('semantic-decorator-invariant: OK — no anonymous trace-changing composition')
}

const isMain = process.argv[1] !== undefined && resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])
if (isMain) runCli()
