#!/usr/bin/env node
// DSL ownership gate (VERIFY-001 layer 0).
// Checks that Program/Domain files do not contain forbidden control-flow escape hatches.
//
// Modes:
//   node scripts/checks/dsl-ownership.mjs                  fail-closed on any violation
//   node scripts/checks/dsl-ownership.mjs --threshold=N    fail-closed on violations > N
//
// CI calls with --threshold to freeze the current backlog while preventing new violations.

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'
import { scanProjectSymbolUses } from './owner-dependencies.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'
const FCS_SCRATCH = join('.fable-build', 'dsl-ownership-fcs')
const FCS_RESULT = join(FCS_SCRATCH, 'symbol-uses.json')
const norm = (p) => p.replace(/\\/g, '/')

/**
 * External-protocol interpreters allowed by FLOW-006 (JSON/TOML/Host-wire
 * codec/parser/interpreter). Boundary is path+semantic, not a blanket `Interpreter`
 * suffix exemption: a `*Interpreter` module is tolerated only when it interprets an
 * external data format, signalled by a Codec/Parser/Wire path segment or suffix.
 */
export const isExternalProtocolPath = (file) => {
  const rel = norm(String(file))
  return (
    /\/Codec\//.test(rel) ||
    /\/Parser\//.test(rel) ||
    /\/Wire\//.test(rel) ||
    /\.(?:Codec|Parser|Wire)\.fs$/.test(rel) ||
    /\.(?:Codec|Parser|Wire)\.[A-Za-z]/.test(rel)
  )
}

/** Process/ is the PTY/process physical layer; its mutable fields and wire
 *  command DUs are runtime resources / external protocol messages, not business
 *  program counters. */
export const isProcessPhysicalPath = (file) => {
  const rel = norm(String(file))
  return rel.includes('/Process/')
}

/** Physical/external adapter and boundary layers. */
export const isInfrastructurePath = (file) => {
  const rel = norm(String(file))
  return (
    rel.includes('/Infrastructure/') ||
    rel.includes('/OpenCode/') ||
    rel.includes('/Process/') ||
    rel.includes('/Git/') ||
    rel.includes('/Host/') ||
    rel.includes('/Composition/') ||
    rel.includes('/Resources/')
  )
}

/** Pty/Node protocol command types are external protocol messages (FLOW-006),
 *  not a business Command/Reply second runtime. */
export const isProcessCommandPath = (file) => {
  const rel = norm(String(file))
  return /\/Process\/(?:ProcessRequest|PtyTypes)\.fs$/.test(rel)
}

/** DSL-MUTABLE declaration categories (PR 9 A). A `let mutable` is legal only
 *  when the immediately preceding 1-2 source lines carry a
 *  `// DSL-MUTABLE: <category>` annotation naming the physical/algorithmic
 *  reason. Directory membership is no longer an exemption: Domain, Session,
 *  Application, Process and Kernel all require the declaration. */
export const DSL_MUTABLE_CATEGORIES = [
  'resource',
  'algorithm-scratch',
  'single-flight',
  'buffer',
  'subscription',
  'cancellation',
]

/** True when the given source line is a `// DSL-MUTABLE: <category>` declaration. */
export const isDslMutableDeclaration = (line) => {
  const m = /\/\/\s*DSL-MUTABLE:\s*(\w[\w-]*)/.exec(line)
  return m !== null && DSL_MUTABLE_CATEGORIES.includes(m[1])
}

/**
 * Any production file may legalize a `let mutable` — but only via a precise
 * `// DSL-MUTABLE: <category>` declaration on the immediately preceding 1-2
 * lines (checked by hasDslMutableDeclaration). Directory membership is no
 * longer an exemption; an unannotated `let mutable` fires fail-closed in every
 * production file.
 */
export const isMutableDeclarationAllowed = (file) => true

/** True when a `let mutable` at 1-based line `line` (index i in lines) is
 *  preceded within the prior 1-2 lines by a DSL-MUTABLE declaration. */
export const hasDslMutableDeclaration = (lines, i) => {
  return isDslMutableDeclaration(lines[i - 1] ?? '') || isDslMutableDeclaration(lines[i - 2] ?? '')
}

/**
 * ControlState class is forbidden as a long-lived runtime tag: "当前执行到哪一步"
 * belongs to the CE call stack, not to a stored field. An explicit exemption
 * set may register boundary/interpreter types that legitimately model state.
 */
export const CONTROL_STATE_EXEMPT = new Set([])


/** Host-facing Session adapters may open OpenCode/Process/Infrastructure.
 *
 * TASK.md §4 (PR 0 rotation): basename allowlist is too wide — any file named
 * Runtime.fs / Host.fs / Workflow.fs / Types.fs would inherit physical-boundary
 * power. Authorization is now exact-path: a new Host adapter must be named
 * here explicitly, and a rename/move turns RED until the entry is updated.
 * The exact paths below are the files that today own an explicit
 * Infrastructure|OpenCode|Process boundary. Surface adapters are registered
 * individually; a basename never grants authority and a rename/move turns RED
 * until its owner path is reviewed.
 */
export const HOST_BOUNDARY_OPEN_PATHS = new Set([
  'src/Wanxiangshu/Change/Host/Host.fs',
  'src/Wanxiangshu/Change/Host/ReviewRunner.fs',
  'src/Wanxiangshu/Change/Host/SessionDirectories.fs',
  'src/Wanxiangshu/Change/Host/Types.fs',
  // JS-native surfaces that are themselves physical adapters. These are
  // explicit paths, never basename authority: a neighboring Surface.fs has
  // no physical-boundary exemption unless it is registered here.
  'src/Wanxiangshu/Change/Surface.fs',
  'src/Wanxiangshu/Change/Host/Surface.fs',
  'src/Wanxiangshu/Composition/Turn/Workflow.fs',
  'src/Wanxiangshu/Context/Companion/Blogger/BloggerCrashRecovery.fs',
  'src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs',
  'src/Wanxiangshu/Context/Companion/Blogger/Runtime/ParkedTransform.fs',
  'src/Wanxiangshu/Context/Companion/Host.fs',
  'src/Wanxiangshu/Context/Companion/HostBlogger.fs',
  'src/Wanxiangshu/Context/Companion/RuntimeSurface.fs',
  'src/Wanxiangshu/Context/Companion/Transform.fs',
  'src/Wanxiangshu/Context/Companion/CompressionSurface.fs',
  'src/Wanxiangshu/Context/Prefix/Wire.fs',
  'src/Wanxiangshu/Context/Trace/Capture.fs',
  'src/Wanxiangshu/Context/Trace/SemanticTraceSurface.fs',
  'src/Wanxiangshu/Context/Trace/TerminalReporter.fs',
  'src/Wanxiangshu/Enforcer/Continuation.fs',
  'src/Wanxiangshu/Enforcer/Cycle/BloggerProbe.fs',
  'src/Wanxiangshu/Enforcer/Cycle/Decode.fs',
  'src/Wanxiangshu/Enforcer/Guidance/DeliveryProjection.fs',
  'src/Wanxiangshu/Enforcer/Guidance/DeliverySurface.fs',
  'src/Wanxiangshu/Enforcer/Guidance/Tip.fs',
  'src/Wanxiangshu/Enforcer/Guidance/TipSurface.fs',
  'src/Wanxiangshu/Enforcer/BlogSurface.fs',
  'src/Wanxiangshu/Enforcer/Host.fs',
  'src/Wanxiangshu/Enforcer/Repair.fs',
  'src/Wanxiangshu/Execution/Delegation/ChildRecoveryWorkflow.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/Runtime.fs',
  'src/Wanxiangshu/Execution/Delegation/Handle/CompletionCodec.fs',
  'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fs',
  'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs',
  'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Workflow.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs',
  'src/Wanxiangshu/Execution/Session/Attachment/SatelliteRuntime.fs',
  'src/Wanxiangshu/Execution/Session/LoopDetectorSurface.fs',
  'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs',
  'src/Wanxiangshu/Execution/Session/SessionStartedAtLedger.fs',
  'src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs',
  'src/Wanxiangshu/Interaction/Dispatch/Dispatcher.fs',
  'src/Wanxiangshu/Interaction/Dispatch/Ingress.fs',
  'src/Wanxiangshu/Interaction/Dispatch/Recovery.fs',
  'src/Wanxiangshu/Interaction/Dispatch/RecoverySurface.fs',
  'src/Wanxiangshu/Interaction/Dispatch/DispatchSurface.fs',
  'src/Wanxiangshu/Interaction/Dispatch/Send.fs',
  'src/Wanxiangshu/Interaction/Repair/CompletedTurn.fs',
  'src/Wanxiangshu/Interaction/Repair/CompletedTurnSurface.fs',
  'src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs',
  'src/Wanxiangshu/Mission/Manager/Background.fs',
  'src/Wanxiangshu/Mission/Manager/Idle.fs',
  'src/Wanxiangshu/Mission/Manager/JobHandoff.fs',
  'src/Wanxiangshu/Mission/Manager/Workflow.fs',
  'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoLocality.fs',
  'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoLocalitySurface.fs',
  'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs',
  'src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembraneSurface.fs',
  'src/Wanxiangshu/Mission/Review/Judgement/Workflow.fs',
  'src/Wanxiangshu/Mission/Review/Assurance/Surface.fs',
  'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs',
  'src/Wanxiangshu/Participant/Provider/Projection/Surface.fs',
  'src/Wanxiangshu/Participant/Provider/LanguageSurface.fs',
  'src/Wanxiangshu/Persistence/Journal/FactCodec.fs',
  'src/Wanxiangshu/Persistence/Journal/Surface.fs',
  'src/Wanxiangshu/Process/Surface.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperRuntime.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/BookkeeperSurface.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/FetchSurface.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/Index.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/Lifecycle.fs',
  'src/Wanxiangshu/Repository/Programming/Js/RuntimeSurface.fs',
  'src/Wanxiangshu/Strength/OpenCode/Speculate.fs',
  'src/Wanxiangshu/Strength/Replica/Runtime.fs',
  'src/Wanxiangshu/Strength/Replica/Transform.fs',
  'src/Wanxiangshu/Strength/Surface.fs',
  'src/Wanxiangshu/Verification/TemporalSurface.fs',
  'src/Wanxiangshu/Strength/TurnEvidence.fs',
])

export const isHostBoundaryOpenPath = (file) => {
  const normalized = norm(String(file))
  return [...HOST_BOUNDARY_OPEN_PATHS].some((path) => normalized === path || normalized.endsWith(`/${path}`))
}

export const FORBIDDEN = [
  // Mutable is now declaration-gated: a `let mutable` fires unless the prior
  // 1-2 lines carry a precise `// DSL-MUTABLE: <category>` declaration. Any
  // production file may use the declaration; there is no path whitelist. The
  // preceding-line check is applied in scanText.
  { gate: 'mutable', pattern: /\blet mutable\b/, label: 'let mutable without DSL-MUTABLE declaration' },
  { gate: 'flow-lift', pattern: /\bFlow\.(?:lift|create)\b/, label: 'Flow.lift / Flow.create' },
  {
    // FLOW-002/FLOW-006 second-runtime forms. Catches realistic bypass shapes:
    //   type|and + optional private/internal/public modifier + *Command|*Reply
    //   (with optional generic params) or *Program (generic only, so a plain
    //   `type OrchestratorProgramDeps =` record stays clean), a `| Step of` /
    //   `| Suspend of` union node, and a ProtocolMismatch compensation token.
    gate: 'second-runtime-protocol',
    pattern:
      /\b(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(?:\w*(?:Command|Reply)(?:<[^=>]*>)?|(?:\w*Program)<[^=>]*>)\s*=|\|\s*(?:Step|Suspend)\s+of\b|\bProtocolMismatch\b/,
    label: 'Command/Reply/Program AST, Step/Suspend node, or ProtocolMismatch compensation',
    skipIf: isProcessCommandPath,
  },
  {
    // FLOW-006 internal business Interpreter. Allows private/internal/top-level
    // forms, but never a legitimate external-protocol interpreter (FLOW-006:
    // JSON/TOML/Host-wire codec/parser/interpreter are not second-runtime).
    gate: 'business-interpreter',
    pattern: /\bmodule\s+(?:private\s+|internal\s+)?(?:\w+\.)*\w*Interpreter\s*=/,
    label: 'internal business Interpreter module',
    skipIf: isExternalProtocolPath,
  },
  {
    gate: 'infrastructure-leak',
    // Matches `open Wanxiangshu.Infrastructure|OpenCode|Process` (explicit
    // import) OR a qualified member reference (`Wanxiangshu.Process.ProcessRunner.run`)
    // so the FQN lexical-escape loop is closed. A bare namespace declaration
    // (`namespace Wanxiangshu.OpenCode`) matches neither branch, so it is not
    // a false-positive leak. Authorized Infrastructure/Process/Host boundary
    // paths stay clean via skipIf.
    pattern:
      /\bopen\s+Wanxiangshu\.(?:Infrastructure|OpenCode|Process)\b|\bWanxiangshu\.(?:Infrastructure|OpenCode|Process)\.\w/,
    label: 'infrastructure namespace open or qualified reference',
    skipIf: (file) =>
      isHostBoundaryOpenPath(file) || isProcessPhysicalPath(file) || isInfrastructurePath(file),
  },
  {
    gate: 'program-counter',
    // PR 9 C: the lexical list is a hard fail, and an explicit
    // `/// DSL-class: ControlState` annotation is forbidden (scanText). NOT
    // IMPLEMENTED (honest): automatic detection of a long-lived record whose
    // DU-typed field lacks a DSL-class — that needs full type resolution, so
    // the declaration requirement + ControlState hard fail are the current
    // minimal reliable enforcement. See docs/how/dsl-structured-program.md.
    pattern:
      /\b(?:Dirty|Running|RepairSpent|ReactivatedAfterSeal|injectRepair|commitUnknown|abandonThenCatchUp|forceConfirmedReviewer|isContinuation|publishToMailbox|openReviewBarrier|CurrentStage|CurrentMode|RuntimeCondition|LifecyclePosition|InFlightFlag|ParkedMarker)\b/,
    label: 'program counter field/parameter',
  },
  {
    gate: 'behaviour-bool',
    // Domain evidence DUs / pure queries ending in Pending|Spent|Phase are allowlisted.
    // Physical/algorithm names that merely contain a suffix (EstimatedRunningSeconds,
    // RecoveryStageProbe type alias, Already* fold rejections, Pending* durable fields)
    // are allowlisted. Residual pattern still catches true stage latches
    // (HasPendingCompletion, CompactionProbePending, isRunning-style counters).
    // Verb-named functions (`let clearStalePending agentId =`) are skipped in scanText.
    pattern:
      /\b(?!PerfectPending\b|isPerfectPending\b|StillPending\b|ConflictPending\b|recoveryBudgetSpent\b|tryTakePending\b|failPending\b|takePending\b|abortPending\b|hasPendingActivation\b|EstimatedRunningSeconds\b|RecoveryStageProbe\b|AlreadyOutstanding\b|AlreadyCounted\b|AlreadyCompleted\b|AlreadyAbandoned\b|AlreadyObserved\b|AlreadyExhausted\b|AlreadyInProgress\b|AlreadyLinkedToOther\b|OpeningAlreadyCaptured\b|TerminalAlreadyCaptured\b|LifeAlreadyOpen\b|FinalityAlreadyActive\b|CompactionAlreadyReanchored\b|PendingConfirmation\b|PendingChallenge\b|PendingClaimUnknown\b|PendingClaims\b|PendingOffer\b|PendingReviewSeals\b|PendingRuns\b|PendingSeal\b|PendingRunCount\b|NoPendingSeal\b)[a-zA-Z]+(?:Stage|Phase|Next|Running|Pending|Spent|Already|Should)\b|\b(HasPendingCompletion|LastCompletionStatus|bloggerTask|bloggerFailed)\b/,
    label: 'behaviour bool or stage field',
    skipIf: isProcessPhysicalPath,
  },
  {
    // PR 9 item 4: multi-bool loop is a FILE-level pattern (two mutable false
    // booleans + a while loop), enforced in scanFiles. Registered here so
    // GATE_NAMES / counts stay authoritative.
    gate: 'bool-loop',
    pattern: /$^/,
    label: 'multi mutable-false booleans with a while loop',
  },
  {
    // PR 9 item 6: duplicate case sets across DUs are a FILE-level pattern
    // (two DUs with the exact same case-name set), enforced in scanFiles.
    // Registered here so GATE_NAMES / counts stay authoritative.
    gate: 'dup-cases',
    pattern: /$^/,
    label: 'duplicate DU case-name set',
  },
  {
    // A whole-source structural check in scanText. Kept in this list so the
    // gate inventory and ratchet counts remain authoritative.
    gate: 'registry-joint-branch',
    pattern: /$^/,
    label: 'joint direct registry probe selects an effect branch',
  },
]

// PR 9 item 6 exemptions — each entry names the FILE:DU whose case set may
// legitimately repeat another DU's, with the reason the pair is not a
// duplicate knowledge representation:
//   ChildRecovery.fs:ChildResolution    pure Decision layer (no payloads),
//                                       1:1 to ChildRecoveryResult after effects
//   ManagedAgent.fs:ManagedAgentParseError  Infrastructure boundary; one-way
//                                       from AgentNameRejection (ManagedAgent.fs:66)
//   MagicTodo.fs:ProcessReviewVerdict   pure Decision façade; 1:1 ofGuard map
//                                       from ReviewGuardVerdict (Fact.fs)
export const DUP_CASES_EXEMPT = new Set([
  'ChildRecovery.fs:ChildResolution',
  'ManagedAgent.fs:ManagedAgentParseError',
  // 1:1 Decision façade over ReviewGuardVerdict via ProcessReviewVerdict.ofGuard
  'MagicTodo.fs:ProcessReviewVerdict',
  'Model.fs:ProcessReviewVerdict',
])

export const GATE_NAMES = FORBIDDEN.map((item) => item.gate)

export const isProgramFile = (path) => {
  const normalized = norm(String(path))
  return (
    normalized.endsWith('.fs') &&
    (normalized.startsWith(`${PRODUCTION_ROOT}/`) || normalized.includes(`/${PRODUCTION_ROOT}/`))
  )
}

/**
 * DSL-state-combination: the categories that legalize a record whose fields
 * form >= 2 independent state axes (DSL-005). `domain` = a true domain
 * combination; `physical` = a physical resource combination. Absence means the
 * author has not classified the product, so it fires fail-closed.
 */
export const STATE_COMBINATION_CATEGORIES = ['domain', 'physical']

/**
 * ControlState must carry a machine-checkable reason for why ordinary CE
 * constructs cannot express the same flow. A bare `DSL-class: ControlState`
 * remains a program counter unless the reason line declares:
 *
 *   - `ce-equivalent=none`  — no ordinary CE expression exists
 *   - `blockers=<list>`     — names every rejected expression means
 *                             (function-call, match!, return!, resource-scope,
 *                             waiter, bounded-recursion)
 *
 * `evidence=<...>` is diagnostic and not mechanically constrained.
 */
const CONTROL_STATE_REASON_REQUIRED = ['ce-equivalent=none']
const CONTROL_STATE_REASON_BLOCKERS = [
  'function-call',
  'match!',
  'return!',
  'resource-scope',
  'waiter',
  'bounded-recursion',
]

/** True when `lines[index]` and a valid reason share one contiguous doc block.
 * A nearby reason belonging to another declaration must not legalize ControlState.
 */
export const hasValidControlStateReason = (lines, index) => {
  const isDocLine = (line) => /^\s*\/\/\//.test(line)
  if (!isDocLine(lines[index] ?? '')) return false

  let from = index
  while (from > 0 && isDocLine(lines[from - 1])) from--
  let to = index
  while (to + 1 < lines.length && isDocLine(lines[to + 1])) to++

  for (let j = from; j <= to; j++) {
    const m = /\/\/\/\s*DSL-control-state-reason:\s*(.+)/.exec(lines[j])
    if (!m) continue
    const reason = m[1]
    if (!CONTROL_STATE_REASON_REQUIRED.every((r) => reason.includes(r))) return false
    const blockerMatch = /blockers=([^;]+)/.exec(reason)
    if (!blockerMatch) return false
    const blockers = blockerMatch[1].split(',').map((b) => b.trim())
    if (!CONTROL_STATE_REASON_BLOCKERS.every((b) => blockers.includes(b))) return false
    return true
  }
  return false
}

/**
 * state-product (DSL-005): a record whose fields form >= 2 independent state
 * axes (locally-defined DUs with >= 2 cases, `option`, or `bool`) is an
 * unclassified orthogonal state product unless it carries a
 * `/// DSL-state-combination: domain|physical` annotation. Field-name
 * independent by construction — it parses structure, not blacklists.
 */
export function scanStateProducts(text, file = '<synthetic>') {
  const lines = text.split('\n')
  const violations = []

  // Collect locally-defined DUs (type/and declarations whose body opens a case list).
  const definedDus = new Set()
  const duDecl = /^\s*(?:type|and)\s+(\w+)\s*=\s*$/
  const caseLine = /^\s*\| /
  let cur = null
  for (const line of lines) {
    const dm = duDecl.exec(line)
    if (dm) {
      // A new type/and declaration flushes the previous DU candidate before the
      // new one starts (else `type Availability` would be shadowed by the next
      // `type` and never registered).
      if (cur && cur.isDu) definedDus.add(cur.name)
      cur = { name: dm[1], isDu: false }
      continue
    }
    if (!cur) continue
    if (caseLine.test(line)) {
      cur.isDu = true
      continue
    }
    if (line.trim() !== '') {
      if (cur.isDu) definedDus.add(cur.name)
      cur = null
    }
  }

  // Walk each record definition and classify its state-typed fields. Records
  // may put `{` on the declaration line or on the following line; both forms
  // are common in the production tree and must share the same fail-closed path.
  const declaration = /^\s*(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(\w+)\s*=\s*(.*)$/
  const bodyOpen = /^\s*\{\s*$/
  const recordEnd = /^\s*\}\s*$/
  // ce.md §13.1: the field rule must also read `mutable Foo: Type` so a
  // mutable-record state machine cannot slip past state-product. A plain
  // `Foo: Type` keeps matching unchanged (the `mutable ` prefix only applies
  // to the mutable form).
  const mutableFieldLine = /^\s*mutable\s+(\w+)\s*:\s*([^=\[\]{}]+?)\s*$/
  const fieldLine = /^\s*(\w+)\s*:\s*([^=\[\]{}]+?)\s*$/
  const stateType = (type) => {
    const t = type.trim()
    if (t === 'bool') return true
    if (/^[\w.]+ option$/i.test(t)) return true
    if (/\bref$/i.test(t)) return true
    return definedDus.has(t)
  }

  const collectDoc = (index) => {
    const doc = []
    for (let j = index - 1; j >= 0; j--) {
      const t = lines[j].trim()
      if (t === '') continue
      if (/^\[</.test(t)) continue
      if (/^\/\//.test(t) && !/^\/\/\//.test(t)) continue
      if (!/^\/\/\//.test(t)) break
      doc.unshift(lines[j])
    }
    return doc
  }

  const emit = (rec) => {
    const stateFields = rec.fields.filter((f) => stateType(f.type))
    const classified = rec.doc.some((l) =>
      STATE_COMBINATION_CATEGORIES.some((c) => l.includes(`DSL-state-combination: ${c}`)),
    )
    if (stateFields.length >= 2 && !classified) {
      violations.push({
        gate: 'state-product',
        file,
        line: rec.line,
        text:
          `record '${rec.name}' combines ${stateFields.length} independent state axes ` +
          `(${stateFields.map((f) => f.name).join(', ')}) without a ` +
          '/// DSL-state-combination: domain|physical classification',
      })
    }
  }

  let pending = null
  let rec = null
  for (let i = 0; i < lines.length; i++) {
    const dm = declaration.exec(lines[i])
    if (dm) {
      if (rec) emit(rec)
      rec = null
      pending = { name: dm[1], line: i + 1, doc: collectDoc(i) }
      if (bodyOpen.test(dm[2])) {
        rec = { ...pending, fields: [] }
        pending = null
      }
      continue
    }

    if (rec) {
      if (recordEnd.test(lines[i])) {
        emit(rec)
        rec = null
        continue
      }
      const mf = mutableFieldLine.exec(lines[i])
      const fm = mf ?? fieldLine.exec(lines[i])
      if (fm) rec.fields.push({ name: fm[1], type: fm[2], isMutable: mf !== null })
      continue
    }

    if (pending) {
      const trimmed = lines[i].trim()
      if (bodyOpen.test(lines[i])) {
        rec = { ...pending, fields: [] }
        pending = null
      } else if (trimmed !== '' && !trimmed.startsWith('//')) {
        // A case or another non-record body means the declaration was a DU,
        // abbreviation, or member block. Do not carry it into the next type.
        pending = null
      }
    }
  }
  if (rec) emit(rec)
  return violations
}

/**
 * business mutable-record-field name: a record field whose name marks it as a
 * business program counter / next-action token. ce.md §13.2/§5/§22: a mutable
 * record field named State/Phase/Stage/Mode/RunState/Handoff/Should/Already/
 * Next/Standing/Disposition/Status is a business stage — never a physical
 * resource. Note `Pending*` slots are NOT listed here: a physical-annotated
 * Session/Process record may legitimately carry a `Pending` queue or
 * `Pending*` completion buffer (e.g. PtySession.Pending). An unannotated
 * Session/Process record already fires regardless of field name (see emit), so
 * StudentRunCell's PendingTeacherReturn/PendingFinal still fire through that
 * branch, not through this business-token override.
 */
const BUSINESS_MUTABLE_FIELD_RE =
  /^(?:State|Phase|Stage|Mode|RunState|Handoff|Should|Already|Next|Standing|Disposition|Status)$/

const isDomainOrApplicationPath = (file) => {
  const rel = norm(String(file))
  return (
    (rel.includes('/Domain/') ||
      rel.includes('/Application/') ||
      rel.includes('/Foundation/') ||
      rel.includes('/Participant/') ||
      rel.includes('/Interaction/') ||
      rel.includes('/Mission/') ||
      rel.includes('/Context/') ||
      rel.includes('/Change/') ||
      rel.includes('/Enforcer/') ||
      rel.includes('/Strength/')) &&
    !rel.includes('/OpenCode/') &&
    !rel.includes('/Host/')
  )
}

/**
 * mutable-record-field (ce.md §13.2/§14): a `mutable Foo: T` record field is a
 * long-lived mutable state axis. Rule:
 *   - business-stage field name → always a violation (no path exemption).
 *   - Domain/Application path → always a violation (no annotation exemption).
 *   - Session/Process path → violation unless the record carries a
 *     `/// DSL-state-combination: physical` annotation proving each axis is a
 *     real physical resource (ce.md §14 — an annotation alone is not proof; the
 *     human proof accompanies it, but the annotation is the mechanical gate).
 * This is the gate that should have caught StudentRunCell's mutable-slots; it
 * is field-name independent for the business tokens.
 */
export const scanMutableRecordFields = (text, file = '<synthetic>') => {
  const lines = text.split('\n')
  const violations = []

  // A record declaration may be `type X = {`, `type X =` (body `{` on a later
  // line), `type private X = {`, `and X =`, etc. The `{` may or may not sit on
  // the declaration line, and a `private|internal|public` modifier may prefix
  // the name. A DU (`type X =` then `| Case`) is not a record — we only commit
  // to a record once a body `{` appears before any `|` case line.
  const declOpen = /^\s*(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(\w+)\s*=\s*$/
  const declOpenInline = /^\s*(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(\w+)\s*=\s*\{\s*$/
  const recordEnd = /^\s*\}\s*$/
  const bodyOpen = /^\s*\{\s*$/
  const caseLine = /^\s*\|/
  // A mutable or `ref` field may carry the record's closing `}` on the same
  // line. `ref` is mutable storage too; treating it as ordinary data would
  // re-open the exact escape hatch this gate closes.
  const recordFieldLine = /^\s*(mutable\s+)?(\w+)\s*:\s*([^=\[\]{}]+?)\s*(?:}|\Z|$)/

  const collectDoc = (i) => {
    const doc = []
    for (let j = i - 1; j >= 0; j--) {
      const t = lines[j].trim()
      if (t === '') continue
      if (/^\[</.test(t)) continue
      if (/^\/\//.test(t) && !/^\/\/\//.test(t)) continue
      if (!/^\/\/\//.test(t)) break
      doc.unshift(lines[j])
    }
    return doc
  }

  const emit = (rec) => {
    const hasPhysical = rec.doc.some((l) => l.includes('DSL-state-combination: physical'))
    const classified = rec.doc.some((line) =>
      STATE_COMBINATION_CATEGORIES.some((category) => line.includes(`DSL-state-combination: ${category}`)),
    )
    if (rec.fields.length >= 2 && !classified) {
      violations.push({
        gate: 'state-product',
        file,
        line: rec.line,
        text: `record '${rec.name}' combines ${rec.fields.length} mutable/ref storage axes without a /// DSL-state-combination: domain|physical classification`,
      })
    }
    const domainLayer = isDomainOrApplicationPath(file)
    for (const field of rec.fields) {
      // Domain/Application: any mutable storage field is a violation, with no
      // annotation exemption (ce.md §13.2). `ref` is not a loophole.
      if (domainLayer) {
        violations.push({
          gate: 'mutable-record-field',
          file,
          line: rec.line,
          text:
            `record '${rec.name}' ${field.storage} field '${field.name}' in Domain/Application is a program counter, not a physical resource`, // eslint-disable-line max-len
        })
        continue
      }
      const business = BUSINESS_MUTABLE_FIELD_RE.test(field.name)
      // Session/Process physical-annotated + non-business → real resource, pass.
      if (hasPhysical && !business) continue
      const reason = business
        ? `record '${rec.name}' has business ${field.storage} field '${field.name}' — a program counter, not a physical resource`
        : `record '${rec.name}' ${field.storage} field '${field.name}' lacks a DSL-state-combination: physical proof of resource ownership` // eslint-disable-line max-len
      violations.push({
        gate: 'mutable-record-field',
        file,
        line: rec.line,
        text: reason,
      })
    }
  }

  // pendingDecl: a `type/and ... =` line whose body has not yet resolved to a
  // record `{` or a DU `|`. Only once a body `{` is seen (before any `|` at the
  // same level) do we commit to a record and start collecting mutable fields.
  let pendingDecl = null
  let inRecord = null

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const trimmed = line.trim()

    // A new declaration line starts (or resets) a pending declaration. If we
    // are already inside a record body, a following `type/and` closes it first
    // (each record's mutable fields must be attributed to their own record).
    const openRe = /^\s*(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(\w+)/
    if (openRe.test(line)) {
      if (inRecord) {
        emit(inRecord)
        inRecord = null
      }
      const inline = declOpenInline.exec(line)
      if (inline) {
        inRecord = { name: inline[1], fields: [], line: i + 1, doc: collectDoc(i) }
        pendingDecl = null
        continue
      }
      const open = declOpen.exec(line)
      if (open) {
        pendingDecl = { name: open[1], line: i + 1, doc: collectDoc(i) }
        continue
      }
      continue
    }

    if (inRecord) {
      if (recordEnd.test(line)) {
        emit(inRecord)
        inRecord = null
        pendingDecl = null
        continue
      }
      const fm = recordFieldLine.exec(line)
      if (fm) {
        const isMutable = fm[1] !== undefined
        const isRef = /\bref\s*$/i.test(fm[3])
        if (isMutable || isRef) {
          inRecord.fields.push({
            name: fm[2],
            type: fm[3],
            storage: isMutable ? 'mutable' : 'ref',
          })
        }
        // A trailing `}` on the same line closes the record body.
        if (/\}\s*$/.test(line)) {
          emit(inRecord)
          inRecord = null
          pendingDecl = null
        }
      }
      continue
    }

    if (pendingDecl) {
      if (bodyOpen.test(line)) {
        inRecord = {
          name: pendingDecl.name,
          fields: [],
          line: pendingDecl.line,
          doc: pendingDecl.doc,
        }
        pendingDecl = null
        continue
      }
      if (caseLine.test(line)) {
        // A `|` case means this is a DU, not a record — discard the candidate.
        pendingDecl = null
        continue
      }
      if (trimmed !== '' && !trimmed.startsWith('//')) {
        if (trimmed.includes('{')) {
          inRecord = {
            name: pendingDecl.name,
            fields: [],
            line: pendingDecl.line,
            doc: pendingDecl.doc,
          }
          pendingDecl = null
          continue
        }
        // A non-comment, non-`{`, non-`|` line means the declaration is not a
        // body-opened record in the expected sense (e.g. a type abbreviation or
        // a generic constraint) — stop hunting.
        pendingDecl = null
      }
    }
  }

  if (inRecord) emit(inRecord)
  return violations
}

/**
 * Detect the narrow, syntactically certain distributed-program-counter shape:
 * two declared mutable registries are directly probed in one `match`/`if`, and
 * a nearby selected branch invokes a named external-effect verb. This is a
 * counterexample gate, not a proof that arbitrary registry composition is a
 * state machine.
 */
export const scanRegistryJointBranches = (text, file = '<synthetic>') => {
  const lines = text.split('\n')
  const registries = new Set()
  const registryDeclaration =
    /^\s*let\s+(\w+)\s*=\s*(?:new\s+)?(?:Concurrent)?(?:Dictionary|HashSet)</
  const probe = /\b(\w+)\.(?:TryGetValue|ContainsKey|TryFind|TryRemove)\b/g
  const effect = /\b(?:send|dispatch|append|publish|write|emit|remove|add|start|stop|abort|create|delete)[A-Z]\w*\b/
  const violations = []

  for (const line of lines) {
    const declaration = registryDeclaration.exec(line)
    if (declaration) registries.add(declaration[1])
  }

  for (let i = 0; i < lines.length; i++) {
    const condition = lines[i]
    if (!/^\s*(?:match|if)\b/.test(condition)) continue
    const probed = new Set()
    for (const match of condition.matchAll(probe)) {
      if (registries.has(match[1])) probed.add(match[1])
    }
    if (probed.size < 2) continue

    const branch = lines.slice(i, Math.min(i + 4, lines.length)).join('\n')
    if (!effect.test(branch)) continue
    violations.push({
      gate: 'registry-joint-branch',
      file,
      line: i + 1,
      text: `joint direct probes of ${[...probed].join(', ')} select an effect branch`,
    })
  }
  return violations
}

export const EXECUTION_POSITION_NAMES = Object.freeze([
  'NextAction',
  'NextStep',
  'ResumeAt',
  'StepIndex',
  'ContinueToken',
])

const EXECUTION_POSITION_DECLARATION =
  /\b(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(?:NextAction|NextStep|ResumeAt\w*|StepIndex|ContinueToken)\b|\|\s*(?:NextAction|NextStep|ResumeAt\w*|StepIndex|ContinueToken)\b|\b(?:NextAction|NextStep|ResumeAt\w*|StepIndex|ContinueToken)\s*:\s*[^=]|\bmember\s+(?:val\s+)?(?:NextAction|NextStep|ResumeAt\w*|StepIndex|ContinueToken)\b|\blet\s+(?:mutable\s+|private\s+|internal\s+)?(?:nextAction|nextStep|resumeAt\w*|stepIndex|continueToken)\b/i

const regexEscape = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

const declarationClassification = (lines, index) => {
  let declaration = index
  for (let i = index; i >= 0; i--) {
    if (/^\s*(?:type|and)\s+/.test(lines[i])) {
      declaration = i
      break
    }
    if (i < index && /^\s*(?:let|module|namespace)\s+/.test(lines[i])) break
  }

  const docs = []
  for (let i = declaration - 1; i >= 0; i--) {
    const trimmed = lines[i].trim()
    if (/^\[<.*>\]$/.test(trimmed)) continue
    if (trimmed === '' || !trimmed.startsWith('///')) break
    docs.unshift(trimmed)
  }
  const match = /DSL-class:\s*([A-Za-z][\w-]*)/.exec(docs.join('\n'))
  return match?.[1]
}

/** Stored/cross-module execution-position declarations are PCs unless the
 * enclosing declaration positively identifies a physical/external protocol
 * value. In addition to the legacy spelling tripwire, this reads record
 * structure and direct data flow: an exported record slot used as a
 * match/conditional discriminant or collection instruction index is a stored
 * branch position regardless of what either the record or field is called.
 * A path or familiar type name never grants an exemption. */
export const scanExecutionPositions = (text, file = '<synthetic>', compilerEvidence = undefined) => {
  const lines = text.split('\n')
  const violations = []
  const emitted = new Set()
  const emit = (line, source) => {
    if (emitted.has(line)) return
    emitted.add(line)
    violations.push({ gate: 'program-counter', file, line, text: source.trim() })
  }

  // Parse public/default-public F# records. The body may be inline or span
  // lines (including the standard `type X =` then `{ ... }` form), and fields
  // may be separated by newlines or semicolons. Private fields are not exported
  // and are left to the mutable/durable storage gates.
  const records = []
  const recordDeclaration =
    /^\s*(?:type|and)\s+(?:(private|internal|public)\s+)?(\w+)(?:<[^=>]*>)?\s*=\s*(.*)$/
  let record = null
  let pendingRecord = null
  const beginRecord = (candidate, body) => {
    record = { ...candidate, body: body.slice(body.indexOf('{') + 1) }
    if (record.body.includes('}')) {
      record.body = record.body.slice(0, record.body.indexOf('}'))
      records.push(record)
      record = null
    }
  }
  for (let i = 0; i < lines.length; i++) {
    const code = lines[i].replace(/\/\/.*$/, '')
    const declaration = recordDeclaration.exec(code)
    if (declaration) {
      record = null
      pendingRecord = null
      const candidate = {
        line: i + 1,
        source: lines[i],
        name: declaration[2],
        exported: declaration[1] !== 'private' && declaration[1] !== 'internal',
        classification: declarationClassification(lines, i),
      }
      if (declaration[3].includes('{')) {
        beginRecord(candidate, declaration[3])
      } else if (declaration[3].trim() === '') pendingRecord = candidate
      continue
    }
    if (pendingRecord) {
      if (code.trim() === '') continue
      if (code.includes('{')) beginRecord(pendingRecord, code)
      pendingRecord = null
      continue
    }
    if (!record) continue
    if (code.includes('}')) {
      record.body += `\n${code.slice(0, code.indexOf('}'))}`
      records.push(record)
      record = null
    } else {
      record.body += `\n${code}`
    }
  }

  const executableLines = lines.map((line) => line.replace(/\/\/.*$/, ''))
  const executable = executableLines.join('\n')
  const sameConsumer = (use) => norm(use.consumerPath ?? '') === norm(file)
  const resolvedUses = (compilerEvidence?.symbolUses ?? []).filter(sameConsumer)
  const resolvedApplications = (compilerEvidence?.applicationUses ?? []).filter(sameConsumer)
  const executionCall =
    /\b(?:validate|send|dispatch|append|publish|write|emit|remove|add|start|stop|abort|create|delete)[A-Z]\w*\s*\(|(?<![.\w])(?:validate|send|dispatch|append|publish|write|emit|remove|add|start|stop|abort|create|delete)\s*\(/
  // Syntax-resolved function values are executable even when this module only
  // returns them through its public seam and the eventual invocation lives in
  // another file. This deliberately keys on the binding shape, not on a list
  // of operation-like names.
  const functionBindings = new Set()
  const callableMembers = new Set()
  for (const line of executableLines) {
    const binding =
      /^\s*let\s+(?:(?:private|internal|public|inline|mutable|rec)\s+)*(?:``([^`]+)``|(\w+))\s+(.+?)\s*=/.exec(
        line,
      )
    if (binding && !/^:\s*/.test(binding[3])) functionBindings.add(binding[1] ?? binding[2])
    const abstractMember = /^\s*abstract\s+([A-Za-z_]\w*)\s*:/.exec(line)
    if (abstractMember) callableMembers.add(abstractMember[1])
    const concreteMember = /^\s*member\s+[^.]*\.([A-Za-z_]\w*)\s*(?:\([^)]*\)|[A-Za-z_])/.exec(line)
    if (concreteMember) callableMembers.add(concreteMember[1])
  }
  const branchSelectsExecution = (region) => {
    const branchLines = region.split('\n')
    for (let i = 0; i < branchLines.length; i++) {
      const arrow = branchLines[i].indexOf('->')
      if (arrow < 0) continue
      const result = branchLines[i].slice(arrow + 2).trim().replace(/^(?:return|return!)\s+/, '')
      if (/^(?:fun\b|function\b)/.test(result)) return true

      const returned = /^(?:\(?\s*)?([A-Za-z_]\w*)(?:\s*\)?)$/.exec(result)
      if (returned && functionBindings.has(returned[1])) {
        let continuation = i + 1
        while (continuation < branchLines.length && branchLines[continuation].trim() === '') continuation++
        const next = branchLines[continuation]
        const armIndent = branchLines[i].search(/\S|$/)
        if (next === undefined || next.trimStart().startsWith('|') || next.search(/\S|$/) <= armIndent) {
          return true
        }
      }

      // An application expression is direct execution evidence regardless of
      // receiver casing or spelling. It includes curried member applications
      // (`port.Send value`) that the old verb-shaped call regex missed.
      const memberApplication = /\b[a-z_]\w*\.([A-Za-z_]\w*)\s*(?:\(|\s+(?=["'\dA-Za-z_({\[]))/.exec(
        result,
      )
      if (memberApplication && callableMembers.has(memberApplication[1])) return true
    }
    return false
  }
  const branchRegion = (offset) => {
    const start = executable.slice(0, offset).split('\n').length - 1
    const baseIndent = executableLines[start].search(/\S|$/)
    let end = start + 1
    while (end < executableLines.length) {
      const line = executableLines[end]
      const trimmed = line.trim()
      if (trimmed !== '') {
        const indent = line.search(/\S|$/)
        if (indent < baseIndent || (indent === baseIndent && !/^(?:\||else\b|elif\b)/.test(trimmed))) break
      }
      end++
    }
    return { start, end, text: executableLines.slice(start, end).join('\n') }
  }
  const compilerBranchSelectsExecution = ({ start, end }) => {
    const inBranch = (use) => {
      const line = use.line ?? use.startLine
      return line >= start + 1 && line <= end
    }
    const applications = resolvedApplications.filter(inBranch)

    // A resolved application is execution evidence when its callable returns
    // unit or an effect carrier. Pure helpers returning immutable domain data
    // intentionally remain green. Every arm must select such an application:
    // a conditional validation inside one arm is not a returned operation seam.
    const armLines = executableLines
      .slice(start, end)
      .map((line, index) => (line.includes('->') ? start + index + 1 : 0))
      .filter(Boolean)
    const effectApplicationLines = new Set(
      applications
        .filter((application) => {
        const resultType = String(application.inferredType ?? '').split('->').at(-1)?.trim() ?? ''
        return /^(?:Microsoft\.FSharp\.Core\.)?unit\b|(?:^|\.)Task(?:<|$)|(?:^|\.)Async</.test(
          resultType,
        )
        })
        .map((application) => application.startLine),
    )
    if (armLines.length > 0 && armLines.every((line) => effectApplicationLines.has(line))) return true

    // A resolved function symbol appearing after an arm arrow without a
    // corresponding application is a function selected for another caller.
    return resolvedUses.some((use) => {
      if (!inBranch(use) || !String(use.inferredType ?? '').includes('->')) return false
      const sourceLine = executableLines[use.line - 1] ?? ''
      const arrow = sourceLine.indexOf('->')
      if (arrow < 0 || use.column <= arrow) return false
      return !applications.some(
        (application) =>
          application.startLine === use.line && application.resolvedTarget === use.symbol,
      )
    })
  }
  for (const rec of records) {
    if (!rec.exported) continue
    if (rec.classification === 'ExternalSignal' || rec.classification === 'PhysicalHandle') continue
    const receivers = new Set([rec.name[0].toLowerCase() + rec.name.slice(1)])
    const typedReceiver = new RegExp(`\\b([a-z_]\\w*)\\s*:\\s*${regexEscape(rec.name)}\\b`, 'g')
    for (const binding of executable.matchAll(typedReceiver)) receivers.add(binding[1])
    const receiver = `(?:${[...receivers].map(regexEscape).join('|')})`
    for (const match of rec.body.matchAll(/\b(\w+)\s*:\s*([^;}\n]+)/g)) {
      const field = regexEscape(match[1])
      const access = `\\b${receiver}\\.${field}\\b`
      const branch = new RegExp(
        `\\bmatch\\s+${access}\\s+with|\\b(?:if|elif)\\s+(?:not\\s+)?${access}\\s+then|` +
          `\\bwhile\\s+(?:not\\s+)?${access}\\s+do`,
        'gm',
      )
      const index = new RegExp(
        `\\b(?:Array|List|Seq)\\.item\\s+${access}|\\b[A-Za-z_]\\w*\\.\\[\\s*${access}\\s*\\]`,
        'm',
      )
      const drivesBranch = [...executable.matchAll(branch)].some((use) => {
        const region = branchRegion(use.index)
        return (
          executionCall.test(region.text) ||
          branchSelectsExecution(region.text) ||
          compilerBranchSelectsExecution(region)
        )
      })
      if (drivesBranch || index.test(executable)) emit(rec.line, rec.source)
    }
  }

  for (let i = 0; i < lines.length; i++) {
    const code = lines[i].replace(/\/\/.*$/, '')
    if (!EXECUTION_POSITION_DECLARATION.test(code)) continue
    const classification = declarationClassification(lines, i)
    if (classification === 'ExternalSignal' || classification === 'PhysicalHandle') continue
    emit(i + 1, lines[i])
  }
  return violations
}

/** Scan one source text. Returns [{gate, file, line, text}, ...]. */
export const scanText = (text, file = '<synthetic>', compilerEvidence = undefined) => {
  const violations = []
  const lines = text.split('\n')

  // state-product is a structure parse, independent of field names, so it runs
  // once over the whole text rather than per line. Same for mutable-record-field.
  violations.push(...scanStateProducts(text, file))
  violations.push(...scanMutableRecordFields(text, file))
  violations.push(...scanRegistryJointBranches(text, file))
  violations.push(...scanExecutionPositions(text, file, compilerEvidence))

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const code = line.replace(/\/\/.*/g, '').trim()

    // ControlState (PR 9 C / DSL-005): a stored DU tagged `/// DSL-class:
    // ControlState` is a program counter unless an explicit exemption admits it
    // OR a structurally valid `/// DSL-control-state-reason:` line explains why
    // ordinary CE constructs cannot express the flow.
    if (!CONTROL_STATE_EXEMPT.has(file) && /DSL-class:\s*ControlState\b/.test(line)) {
      if (!hasValidControlStateReason(lines, i)) {
        violations.push({ gate: 'program-counter', file, line: i + 1, text: line.trim() })
      }
    }

    if (!code) continue

    for (const { gate, pattern, skipIf } of FORBIDDEN) {
      if (skipIf && skipIf(file)) continue
      if (gate === 'mutable' && isMutableDeclarationAllowed(file) && hasDslMutableDeclaration(lines, i)) continue
      // A `let name arg =` / `let name (args) =` binding is a pure operation
      // (work being performed), not a stored stage latch. Function bindings are
      // skipped so verb-named operations like `clearStalePending` do not fire
      // behaviour-bool; stored stage slots (record fields, member properties)
      // are not `let`-function shapes and still fire via the residual pattern.
      if (
        gate === 'behaviour-bool' &&
        /\blet\s+(?!mutable\b)\w+(?:\s+\w+|\s*\([^)]*\))+\s*=/.test(code)
      ) {
        continue
      }
      if (pattern.test(code)) {
        violations.push({ gate, file, line: i + 1, text: line.trim() })
      }
    }
  }
  return violations
}

/** PR 9 item 5 / D: large-DU classification.
 *
 * A DU with >= 10 cases must carry a `/// DSL-class:` doc annotation naming
 * its vocabulary category (Vocabulary / DurableFact / Evidence / Decision /
 * ExternalSignal / Witness / Capability / Receipt / PhysicalHandle).
 * ControlState is a separate forbidden class (see scanText).
 *
 * Hard gate: since PR 9 D, an unclassified large DU fails the build in
 * runCli (it is folded into the violation list, no longer report-only).
 */
export const LARGE_DU_THRESHOLD = 10
// ControlState is intentionally absent: it is a forbidden program-counter
// class (see scanText), not a legitimate large-DU vocabulary category.
export const DSL_CLASSES = [
  'Vocabulary',
  'DurableFact',
  'Evidence',
  'Decision',
  'ExternalSignal',
  'Witness',
  'Capability',
  'Receipt',
  'PhysicalHandle',
]

/** Returns [{ file, line, name, cases }] for large DUs lacking a DSL-class annotation. */
export const scanLargeDus = (text, file) => {
  const lines = text.split('\n')
  const duRe = /^\s*(?:type|and)\s+(\w+)\s*=\s*/
  const caseRe = /^\s*\| ([A-Z]\w+)/
  const missing = []
  let cur = null
  for (let i = 0; i < lines.length; i++) {
    const code = lines[i].replace(/\/\/.*/g, '')
    const dm = duRe.exec(code)
    if (dm) {
      // Walk up from the type declaration, skipping blank lines, `[<...>]`
      // attribute lines, and (optionally) `//` comments, while still collecting
      // `///` doc lines so a `/// DSL-class:` annotation separated from the
      // `type` by `[<RequireQualifiedAccess>]` is still matched (Roles.fs).
      const docAbove = []
      for (let j = i - 1; j >= 0; j--) {
        const t = lines[j].trim()
        if (t === '') continue
        if (/^\[</.test(t)) continue
        if (/^\/\//.test(t) && !/^\/\/\//.test(t)) continue
        if (!/^\/\/\//.test(t)) break
        docAbove.unshift(lines[j])
      }
      const classified = docAbove.some((l) => DSL_CLASSES.some((c) => l.includes(`DSL-class: ${c}`)))
      cur = { name: dm[1], cases: [], line: i + 1, classified }
      for (const c of code.matchAll(/\| ([A-Z]\w+)/g)) cur.cases.push(c[1])
      continue
    }
    if (cur) {
      const cm = caseRe.exec(code)
      if (cm) {
        cur.cases.push(cm[1])
      } else if (code.trim() && !/^\s*[{}]/.test(code) && !code.includes('of ')) {
        if (cur.cases.length >= LARGE_DU_THRESHOLD && !cur.classified) {
          missing.push({ file, line: cur.line, name: cur.name, cases: cur.cases.length })
        }
        cur = null
      }
    }
  }
  if (cur && cur.cases.length >= LARGE_DU_THRESHOLD && !cur.classified) {
    missing.push({ file, line: cur.line, name: cur.name, cases: cur.cases.length })
  }
  return missing
}

/** Scan {file, text} entries. */
export const scanFiles = (entries, compilerEvidence = undefined) => {
  const violations = []
  for (const entry of entries) {
    const file = entry.file
    const text = entry.text
    for (const v of scanText(text, file, compilerEvidence)) violations.push(v)

    // PR 9 item 4 / B: multi-bool loop detection applies to every Program
    // file, Process/ included (a PTY/process layer is not immune to a
    // state-machine of `let mutable x = false` flags around a while loop).
    const code = text.split('\n').map((l) => l.replace(/\/\/.*/g, ''))
    const bools = code.filter((l) => /\blet mutable\s+\w+\s*=\s*false\b/.test(l)).length
    const hasWhile = code.some((l) => /\bwhile\s/.test(l))
    if (bools >= 2 && hasWhile) {
      const first = code.findIndex((l) => /\blet mutable\s+\w+\s*=\s*false\b/.test(l))
      violations.push({
        gate: 'bool-loop',
        file,
        line: first + 1,
        text: `${bools} mutable false booleans with a while loop`,
      })
    }
  }

  // PR 9 item 6 / E: duplicate DU case-name sets across the whole tree. Two
  // DUs (in any files) sharing the exact same case-name set are the same
  // knowledge in two shapes unless a registered exemption admits the pair.
  const duRe = /^\s*(?:type|and)\s+(\w+)\s*=\s*/
  const caseRe = /^\s*\| ([A-Z]\w+)/
  const byCaseSet = new Map()
  for (const entry of entries) {
    const file = entry.file
    const base = norm(String(file)).split('/').pop() ?? ''
    const linesArr = entry.text.split('\n')
    let cur = null
    for (let i = 0; i < linesArr.length; i++) {
      const code2 = linesArr[i].replace(/\/\/.*/g, '')
      const dm = duRe.exec(code2)
      if (dm) {
        cur = { file, base, name: dm[1], cases: [], line: i + 1 }
        for (const c of code2.matchAll(/\| ([A-Z]\w+)/g)) cur.cases.push(c[1])
        continue
      }
      if (cur) {
        const cm = caseRe.exec(code2)
        if (cm) {
          cur.cases.push(cm[1])
        } else if (code2.trim() && !/^\s*[{}]/.test(code2) && !code2.includes('of ') && code2.trim() !== '|') {
          const key = [...cur.cases].sort().join('|')
          if (key) {
            if (!byCaseSet.has(key)) byCaseSet.set(key, [])
            byCaseSet.get(key).push(cur)
          }
          cur = null
        }
      }
    }
    if (cur) {
      const key = [...cur.cases].sort().join('|')
      if (key) {
        if (!byCaseSet.has(key)) byCaseSet.set(key, [])
        byCaseSet.get(key).push(cur)
      }
    }
  }
  for (const group of byCaseSet.values()) {
    if (group.length < 2) continue
    for (let i = 0; i < group.length; i++) {
      for (let j = i + 1; j < group.length; j++) {
        const a = group[i]
        const b = group[j]
        if (a.cases.length < 2) continue
        const keyA = `${a.base}:${a.name}`
        const keyB = `${b.base}:${b.name}`
        if (DUP_CASES_EXEMPT.has(keyA) || DUP_CASES_EXEMPT.has(keyB)) continue
        if (a === b) continue
        const [first, second] =
          `${a.file}:${a.name}` < `${b.file}:${b.name}` ? [a, b] : [b, a]
        violations.push({
          gate: 'dup-cases',
          file: first.file,
          line: first.line,
          text: `DU '${first.name}' repeats case set of '${second.file}:${second.name}': ${[...first.cases].sort().join(' | ')}`,
        })
      }
    }
  }
  return violations
}

export const groupByGate = (violations) => {
  const byGate = new Map()
  for (const v of violations) {
    if (!byGate.has(v.gate)) byGate.set(v.gate, [])
    byGate.get(v.gate).push(v)
  }
  return byGate
}

/** Exit decision: { ok, reason }. threshold < 0 means zero-tolerance. */
export const evaluateThreshold = (violationCount, threshold) => {
  if (violationCount === 0) return { ok: true, reason: 'clean' }
  if (threshold >= 0 && violationCount <= threshold) {
    return { ok: true, reason: 'within-threshold' }
  }
  if (threshold >= 0) return { ok: false, reason: 'exceeds-threshold' }
  return { ok: false, reason: 'fail-closed' }
}

const runCli = () => {
  const thresholdArg = process.argv.find((arg) => arg.startsWith('--threshold='))
  const threshold = thresholdArg ? Number(thresholdArg.split('=')[1]) : -1

  const productionFiles = walk(PRODUCTION_ROOT, ['.fs']).map(norm).filter(isProgramFile)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const compilerEvidence = scanProjectSymbolUses({
    scratchRoot: FCS_SCRATCH,
    resultPath: FCS_RESULT,
  })
  const violations = scanFiles(entries, compilerEvidence)
  const byGate = groupByGate(violations)
  const write = threshold >= 0 ? console.log : console.error

  // PR 9 item 5 / D: large-DU classification is now a hard gate. A DU with
  // >= LARGE_DU_THRESHOLD cases must carry a `/// DSL-class:` annotation,
  // else the build fails (no longer a report-only warning).
  const unclassifiedLarge = []
  for (const entry of entries) {
    unclassifiedLarge.push(...scanLargeDus(entry.text, entry.file))
  }
  for (const d of unclassifiedLarge) {
    violations.push({
      gate: 'large-DU',
      file: d.file,
      line: d.line,
      text: `DU '${d.name}' has ${d.cases} cases and lacks /// DSL-class: annotation`,
    })
  }
  // Re-group so large-DU appears in the printed breakdown.
  for (const v of unclassifiedLarge) {
    if (!byGate.has('large-DU')) byGate.set('large-DU', [])
    byGate.get('large-DU').push(v)
  }

  if (violations.length === 0) {
    write(`dsl-ownership: OK — ${productionFiles.length} Program/Domain files`)
    process.exit(0)
  }

  write(`dsl-ownership: ${violations.length} violation(s) — ${productionFiles.length} files\n`)
  for (const [gate, items] of byGate) {
    write(`${gate} (${items.length})`)
    for (const v of items) {
      if (gate === 'large-DU') {
        write(`  ${v.file}:${v.line}  ${v.name} (${v.cases} cases) — add /// DSL-class: <Vocabulary|DurableFact|Evidence|Decision|ExternalSignal|Witness|Capability|Receipt|PhysicalHandle>`)
      } else {
        write(`  ${v.file}:${v.line}  ${v.text}`)
      }
    }
    write('')
  }

  const decision = evaluateThreshold(violations.length, threshold)
  if (decision.reason === 'within-threshold') {
    console.log(
      `dsl-ownership: within threshold — ${violations.length}/${threshold} violations (freeze only)`,
    )
    process.exit(0)
  }
  if (decision.reason === 'exceeds-threshold') {
    console.error(`dsl-ownership: exceeds threshold — ${violations.length} > ${threshold}`)
    process.exit(1)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()

