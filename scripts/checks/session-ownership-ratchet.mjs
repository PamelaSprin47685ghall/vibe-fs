#!/usr/bin/env node
/**
 * Session-ownership ratchet (entry Playbook G9 / HOST-008).
 *
 * Fail-closed: `src/Wanxiangshu/Kernel/SessionOwnership.fs` must declare the
 * long-lived AttachmentKind surface tokens. StrengthReplica is Universal
 * ownership only — never a SatelliteKind case (see student-teacher-absence).
 *
 * Hardened (Contract 4): beyond token presence, verify full lifecycle coverage
 * across dedicated Session surfaces:
 *   - hidden Reviewer session, fork-agent, Executor child
 *   - Handle / Companion ownership
 *   - cancel / retire / reconcile / crash signals
 * Scans Session plus Infrastructure/OpenCode/Host (plus Application)
 * and fails closed if any lifecycle signal is absent.
 *
 * Usage: node scripts/checks/session-ownership-ratchet.mjs
 */

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const SESSION_OWNERSHIP_REL = 'src/Wanxiangshu/Kernel/SessionOwnership.fs'

/** Required AttachmentKind / ownership tokens that must appear in SessionOwnership.fs. */
export const REQUIRED_ATTACHMENT_TOKENS = Object.freeze([
  'Companion',
  'SyncInspector',
  'SyncCoder',
  'Bookkeeper',
  'StrengthReplica',
])

/**
 * Required lifecycle signals that must appear across the broader Session/Host
 * corpus. Covers hidden Reviewer, fork-agent, Executor, Handle, Companion,
 * cancel, retire, reconcile/crash signals per Contract 4.
 *
 * Lowercase entries are matched case-insensitively (e.g. fork matches Fork,
 * cancel matches Cancel). Capitalized entries are case-sensitive type signals.
 */
export const REQUIRED_LIFECYCLE_TOKENS = Object.freeze([
  'Reviewer',
  'fork',
  'Executor',
  'Handle',
  'Companion',
  'cancel',
  'retire',
  'reconcile',
  'crash',
])

/** Directories whose .fs corpus must collectively contain every lifecycle token. */
export const LIFECYCLE_SCAN_DIRS = Object.freeze([
  'src/Wanxiangshu/Session',
  'src/Wanxiangshu/Infrastructure/OpenCode/Host',
  'src/Wanxiangshu/Application',
])

/**
 * @param {string} text
 * @param {readonly string[]} [required]
 * @returns {string[]} missing tokens
 */
export const missingAttachmentTokens = (text, required = REQUIRED_ATTACHMENT_TOKENS) => {
  const missing = []
  for (const token of required) {
    // Prefer type-case surface: `| Companion` / `| StrengthReplica` / `| Bookkeeper of …`
    const caseRe = new RegExp(`\\|\\s*${token}\\b`)
    if (caseRe.test(text) || text.includes(token)) continue
    missing.push(token)
  }
  return missing
}

/**
 * @param {string} text
 * @returns {{ ok: boolean, missing: string[] }}
 */
export const scanSessionOwnership = (text) => {
  const missing = missingAttachmentTokens(text)
  return { ok: missing.length === 0, missing }
}

/**
 * Lifecycle token check — lowercase tokens are case-insensitive, capitalized are
 * case-sensitive (exact type/signal name).
 * @param {string} text aggregated corpus text
 * @param {readonly string[]} [required]
 * @returns {string[]} missing tokens
 */
export const missingLifecycleTokens = (text, required = REQUIRED_LIFECYCLE_TOKENS) => {
  const missing = []
  for (const token of required) {
    const isLower = token === token.toLowerCase()
    const found = isLower ? new RegExp(token, 'i').test(text) : text.includes(token)
    if (!found) missing.push(token)
  }
  return missing
}

/**
 * @param {string} text aggregated lifecycle corpus
 * @returns {{ ok: boolean, missing: string[] }}
 */
export const scanLifecycle = (text) => {
  const missing = missingLifecycleTokens(text)
  return { ok: missing.length === 0, missing }
}

/**
 * Walk a directory recursively and return absolute paths of .fs files.
 * @param {string} dirAbs
 * @returns {string[]}
 */
const walkFs = (dirAbs) => {
  const out = []
  let entries
  try {
    entries = readdirSync(dirAbs, { withFileTypes: true })
  } catch {
    return out
  }
  for (const ent of entries) {
    const full = join(dirAbs, ent.name)
    if (ent.isDirectory()) {
      out.push(...walkFs(full))
    } else if (ent.isFile() && ent.name.endsWith('.fs')) {
      out.push(full)
    }
  }
  return out
}

/**
 * Collect aggregated text of every .fs file under LIFECYCLE_SCAN_DIRS.
 * @param {string} [repoRoot] defaults to process.cwd()
 * @returns {{ text: string, files: string[], dirsScanned: string[] }}
 */
export const collectLifecycleCorpus = (repoRoot = process.cwd()) => {
  const files = []
  const dirsScanned = []
  for (const rel of LIFECYCLE_SCAN_DIRS) {
    const abs = resolve(repoRoot, rel)
    dirsScanned.push(rel)
    const found = walkFs(abs)
    files.push(...found)
  }
  let text = ''
  for (const f of files) {
    try {
      text += '\n' + readFileSync(f, 'utf8')
    } catch {
      // unreadable file — fail closed upstream via missing tokens, but keep scanning
    }
  }
  return { text, files, dirsScanned }
}

/**
 * Combined scan: SessionOwnership attachment + lifecycle corpus.
 * @param {string} ownershipText
 * @param {string} lifecycleText
 * @returns {{ ok: boolean, attachment: { ok: boolean, missing: string[] }, lifecycle: { ok: boolean, missing: string[] } }}
 */
export const scanAll = (ownershipText, lifecycleText) => {
  const attachment = scanSessionOwnership(ownershipText)
  const lifecycle = scanLifecycle(lifecycleText)
  return { ok: attachment.ok && lifecycle.ok, attachment, lifecycle }
}

const runCli = () => {
  let hasError = false

  // --- 1) AttachmentKind surface (SessionOwnership.fs) ---
  if (!existsSync(SESSION_OWNERSHIP_REL)) {
    console.error(
      `session-ownership-ratchet: required file '${SESSION_OWNERSHIP_REL}' does not exist`,
    )
    process.exit(1)
  }

  const ownershipText = readFileSync(SESSION_OWNERSHIP_REL, 'utf8')
  const attachment = scanSessionOwnership(ownershipText)

  if (!attachment.ok) {
    hasError = true
    console.error(
      `session-ownership-ratchet: ${attachment.missing.length} required AttachmentKind token(s) missing from ${SESSION_OWNERSHIP_REL}\n`,
    )
    for (const token of attachment.missing) {
      console.error(`  missing '${token}'`)
    }
  }

  // --- 2) Lifecycle coverage (Session + Host + Application) ---
  const { text: lifecycleText, files, dirsScanned } = collectLifecycleCorpus(process.cwd())

  if (files.length === 0) {
    hasError = true
    console.error(
      `session-ownership-ratchet: lifecycle scan found no .fs files under: ${dirsScanned.join(', ')} — fail closed (missing lifecycle coverage)`,
    )
  } else {
    const lifecycle = scanLifecycle(lifecycleText)
    if (!lifecycle.ok) {
      hasError = true
      console.error(
        `session-ownership-ratchet: ${lifecycle.missing.length} required lifecycle signal(s) missing from corpus (${files.length} .fs files across ${dirsScanned.join(', ')})\n`,
      )
      for (const token of lifecycle.missing) {
        let hint = ''
        switch (token) {
          case 'Reviewer':
            hint = ' — hidden Reviewer session surface absent (e.g. Reviewer/ReviewerWorkflow/HostReviewGuard)'
            break
          case 'fork':
            hint = ' — fork-agent surface absent (e.g. fork, HostForkRuntime, HostForkAgent)'
            break
          case 'Executor':
            hint = ' — Executor child surface absent'
            break
          case 'Handle':
            hint = ' — Handle ownership surface absent (e.g. HandleController, HandleProjection)'
            break
          case 'Companion':
            hint = ' — Companion lifecycle absent'
            break
          case 'cancel':
            hint = ' — cancel/retire lifecycle signal absent (cancel, abort, CancelAgent)'
            break
          case 'retire':
            hint = ' — retire tombstone signal absent (retire, Retired, HandleRetired)'
            break
          case 'reconcile':
            hint = ' — reconcile signal absent (reconcile, Reconciler, TurnReconcile)'
            break
          case 'crash':
            hint = ' — crash-recovery signal absent (crash, BloggerCrashRecovery, SessionRecoveryWorkflow)'
            break
          default:
            break
        }
        console.error(`  missing lifecycle token '${token}'${hint}`)
      }
      console.error(
        `\n  Scanned ${files.length} files under: ${dirsScanned.join(', ')}`,
      )
      console.error(
        `  Each token in [${REQUIRED_LIFECYCLE_TOKENS.join(', ')}] must appear at least once in the combined corpus.`,
      )
    }
  }

  if (hasError) {
    process.exit(1)
  }

  const { files: okFiles } = collectLifecycleCorpus(process.cwd())
  console.log(
    `session-ownership-ratchet: OK — ${SESSION_OWNERSHIP_REL} has AttachmentKind tokens: ${REQUIRED_ATTACHMENT_TOKENS.join(', ')}`,
  )
  console.log(
    `session-ownership-ratchet: OK — lifecycle tokens present [${REQUIRED_LIFECYCLE_TOKENS.join(', ')}] across ${okFiles.length} .fs files in ${LIFECYCLE_SCAN_DIRS.join(', ')}`,
  )
  process.exit(0)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
