#!/usr/bin/env node
/**
 * Session-ownership ratchet (entry Playbook G9 / HOST-008).
 *
 * Fail-closed: `src/Wanxiangshu/Kernel/SessionOwnership.fs` must declare the
 * long-lived AttachmentKind surface tokens. StrengthReplica is Universal
 * ownership only — never a SatelliteKind case (see student-teacher-absence).
 *
 * Usage: node scripts/checks/session-ownership-ratchet.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
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

const runCli = () => {
  if (!existsSync(SESSION_OWNERSHIP_REL)) {
    console.error(
      `session-ownership-ratchet: required file '${SESSION_OWNERSHIP_REL}' does not exist`,
    )
    process.exit(1)
  }

  const text = readFileSync(SESSION_OWNERSHIP_REL, 'utf8')
  const { ok, missing } = scanSessionOwnership(text)

  if (ok) {
    console.log(
      `session-ownership-ratchet: OK — ${SESSION_OWNERSHIP_REL} has AttachmentKind tokens: ${REQUIRED_ATTACHMENT_TOKENS.join(', ')}`,
    )
    process.exit(0)
  }

  console.error(
    `session-ownership-ratchet: ${missing.length} required AttachmentKind token(s) missing from ${SESSION_OWNERSHIP_REL}\n`,
  )
  for (const token of missing) {
    console.error(`  missing '${token}'`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
