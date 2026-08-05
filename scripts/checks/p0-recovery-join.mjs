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
  {
    id: 'join-tool-family-blocked',
    fileHint: 'JoinTool.fs',
    pattern: /FamilyBlocked/,
    label: 'JoinTool must match FamilyBlocked',
    positive: true,
  },
  {
    id: 'join-tool-join-program',
    fileHint: 'JoinTool.fs',
    pattern: /joinAny|JoinProgram|JoinInterpreter/,
    label: 'JoinTool must enter JoinProgram / joinAny / JoinInterpreter',
    positive: true,
  },
  {
    id: 'join-tool-no-bare-runtime-join',
    fileHint: 'JoinTool.fs',
    // P0 §五 / §十: JoinTool must not call runtime.Join directly.
    pattern: /runtime\.Join\s*\(/,
    label: 'JoinTool must not call runtime.Join; use joinAny + JoinInterpreter',
  },
  {
    id: 'host-fork-restart-false-finality',
    fileHint: 'HostForkRestart.fs',
    // Synthetic aborted / restored finality must not be published on restart.
    pattern: /AgentCompletion\.aborted|makeAborted|ofSimpleText[\s\S]{0,100}?restored/,
    label: 'HostForkRestart must not mint aborted or synthetic restored finality',
  },
  {
    id: 'host-fork-restart-proof-structure',
    fileHint: 'HostForkRestart.fs',
    // Restart recovery must walk interpreter / JoinableCompletion path.
    pattern:
      /ChildRecoveryInterpreter|tryFromProvenTerminal|JoinableCompletion|recordCompletion|HandleCompletionCodec\.tryRead/,
    label: 'HostForkRestart must use proven terminal or durable completion structure',
    positive: true,
  },
  {
    id: 'host-fork-restart-bare-publish',
    fileHint: 'HostForkRestart.fs',
    pattern: /AgentCompletion\.completed[\s\S]{0,400}PublishCompletion/,
    label: 'HostForkRestart must not PublishCompletion from bare AgentCompletion.completed',
  },
  {
    id: 'fork-runtime-parent-cancelled-aborted',
    fileHint: 'ForkRuntime.fs',
    pattern: /ParentCancelled[\s\S]{0,120}makeAborted|makeAborted[\s\S]{0,80}parent cancelled/,
    label: 'ParentCancelled must not mint makeAborted completion cell',
  },
  {
    // P0 §十: production recordCompletion call sites must be definition or ChildRecoveryInterpreter.
    // Scanned across all src/Wanxiangshu/**/*.fs (no fileHint). Comments stripped before match.
    id: 'record-completion-single-owner',
    fileHint: null,
    pattern: /\brecordCompletion\b/,
    label:
      'HandleController.recordCompletion production caller must be only ChildRecoveryInterpreter (or definition)',
  },
]

export const RULE_IDS = RULES.map((r) => r.id)

const norm = (p) => p.replace(/\\/g, '/')

const stripComments = (line) => line.replace(/\/\/.*/g, '')

/** Basename allowlist for record-completion-single-owner (definition + sole commit owner). */
const RECORD_COMPLETION_OWNER_BASENAMES = new Set([
  'HandleController.fs',
  'ChildRecoveryInterpreter.fs',
])

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
        // Sole-owner rule: definition + ChildRecoveryInterpreter may call; others red.
        if (
          rule.id === 'record-completion-single-owner' &&
          RECORD_COMPLETION_OWNER_BASENAMES.has(base)
        ) {
          found = true
          break
        }
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
      if (
        rule.id === 'record-completion-single-owner' &&
        RECORD_COMPLETION_OWNER_BASENAMES.has(base)
      ) {
        continue
      }
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
