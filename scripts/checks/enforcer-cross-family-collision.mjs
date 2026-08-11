#!/usr/bin/env node
/**
 * Enforcer cross-family collision review (rulebook A40).
 *
 * Mechanical stand-in for the A40 tournament: parse Trigger When + Definition
 * of every tip under `resources/enforcer/`, score lexical overlap across
 * different tips, and report collisions. Does not require a human tournament.
 *
 * Fail closed only on extreme duplicates — near-identical Trigger When across
 * two tips that are not mutual siblings (Distinguish From). High overlap and
 * sibling-acknowledged collisions are listed so A40 is evidenced, not skipped.
 *
 * Usage:
 *   node scripts/checks/enforcer-cross-family-collision.mjs
 *   node scripts/checks/enforcer-cross-family-collision.mjs --root=/tmp/tips
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

/**
 * How collisions are scored (exported for unit tests + CLI evidence).
 *
 * Tokens: lowercase, strip markdown punctuation, drop stopwords and tokens
 * shorter than 3 (CJK tokens shorter than 2).
 *
 *   jaccard(A,B)     = |set(A) ∩ set(B)| / |set(A) ∪ set(B)|
 *   cosine(A,B)      = bag-of-words cosine
 *   triggerScore     = max(jaccard, cosine) on Trigger When tokens
 *   definitionScore  = max(jaccard, cosine) on Definition tokens
 *   combinedScore    = 0.7 * triggerScore + 0.3 * definitionScore
 *   triggerLev       = 1 - levenshtein(normA, normB) / max(|normA|, |normB|)
 *                      after stripping leading "trigger when" / "fire when"
 *
 * Severity:
 *   fail  — trigger Jaccard ≥ 0.90 OR triggerLev ≥ 0.95, both triggers long
 *           enough, and the pair is not mutual siblings
 *   warn  — combined ≥ 0.50 or triggerScore ≥ 0.55, and not listed as siblings
 *   note  — same overlap thresholds, but at least one side lists the other
 *
 * Evidence always includes the top-N pairs by combinedScore so a clean corpus
 * still proves A40 ran.
 */
export const SCORING = Object.freeze({
  triggerJaccardFail: 0.9,
  triggerLevenshteinFail: 0.95,
  warnTrigger: 0.55,
  warnCombined: 0.5,
  combinedTriggerWeight: 0.7,
  combinedDefinitionWeight: 0.3,
  minTriggerCharsForFail: 24,
  evidenceTopN: 8,
})

export const STOPWORDS = Object.freeze(new Set([
  'a', 'an', 'the', 'and', 'or', 'of', 'to', 'in', 'on', 'for', 'with', 'without',
  'when', 'then', 'that', 'this', 'those', 'these', 'is', 'are', 'was', 'were',
  'be', 'been', 'being', 'it', 'its', 'as', 'at', 'by', 'from', 'into', 'over',
  'under', 'not', 'no', 'nor', 'so', 'if', 'but', 'than', 'also', 'only', 'just',
  'already', 'before', 'after', 'must', 'should', 'may', 'can', 'will', 'would',
  'do', 'does', 'did', 'doing', 'have', 'has', 'had', 'having', 'which', 'who',
  'whom', 'whose', 'where', 'why', 'how', 'vs', 'via', 'per', 'both', 'same',
  'other', 'between', 'among', 'such', 'any', 'all', 'each', 'every', 'there',
  'their', 'them', 'they', 'you', 'your', 'we', 'our',
  'trigger', 'triggers', 'triggered', 'fire', 'fires', 'skip', 'case', 'rule',
]))

/**
 * Body of an ATX `## <heading>` section (until the next H2), or `''` if absent.
 * @param {string} text
 * @param {string} heading
 */
export const extractSection = (text, heading) => {
  const escaped = heading.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const re = new RegExp(`^##\\s+${escaped}\\s*$`, 'm')
  const match = re.exec(text)
  if (!match) return ''
  const rest = text.slice(match.index + match[0].length)
  const next = /^##\s+/m.exec(rest)
  return next ? rest.slice(0, next.index) : rest
}

/**
 * Sibling-like tip names: kebab-case or `resources/enforcer/<name>` refs.
 * @param {string} text
 * @returns {Set<string>}
 */
export const collectSiblingNames = (text) => {
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

/**
 * @param {string} text
 * @returns {string[]}
 */
export const tokenize = (text) => {
  const words = String(text ?? '')
    .toLowerCase()
    .replace(/[`*_>#\[\]()]/g, ' ')
    .replace(/[^a-z0-9\u4e00-\u9fff]+/g, ' ')
    .split(/\s+/)
    .filter((w) => {
      if (!w) return false
      if (STOPWORDS.has(w)) return false
      const cjk = /[\u4e00-\u9fff]/.test(w)
      return cjk ? w.length >= 2 : w.length >= 3
    })
  return words
}

/**
 * @param {readonly string[]} a
 * @param {readonly string[]} b
 */
export const jaccard = (a, b) => {
  const A = new Set(a)
  const B = new Set(b)
  if (A.size === 0 && B.size === 0) return 1
  let inter = 0
  for (const x of A) {
    if (B.has(x)) inter += 1
  }
  const union = A.size + B.size - inter
  return union === 0 ? 0 : inter / union
}

/**
 * @param {readonly string[]} a
 * @param {readonly string[]} b
 */
export const cosine = (a, b) => {
  /** @type {Map<string, number>} */
  const bagA = new Map()
  /** @type {Map<string, number>} */
  const bagB = new Map()
  for (const w of a) bagA.set(w, (bagA.get(w) ?? 0) + 1)
  for (const w of b) bagB.set(w, (bagB.get(w) ?? 0) + 1)
  let dot = 0
  let na = 0
  let nb = 0
  for (const v of bagA.values()) na += v * v
  for (const v of bagB.values()) nb += v * v
  for (const [k, v] of bagA) {
    const w = bagB.get(k)
    if (w) dot += v * w
  }
  if (na === 0 || nb === 0) return 0
  return dot / Math.sqrt(na * nb)
}

const levenshtein = (a, b) => {
  const n = a.length
  const m = b.length
  if (n === 0) return m
  if (m === 0) return n
  /** @type {number[]} */
  let prev = new Array(m + 1)
  /** @type {number[]} */
  let curr = new Array(m + 1)
  for (let j = 0; j <= m; j++) prev[j] = j
  for (let i = 1; i <= n; i++) {
    curr[0] = i
    const ca = a.charCodeAt(i - 1)
    for (let j = 1; j <= m; j++) {
      const cost = ca === b.charCodeAt(j - 1) ? 0 : 1
      curr[j] = Math.min(prev[j] + 1, curr[j - 1] + 1, prev[j - 1] + cost)
    }
    const tmp = prev
    prev = curr
    curr = tmp
  }
  return prev[m]
}

/**
 * @param {string} a
 * @param {string} b
 */
export const levenshteinRatio = (a, b) => {
  if (a === b) return 1
  if (!a.length || !b.length) return 0
  return 1 - levenshtein(a, b) / Math.max(a.length, b.length)
}

export const normalizeTrigger = (text) => {
  const lowered = String(text ?? '')
    .toLowerCase()
    .replace(/[`*_>#]+/g, ' ')
    .replace(/[^a-z0-9\u4e00-\u9fff]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
  return lowered.replace(/^(?:trigger|fire) when /, '')
}

const combinedScore = (triggerScore, definitionScore) =>
  SCORING.combinedTriggerWeight * triggerScore +
  SCORING.combinedDefinitionWeight * definitionScore

/**
 * @typedef {{
 *   name: string,
 *   path: string,
 *   trigger: string,
 *   definition: string,
 *   siblings: Set<string>,
 *   triggerTokens: string[],
 *   definitionTokens: string[],
 *   triggerNorm: string,
 * }} TipDoc
 */

/**
 * Score one unordered pair. Does not assign severity (needs sibling flags).
 * @param {TipDoc} left
 * @param {TipDoc} right
 */
export const scorePair = (left, right) => {
  const triggerJaccard = jaccard(left.triggerTokens, right.triggerTokens)
  const triggerCosine = cosine(left.triggerTokens, right.triggerTokens)
  const definitionJaccard = jaccard(left.definitionTokens, right.definitionTokens)
  const definitionCosine = cosine(left.definitionTokens, right.definitionTokens)
  const triggerScore = Math.max(triggerJaccard, triggerCosine)
  const definitionScore = Math.max(definitionJaccard, definitionCosine)
  const combined = combinedScore(triggerScore, definitionScore)
  const triggerLevenshtein = levenshteinRatio(left.triggerNorm, right.triggerNorm)
  const listedSiblings =
    left.siblings.has(right.name) || right.siblings.has(left.name)
  const mutualSiblings =
    left.siblings.has(right.name) && right.siblings.has(left.name)
  const longEnough =
    left.triggerNorm.length >= SCORING.minTriggerCharsForFail &&
    right.triggerNorm.length >= SCORING.minTriggerCharsForFail
  const nearIdenticalTrigger =
    longEnough &&
    (triggerJaccard >= SCORING.triggerJaccardFail ||
      triggerLevenshtein >= SCORING.triggerLevenshteinFail)
  const highOverlap =
    combined >= SCORING.warnCombined || triggerScore >= SCORING.warnTrigger

  /** @type {'fail' | 'warn' | 'note' | null} */
  let severity = null
  /** @type {string | null} */
  let code = null
  if (nearIdenticalTrigger && !mutualSiblings) {
    severity = 'fail'
    code = 'extreme-trigger-duplicate'
  } else if (nearIdenticalTrigger && mutualSiblings) {
    severity = 'warn'
    code = 'extreme-trigger-duplicate-siblings'
  } else if (highOverlap && !listedSiblings) {
    severity = 'warn'
    code = 'high-lexical-overlap'
  } else if (highOverlap && listedSiblings) {
    severity = 'note'
    code = 'sibling-overlap'
  }

  return {
    a: left.name,
    b: right.name,
    pathA: left.path,
    pathB: right.path,
    triggerJaccard,
    triggerCosine,
    triggerLevenshtein,
    definitionJaccard,
    definitionCosine,
    triggerScore,
    definitionScore,
    combined,
    listedSiblings,
    mutualSiblings,
    nearIdenticalTrigger,
    severity,
    code,
  }
}

/**
 * @param {string} argvFlag
 * @param {string[]} argv
 */
export const parseCliArgs = (argv = process.argv.slice(2)) => {
  let root = null
  for (const arg of argv) {
    if (arg.startsWith('--root=')) {
      root = arg.slice('--root='.length)
      continue
    }
    if (arg === '--root') {
      continue
    }
  }
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--root' && argv[i + 1] && !argv[i + 1].startsWith('-')) {
      root = argv[i + 1]
    }
  }
  return { root }
}

/**
 * Load tip docs from a rulebook root (directory of kebab-case tip dirs).
 * @param {string} rootAbs
 * @returns {{ tips: TipDoc[], loadErrors: { code: string, path?: string, detail?: string }[] }}
 */
export const loadTips = (rootAbs) => {
  /** @type {TipDoc[]} */
  const tips = []
  /** @type {{ code: string, path?: string, detail?: string }[]} */
  const loadErrors = []

  if (!existsSync(rootAbs)) {
    return {
      tips,
      loadErrors: [{ code: 'missing-root', path: rootAbs, detail: 'rulebook root does not exist' }],
    }
  }

  let rootStat
  try {
    rootStat = statSync(rootAbs)
  } catch (err) {
    return {
      tips,
      loadErrors: [{ code: 'unreadable-root', path: rootAbs, detail: String(err?.message ?? err) }],
    }
  }
  if (!rootStat.isDirectory()) {
    return {
      tips,
      loadErrors: [{ code: 'root-not-directory', path: rootAbs }],
    }
  }

  const entries = readdirSync(rootAbs, { withFileTypes: true })
  for (const ent of entries) {
    if (!ent.isDirectory()) continue
    const dirRel = join(RULEBOOK_REL, ent.name)
    const fileAbs = join(rootAbs, ent.name, 'enforcer.md')
    const fileRel = join(dirRel, 'enforcer.md')
    if (!existsSync(fileAbs)) {
      loadErrors.push({
        code: 'missing-enforcer',
        path: fileRel,
        detail: 'tip directory has no enforcer.md; skipped',
      })
      continue
    }
    let text
    try {
      text = new TextDecoder('utf-8', { fatal: true }).decode(readFileSync(fileAbs))
    } catch (err) {
      loadErrors.push({
        code: 'unreadable-file',
        path: fileRel,
        detail: String(err?.message ?? err),
      })
      continue
    }
    const trigger = extractSection(text, 'Trigger When')
    const definition = extractSection(text, 'Definition')
    const distinguish = extractSection(text, 'Distinguish From')
    const siblings = collectSiblingNames(distinguish)
    siblings.delete(ent.name)
    tips.push({
      name: ent.name,
      path: fileRel,
      trigger,
      definition,
      siblings,
      triggerTokens: tokenize(trigger),
      definitionTokens: tokenize(definition),
      triggerNorm: normalizeTrigger(trigger),
    })
  }

  tips.sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))
  return { tips, loadErrors }
}

/**
 * @param {string} rootAbs absolute path to a rulebook root (tip directories)
 * @param {{ evidenceTopN?: number }} [opts]
 */
export const scanCollisions = (rootAbs, opts = {}) => {
  const evidenceTopN = opts.evidenceTopN ?? SCORING.evidenceTopN
  const { tips, loadErrors } = loadTips(rootAbs)
  /** @type {ReturnType<typeof scorePair>[]} */
  const scored = []
  for (let i = 0; i < tips.length; i++) {
    for (let j = i + 1; j < tips.length; j++) {
      scored.push(scorePair(tips[i], tips[j]))
    }
  }

  const failures = scored.filter((p) => p.severity === 'fail')
  const warnings = scored.filter((p) => p.severity === 'warn')
  const notes = scored.filter((p) => p.severity === 'note')
  const evidence = [...scored]
    .sort((a, b) => b.combined - a.combined || b.triggerScore - a.triggerScore)
    .slice(0, evidenceTopN)

  const missingRoot = loadErrors.some((e) => e.code === 'missing-root' || e.code === 'root-not-directory')
  return {
    ok: failures.length === 0 && !missingRoot,
    count: tips.length,
    pairsCompared: scored.length,
    failures,
    warnings,
    notes,
    evidence,
    loadErrors,
    scoring: SCORING,
  }
}

/**
 * @param {string} [repoRoot]
 * @param {Parameters<typeof scanCollisions>[1]} [opts]
 */
export const scanRepoCollisions = (repoRoot = process.cwd(), opts) => {
  const rootAbs = resolve(repoRoot, RULEBOOK_REL)
  return scanCollisions(rootAbs, opts)
}

const fmtScore = (n) => Number(n).toFixed(3)

const formatPair = (p) => {
  const loc = `${p.a} vs ${p.b}`
  const scores =
    `combined=${fmtScore(p.combined)} trigger=${fmtScore(p.triggerScore)} ` +
    `(j=${fmtScore(p.triggerJaccard)} cos=${fmtScore(p.triggerCosine)} lev=${fmtScore(p.triggerLevenshtein)}) ` +
    `def=${fmtScore(p.definitionScore)} siblings=${p.mutualSiblings ? 'mutual' : p.listedSiblings ? 'listed' : 'none'}`
  return `  ${loc}: ${p.code ?? 'pair'} — ${scores}`
}

const runCli = () => {
  const { root } = parseCliArgs()
  const rootAbs = root ? resolve(root) : resolve(process.cwd(), RULEBOOK_REL)
  const result = scanCollisions(rootAbs)

  const heading =
    `enforcer-cross-family-collision: A40 review — scanned ${result.count} tip(s), ` +
    `${result.pairsCompared} pair(s)`
  const sink = result.ok ? console.log : console.error
  sink(heading)

  if (result.loadErrors.length > 0) {
    console.error(`  load-errors: ${result.loadErrors.length}`)
    for (const e of result.loadErrors) {
      console.error(`  ${e.path ?? rootAbs}: ${e.code}${e.detail ? ` — ${e.detail}` : ''}`)
    }
  }

  console.error(`  failures: ${result.failures.length} (fail-closed extreme Trigger When duplicates)`)
  for (const p of result.failures) console.error(formatPair(p))

  console.error(`  warnings: ${result.warnings.length} (high overlap, not mutual-sibling extreme)`)
  for (const p of result.warnings) console.error(formatPair(p))

  console.error(`  notes: ${result.notes.length} (high overlap, sibling-listed)`)
  for (const p of result.notes) console.error(formatPair(p))

  console.error('  evidence (top combined; A40 not skipped):')
  if (result.evidence.length === 0) {
    console.error('    (no pairs — need at least two tips)')
  } else {
    for (const p of result.evidence) console.error(formatPair(p))
  }

  console.error(
    '  scoring: fail if trigger Jaccard≥' +
      SCORING.triggerJaccardFail +
      ' or lev≥' +
      SCORING.triggerLevenshteinFail +
      ' and not mutual siblings; warn if combined≥' +
      SCORING.warnCombined +
      ' or triggerScore≥' +
      SCORING.warnTrigger +
      `; combined=${SCORING.combinedTriggerWeight}*trigger+${SCORING.combinedDefinitionWeight}*definition`,
  )

  if (result.ok) {
    console.log(
      'enforcer-cross-family-collision: OK — no extreme non-sibling Trigger When duplicates',
    )
    process.exit(0)
  }

  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
