#!/usr/bin/env node
/**
 * ARCH-016 Gate B — Provider Leak Gate.
 * Provider-visible schema / fixed prose / renderer output must not leak internal vocabulary.
 *
 * Usage:
 *   node scripts/checks/provider-leak-gate.mjs [--baseline=<json-path>]
 *   node scripts/checks/provider-leak-gate.mjs --generate [--out=<path>]
 */

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const norm = (p) => p.replace(/\\/g, '/')

/** Provider renderer surfaces (Join / horizon / tool catalog prose). */
export const PROVIDER_SCAN_ROOTS = Object.freeze([
  'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinResultRenderer.fs',
  'src/Wanxiangshu/Execution/Session/OpenCode/HorizonTool.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/ChronicleTool.fs',
  'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs',
  'src/Wanxiangshu/OpenCode/Tools/PtyTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/ExecutorTool.fs',
  'src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/CoderTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/InspectorTool.fs',
  'src/Wanxiangshu/Repository/Programming/Js/OpenCode/BookkeeperTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/FetchTool.fs',
  'src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs',
  'src/Wanxiangshu/OpenCode/Tools/BashHoneypotTool.fs',
  'src/Wanxiangshu/OpenCode/Tools/FileMutationTools.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
  'src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs',
  'src/Wanxiangshu/Repository/Knowledge/Casebook/OpenCode/Tools.fs',
])

/** Lines that assemble provider-visible prose or wire fields. */
const PROVIDER_OUTPUT_LINE_RE =
  /Description\s*=|field\s+"|tomlObject|tomlObjectWithInstructions|\[\s*"error"|ToolHostCodec\.(?:TString|TTable)|\btString\s+\(|\bTString\s+"|instructions\s*=|hookSuffix|catalogDescription/

/** VERIFY-005 Gate B leak vocabulary (substring tokens). */
export const FORBIDDEN_TOKENS = Object.freeze([
  'SessionId',
  'AgentId',
  'ManagerJobId',
  'PtyId',
  'FissionGroupId',
  'lane_index',
  'worktree',
  'FallbackOffset',
  'fallback offset',
  'spoolPath',
  'spool path',
  '/spool/',
  'agent_id',
  'pty_id',
  'session_id',
])

/** Join/horizon generic DTO field names in renderer output. */
export const FORBIDDEN_DTO_PATTERNS = Object.freeze([
  { id: 'field-status', re: /\bfield\s+"status"/ },
  { id: 'field-code', re: /\bfield\s+"code"/ },
  { id: 'field-error', re: /\bfield\s+"error"/ },
  { id: 'field-ordinal', re: /\bfield\s+"ordinal"/ },
  { id: 'field-kind', re: /\bfield\s+"kind"/ },
  { id: 'field-count', re: /\bfield\s+"count"/ },
  { id: 'toml-error-dto', re: /\[\s*"error"\s*,|\[\s*"error",\s*t(?:String|Table)/ },
])

/** fast-/deep- execution binding must not appear in provider-facing prose. */
export const FAST_DEEP_BINDING_RE = /\b(?:fast|deep)-(?:student|teacher|inspector|coder|manager|orchestrator|blogger|devops|browser|inquiry|bookkeeper|reviewer)\b/

export const DEFAULT_BASELINE = join(
  dirname(fileURLToPath(import.meta.url)),
  'provider-leak-gate-baseline.json',
)

/**
 * @typedef {{ id: string, file: string, line: number, text: string }} Violation
 */

/**
 * @param {string} file
 * @param {string} text
 * @returns {Violation[]}
 */
export const scanText = (file, text) => {
  /** @type {Violation[]} */
  const violations = []
  const rel = norm(file)
  const lines = text.split('\n')

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    if (/^\s*\/\//.test(line) || /^\s*\/\*/.test(line) || /^\s*\*\//.test(line)) continue

    for (const token of FORBIDDEN_TOKENS) {
      if (!PROVIDER_OUTPUT_LINE_RE.test(line)) continue
      if (line.includes(token)) {
        violations.push({ id: `token:${token}`, file: rel, line: i + 1, text: line.trim() })
      }
    }

    if (PROVIDER_OUTPUT_LINE_RE.test(line) && FAST_DEEP_BINDING_RE.test(line)) {
      violations.push({ id: 'fast-deep-binding', file: rel, line: i + 1, text: line.trim() })
    }

    for (const { id, re } of FORBIDDEN_DTO_PATTERNS) {
      if (re.test(line)) {
        violations.push({ id, file: rel, line: i + 1, text: line.trim() })
      }
    }
  }

  return violations
}

/**
 * @param {{ file: string, text: string }[]} entries
 * @returns {Violation[]}
 */
export const scanEntries = (entries) => {
  /** @type {Violation[]} */
  const violations = []
  for (const { file, text } of entries) violations.push(...scanText(file, text))
  return violations
}

const collectEntries = (repoRoot) => {
  /** @type {{ file: string, text: string }[]} */
  const entries = []
  for (const root of PROVIDER_SCAN_ROOTS) {
    const abs = resolve(repoRoot, root)
    if (!existsSync(abs)) {
      throw new Error(`provider-leak-gate: scan root missing on disk: ${root}`)
    }
    if (abs.endsWith('.fs')) {
      entries.push({ file: norm(root), text: readFileSync(abs, 'utf8') })
      continue
    }
    for (const file of walk(abs, ['.fs'])) {
      entries.push({
        file: norm(file.slice(repoRoot.length + 1)),
        text: readFileSync(file, 'utf8'),
      })
    }
  }
  return entries
}

/** @param {Violation[]} violations */
export const countByFile = (violations) => {
  /** @type {Record<string, number>} */
  const counts = {}
  for (const v of violations) counts[v.file] = (counts[v.file] ?? 0) + 1
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
 * @param {{ baseline?: Record<string, number> }} [opts]
 * @returns {{ ok: boolean, violations: Violation[], counts: Record<string, number> }}
 */
export const scanRepo = (repoRoot = process.cwd(), opts = {}) => {
  const violations = scanEntries(collectEntries(repoRoot))
  const counts = countByFile(violations)
  if (!opts.baseline) return { ok: violations.length === 0, violations, counts }
  const { ok, regressions } = compareBaseline(opts.baseline, counts)
  return {
    ok,
    violations: ok
      ? []
      : regressions.map((r) => ({
          id: 'baseline-regression',
          file: r.file,
          line: 0,
          text: `violations ${r.current} > baseline ${r.baseline}`,
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
    const { counts } = scanRepo()
    writeFileSync(out, `${JSON.stringify(counts, null, 2)}\n`)
    console.log(`provider-leak-gate: wrote baseline (${Object.keys(counts).length} files) → ${out}`)
    process.exit(0)
  }

  const baselinePath = value('baseline')
  const baseline = baselinePath ? parseBaselineArg(baselinePath) : undefined
  const result = scanRepo(process.cwd(), { baseline })

  if (result.ok) {
    const mode = baseline ? 'baseline ratchet' : 'zero violations'
    console.log(`provider-leak-gate: OK — provider renderer surfaces pass Gate B (${mode})`)
    process.exit(0)
  }

  console.error(`provider-leak-gate: ${result.violations.length} violation(s)\n`)
  for (const v of result.violations) {
    const loc = v.line ? `${v.file}:${v.line}` : v.file
    console.error(`  ${loc}: ${v.id}${v.text ? ` — ${v.text}` : ''}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
