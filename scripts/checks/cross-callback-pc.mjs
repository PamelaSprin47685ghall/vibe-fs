#!/usr/bin/env node
/**
 * Cross-callback program-counter gate (STRUCTURED-WORKFLOW-017 structural invariant).
 *
 * Detects mutable/registry values written in callback A, read in callback B,
 * where B's presence/value determines the next business effect — without proof
 * that the cell is an opaque physical capability/outcome.
 *
 * Four pattern signatures:
 *  1. TryTake continuation consumption: methods named TryTake* returning option
 *  2. Armed presence probe: methods named IsArmed/HasArmed/TryArm returning bool/option
 *  3. DU await state: Dictionary<_, DU> where DU has Await/Armed/Pending-prefixed cases
 *  4. Clear/Drop presence-clearing: methods named Clear* / Drop* that clear registry
 *     presence and whose return value or side-effect drives the next business effect
 *
 * Structural invariant:
 *   ∀ mutable/registry value, if written in callback A, read in callback B,
 *   and B's presence/value determines the next business effect, then it must
 *   be proven as an opaque physical capability/outcome, otherwise it is a
 *   cross-boundary program counter.
 *
 * Whitelist: ``` /// DSL-cross-callback-proof: physical <category> ``` annotation
 * on the declaration line's preceding doc block proves the cell is an opaque
 * physical capability/outcome. <category> must be one of EXEMPTION_CATEGORIES.
 * Bare `physical` without a category and the broad `physical resource` label
 * are NOT auto-passes; cells without a narrow category must be exactly
 * registered in REGISTERED_DECLARATIONS with owner, issuer, key, revoke,
 * allowed consumers, and test-anchor. A generic `resource` comment never
 * suppresses a finding.
 *
 * Exemption categories (physical capabilities that are NOT program counters):
 *  pty, timer, waiter, single-flight, quiescence-permit, process-handle,
 *  socket, cancellation-token
 *
 * Static guarantee (lexical only): every Dictionary/HashSet/Map/ref registry
 * whose name is read or consumed inside a TryTake, IsArmed, Clear, Drop, or
 * Consume executable block — a type member or a module/type-level `let`
 * function — either carries a narrow categorized proof or resolves to a live
 * exact registration whose observed callers are all allowlisted. This gate
 * proves declaration-shape coverage; it claims no runtime causality.
 *
 * Legal reference: SessionQuiescenceGate.fs — process-local side-effect
 * admission gate. ObserveIdle returns opaque QuiescencePermit; TryConsume(permit)
 * checks state == Idle(permit.AttemptSerial). The permit is an unforgeable
 * typed capability, not a presence probe. Restart clears the gate (HOST-007).
 *
 * Every detected cell without a narrow proof is RED. There is no debt
 * baseline or ceiling: ownership evidence is required at the declaration.
 */

import { existsSync, readFileSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = resolve(HERE, '..', '..')

export const PRODUCTION_ROOT = 'src/Wanxiangshu'
const norm = (p) => p.replace(/\\/g, '/')

/**
 * Physical capability exemption categories.
 * A proof annotation must reference one of these narrow categories. Bare
 * `physical` without a category and the broad `resource` label never whitelist.
 */
export const EXEMPTION_CATEGORIES = new Set([
  'pty',
  'timer',
  'waiter',
  'single-flight',
  'quiescence-permit',
  'process-handle',
  'socket',
  'cancellation-token',
])

/**
 * Narrow declaration-symbol exemptions: exact `file:declaration` records with
 * real owner, issuer, key, revoke, consumer, and test metadata. A registration
 * only exempts while it is live — the source file still declares the name and
 * the test anchor still exists (see isRegistrationLive) — and every observed
 * caller is listed in allowedConsumers. Path or basename alone is never an
 * authorization.
 */
export const REGISTERED_DECLARATIONS = new Map([
  [
    'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs:pendingAttemptPlans',
    {
      owner: 'managed-chat-execution',
      issuer: 'PluginRecoveryScope.FreezePendingAttemptPlan',
      key: 'SessionId * PhysicalUserMessageId',
      rules: 'single-flight admission; retained through bind until terminal consume; session deletion is the only non-bind cleanup',
      allowedConsumers: [
        'PluginRecoveryScope.FreezePendingAttemptPlan',
        'PluginRecoveryScope.RecordPendingAttemptPlan',
        'PluginRecoveryScope.TryPeekPendingAttemptPlan',
        'PluginRecoveryScope.BindPendingAttemptPlan',
        'PluginRecoveryScope.TryBindAttemptPlan',
        'PluginRecoveryScope.RecordAttemptPlan',
        'PluginRecoveryScope.ConsumeAttemptPlan',
        'PluginRecoveryScope.ClearAttemptPlansFor',
        'PluginRecoveryScope.ClearSession',
  ],
      revoke: 'PluginRecoveryScope.ClearSession',
      testAnchor: 'requirements/provider-attempt-recovery/tests/freeze-admission.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs:attemptPlans',
    {
      owner: 'managed-chat-execution',
      issuer: 'PluginRecoveryScope.BindPendingAttemptPlan',
      key: 'SessionId * ProviderRunIdentity',
      rules: 'exact provider-run channel; bound once under the exact physical parent, consumed once on terminal',
      allowedConsumers: [
        'PluginRecoveryScope.BindPendingAttemptPlan',
        'PluginRecoveryScope.TryBindAttemptPlan',
        'PluginRecoveryScope.RecordAttemptPlan',
        'PluginRecoveryScope.TryPeekAttemptPlan',
        'PluginRecoveryScope.TryAttemptPlan',
        'PluginRecoveryScope.ConsumeAttemptPlan',
        'PluginRecoveryScope.ClearAttemptPlansFor',
  ],
      revoke: 'PluginRecoveryScope.ClearSession',
      testAnchor: 'requirements/provider-attempt-recovery/tests/freeze-admission.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs:pendingOffer',
    {
      owner: 'blogger-companion',
      issuer: 'PluginBloggerScope.OfferMaterial',
      key: 'SessionId',
      rules: 'single-slot inbound mailbox owned by Blogger convergence; consumed by ParkTransform; newest covers unread material; per-session revoke CancelParked, full cleanup BeginShutdown/Dispose',
      allowedConsumers: [
        'PluginBloggerScope.ParkTransform',
        'PluginBloggerScope.OfferMaterial',
        'PluginBloggerScope.CancelParked',
        'PluginBloggerScope.BeginShutdown',
        'PluginBloggerScope.Dispose',
  ],
      revoke: 'PluginBloggerScope.CancelParked',
      testAnchor: 'requirements/context-compression/tests/blogger-boundary-cleanbreak.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs:episodes',
    {
      owner: 'blogger-companion',
      issuer: 'PluginBloggerScope.ClaimRepairEpisode',
      key: 'BloggerSessionId with exact RequestId * AuthorityRoot',
      rules: 'claim requires the live flight with the exact request id; replays the same identity; rejects foreign request/root without overwrite; cancel, drain, flight release, and dispose remove',
      allowedConsumers: [
        'PluginBloggerScope.ClaimRepairEpisode',
        'PluginBloggerScope.TryGetRepairEpisode',
        'PluginBloggerScope.CancelRepairEpisode',
        'PluginBloggerScope.ReleaseCurrentRequest',
        'PluginBloggerScope.CancelEpisodesForSession',
        'PluginBloggerScope.DrainRepairEpisodes',
        'PluginBloggerScope.BeginShutdown',
        'PluginBloggerScope.Dispose',
  ],
      revoke: 'PluginBloggerScope.CancelRepairEpisode',
      testAnchor: 'requirements/capability-enforcement/tests/blogger-repair-trace.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:deletedInspectorsByOwnerScope',
    {
      owner: 'sync-delegate-execution',
      issuer: 'SyncDelegateCallStore.PutDeletedInspector',
      key: 'ReuseScopeId (owner scope)',
      rules: 'retired child identity retained only for draft/session cleanup',
      allowedConsumers: [
        'SyncDelegateCallStore.TryTakeDeletedInspector',
        'SyncDelegateCallStore.ClearDeletedInspector',
        'SyncDelegateCallStore.ClearAll',
  ],
      revoke: 'SyncDelegateCallStore.ClearAll',
      testAnchor: 'requirements/structured-workflow/tests/cross-callback-pc.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Execution/Session/Attachment/AttachedRuntime.fs:bindings',
    {
      owner: 'managed-session-lifecycle',
      issuer: 'AttachedSessionRuntime.GetOrCreate',
      key: 'ReuseScopeId * SyncDelegateRole',
      rules: 'single-flight admission per scope and role; reuse existing binding or reject conflicting observation',
      allowedConsumers: [
        'AttachedSessionRuntime.GetOrCreate',
        'AttachedSessionRuntime.TryFind',
        'AttachedSessionRuntime.TryFindByScope',
        'AttachedSessionRuntime.TryFindOwner',
        'AttachedSessionRuntime.Remove',
        'AttachedSessionRuntime.RemoveByDelegateSession',
        'AttachedSessionRuntime.Clear',
      ],
      revoke: 'AttachedSessionRuntime.Clear',
      testAnchor: 'requirements/managed-session-lifecycle/tests/attached-session-runtime.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:callsByOwnerScope',
    {
      owner: 'sync-delegate-execution',
      issuer: 'SyncDelegateCallStore.BeginCall',
      key: 'ReuseScopeId (owner scope)',
      rules: 'live call rendezvous by caller scope; removed on delegate completion or scope cancellation',
      allowedConsumers: [
        'SyncDelegateCallStore.BeginCall',
        'SyncDelegateCallStore.TryPopCallByDelegate',
        'SyncDelegateCallStore.CancelScope',
        'SyncDelegateCallStore.ClearAll',
      ],
      revoke: 'SyncDelegateCallStore.ClearAll',
      testAnchor: 'requirements/delegation/tests/sync-delegate-runtime.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:observedProviderRuns',
    {
      owner: 'sync-delegate-execution',
      issuer: 'SyncDelegateCallStore.ObserveProviderToolCall',
      key: 'SessionId * ProviderRunIdentity',
      rules: 'Host event projection; accumulate and deduplicate tool calls for exact provider run; cleared on cancel',
      allowedConsumers: [
        'SyncDelegateCallStore.ObserveProviderToolCall',
        'SyncDelegateCallStore.TryObservedBatch',
        'SyncDelegateCallStore.CancelScope',
        'SyncDelegateCallStore.ClearAll',
      ],
      revoke: 'SyncDelegateCallStore.ClearAll',
      testAnchor: 'requirements/delegation/tests/sync-delegate-host-observation.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Strength/OpenCode/PluginScope.fs:episodes',
    {
      owner: 'speculative-investigation',
      issuer: 'PluginStrengthScope.ArmStrengthCounterfactual',
      key: 'SessionId',
      rules:
        'one bounded episode per exact session; arm creates only when absent, unrelated runs only join seen, target creates the immutable first, same run never serves as second, next distinct run completes the pair and removes the episode',
      allowedConsumers: [
        'CounterfactualCollector.Arm',
        'CounterfactualCollector.Observe',
        'CounterfactualCollector.ObserveLocked',
        'CounterfactualCollector.ClearSession',
        'CounterfactualCollector.ClearAll',
        'PluginStrengthScope.ArmStrengthCounterfactual',
        'PluginStrengthScope.ObserveStrengthPrimary',
        'PluginStrengthScope.ClearSession',
        'PluginStrengthScope.Dispose',
      ],
      revoke: 'PluginStrengthScope.ClearSession',
      testAnchor: 'requirements/speculative-investigation/tests/predictor-rollout.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Borrowing.fs:creditSourceByExecution',
    {
      owner: 'execution-model-routing',
      issuer: 'BorrowingCapacity.RouteFresh',
      key: 'executionKey(SessionId, PhysicalUserMessageId) with explicit LenderSessionId + Distance',
      rules: 'borrowed execution maps to exactly one lender token with an explicit lender and distance; cleared on retire/release; never changes the real token count',
      allowedConsumers: [
        'BorrowingCapacity.RouteFresh',
        'BorrowingCapacity.ReserveFresh',
        'BorrowingCapacity.AdoptReservation',
        'BorrowingCapacity.ReleaseSession',
        'BorrowingCapacity.ReleasePhysical',
        'BorrowingCapacity.ExactCredit',
  ],
      revoke: 'BorrowingCapacity.ReleaseSession',
      testAnchor: 'requirements/execution-model-routing/tests/model-routing-runtime.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs:armed',
    {
      owner: 'degeneration-guard',
      issuer: 'LoopSensor.TryArm',
      key: 'SessionId with exact ProviderRunIdentity',
      rules: 'anomaly ownership bounded to the exact execution/run; armed once until reconcile; only the exact reconciled run consumes; stale runs never consume',
      allowedConsumers: [
        'LoopSensor.TryArm',
        'LoopSensor.Interrupt',
        'LoopSensor.ConsumeAbortCause',
        'LoopSensor.RollbackInterrupt',
        'LoopSensor.DropSession',
  ],
      revoke: 'LoopSensor.DropSession',
      testAnchor: 'requirements/degeneration-guard/tests/loop-sensor.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs:activeInterrupts',
    {
      owner: 'degeneration-guard',
      issuer: 'LoopSensor.Interrupt',
      key: 'SessionId with exact ProviderRunIdentity',
      rules: 'active interrupt/continuation task per exact run; removed only on matching task identity and run; reconcile transfers armed ownership into its exactly-once continuation',
      allowedConsumers: [
        'LoopSensor.Interrupt',
        'LoopSensor.RunInterrupt',
        'LoopSensor.RunContinue',
        'LoopSensor.ConsumeAbortCause',
        'LoopSensor.ClearActiveIfCurrent',
        'LoopSensor.RemoveActiveIfTask',
        'LoopSensor.RollbackInterrupt',
        'LoopSensor.ActiveInterruptTask',
        'LoopSensor.DropSession',
  ],
      revoke: 'LoopSensor.DropSession',
      testAnchor: 'requirements/degeneration-guard/tests/loop-sensor.test.mjs',
    },
  ],
])

/**
 * Pattern 1: TryTake continuation consumption.
 * Matches method names like TryTakeRecoveryPermit, TryTakeAttemptPlan,
 * TryTakePair, TryTake that return option (one-shot consumption).
 */
const TRYTAKE_PATTERN = /\b(?:member\s+(?:private\s+)?(?:_\.|this\.)\s*|let\s+(?:private\s+)?)(TryTake\w*|tryTake\w*)\s*[<(]/

/**
 * Pattern 2: Armed presence probe.
 * Matches method names like IsArmed, HasArmed, HasArmedSession, TryArm.
 */
const ARMED_PROBE_PATTERN = /\b(?:member\s+(?:private\s+)?(?:_\.|this\.)\s*|let\s+(?:private\s+)?)(?:IsArmed\w*|HasArmed\w*|TryArm\w*|IsDrainOpen\w*|HasDrainOpen\w*|IsOpen\w*)\s*[<(]/

/**
 * Pattern 3: DU await state.
 * Matches type declarations with Await/Armed/Pending-prefixed cases that
 * are used as Dictionary value types.
 */
const DU_AWAIT_CASE_PATTERN = /^\s*\|\s*(Await\w+|Armed\w*|Pending\w*)\s+of\b/

/**
 * Pattern 4: Clear/Drop presence-clearing probe.
 * Matches method names like ClearArmed, ClearRecovery, DropAttempt, DropSession
 * that clear registry presence and whose return value or side-effect drives
 * the next business effect decision (e.g. IsArmed → ClearArmed → bool → branch).
 */
const CLEAR_PRESENCE_PATTERN = /\b(?:member\s+(?:private\s+)?(?:_\.|this\.)\s*|let\s+(?:private\s+)?)(Clear(?!Session\b)\w*|Drop(?!Session\b)\w*|Consume\w*)\s*[<(]/

/**
 * Registry declaration: Dictionary, HashSet, mutable Map.empty, or ref cell.
 */
const REGISTRY_DECLARATION =
  /^\s*let\s+(?:mutable\s+)?(\w+)\s*=\s*(?:ref\b|(?:new\s+)?(?:Concurrent)?(?:Dictionary|HashSet)<|(?:Map|ResizeArray)\.empty)/

/**
 * Categorized proof annotation: must appear in the doc block preceding the declaration.
 */
const CATEGORIZED_PROOF_ANNOTATION = /DSL-cross-callback-proof:\s*physical\s+([a-z-]+)/

/**
 * Check if the preceding doc block (1-5 lines before) contains a valid categorized proof annotation.
 */
const getCategorizedProof = (lines, index) => {
  for (let j = index - 1; j >= Math.max(0, index - 5); j--) {
    const line = lines[j]
    const m = CATEGORIZED_PROOF_ANNOTATION.exec(line)
    if (m && EXEMPTION_CATEGORIES.has(m[1])) return m[1]
    // Stop at non-comment, non-blank lines
    if (line.trim() !== '' && !/^\s*\/\//.test(line) && !/^\s*\[</.test(line)) break
  }
  return null
}

const memberBlocks = (lines) => {
  const blocks = []
  for (let i = 0; i < lines.length; i++) {
    const member = /^(\s*)member\s+/.exec(lines[i])
    if (!member) continue

    const indent = member[1].length
    let end = i + 1
    while (end < lines.length) {
      const nextMember = /^(\s*)member\s+/.exec(lines[end])
      if (nextMember && nextMember[1].length <= indent) break

      const nextType = /^(\s*)(?:type|and)\s+/.exec(lines[end])
      if (nextType && nextType[1].length < indent) break
      end++
    }

    blocks.push(lines.slice(i, end).join('\n'))
  }
  return blocks
}

const referencesRegistry = (block, name) =>
  new RegExp(`\\b${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\b`).test(block)

const LET_HEADER = /^(\s*)let\s+(?:private\s+|inline\s+|rec\s+)*(?:``[^`]+``|\w+)/

/**
 * Module and type-level `let` function blocks. Only bindings at indent <= 4
 * start a block: nested `let` lines inside a function body belong to their
 * enclosing block. Registry declaration lines themselves match LET_HEADER
 * but carry no consumption-pattern name, so they never self-implicate.
 */
const letBlocks = (lines) => {
  const blocks = []
  for (let i = 0; i < lines.length; i++) {
    const header = LET_HEADER.exec(lines[i])
    if (!header || header[1].length > 4) continue
    const indent = header[1].length
    let end = i + 1
    while (end < lines.length) {
      const nextMember = /^(\s*)member\s+/.exec(lines[end])
      if (nextMember && nextMember[1].length <= indent) break
      const nextLet = LET_HEADER.exec(lines[end])
      if (nextLet && nextLet[1].length <= indent && nextLet[1].length <= 4) break
      const nextType = /^(\s*)(?:type|and)\s+/.exec(lines[end])
      if (nextType && nextType[1].length <= indent) break
      end++
    }
    blocks.push(lines.slice(i, end).join('\n'))
  }
  return blocks
}

/** Declared consumer name of an executable block: member or let binding. */
const blockConsumerName = (block) => {
  const first = block.split('\n')[0] ?? ''
  let m = /^\s*member\s+(?:private\s+)?(?:_\.|this\.)\s*(``[^`]+``|\w+)/.exec(first)
  if (m) return m[1].replace(/``/g, '')
  m = /^\s*let\s+(?:private\s+|inline\s+|rec\s+|mutable\s+)*(``[^`]+``|\w+)/.exec(first)
  if (m) return m[1].replace(/``/g, '')
  return null
}

/**
 * Every executable block that can consume a registry: type members plus
 * module and type-level `let` functions. A TryTake continuation hidden in a
 * module `let` implicates its registry exactly like a member does.
 */
const executableBlocks = (lines) => [
  ...memberBlocks(lines).map((text) => ({ name: blockConsumerName(text), text })),
  ...letBlocks(lines).map((text) => ({ name: blockConsumerName(text), text })),
]

/** Registry declarations with their preceding categorized proof, if any. */
const collectRegistries = (lines) => {
  const registries = []
  for (let i = 0; i < lines.length; i++) {
    const m = REGISTRY_DECLARATION.exec(lines[i])
    if (m) {
      registries.push({ name: m[1], line: i, category: getCategorizedProof(lines, i) })
    }
  }
  return registries
}

/** DU names with Await, Armed, or Pending-prefixed cases. */
const collectAwaitDus = (lines) => {
  const awaitDuNames = new Set()
  let currentType = null
  for (let i = 0; i < lines.length; i++) {
    const typeDecl = /^\s*(?:type|and)\s+(?:private\s+)?(\w+)\s*=/.exec(lines[i])
    if (typeDecl) {
      currentType = typeDecl[1]
      continue
    }
    if (currentType && DU_AWAIT_CASE_PATTERN.test(lines[i])) {
      awaitDuNames.add(currentType)
    }
  }
  return awaitDuNames
}

/**
 * Pattern and observed callers for one registry. A member or `let` pattern
 * only implicates the registry it actually reads or consumes. Callers are
 * the declared names of consumption-shaped blocks referencing the registry.
 */
const describeRegistry = (reg, lines, executables, awaitDuNames) => {
  const refs = (b) => referencesRegistry(b.text, reg.name)
  const consumes = (b) =>
    TRYTAKE_PATTERN.test(b.text) || ARMED_PROBE_PATTERN.test(b.text) || CLEAR_PRESENCE_PATTERN.test(b.text)
  const hasTryTake = executables.some((b) => TRYTAKE_PATTERN.test(b.text) && refs(b))
  const hasArmedProbe = executables.some((b) => ARMED_PROBE_PATTERN.test(b.text) && refs(b))
  const hasClearPresence = executables.some((b) => CLEAR_PRESENCE_PATTERN.test(b.text) && refs(b))
  const declLine = lines[reg.line]
  const valueMatch = /(?:Dictionary|Map)<[^,]+,\s*(\w+)>/.exec(declLine)
  const hasDuAwait = valueMatch && awaitDuNames.has(valueMatch[1])

  let pattern = null
  if (hasTryTake) pattern = 'trytake-continuation'
  else if (hasArmedProbe) pattern = 'armed-presence-probe'
  else if (hasClearPresence) pattern = 'clear-presence-probe'
  else if (hasDuAwait) pattern = 'du-await-state'

  const consumers = []
  if (pattern) {
    for (const b of executables) {
      if (b.name && refs(b) && consumes(b) && !consumers.includes(b.name)) consumers.push(b.name)
    }
  }
  return { pattern, consumers }
}

const REGISTRATION_FIELDS = ['owner', 'issuer', 'key', 'rules', 'revoke', 'allowedConsumers', 'testAnchor']

/**
 * An exact registration is live only when its metadata is complete, its
 * source file still declares the name, and its test anchor still exists.
 * Dead registrations (phantom file, renamed declaration, removed anchor)
 * never exempt. Paths resolve from the repository root.
 */
export const isRegistrationLive = (regKey, entry) => {
  if (!entry || typeof entry !== 'object') return false
  for (const field of REGISTRATION_FIELDS) {
    if (entry[field] === undefined || entry[field] === null || entry[field] === '') return false
  }
  if (!Array.isArray(entry.allowedConsumers) || entry.allowedConsumers.length === 0) return false
  const sep = String(regKey).lastIndexOf(':')
  if (sep < 0) return false
  const regFile = String(regKey).slice(0, sep)
  const regName = String(regKey).slice(sep + 1)
  if (!regName) return false
  try {
    const absFile = resolve(REPO_ROOT, norm(regFile))
    if (!existsSync(absFile)) return false
    const src = readFileSync(absFile, 'utf8')
    const escaped = regName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    const decl = new RegExp(`^\\s*let\\s+(?:mutable\\s+)?${escaped}\\b`)
    if (!src.split('\n').some((line) => decl.test(line))) return false
    if (!existsSync(resolve(REPO_ROOT, norm(String(entry.testAnchor))))) return false
  } catch {
    return false
  }
  return true
}

/** An observed caller is authorized by exact name or `Type.name` suffix. */
const isAllowedConsumer = (entry, consumerName) => {
  if (!consumerName) return false
  const allowed = entry.allowedConsumers ?? []
  return allowed.some((c) => c === consumerName || String(c).endsWith(`.${consumerName}`))
}

/**
 * Scan one file body for cross-callback PC patterns.
 * @returns {{ file, line, name, pattern, text }[]}
 */
export const scanText = (text, file = '<synthetic>') => {
  const lines = text.split('\n')
  const violations = []
  const executables = executableBlocks(lines)
  const registries = collectRegistries(lines)

  if (registries.length === 0) return violations

  const awaitDuNames = collectAwaitDus(lines)

  // Check each registry for pattern matches
  for (const reg of registries) {
    const fileNorm = norm(String(file))
    const regKey = `${fileNorm}:${reg.name}`
    const { pattern, consumers } = describeRegistry(reg, lines, executables, awaitDuNames)
    if (!pattern) continue

    // A narrow categorized proof positively classifies this physical cell.
    // Bare `physical` and the broad `physical resource` label never auto-pass.
    if (reg.category) continue

    // An exact registration only exempts while it is live and every observed
    // caller is allowlisted; otherwise the finding stands with its reason.
    const entry = REGISTERED_DECLARATIONS.get(regKey)
    if (entry) {
      if (!isRegistrationLive(regKey, entry)) {
        violations.push({
          file: fileNorm,
          line: reg.line + 1,
          name: reg.name,
          pattern,
          text: lines[reg.line].trim(),
          reason: 'dead-registration',
        })
        continue
      }
      if (consumers.length > 0 && !consumers.some((c) => isAllowedConsumer(entry, c))) {
        violations.push({
          file: fileNorm,
          line: reg.line + 1,
          name: reg.name,
          pattern,
          text: lines[reg.line].trim(),
          reason: 'unauthorized-consumer',
        })
        continue
      }
      continue
    }

    violations.push({
      file: fileNorm,
      line: reg.line + 1,
      name: reg.name,
      pattern,
      text: lines[reg.line].trim(),
    })
  }

  return violations
}

/**
 * Scan text and return full audit metadata: violations, exempted candidates, and coverage gaps.
 */
export const auditText = (text, file = '<synthetic>') => {
  const lines = text.split('\n')
  const violations = []
  const exempted = []
  let coverageGaps = 0
  const executables = executableBlocks(lines)
  const registries = collectRegistries(lines)
  for (let i = 0; i < lines.length; i++) {
    // Mutable Map/ref cells outside Dictionary/HashSet count towards honest coverage gap tracking
    if (/^\s*let\s+(?:mutable\s+)?\w+\s*=\s*(?:ref\b|Map\.empty)/.test(lines[i])) {
      coverageGaps++
    }
  }

  const awaitDuNames = collectAwaitDus(lines)

  for (const reg of registries) {
    const fileNorm = norm(String(file))
    const regKey = `${fileNorm}:${reg.name}`
    const { pattern, consumers } = describeRegistry(reg, lines, executables, awaitDuNames)
    if (!pattern) continue

    const entry = REGISTERED_DECLARATIONS.get(regKey)
    const live = entry && isRegistrationLive(regKey, entry)
    const authorized = !live || consumers.length === 0 || consumers.some((c) => isAllowedConsumer(entry, c))
    if (reg.category || (live && authorized)) {
      exempted.push({ file: fileNorm, line: reg.line + 1, name: reg.name, pattern, exemption: reg.category ? `categorized:${reg.category}` : 'registered' })
    } else if (entry && !live) {
      violations.push({ file: fileNorm, line: reg.line + 1, name: reg.name, pattern, text: lines[reg.line].trim(), reason: 'dead-registration' })
    } else if (entry) {
      violations.push({ file: fileNorm, line: reg.line + 1, name: reg.name, pattern, text: lines[reg.line].trim(), reason: 'unauthorized-consumer' })
    } else {
      violations.push({ file: fileNorm, line: reg.line + 1, name: reg.name, pattern, text: lines[reg.line].trim() })
    }
  }
  return { violations, exempted, coverageGaps }
}

/** @param {{ file: string, text: string }[]} entries */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    violations.push(...scanText(entry.text, entry.file))
  }
  return violations
}

/** Full audit over multiple files. */
export const auditFiles = (entries) => {
  const violations = []
  const exempted = []
  let coverageGaps = 0
  for (const entry of entries) {
    const res = auditText(entry.text, entry.file)
    violations.push(...res.violations)
    exempted.push(...res.exempted)
    coverageGaps += res.coverageGaps
  }
  return { violations, exempted, coverageGaps }
}

/** Every unproved detection is a regression. */
export const evaluateViolations = (violations) => ({
  regressions: [...violations],
  ok: violations.length === 0,
})

const runCli = () => {
  const productionFiles = walk(PRODUCTION_ROOT, ['.fs']).map(norm)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const { violations, exempted, coverageGaps } = auditFiles(entries)
  const { regressions } = evaluateViolations(violations)

  if (regressions.length > 0) {
    console.error(`cross-callback-pc: ${regressions.length} lexical violation(s) — cross-callback program-counter candidate without narrow proof`)
    for (const v of regressions) {
      console.error(`  [RED] ${v.file}:${v.line}  ${v.name} (${v.pattern}${v.reason ? `, ${v.reason}` : ''})`)
      console.error(`    ${v.text}`)
      console.error(`    Add /// DSL-cross-callback-proof: physical <category> or register declaration in REGISTERED_DECLARATIONS`)
    }
    process.exit(1)
  }

  console.log(`cross-callback-pc: OK (lexical) — ${productionFiles.length} files, zero unexempted lexical candidates (${exempted.length} exempted, ${coverageGaps} coverage gaps; static shape only, no runtime causality claimed)`)
  process.exit(0)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
