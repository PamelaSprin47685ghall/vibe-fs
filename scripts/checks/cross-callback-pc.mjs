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
 * Backward compat: bare `physical` without category is still accepted.
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
 * Baseline: known debt entries (file::name) are allowed but reported; new debt
 * not in baseline and without proof annotation is RED. BASELINE_MAX_SIZE is a
 * ratchet — it only decreases as debt items are fixed, never increases.
 */

import { readFileSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'
const norm = (p) => p.replace(/\\/g, '/')

/**
 * Known debt baseline: each entry is `file::variableName`.
 * These are the existing cross-callback PC patterns identified in the
 * 2026-08-18 obligation account. They are allowed (reported but not RED)
 * until fixed; any NEW pattern not listed here is RED.
 */
export const KNOWN_DEBT_BASELINE = new Set([
  // LoopSensor — armed
  'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs::armed',
  // NeedHelpSensor — armed
  'src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs::armed',
])

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
 * Baseline ratchet ceiling: KNOWN_DEBT_BASELINE.size must never exceed this.
 * When a debt item is fixed, remove it from KNOWN_DEBT_BASELINE and decrease
 * BASELINE_MAX_SIZE by 1. Increasing BASELINE_MAX_SIZE is a ratchet violation
 * (VERIFICATION-SYSTEM-010: acceptance criteria only tighten).
 */
export const BASELINE_MAX_SIZE = 2

/**
 * Pattern 1: TryTake continuation consumption.
 * Matches method names like TryTakeRecoveryPermit, TryTakeAttemptPlan,
 * TryTakePair, TryTake that return option (one-shot consumption).
 */
const TRYTAKE_PATTERN = /\bmember\s+(?:_\.|this\.)\s*(TryTake\w*)\s*[<(]/

/**
 * Pattern 2: Armed presence probe.
 * Matches method names like IsArmed, HasArmed, HasArmedSession, TryArm.
 */
const ARMED_PROBE_PATTERN = /\bmember\s+(?:_\.|this\.)\s*(?:IsArmed|HasArmed\w*|TryArm)\s*[<(]/

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
const CLEAR_PRESENCE_PATTERN = /\bmember\s+(?:_\.|this\.)\s*(Clear\w*|Drop\w*)\s*[<(]/

/**
 * Registry declaration: Dictionary or HashSet with DSL-MUTABLE annotation.
 */
const REGISTRY_DECLARATION =
  /^\s*let\s+(\w+)\s*=\s*(?:new\s+)?(?:Concurrent)?(?:Dictionary|HashSet)</

/**
 * Proof annotation: must appear in the doc block preceding the declaration.
 */
const PROOF_ANNOTATION = /DSL-cross-callback-proof:\s*physical/

/**
 * Check if the preceding doc block (1-5 lines before) contains the proof annotation.
 */
const hasProofAnnotation = (lines, index) => {
  for (let j = index - 1; j >= Math.max(0, index - 5); j--) {
    const line = lines[j]
    if (PROOF_ANNOTATION.test(line)) return true
    // Stop at non-comment, non-blank lines
    if (line.trim() !== '' && !/^\s*\/\//.test(line) && !/^\s*\[</.test(line)) break
  }
  return false
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
      registries.push({ name: m[1], line: i, hasProof: hasProofAnnotation(lines, i) })
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
    const debtKey = `${fileNorm}::${reg.name}`

    // If proof annotation exists, this is whitelisted
    if (reg.hasProof) continue

    // A member pattern only implicates the registry it actually reads/consumes.
    const hasTryTake = members.some((block) => TRYTAKE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const hasArmedProbe = members.some((block) => ARMED_PROBE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    const hasClearPresence = members.some((block) => CLEAR_PRESENCE_PATTERN.test(block) && referencesRegistry(block, reg.name))
    // Check for DU await state: registry value type is an await DU
    const declLine = lines[reg.line]
    const valueMatch = /Dictionary<[^,]+,\s*(\w+)>/.exec(declLine)
    const hasDuAwait = valueMatch && awaitDuNames.has(valueMatch[1])

    // Determine which pattern matched
    let pattern = null
    if (hasTryTake) pattern = 'trytake-continuation'
    else if (hasArmedProbe) pattern = 'armed-presence-probe'
    else if (hasClearPresence) pattern = 'clear-presence-probe'
    else if (hasDuAwait) pattern = 'du-await-state'

    if (!pattern) continue

    // Check if this is known debt (baseline)
    const isKnownDebt = KNOWN_DEBT_BASELINE.has(debtKey)

    violations.push({
      file: fileNorm,
      line: reg.line + 1,
      name: reg.name,
      pattern,
      text: lines[reg.line].trim(),
      knownDebt: isKnownDebt,
    })
  }

  return violations
}

/** @param {{ file: string, text: string }[]} entries */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    violations.push(...scanText(entry.text, entry.file))
  }
  return violations
}

/**
 * Evaluate violations against baseline.
 * Known debt is reported but not RED; new debt is RED.
 */
export const evaluateViolations = (violations) => {
  const regressions = violations.filter((v) => !v.knownDebt)
  const knownDebt = violations.filter((v) => v.knownDebt)
  return { regressions, knownDebt, ok: regressions.length === 0 }
}

const runCli = () => {
  const productionFiles = walk(PRODUCTION_ROOT, ['.fs']).map(norm)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const violations = scanFiles(entries)
  const { regressions, knownDebt, ok } = evaluateViolations(violations)

  if (knownDebt.length > 0) {
    console.error(`cross-callback-pc: ${knownDebt.length} known debt entry(s) (baseline — fix to remove)`)
    for (const v of knownDebt) {
      console.error(`  [KNOWN] ${v.file}:${v.line}  ${v.name} (${v.pattern})`)
    }
    console.error('')
  }

  if (regressions.length > 0) {
    console.error(`cross-callback-pc: ${regressions.length} NEW violation(s) — cross-callback program counter without proof`)
    for (const v of regressions) {
      console.error(`  [RED] ${v.file}:${v.line}  ${v.name} (${v.pattern})`)
      console.error(`    ${v.text}`)
      console.error(`    Add /// DSL-cross-callback-proof: physical or refactor to owning CE`)
    }
    process.exit(1)
  }

  if (knownDebt.length > 0) {
    console.log(`cross-callback-pc: OK with ${knownDebt.length} known debt (baseline ratchet)`)
  } else {
    console.log(`cross-callback-pc: OK — ${productionFiles.length} files, zero cross-callback PC`)
  }
  process.exit(0)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
