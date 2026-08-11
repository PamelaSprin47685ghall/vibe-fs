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
 *   - optional structural rubric (`requireRubric` / `--strict` / `--require-rubric`):
 *       Mechanical A37 (enforcer.md) + A38 (main.md) items encoded below.
 *       This gate does NOT claim human-only work: paired-history 120,
 *       A39 pair review, A40 tournament.
 *       enforcer.md → Do Not Trigger When ≥3 bullets; Distinguish From ≥2 sibling names
 *                     plus tie-break language; root-cause present; no remediation drift;
 *                     Trigger When semantic; Definition nonempty and not title-only;
 *                     Examples positive / near-miss / counterexample markers
 *       main.md     → Common Wrong Fixes ≥3 items; Decision Branches ≥2;
 *                     Verification mentions invariant/不变量; Done When present;
 *                     owner/root language; authority not exceeded; no reclassification;
 *                     scope 不扩张
 *
 * Usage:
 *   node scripts/checks/enforcer-rulebook-gate.mjs
 *   node scripts/checks/enforcer-rulebook-gate.mjs --require-headings
 *   node scripts/checks/enforcer-rulebook-gate.mjs --strict
 *   node scripts/checks/enforcer-rulebook-gate.mjs --require-rubric
 *
 * check.mjs enables `--require-headings --strict` (120/120 headings + structural rubric).
 * Bare invocation stays headings-off / rubric-off for unit fixtures.
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
 * Remaining A37/A38-adjacent work this gate explicitly does not claim.
 * Paired-history coverage of all 120, A39 pair review, and A40 cross-family
 * tournament stay human-only (plus non-mechanical A37/A38 prose quality).
 */
export const HUMAN_ONLY_RUBRIC_ITEMS = Object.freeze([
  'paired-history 120',
  'A39 pair review',
  'A40 tournament',
])

/** Mechanical A37/A38 violation codes added beyond the original count-marker subset. */
export const NEW_RUBRIC_CODES = Object.freeze([
  'rubric-distinguish-tie-break',
  'rubric-root-cause',
  'rubric-remediation-drift',
  'rubric-trigger-semantic',
  'rubric-definition',
  'rubric-owner-root',
  'rubric-authority-overreach',
  'rubric-reclassification',
  'rubric-scope-expansion',
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
 * `--strict` and `--require-rubric` enable the same structural-rubric flag.
 * @param {string[]} argv
 * @returns {{ requireHeadings: boolean, requireRubric: boolean }}
 */
export const parseCliArgs = (argv = process.argv.slice(2)) => {
  let requireHeadings = false
  let requireRubric = false
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
      continue
    }
    if (arg === '--strict' || arg === '--require-rubric') {
      requireRubric = true
      continue
    }
    if (arg === '--no-strict' || arg === '--no-require-rubric') {
      requireRubric = false
      continue
    }
    if (arg.startsWith('--strict=') || arg.startsWith('--require-rubric=')) {
      const v = arg.slice(arg.indexOf('=') + 1).toLowerCase()
      requireRubric = v !== 'false' && v !== '0' && v !== 'off'
    }
  }
  return { requireHeadings, requireRubric }
}

/**
 * Body of an ATX `## <heading>` section (until the next H2), or `''` if absent.
 * @param {string} text
 * @param {string} heading
 */
const extractSection = (text, heading) => {
  const escaped = heading.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const re = new RegExp(`^##\\s+${escaped}\\s*$`, 'm')
  const match = re.exec(text)
  if (!match) return ''
  const rest = text.slice(match.index + match[0].length)
  const next = /^##\s+/m.exec(rest)
  return next ? rest.slice(0, next.index) : rest
}

/** Markdown list items: `-` / `*` / `+` / ordered `1.` / `1)`. */
const LIST_ITEM_RE = /^[ \t]*(?:[-*+]|\d+[.)])[ \t]+\S/gm

/**
 * @param {string} text
 * @returns {number}
 */
const countListItems = (text) => {
  LIST_ITEM_RE.lastIndex = 0
  const matches = text.match(LIST_ITEM_RE)
  return matches ? matches.length : 0
}

/**
 * Sibling-like tip names: kebab-case (`foo-bar`) or `resources/enforcer/<name>` refs.
 * @param {string} text
 * @returns {Set<string>}
 */
const collectSiblingNames = (text) => {
  /** @type {Set<string>} */
  const names = new Set()
  const resourceRe = /resources\/enforcer\/([a-z0-9]+(?:-[a-z0-9]+)*)/g
  let m
  while ((m = resourceRe.exec(text))) {
    names.add(m[1])
  }
  const kebabRe = /\b[a-z][a-z0-9]*(?:-[a-z0-9]+)+\b/g
  while ((m = kebabRe.exec(text))) {
    names.add(m[0])
  }
  return names
}

const hasPositiveExampleMarker = (text) =>
  /\bpositive\b|正例/i.test(text)

const hasNearMissExampleMarker = (text) =>
  /near[-\s]?miss|近邻/i.test(text)

const hasCounterexampleMarker = (text) =>
  /counter[-\s]?example|反例/i.test(text)

const TIE_BREAK_RE = /tie-break|决胜|区分规则|若相似/i
const ROOT_CAUSE_RE = /root-cause|根因|root cause/i
const OWNER_ROOT_RE = /修的是\s*owner|root-cause\s+owner|who owns/i
const SCOPE_EXPANSION_RE = /rewrite(?:s|ing)?(?:\s+\w+){0,4}\s+the\s+system|unrelated modules?|重写(?:整个)?系统|无关(?:的)?模块/i
const AUTHORITY_OVERREACH_RE =
  /(?:change|edit|rewrite|modify|update|alter|修改|改写).{0,48}(?:rulebook|architecture|playbook)\s+gates?|(?:rulebook|architecture|playbook)\s+gates?.{0,48}(?:change|edit|rewrite|modify|update|alter|修改|改写)/i

const BEHAVIORAL_TRIGGER_RE =
  /when|trigger|fire|fail|behavio(?:u)?r|mutat|edit|claim|before|after|\bif\b|because|unless|owner|contract|invariant|cause|observ|violat|should|must|begin|appear|若|当|行为|触发|失败|编辑|不变/i

const GLOBISH_RE =
  /`[^`]*`|\*\*\/\S+|\*+\.[A-Za-z0-9*?]+|\b[\w./-]+\.[A-Za-z0-9]{1,8}\b|\.[A-Za-z][A-Za-z0-9]{0,7}\b/g

const FILE_VOCAB_RE =
  /\b(?:files?|filenames?|extensions?|globs?|paths?|dirs?|directories|matching|patterns?)\b/gi

/**
 * Drop filename / extension / glob tokens so Trigger When can be judged as prose.
 * @param {string} text
 */
const stripGlobish = (text) =>
  String(text)
    .replace(GLOBISH_RE, ' ')
    .replace(FILE_VOCAB_RE, ' ')
    .replace(/^[ \t]*(?:[-*+]|\d+[.)])[ \t]+/gm, ' ')
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim()

/**
 * True when Trigger When is semantic (behavioral language, not glob lists only).
 * @param {string} section
 */
const isTriggerSemantic = (section) => {
  const text = String(section ?? '').trim()
  if (!text) return false
  const core = stripGlobish(text)
  return core.length > 0 && BEHAVIORAL_TRIGGER_RE.test(core)
}

const normalizeProse = (s) =>
  String(s)
    .replace(/[`*_>#]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase()

/**
 * Strip negated spans so "do not rewrite the system" does not look like overreach.
 * @param {string} text
 */
const stripNegatedSpans = (text) =>
  String(text).replace(
    /(?:\bdo(?:es)?\s+not\b|\bdon't\b|\bnever\b|禁止|不得|不要)[^.!\n]{0,200}/gi,
    ' ',
  )

/**
 * Mechanical A37 checks for `enforcer.md`.
 * Independent of constitution heading presence.
 * Does not claim paired-history 120, A39 pair review, or A40 tournament.
 * @param {string} text
 * @returns {{ code: string, path?: string, detail?: string }[]}
 */
export const checkEnforcerRubric = (text) => {
  /** @type {{ code: string, path?: string, detail?: string }[]} */
  const out = []
  const body = String(text ?? '')

  const definition = extractSection(body, 'Definition')
  const defNorm = normalizeProse(definition)
  if (!defNorm) {
    out.push({
      code: 'rubric-definition',
      detail: 'Definition section must be nonempty',
    })
  } else {
    const titleLine = (body.match(/^#\s+(.+)$/m) || [])[1] ?? ''
    const titleNorm = normalizeProse(titleLine)
    const nameNorm = normalizeProse(titleLine.split(/\s+[—–-]\s+/)[0] ?? '')
    if (defNorm === titleNorm || (nameNorm && defNorm === nameNorm)) {
      out.push({
        code: 'rubric-definition',
        detail: 'Definition must not be identical to the title only',
      })
    }
  }

  const trigger = extractSection(body, 'Trigger When')
  if (!isTriggerSemantic(trigger)) {
    out.push({
      code: 'rubric-trigger-semantic',
      detail:
        'Trigger When must be semantic behavioral language, not only filename/extension glob lists',
    })
  }

  const dnt = extractSection(body, 'Do Not Trigger When')
  const dntCount = countListItems(dnt)
  if (dntCount < 3) {
    out.push({
      code: 'rubric-do-not-trigger-count',
      detail: `Do Not Trigger When must contain ≥3 bullets (found ${dntCount})`,
    })
  }

  const distinguish = extractSection(body, 'Distinguish From')
  const siblings = collectSiblingNames(distinguish)
  if (siblings.size < 2) {
    out.push({
      code: 'rubric-distinguish-siblings',
      detail: `Distinguish From must mention ≥2 sibling-like kebab names or resources/enforcer/ refs (found ${siblings.size})`,
    })
  }
  if (!TIE_BREAK_RE.test(distinguish)) {
    out.push({
      code: 'rubric-distinguish-tie-break',
      detail: 'Distinguish From must contain tie-break language (tie-break / 决胜 / 区分规则 / 若相似)',
    })
  }

  if (!ROOT_CAUSE_RE.test(body)) {
    out.push({
      code: 'rubric-root-cause',
      detail: 'enforcer.md must contain root-cause language (root-cause / 根因 / Root cause)',
    })
  }

  if (hasHeading(body, 'What To Do Now') || hasHeading(body, 'Common Wrong Fixes')) {
    out.push({
      code: 'rubric-remediation-drift',
      detail:
        "enforcer.md must not contain remediation headings '## What To Do Now' or '## Common Wrong Fixes'",
    })
  }

  const examples = extractSection(body, 'Examples')
  if (!hasPositiveExampleMarker(examples)) {
    out.push({
      code: 'rubric-examples-positive',
      detail: "Examples must contain a positive / 正例 marker",
    })
  }
  if (!hasNearMissExampleMarker(examples)) {
    out.push({
      code: 'rubric-examples-near-miss',
      detail: "Examples must contain a near-miss / 近邻 marker",
    })
  }
  if (!hasCounterexampleMarker(examples)) {
    out.push({
      code: 'rubric-examples-counterexample',
      detail: "Examples must contain a counterexample / 反例 marker",
    })
  }

  return out
}

/**
 * Count decision-branch cues: list items, or If/When/Otherwise lines.
 * @param {string} text
 */
const countDecisionBranches = (text) => {
  const items = countListItems(text)
  const cueRe = /^[ \t]*(?:[-*+]|\d+[.)])?[ \t]*(?:if|when|otherwise|else(?:\s+if)?)\b/gim
  const cues = text.match(cueRe)
  const cueCount = cues ? cues.length : 0
  return Math.max(items, cueCount)
}

/**
 * Mechanical A38 checks for `main.md`.
 * Does not claim paired-history 120, A39 pair review, or A40 tournament.
 * @param {string} text
 * @returns {{ code: string, path?: string, detail?: string }[]}
 */
export const checkMainRubric = (text) => {
  /** @type {{ code: string, path?: string, detail?: string }[]} */
  const out = []
  const body = String(text ?? '')
  const actionable = stripNegatedSpans(body)

  const wrongFixes = extractSection(body, 'Common Wrong Fixes')
  const wrongCount = countListItems(wrongFixes)
  if (wrongCount < 3) {
    out.push({
      code: 'rubric-wrong-fixes-count',
      detail: `Common Wrong Fixes must contain ≥3 items (found ${wrongCount})`,
    })
  }

  const branches = extractSection(body, 'Decision Branches')
  const branchCount = countDecisionBranches(branches)
  if (branchCount < 2) {
    out.push({
      code: 'rubric-decision-branches-count',
      detail: `Decision Branches must contain ≥2 branches (found ${branchCount})`,
    })
  }

  const verification = extractSection(body, 'Verification')
  if (!/invariant|不变量/i.test(verification)) {
    out.push({
      code: 'rubric-verification-invariant',
      detail: 'Verification must mention invariant / 不变量',
    })
  }

  if (!hasHeading(body, 'Done When')) {
    out.push({
      code: 'rubric-done-when',
      detail: "missing required heading '## Done When'",
    })
  }

  if (!OWNER_ROOT_RE.test(body)) {
    out.push({
      code: 'rubric-owner-root',
      detail: 'main.md must contain owner/root language (修的是 owner / root-cause owner / who owns)',
    })
  }

  if (AUTHORITY_OVERREACH_RE.test(actionable)) {
    out.push({
      code: 'rubric-authority-overreach',
      detail: 'main.md must not tell the model to change Rulebook / architecture / Playbook gates',
    })
  }

  if (hasHeading(body, 'Definition') || hasHeading(body, 'Detection')) {
    out.push({
      code: 'rubric-reclassification',
      detail: 'main.md must not re-do Detection / Definition (classification stays in enforcer.md)',
    })
  }

  if (SCOPE_EXPANSION_RE.test(actionable)) {
    out.push({
      code: 'rubric-scope-expansion',
      detail: 'main.md must not expand scope (rewrite the system / unrelated modules)',
    })
  }

  return out
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
 *   requireRubric?: boolean,
 * }} [opts]
 * @returns {{ ok: boolean, count: number, violations: Violation[] }}
 */
export const scanRulebook = (rootAbs, opts = {}) => {
  const expectedCount = opts.expectedCount ?? EXPECTED_RULE_COUNT
  const requireTitle = opts.requireTitle !== false
  // Default false: unit fixtures are structural-only. CLI / check.mjs enable true.
  const requireHeadings = opts.requireHeadings === true
  // Default false: unit fixtures are headings-only. CLI `--strict` / check.mjs enable true.
  const requireRubric = opts.requireRubric === true
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

      if (requireRubric) {
        const rubric =
          req === 'enforcer.md'
            ? checkEnforcerRubric(text)
            : req === 'main.md'
              ? checkMainRubric(text)
              : []
        for (const v of rubric) {
          violations.push({ ...v, path: v.path ?? fileRel })
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
  const { requireHeadings, requireRubric } = parseCliArgs()
  const result = scanRepoRulebook(process.cwd(), { requireHeadings, requireRubric })
  if (result.ok) {
    const headingNote = requireHeadings
      ? '; constitution headings enforced'
      : '; constitution headings not required (pass --require-headings)'
    const rubricNote = requireRubric ? '; structural rubric enforced' : ''
    console.log(
      `enforcer-rulebook-gate: OK — ${result.count} kebab-case rule dirs, each with enforcer.md + main.md (no catalog.json)${headingNote}${rubricNote}`,
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
