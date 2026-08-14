#!/usr/bin/env node
/**
 * Session-ownership ratchet (entry Playbook G9 / HOST-008 / §24.4).
 *
 * This script is the §24.4 questionnaire, not full G9. Symbol ratchet,
 * storage ratchet, and capability-isomorphism ratchet already exist as
 * separate gates. A green run here is still a smoke check — do not claim
 * release-close.
 *
 * Fail-closed:
 *   1. `src/Wanxiangshu/Kernel/SessionOwnership.fs` must declare the
 *      long-lived AttachmentKind surface tokens (Companion, SyncInspector,
 *      SyncCoder, Bookkeeper, StrengthReplica). StrengthReplica is Universal
 *      ownership only — never a SatelliteKind case.
 *   2. Every managed session kind in the §24.4 matrix must answer:
 *      owner, reusable, cancel, retire, handle, companion, crashReconcile,
 *      evidencePath. Missing kind, empty field, missing evidence file, or
 *      evidence file without the related token → exit 1.
 *
 * Bookkeeper live session runtime is owned by G6. evidencePath points at
 * src/Wanxiangshu/Infrastructure/BookkeeperRuntime.fs (bindSession /
 * AttachmentKind.Bookkeeper txId). SessionOwnership.fs remains the
 * AttachmentKind surface ratchet. This questionnaire is still not full G9.
 *
 * Usage: node scripts/checks/session-ownership-ratchet.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url))

export const SESSION_OWNERSHIP_REL = 'src/Wanxiangshu/Execution/Session/Ownership.fs'
export const SESSION_OWNERSHIP_MATRIX_REL = 'scripts/checks/session-ownership-matrix.json'

/** Required AttachmentKind / ownership tokens that must appear in SessionOwnership.fs. */
export const REQUIRED_ATTACHMENT_TOKENS = Object.freeze([
  'Companion',
  'SyncInspector',
  'SyncCoder',
  'Bookkeeper',
  'StrengthReplica',
])

/**
 * Playbook §24.4 managed session kinds. Closed set: matrix must answer each
 * exactly; extra kinds fail closed so undocumented kinds cannot sneak in.
 */
export const REQUIRED_KINDS = Object.freeze([
  'Companion',
  'SyncInspector',
  'SyncCoder',
  'Bookkeeper',
  'hidden Reviewer',
  'StrengthReplica',
  'fork agent',
  'Distiller child',
])

/** Structured questionnaire fields. Every value must be a non-empty string. */
export const MATRIX_FIELDS = Object.freeze([
  'owner',
  'reusable',
  'cancel',
  'retire',
  'handle',
  'companion',
  'crashReconcile',
  'evidencePath',
])

const SPECIAL_PLEADING_RE = /this one is special/i

/**
 * Token that evidencePath must contain. Kind ids with spaces map onto the
 * type/signal the production file actually spells.
 * @param {string} kind
 */
export const relatedEvidenceToken = (kind) => {
  switch (kind) {
    case 'hidden Reviewer':
      return 'Reviewer'
    case 'fork agent':
      return 'Fork'
    case 'Distiller child':
      return 'Distiller'
    default:
      return kind
  }
}

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

const posix = (p) => p.replaceAll('\\', '/')

const isNonEmptyString = (value) => typeof value === 'string' && value.trim().length > 0

const failure = (code, message, extra = {}) => ({ code, message, ...extra })

/**
 * Parse and validate a matrix document (already-parsed JSON object).
 * `readFile` / `exists` are injectable so tests can use a fixture root.
 *
 * @param {unknown} matrix
 * @param {{ repoRoot?: string, existsSync?: (p: string) => boolean, readFileSync?: (p: string, enc: string) => string }} [opts]
 * @returns {{ ok: boolean, failures: object[] }}
 */
export const scanMatrix = (matrix, opts = {}) => {
  const repoRoot = opts.repoRoot ?? process.cwd()
  const exists = opts.existsSync ?? existsSync
  const read = opts.readFileSync ?? readFileSync
  const failures = []

  if (matrix === null || typeof matrix !== 'object' || Array.isArray(matrix)) {
    return { ok: false, failures: [failure('invalid-matrix', 'matrix must be a JSON object')] }
  }

  const kinds = matrix.kinds
  if (kinds === null || typeof kinds !== 'object' || Array.isArray(kinds)) {
    return { ok: false, failures: [failure('invalid-matrix', "matrix missing object field 'kinds'")] }
  }

  for (const kind of REQUIRED_KINDS) {
    if (!Object.prototype.hasOwnProperty.call(kinds, kind)) {
      failures.push(failure('missing-kind', `missing kind '${kind}'`, { kind }))
    }
  }

  for (const kind of Object.keys(kinds)) {
    if (!REQUIRED_KINDS.includes(kind)) {
      failures.push(failure('unexpected-kind', `unexpected kind '${kind}'`, { kind }))
    }
  }

  for (const kind of REQUIRED_KINDS) {
    if (!Object.prototype.hasOwnProperty.call(kinds, kind)) continue
    const row = kinds[kind]
    if (row === null || typeof row !== 'object' || Array.isArray(row)) {
      failures.push(failure('invalid-matrix', `kind '${kind}' must be an object`, { kind }))
      continue
    }

    for (const field of MATRIX_FIELDS) {
      const value = row[field]
      if (!isNonEmptyString(value)) {
        failures.push(
          failure('empty-field', `kind '${kind}' empty field '${field}'`, { kind, field }),
        )
        continue
      }
      if (SPECIAL_PLEADING_RE.test(value)) {
        failures.push(
          failure(
            'special-pleading',
            `kind '${kind}' field '${field}' answers "this one is special"`,
            { kind, field },
          ),
        )
      }
    }

    const evidencePath = row.evidencePath
    if (!isNonEmptyString(evidencePath)) continue

    const normalized = posix(evidencePath.trim())
    if (normalized.includes('..') || isAbsolute(evidencePath) || !normalized.startsWith('src/')) {
      failures.push(
        failure(
          'evidence-not-src',
          `kind '${kind}' evidencePath must be a repo-relative src/ file: ${evidencePath}`,
          { kind, field: 'evidencePath' },
        ),
      )
      continue
    }

    const abs = resolve(repoRoot, normalized.split('/').join(sep))
    const rel = posix(relative(repoRoot, abs))
    if (rel.startsWith('..')) {
      failures.push(
        failure(
          'evidence-not-src',
          `kind '${kind}' evidencePath escapes repo root: ${evidencePath}`,
          { kind, field: 'evidencePath' },
        ),
      )
      continue
    }

    if (!exists(abs)) {
      failures.push(
        failure(
          'missing-evidence-file',
          `kind '${kind}' evidencePath file missing: ${normalized}`,
          { kind, field: 'evidencePath', evidencePath: normalized },
        ),
      )
      continue
    }

    let text = ''
    try {
      text = read(abs, 'utf8')
    } catch {
      failures.push(
        failure(
          'missing-evidence-file',
          `kind '${kind}' evidencePath unreadable: ${normalized}`,
          { kind, field: 'evidencePath', evidencePath: normalized },
        ),
      )
      continue
    }

    const token = relatedEvidenceToken(kind)
    if (!text.includes(token)) {
      failures.push(
        failure(
          'missing-evidence-token',
          `kind '${kind}' evidencePath '${normalized}' lacks related token '${token}'`,
          { kind, field: 'evidencePath', token, evidencePath: normalized },
        ),
      )
    }
  }

  return { ok: failures.length === 0, failures }
}

/**
 * @param {string} [absPath]
 * @returns {{ ok: boolean, matrix?: object, error?: string }}
 */
export const loadMatrixFile = (absPath = resolve(process.cwd(), SESSION_OWNERSHIP_MATRIX_REL)) => {
  if (!existsSync(absPath)) {
    return { ok: false, error: `matrix file missing: ${absPath}` }
  }
  let raw
  try {
    raw = readFileSync(absPath, 'utf8')
  } catch (err) {
    return { ok: false, error: `matrix file unreadable: ${absPath} (${err.message})` }
  }
  try {
    return { ok: true, matrix: JSON.parse(raw) }
  } catch (err) {
    return { ok: false, error: `matrix JSON invalid: ${absPath} (${err.message})` }
  }
}

/**
 * Combined scan: AttachmentKind surface + §24.4 matrix.
 * @param {string} [repoRoot]
 * @returns {{ ok: boolean, attachment: { ok: boolean, missing: string[] }, matrix: { ok: boolean, failures: object[] }, matrixPath: string }}
 */
export const scanRepo = (repoRoot = process.cwd()) => {
  const ownershipAbs = resolve(repoRoot, SESSION_OWNERSHIP_REL)
  const matrixAbs = resolve(repoRoot, SESSION_OWNERSHIP_MATRIX_REL)
  let attachment
  if (!existsSync(ownershipAbs)) {
    attachment = { ok: false, missing: [`<file missing: ${SESSION_OWNERSHIP_REL}>`] }
  } else {
    attachment = scanSessionOwnership(readFileSync(ownershipAbs, 'utf8'))
  }

  const loaded = loadMatrixFile(matrixAbs)
  const matrix = loaded.ok
    ? scanMatrix(loaded.matrix, { repoRoot })
    : { ok: false, failures: [failure('missing-matrix', loaded.error)] }

  return {
    ok: attachment.ok && matrix.ok,
    attachment,
    matrix,
    matrixPath: posix(relative(repoRoot, matrixAbs)) || SESSION_OWNERSHIP_MATRIX_REL,
  }
}

const defaultMatrixPath = () => {
  const fromCwd = resolve(process.cwd(), SESSION_OWNERSHIP_MATRIX_REL)
  if (existsSync(fromCwd)) return fromCwd
  return join(SCRIPT_DIR, 'session-ownership-matrix.json')
}

const runCli = () => {
  let hasError = false

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

  const matrixAbs = defaultMatrixPath()
  const loaded = loadMatrixFile(matrixAbs)
  if (!loaded.ok) {
    hasError = true
    console.error(`session-ownership-ratchet: ${loaded.error}`)
  } else {
    const matrix = scanMatrix(loaded.matrix, { repoRoot: process.cwd() })
    if (!matrix.ok) {
      hasError = true
      console.error(
        `session-ownership-ratchet: ${matrix.failures.length} §24.4 matrix failure(s) in ${SESSION_OWNERSHIP_MATRIX_REL}\n`,
      )
      for (const item of matrix.failures) {
        console.error(`  ${item.code}: ${item.message}`)
      }
    }
  }

  if (hasError) {
    process.exit(1)
  }

  console.log(
    `session-ownership-ratchet: OK — ${SESSION_OWNERSHIP_REL} has AttachmentKind tokens: ${REQUIRED_ATTACHMENT_TOKENS.join(', ')}`,
  )
  console.log(
    `session-ownership-ratchet: OK — §24.4 questionnaire answered for [${REQUIRED_KINDS.join(', ')}] via ${SESSION_OWNERSHIP_MATRIX_REL}`,
  )
  console.log(
    'session-ownership-ratchet: note — this is the §24.4 smoke questionnaire, not full G9 release-close (symbol/storage/capability ratchets are separate).',
  )
  process.exit(0)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
