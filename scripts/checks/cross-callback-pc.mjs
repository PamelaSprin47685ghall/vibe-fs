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
 * physical capability/outcome. <category> should be one of EXEMPTION_CATEGORIES.
 * Bare `physical` without category is NOT an auto-pass; cells without a valid
 * category must be narrowly registered in REGISTERED_DECLARATIONS with owner,
 * issuer, key, rules, and test-anchor.
 *
 * Exemption categories (physical capabilities that are NOT program counters):
 *  pty, timer, waiter, single-flight, quiescence-permit, process-handle,
 *  socket, cancellation-token, resource (backward compat)
 *
 * Legal reference: SessionQuiescenceGate.fs — process-local side-effect
 * admission gate. ObserveIdle returns opaque QuiescencePermit; TryConsume(permit)
 * checks state == Idle(permit.AttemptSerial). The permit is an unforgeable
 * typed capability, not a presence probe. Restart clears the gate (HOST-007).
 *
 * Every detected cell without a proof annotation is RED. There is no debt
 * baseline or ceiling: ownership evidence is required at the declaration.
 */

import { readFileSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'
const norm = (p) => p.replace(/\\/g, '/')

/**
 * Physical capability exemption categories.
 * A proof annotation should reference one of these categories to whitelist
 * a mutable/registry value as an opaque physical capability/outcome.
 * Backward compat: bare 'physical' without category is still accepted.
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
  'resource',
])

/**
 * Narrow declaration-symbol exemptions.
 * For declarations that lack a category annotation or require explicit
 * registration of owner, issuer, key identity, and test anchor.
 * Path or basename alone is never an authorization.
 */
export const REGISTERED_DECLARATIONS = new Map([
  [
    'src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs:pendingAttemptPlans',
    {
      owner: 'managed-chat-execution',
      issuer: 'PluginRecoveryScope.RecordPendingAttemptPlan',
      key: 'SessionId * PhysicalUserMessageId',
      rules: 'single-flight admission; consume on provider run bind',
      allowedConsumers: ['PluginRecoveryScope.TryBindPendingAttemptPlan', 'PluginRecoveryScope.ClearPendingAttemptPlan'],
      testAnchor: 'requirements/structured-workflow/tests/direct-ce-contract.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Context/Companion/Blogger/OpenCode/PluginScope.fs:pendingOffer',
    {
      owner: 'blogger-companion',
      issuer: 'BloggerPluginScope.SetPendingOffer',
      key: 'SessionId',
      rules: 'single-slot inbound buffer owned by Blogger convergence; consumed by TryTakePendingOffer',
      allowedConsumers: ['BloggerPluginScope.TryTakePendingOffer', 'BloggerPluginScope.ClearSession'],
      testAnchor: 'requirements/structured-workflow/tests/cross-callback-pc.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Store.fs:deletedInspectorsByOwnerScope',
    {
      owner: 'sync-delegate-execution',
      issuer: 'Store.stageDeletedChild',
      key: 'SessionId (owner scope)',
      rules: 'retired child identity retained only for draft/session cleanup',
      allowedConsumers: ['Store.commitDeletedChild', 'Store.clearOwnerScope'],
      testAnchor: 'requirements/structured-workflow/tests/cross-callback-pc.test.mjs',
    },
  ],
  [
    'src/Wanxiangshu/Process/PtyManager.fs:ptyHandles',
    {
      owner: 'process-execution',
      issuer: 'PtyManager.CreateHandle',
      key: 'SessionId',
      rules: 'PTY process handle registry; consumed by TryTakeHandle',
      allowedConsumers: ['PtyManager.TryTakeHandle'],
      testAnchor: 'requirements/structured-workflow/tests/cross-callback-pc.test.mjs',
    },
  ],
])

/**
 * Pattern 1: TryTake continuation consumption.
 * Matches method names like TryTakeRecoveryPermit, TryTakeAttemptPlan,
 * TryTakePair, TryTake that return option (one-shot consumption).
 */
const TRYTAKE_PATTERN = /\b(?:member\s+(?:_\.|this\.)\s*|let\s+(?:private\s+)?)(TryTake\w*|tryTake\w*)\s*[<(]/

/**
 * Pattern 2: Armed presence probe.
 * Matches method names like IsArmed, HasArmed, HasArmedSession, TryArm.
 */
const ARMED_PROBE_PATTERN = /\b(?:member\s+(?:_\.|this\.)\s*|let\s+(?:private\s+)?)(?:IsArmed\w*|HasArmed\w*|TryArm\w*|IsDrainOpen\w*|HasDrainOpen\w*|IsOpen\w*)\s*[<(]/

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
const CLEAR_PRESENCE_PATTERN = /\b(?:member\s+(?:_\.|this\.)\s*|let\s+(?:private\s+)?)(Clear(?!Session\b)\w*|Drop(?!Session\b)\w*|Consume\w*)\s*[<(]/

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

/**
 * Scan one file body for cross-callback PC patterns.
 * @returns {{ file, line, name, pattern, text }[]}
 */
export const scanText = (text, file = '<synthetic>') => {
  const lines = text.split('\n')
  const violations = []
  const members = memberBlocks(lines)

  // Collect registry declarations
  const registries = []
  for (let i = 0; i < lines.length; i++) {
    const m = REGISTRY_DECLARATION.exec(lines[i])
    if (m) {
      registries.push({ name: m[1], line: i, category: getCategorizedProof(lines, i) })
    }
  }

  if (registries.length === 0) return violations

  // Collect DU await types (cases named Await*/Armed/Pending*)
  const awaitDuNames = new Set()
  let currentType = null
  for (let i = 0; i < lines.length; i++) {
    const typeDecl = /^\s*(?:type|and)\s+(?:private\s+)?(\w+)\s*=/.exec(lines[i])
    if (typeDecl) {
      if (currentType && awaitDuNames.size > 0) {
        // currentType had await cases
      }
      currentType = typeDecl[1]
      // Check if this type is used as a Dictionary value type
      continue
    }
    if (currentType && DU_AWAIT_CASE_PATTERN.test(lines[i])) {
      awaitDuNames.add(currentType)
    }
  }

  // Check each registry for pattern matches
  for (const reg of registries) {
    const fileNorm = norm(String(file))
    const regKey = `${fileNorm}:${reg.name}`
    const isRegistered = REGISTERED_DECLARATIONS.has(regKey)

    // If a valid categorized proof annotation or narrow registered declaration exists,
    // this physical cell is positively classified. Bare 'physical' without category or
    // registration is NOT an auto-pass.
    if (reg.category || isRegistered) continue

    // A member or helper pattern only implicates the registry it actually reads/consumes.
    const hasTryTake = members.some((block) => TRYTAKE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const hasArmedProbe = members.some((block) => ARMED_PROBE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const hasClearPresence = members.some((block) => CLEAR_PRESENCE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    // Check for DU await state: registry value type is an await DU
    const declLine = lines[reg.line]
    const valueMatch = /(?:Dictionary|Map)<[^,]+,\s*(\w+)>/.exec(declLine)
    const hasDuAwait = valueMatch && awaitDuNames.has(valueMatch[1])

    // Determine which pattern matched
    let pattern = null
    if (hasTryTake) pattern = 'trytake-continuation'
    else if (hasArmedProbe) pattern = 'armed-presence-probe'
    else if (hasClearPresence) pattern = 'clear-presence-probe'
    else if (hasDuAwait) pattern = 'du-await-state'

    if (!pattern) continue

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
  const members = memberBlocks(lines)

  const registries = []
  for (let i = 0; i < lines.length; i++) {
    const m = REGISTRY_DECLARATION.exec(lines[i])
    if (m) {
      registries.push({ name: m[1], line: i, category: getCategorizedProof(lines, i) })
    }
    // Mutable Map/ref cells outside Dictionary/HashSet count towards honest coverage gap tracking
    if (/^\s*let\s+(?:mutable\s+)?\w+\s*=\s*(?:ref\b|Map\.empty)/.test(lines[i])) {
      coverageGaps++
    }
  }

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

  for (const reg of registries) {
    const fileNorm = norm(String(file))
    const regKey = `${fileNorm}:${reg.name}`
    const isRegistered = REGISTERED_DECLARATIONS.has(regKey)

    const hasTryTake = members.some((block) => TRYTAKE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const hasArmedProbe = members.some((block) => ARMED_PROBE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const hasClearPresence = members.some((block) => CLEAR_PRESENCE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const declLine = lines[reg.line]
    const valueMatch = /(?:Dictionary|Map)<[^,]+,\s*(\w+)>/.exec(declLine)
    const hasDuAwait = valueMatch && awaitDuNames.has(valueMatch[1])

    let pattern = null
    if (hasTryTake) pattern = 'trytake-continuation'
    else if (hasArmedProbe) pattern = 'armed-presence-probe'
    else if (hasClearPresence) pattern = 'clear-presence-probe'
    else if (hasDuAwait) pattern = 'du-await-state'

    if (!pattern) continue

    if (reg.category || isRegistered) {
      exempted.push({ file: fileNorm, line: reg.line + 1, name: reg.name, pattern, exemption: reg.category ? `categorized:${reg.category}` : 'registered' })
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
    console.error(`cross-callback-pc: ${regressions.length} violation(s) — cross-callback program counter without proof`)
    for (const v of regressions) {
      console.error(`  [RED] ${v.file}:${v.line}  ${v.name} (${v.pattern})`)
      console.error(`    ${v.text}`)
      console.error(`    Add /// DSL-cross-callback-proof: physical <category> or register declaration in REGISTERED_DECLARATIONS`)
    }
    process.exit(1)
  }

  console.log(`cross-callback-pc: OK — ${productionFiles.length} files, zero unexempted candidates (${exempted.length} exempted, ${coverageGaps} coverage gaps)`)
  process.exit(0)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
