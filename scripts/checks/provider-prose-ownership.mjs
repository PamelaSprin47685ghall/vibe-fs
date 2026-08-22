#!/usr/bin/env node
/**
 * ARCH-016 Gate E — Provider Prose Ownership.
 * Known owner files must not gain NEW provider-visible natural-language literals.
 * Grandfathered debt lives in the baseline; counts must only shrink.
 *
 * Usage:
 *   node scripts/checks/provider-prose-ownership.mjs [--baseline=<json-path>]
 *   node scripts/checks/provider-prose-ownership.mjs --generate [--out=<path>]
 */

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const norm = (p) => p.replace(/\\/g, '/')

/** Gate 0 owner surfaces — Domain prompts + OpenCode tool/renderer prose. */
export const PROVIDER_PROSE_SCAN_ROOTS = Object.freeze([
  'src/Wanxiangshu/Interaction/Dispatch/Nudge.fs',
  'src/Wanxiangshu/Mission/Review/Judgement/Challenge.fs',
  'src/Wanxiangshu/Mission/Manager/Narrative.fs',
  'src/Wanxiangshu/Mission/Manager/Life/Prompt.fs',
  'src/Wanxiangshu/Mission/Finality/Prompt.fs',
  'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Prompt.fs',
  'src/Wanxiangshu/Context/Companion/Prompt.fs',
  'src/Wanxiangshu/Mission/Review/Prompt.fs',
  'src/Wanxiangshu/Repository/Programming/Js/Description.fs',
  'src/Wanxiangshu/Repository/Investigation/WarmStart/Prompt.fs',
  'src/Wanxiangshu/Mission/Obligation/Todo/Surface.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/Payload.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinTool.fs',
  'src/Wanxiangshu/Execution/Session/OpenCode/HorizonTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/CoderTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/InspectorTool.fs',
  'src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/ExecutorTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/FetchTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/ChronicleTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/PtyTool.fs',
  'src/Wanxiangshu/Repository/Programming/Js/OpenCode/BookkeeperTool.fs',
  'src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs',
  'src/Wanxiangshu/OpenCode/Tools/FileMutationTools.fs',
  'src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs',
  'src/Wanxiangshu/OpenCode/Tools/Distillation.fs',
  'src/Wanxiangshu/OpenCode/Tools/BashHoneypotTool.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/OpenCode/Tools.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
  'src/Wanxiangshu/Repository/Programming/Js/OpenCode/ToolHost.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinResultRenderer.fs',
])

export const DEFAULT_BASELINE = join(
  dirname(fileURLToPath(import.meta.url)),
  'provider-prose-ownership-baseline.json',
)

const CJK_RE = /[\u4e00-\u9fff]/
const ENGLISH_WORD_RE = /[A-Za-z]{3,}/
const PATH_PREFIX_RE = /^(?:resources|role|world|library|lifecycle|runtime|tool|provider)\//
const SNAKE_RE = /^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$/
const KEBAB_RE = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)+$/
const PASCAL_RE = /^[A-Z][a-zA-Z0-9]*$/
const ALL_CAPS_RE = /^[A-Z][A-Z0-9_]*$/

/** Wire / protocol tokens that are never provider prose by themselves. */
const TECH_TOKENS = new Set([
  'exit_code',
  'deadline_seconds',
  'world_lock',
  'PERFECT',
  'REVISE',
  'horizon',
  'join',
  'fork',
  'chronicle',
  'pty',
  'executor',
  'judge',
  'coder',
  'inspector',
  'fetch',
  'finality',
  'bash',
  'read',
  'write',
  'edit',
  'verdict',
])

/** F# double-quoted literals on one line: regular `"..."` or verbatim `@"..."`. */
const F_STRING_RE = /@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*"/g

/**
 * @typedef {{ id: string, file: string, line: number, text: string }} Hit
 */

/**
 * @param {string} raw quoted literal including delimiters
 * @returns {string}
 */
const unescapeLiteral = (raw) => {
  if (raw.startsWith('@"')) {
    return raw.slice(2, -1).replace(/""/g, '"')
  }
  return raw
    .slice(1, -1)
    .replace(/\\"/g, '"')
    .replace(/\\n/g, '\n')
    .replace(/\\t/g, '\t')
    .replace(/\\r/g, '\r')
    .replace(/\\\\/g, '\\')
}

/**
 * Mostly `{{...}}` / `{...}` placeholders with little remaining prose.
 * @param {string} s
 */
const isFormatOnly = (s) => {
  const stripped = s
    .replace(/\{\{[^}]*\}\}/g, '')
    .replace(/\{[^}]*\}/g, '')
    .replace(/%[sdifoxX]/g, '')
    .trim()
  if (stripped.length === 0) return true
  if (!CJK_RE.test(stripped) && stripped.length < 12 && !/\s/.test(stripped)) return true
  const ph = (s.match(/\{\{[^}]*\}\}/g) ?? []).join('').length
  return ph > s.length * 0.5 && stripped.length < 20 && !CJK_RE.test(stripped)
}

/**
 * @param {string} s
 * @returns {boolean}
 */
const isTechnical = (s) => {
  const t = s.trim()
  if (t.length === 0) return true
  if (t.includes('/') || PATH_PREFIX_RE.test(t)) return true
  if (TECH_TOKENS.has(t)) return true
  if (isFormatOnly(t)) return true
  if (!/\s/.test(t)) {
    if (SNAKE_RE.test(t) || KEBAB_RE.test(t) || PASCAL_RE.test(t) || ALL_CAPS_RE.test(t)) return true
    if (!CJK_RE.test(t) && t.length <= 11) return true
  }
  return false
}

/**
 * Provider-visible natural-language candidate?
 * @param {string} s
 */
export const isProviderProseLiteral = (s) => {
  if (isTechnical(s)) return false
  if (CJK_RE.test(s)) return true
  const t = s.trim()
  return t.length >= 12 && /\s/.test(t) && ENGLISH_WORD_RE.test(t)
}

/**
 * @param {string} line
 */
const isCommentOnlyLine = (line) =>
  /^\s*\/\//.test(line) || /^\s*\(\*/.test(line) || /^\s*\*\)/.test(line)

/**
 * @param {string} file
 * @param {string} text
 * @returns {Hit[]}
 */
export const scanText = (file, text) => {
  /** @type {Hit[]} */
  const hits = []
  const rel = norm(file)
  const lines = text.split('\n')

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    if (isCommentOnlyLine(line)) continue

    F_STRING_RE.lastIndex = 0
    let m
    while ((m = F_STRING_RE.exec(line)) !== null) {
      const value = unescapeLiteral(m[0])
      if (!isProviderProseLiteral(value)) continue
      hits.push({
        id: 'provider-prose',
        file: rel,
        line: i + 1,
        text: value.length > 80 ? `${value.slice(0, 77)}...` : value,
      })
    }
  }

  return hits
}

/**
 * @param {{ file: string, text: string }[]} entries
 * @returns {Hit[]}
 */
export const scanEntries = (entries) => {
  /** @type {Hit[]} */
  const hits = []
  for (const { file, text } of entries) hits.push(...scanText(file, text))
  return hits
}

/**
 * Read every configured owner root. Each root is REQUIRED: a missing,
 * non-file, or unreadable root throws with path context so provider-visible
 * prose cannot slip past the gate when an owner surface is moved or deleted.
 *
 * @param {string} repoRoot
 * @param {readonly string[]} [roots] injectable for tests; defaults to the
 *   canonical configured set.
 * @returns {{ file: string, text: string }[]}
 */
export const collectEntries = (repoRoot, roots = PROVIDER_PROSE_SCAN_ROOTS) => {
  /** @type {{ file: string, text: string }[]} */
  const entries = []
  for (const root of roots) {
    const abs = resolve(repoRoot, root)
    if (!existsSync(abs)) {
      throw new Error(`provider-prose-ownership: scan root missing on disk: ${root}`)
    }
    let text
    try {
      text = readFileSync(abs, 'utf8')
    } catch (cause) {
      throw new Error(`provider-prose-ownership: scan root unreadable: ${root}`, { cause })
    }
    entries.push({ file: norm(root), text })
  }
  return entries
}

/** @param {Hit[]} hits */
export const countByFile = (hits) => {
  /** @type {Record<string, number>} */
  const counts = {}
  for (const h of hits) counts[h.file] = (counts[h.file] ?? 0) + 1
  return counts
}

/**
 * @param {Record<string, number>} baseline
 * @param {Record<string, number>} current
 * @returns {{ ok: boolean, regressions: { file: string, baseline: number, current: number }[] }}
 */
export const compareBaseline = (baseline, current) => {
  /** @type {{ file: string, baseline: number, current: number }[]} */
  const regressions = []
  const files = new Set([...Object.keys(baseline), ...Object.keys(current)])
  for (const file of files) {
    const base = baseline[file] ?? 0
    const now = current[file] ?? 0
    if (now > base) regressions.push({ file, baseline: base, current: now })
  }
  return { ok: regressions.length === 0, regressions }
}

/**
 * @param {string} [repoRoot]
 * @returns {Record<string, number>} non-zero per-file hit counts
 */
export const generateBaseline = (repoRoot = process.cwd(), opts = {}) => {
  const hits = scanEntries(collectEntries(repoRoot, opts.roots))
  return countByFile(hits)
}

/**
 * @param {string} [repoRoot]
 * @param {{ baseline?: Record<string, number> }} [opts]
 * @returns {{ ok: boolean, hits: Hit[], counts: Record<string, number> }}
 */
export const scanRepo = (repoRoot = process.cwd(), opts = {}) => {
  const hits = scanEntries(collectEntries(repoRoot, opts.roots))
  const counts = countByFile(hits)
  if (!opts.baseline) {
    return { ok: hits.length === 0, hits, counts }
  }
  const { ok, regressions } = compareBaseline(opts.baseline, counts)
  return {
    ok,
    hits: ok
      ? []
      : regressions.map((r) => ({
          id: 'baseline-regression',
          file: r.file,
          line: 0,
          text: `hits ${r.current} > baseline ${r.baseline}`,
        })),
    counts,
  }
}

export const parseBaselineArg = (arg) => JSON.parse(readFileSync(arg, 'utf8'))

const runCli = () => {
  const argv = process.argv.slice(2)
  const value = (name) => {
    const hit = argv.find((a) => a.startsWith(`--${name}=`))
    return hit ? hit.slice(name.length + 3) : undefined
  }

  if (argv.includes('--generate')) {
    const out = value('out') ?? DEFAULT_BASELINE
    const counts = generateBaseline()
    writeFileSync(out, `${JSON.stringify(counts, null, 2)}\n`)
    const total = Object.values(counts).reduce((a, b) => a + b, 0)
    console.log(
      `provider-prose-ownership: wrote baseline (${Object.keys(counts).length} files, ${total} hits) → ${out}`,
    )
    process.exit(0)
  }

  const baselinePath = value('baseline')
  const baseline = baselinePath ? parseBaselineArg(baselinePath) : undefined
  const result = scanRepo(process.cwd(), { baseline })

  if (result.ok) {
    const mode = baseline ? 'baseline ratchet' : 'zero hits'
    console.log(`provider-prose-ownership: OK — Gate E provider prose ownership (${mode})`)
    process.exit(0)
  }

  console.error(`provider-prose-ownership: ${result.hits.length} violation(s)\n`)
  for (const h of result.hits) {
    const loc = h.line ? `${h.file}:${h.line}` : h.file
    console.error(`  ${loc}: ${h.id}${h.text ? ` — ${h.text}` : ''}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
