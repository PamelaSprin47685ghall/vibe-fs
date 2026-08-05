#!/usr/bin/env node
// P0-RECOVERY-JOIN-001 §10: production source patterns that reintroduce false finality.
//
// Modes:
//   node scripts/checks/p0-recovery-join.mjs           scan production tree
//   import { scanText, scanFiles, RULES } from ...      pure synthetic tests

import { readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'

/** @typedef {{ id: string, fileHint: string | null, pattern: RegExp, label: string, positive?: boolean }} Rule */

/** Pure rules: each id is one CI-checked invariant. */
export const RULES = [
  {
    id: 'lifecycle-aborted-completion',
    fileHint: 'HostForkRunLifecycle.fs',
    pattern: /AgentCompletion\.aborted\b/,
    label: 'HostForkRunLifecycle must not mint AgentCompletion.aborted',
  },
  {
    id: 'lifecycle-aborted-record',
    fileHint: 'HostForkRunLifecycle.fs',
    // Aborted branch that still calls recordCompletion (comment-stripped line)
    pattern: /\brecordCompletion\b[\s\S]{0,80}\bAborted\b|\bAborted\b[\s\S]{0,120}\brecordCompletion\b/,
    label: 'TerminalOutcome.Aborted must not call recordCompletion',
  },
  {
    id: 'lifecycle-aborted-setresult',
    fileHint: 'HostForkRunLifecycle.fs',
    pattern: /\bAborted\b[\s\S]{0,200}\.SetResult\b|\.SetResult\b[\s\S]{0,80}\bAborted\b/,
    label: 'Aborted path must not SetResult on completion cell',
  },
  {
    id: 'fork-recovery-synthetic-restored',
    fileHint: 'ForkRecovery.fs',
    pattern: /ofSimpleText[\s\S]{0,160}?restored|Completion\.TrySet|\.TrySetResult\b/,
    label: 'ForkRecovery must not synthesize restored completions',
  },
  {
    id: 'fork-recovery-interrupted-finality',
    fileHint: 'ForkRecovery.fs',
    pattern: /RunCompletion|makeAborted|AgentCompletion\.(?:aborted|failed|completed)|INTERRUPTED/,
    label: 'ForkRecovery.markInterrupted must not construct RunCompletion / INTERRUPTED finality',
  },
  {
    id: 'ensure-recovery-unit',
    fileHint: 'PluginRuntimeScope.fs',
    // Collapse FamilyRecovery → Task unit (old fail-open shape)
    pattern: /EnsureRecoveryDone[^\n]*:\s*Task\s*<\s*unit\s*>/,
    label: 'EnsureRecoveryDone must not return Task<unit>',
  },
  {
    id: 'missing-ports-family-ready',
    fileHint: 'PluginRuntimeScope.fs',
    // Synthetic FamilyReady when ports missing
    pattern:
      /familyRecoveryPorts[\s\S]{0,200}None[\s\S]{0,120}FamilyReady|None\s*->\s*[\s\S]{0,80}FamilyReady/,
    label: 'missing ports must not synthesize FamilyReady',
  },
  {
    id: 'join-tool-family-recovery',
    fileHint: 'JoinTool.fs',
    pattern: /RequireFamilyRecovery|EnsureRecoveryDone|FamilyReady/,
    label: 'JoinTool must RequireFamilyRecovery / match FamilyReady',
    positive: true,
  },
]

export const RULE_IDS = RULES.map((r) => r.id)

const norm = (p) => p.replace(/\\/g, '/')

const stripComments = (line) => line.replace(/\/\/.*/g, '')

/**
 * Scan one file body. Multi-line rules see joined non-comment text; single-line
 * rules still report the first matching line number when possible.
 * @returns {{ id: string, file: string, line: number, label: string, text: string }[]}
 */
export const scanText = (text, file = '<synthetic>') => {
  const base = file.split(/[/\\]/).pop() || file
  const lines = text.split('\n')
  const codeLines = lines.map((line) => stripComments(line))
  const joined = codeLines.join('\n')
  const hits = []

  for (const rule of RULES) {
    if (rule.fileHint && base !== rule.fileHint && file !== '<synthetic>') {
      // Production scan: only apply file-scoped rules to matching basename.
      // Synthetic tests pass file = the basename under test.
      if (!file.endsWith(rule.fileHint) && base !== rule.fileHint) continue
    }

    if (rule.positive) {
      if (!rule.pattern.test(joined) && !rule.pattern.test(text)) {
        hits.push({
          id: rule.id,
          file,
          line: 1,
          label: rule.label,
          text: 'missing required pattern',
        })
      }
      continue
    }

    // Prefer line-local match for simple patterns; fall back to multi-line search.
    let found = false
    for (let i = 0; i < codeLines.length; i++) {
      if (rule.pattern.test(codeLines[i])) {
        hits.push({
          id: rule.id,
          file,
          line: i + 1,
          label: rule.label,
          text: lines[i].trim(),
        })
        found = true
        break
      }
    }
    if (!found && rule.pattern.test(joined)) {
      // Multi-line hit: approximate first line of match.
      const m = joined.match(rule.pattern)
      let line = 1
      if (m && typeof m.index === 'number') {
        line = joined.slice(0, m.index).split('\n').length
      }
      hits.push({
        id: rule.id,
        file,
        line,
        label: rule.label,
        text: (m && m[0] ? m[0].replace(/\s+/g, ' ').slice(0, 120) : rule.label),
      })
    }
  }
  return hits
}

/** @param {{ file: string, text: string }[]} entries */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    for (const hit of scanText(entry.text, entry.file)) violations.push(hit)
  }
  return violations
}

const runCli = () => {
  const productionFiles = walk(PRODUCTION_ROOT, ['.fs']).map(norm)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const violations = scanFiles(entries)

  if (violations.length === 0) {
    console.log(`p0-recovery-join: OK — ${productionFiles.length} files, ${RULES.length} rules`)
    process.exit(0)
  }

  console.error(`p0-recovery-join: ${violations.length} violation(s)\n`)
  for (const v of violations) {
    console.error(`  [${v.id}] ${v.file}:${v.line}  ${v.label}`)
    console.error(`    ${v.text}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
