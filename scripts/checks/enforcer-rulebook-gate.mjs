#!/usr/bin/env node
/**
 * Enforcer rulebook authoring gate (folder SSOT).
 *
 * Fail-closed structural scan of `resources/enforcer/`:
 *   - root has only rule directories (no catalog.json, no loose files)
 *   - exactly EXPECTED_RULE_COUNT kebab-case directories
 *   - each dir contains exactly enforcer.md + main.md (no third entry)
 *   - both files are nonempty UTF-8 text with at least one `#` title line
 *   - optional constitution body headings (`requireHeadings` / `--require-headings`):
 *       enforcer.md → ## Definition / ## Trigger When / ## Nudge
 *       main.md     → ## What To Do Now / ## Verification / ## Done When
 *
 * Usage:
 *   node scripts/checks/enforcer-rulebook-gate.mjs
 *   node scripts/checks/enforcer-rulebook-gate.mjs --require-headings
 *
 * check.mjs enables --require-headings once all 120 tips carry constitution headings.
 */

import {
  existsSync,
  readdirSync,
  readFileSync,
  statSync,
} from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export const RULEBOOK_REL = 'resources/enforcer'
export const EXPECTED_RULE_COUNT = 120
export const REQUIRED_FILES = Object.freeze(['enforcer.md', 'main.md'])

/** TipName directory: lowercase alphanumerics separated by single hyphens. */
export const KEBAB_CASE_RE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

/** At least one markdown ATX title line (`# …`) with non-whitespace after `#`. */
export const TITLE_LINE_RE = /^#\s+\S/m

/**
 * Constitution mandatory H2 headings (Appendix A). Checked only when
 * `requireHeadings: true`. Default false so synthetic unit fixtures stay lean.
 */
export const ENFORCER_REQUIRED_HEADINGS = Object.freeze([
  'Definition',
  'Trigger When',
  'Nudge',
])

export const MAIN_REQUIRED_HEADINGS = Object.freeze([
  'What To Do Now',
  'Verification',
  'Done When',
])

/**
 * True when `text` has an ATX `## <heading>` line (optional trailing whitespace).
 * @param {string} text
 * @param {string} heading
 */
export const hasHeading = (text, heading) => {
  const escaped = heading.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return new RegExp(`^##\\s+${escaped}\\s*$`, 'm').test(text)
}

/**
 * @param {string} text
 * @param {readonly string[]} headings
 * @param {string} [fileRel]
 * @returns {{ code: string, path?: string, detail?: string }[]}
 */
export const missingHeadings = (text, headings, fileRel) => {
  /** @type {{ code: string, path?: string, detail?: string }[]} */
  const out = []
  for (const h of headings) {
    if (!hasHeading(text, h)) {
      out.push({
        code: 'missing-heading',
        path: fileRel,
        detail: `missing required heading '## ${h}'`,
      })
    }
  }
  return out
}

/**
 * Parse CLI flags for the gate.
 * @param {string[]} argv
 * @returns {{ requireHeadings: boolean }}
 */
export const parseCliArgs = (argv = process.argv.slice(2)) => {
  let requireHeadings = false
  for (const arg of argv) {
    if (arg === '--require-headings') {
      requireHeadings = true
      continue
    }
    if (arg === '--no-require-headings') {
      requireHeadings = false
      continue
    }
    if (arg.startsWith('--require-headings=')) {
      const v = arg.slice('--require-headings='.length).toLowerCase()
      requireHeadings = v !== 'false' && v !== '0' && v !== 'off'
    }
  }
  return { requireHeadings }
}

/**
 * @typedef {{
 *   code: string,
 *   path?: string,
 *   detail?: string,
 * }} Violation
 */

/**
 * @param {string} rootAbs absolute path to resources/enforcer
 * @param {{
 *   expectedCount?: number,
 *   requireTitle?: boolean,
 *   requireHeadings?: boolean,
 * }} [opts]
 * @returns {{ ok: boolean, count: number, violations: Violation[] }}
 */
export const scanRulebook = (rootAbs, opts = {}) => {
  const expectedCount = opts.expectedCount ?? EXPECTED_RULE_COUNT
  const requireTitle = opts.requireTitle !== false
  // Default false: unit fixtures are structural-only. CLI / check.mjs enable true.
  const requireHeadings = opts.requireHeadings === true
  /** @type {Violation[]} */
  const violations = []

  if (!existsSync(rootAbs)) {
    return {
      ok: false,
      count: 0,
      violations: [{ code: 'missing-root', path: rootAbs, detail: 'rulebook root does not exist' }],
    }
  }

  let rootStat
  try {
    rootStat = statSync(rootAbs)
  } catch (err) {
    return {
      ok: false,
      count: 0,
      violations: [
        {
          code: 'unreadable-root',
          path: rootAbs,
          detail: String(err?.message ?? err),
        },
      ],
    }
  }

  if (!rootStat.isDirectory()) {
    return {
      ok: false,
      count: 0,
      violations: [{ code: 'root-not-directory', path: rootAbs }],
    }
  }

  const entries = readdirSync(rootAbs, { withFileTypes: true })
  const dirs = []
  for (const ent of entries) {
    const rel = join(RULEBOOK_REL, ent.name)
    if (ent.isDirectory()) {
      dirs.push(ent.name)
      continue
    }
    if (ent.name === 'catalog.json') {
      violations.push({
        code: 'catalog-json-forbidden',
        path: rel,
        detail: 'catalog.json must not exist after folder cutover',
      })
      continue
    }
    // Refuse loose root files (including .gitkeep) — only rule directories allowed.
    violations.push({
      code: 'root-loose-entry',
      path: rel,
      detail: ent.isFile() ? 'file at rulebook root' : 'non-directory entry at rulebook root',
    })
  }

  dirs.sort()

  if (dirs.length !== expectedCount) {
    violations.push({
      code: 'rule-count',
      path: RULEBOOK_REL,
      detail: `expected exactly ${expectedCount} rule directories, found ${dirs.length}`,
    })
  }

  const seen = new Set()
  for (const name of dirs) {
    const dirRel = join(RULEBOOK_REL, name)
    if (seen.has(name)) {
      violations.push({ code: 'duplicate-dirname', path: dirRel })
    }
    seen.add(name)

    if (!KEBAB_CASE_RE.test(name)) {
      violations.push({
        code: 'dirname-not-kebab',
        path: dirRel,
        detail: 'directory name must be kebab-case ([a-z0-9]+(-[a-z0-9]+)*)',
      })
    }

    const dirAbs = join(rootAbs, name)
    let kids
    try {
      kids = readdirSync(dirAbs)
    } catch (err) {
      violations.push({
        code: 'unreadable-dir',
        path: dirRel,
        detail: String(err?.message ?? err),
      })
      continue
    }

    const kidSet = new Set(kids)
    for (const req of REQUIRED_FILES) {
      if (!kidSet.has(req)) {
        violations.push({
          code: 'missing-file',
          path: join(dirRel, req),
          detail: `required file missing in ${name}/`,
        })
      }
    }

    for (const kid of kids) {
      if (!REQUIRED_FILES.includes(kid)) {
        violations.push({
          code: 'extra-entry',
          path: join(dirRel, kid),
          detail: `only ${REQUIRED_FILES.join(' + ')} allowed; refuse third entries`,
        })
      }
    }

    for (const req of REQUIRED_FILES) {
      if (!kidSet.has(req)) continue
      const fileAbs = join(dirAbs, req)
      const fileRel = join(dirRel, req)
      let st
      try {
        st = statSync(fileAbs)
      } catch (err) {
        violations.push({
          code: 'unreadable-file',
          path: fileRel,
          detail: String(err?.message ?? err),
        })
        continue
      }
      if (!st.isFile()) {
        violations.push({
          code: 'not-a-file',
          path: fileRel,
          detail: 'required path is not a regular file',
        })
        continue
      }

      let buf
      try {
        buf = readFileSync(fileAbs)
      } catch (err) {
        violations.push({
          code: 'unreadable-file',
          path: fileRel,
          detail: String(err?.message ?? err),
        })
        continue
      }

      // Reject UTF-8 BOM-only / invalid UTF-8 by round-tripping through TextDecoder fatal.
      let text
      try {
        text = new TextDecoder('utf-8', { fatal: true }).decode(buf)
      } catch {
        violations.push({
          code: 'not-utf8',
          path: fileRel,
          detail: 'file must be valid UTF-8',
        })
        continue
      }

      if (text.trim().length === 0) {
        violations.push({
          code: 'empty-file',
          path: fileRel,
          detail: 'file must be nonempty after trim',
        })
        continue
      }

      if (requireTitle && !TITLE_LINE_RE.test(text)) {
        violations.push({
          code: 'missing-title',
          path: fileRel,
          detail: 'file must contain a markdown # title line',
        })
      }

      if (requireHeadings) {
        const required =
          req === 'enforcer.md'
            ? ENFORCER_REQUIRED_HEADINGS
            : req === 'main.md'
              ? MAIN_REQUIRED_HEADINGS
              : null
        if (required) {
          violations.push(...missingHeadings(text, required, fileRel))
        }
      }
    }
  }

  return {
    ok: violations.length === 0,
    count: dirs.length,
    violations,
  }
}

/**
 * Scan the repository rulebook at `cwd`/`RULEBOOK_REL` (or absolute override).
 * @param {string} [repoRoot]
 * @param {Parameters<typeof scanRulebook>[1]} [opts]
 */
export const scanRepoRulebook = (repoRoot = process.cwd(), opts) => {
  const rootAbs = resolve(repoRoot, RULEBOOK_REL)
  return scanRulebook(rootAbs, opts)
}

const formatViolation = (v) => {
  const loc = v.path ? v.path : RULEBOOK_REL
  const detail = v.detail ? ` — ${v.detail}` : ''
  return `  ${loc}: ${v.code}${detail}`
}

const runCli = () => {
  const { requireHeadings } = parseCliArgs()
  const result = scanRepoRulebook(process.cwd(), { requireHeadings })
  if (result.ok) {
    const headingNote = requireHeadings
      ? '; constitution headings enforced'
      : '; constitution headings not required (pass --require-headings)'
    console.log(
      `enforcer-rulebook-gate: OK — ${result.count} kebab-case rule dirs, each with enforcer.md + main.md (no catalog.json)${headingNote}`,
    )
    process.exit(0)
  }

  console.error(
    `enforcer-rulebook-gate: ${result.violations.length} violation(s); scanned ${result.count} director(y/ies)\n`,
  )
  for (const v of result.violations) {
    console.error(formatViolation(v))
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
