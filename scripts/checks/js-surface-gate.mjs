#!/usr/bin/env node
// JS-001 / JS-004 static gate: no handwritten role→JS tool matrix, no
// G3-rebase-debt js-student/js-teacher surface, no Meditator filesystem JS.
//
// The only legitimate js-* tool name is the one JsToolGenerator.toolNameFor
// builds at runtime ("js-" + roleName). Any literal per-role js-* name in
// production source means a handwritten variant was introduced — fail closed.
//
// Usage: node scripts/checks/js-surface-gate.mjs

import { readFileSync } from 'node:fs'
import { walk } from '../lib/walk.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu'

/** G3 rebase debt: Student/Teacher must never get a js-* surface again. */
export const FORBIDDEN_TOKENS = [
  'js-student',
  'js-teacher',
  'JsStudent',
  'JsTeacher',
  'StudentCompileJs',
  'StudentLearnJs',
  'StudentTeacherJs',
]

/** Literal per-role js-* tool names — only the generator may produce them, at runtime. */
export const HANDWRITTEN_ROLE_TOOL_TOKENS = [
  'js-coder',
  'js-inspector',
  'js-reviewer',
  'js-devops',
  'js-browser',
  'js-meditator',
]

const norm = (path) => path.replace(/\\/g, '/')

export const scanEntries = (entries) => {
  const violations = []
  for (const { file, text } of entries) {
    const lines = text.split('\n')
    const check = (i, token, kind) =>
      violations.push({ file, line: i + 1, token, kind, text: lines[i].trim() })
    for (let i = 0; i < lines.length; i++) {
      for (const token of FORBIDDEN_TOKENS) {
        if (lines[i].includes(token)) check(i, token, 'forbidden')
      }
      for (const token of HANDWRITTEN_ROLE_TOOL_TOKENS) {
        if (lines[i].includes(token)) check(i, token, 'handwritten-role-tool')
      }
    }
  }
  return violations
}

const main = () => {
  const entries = walk(PRODUCTION_ROOT)
    .filter((file) => file.endsWith('.fs') || file.endsWith('.fsi'))
    .map((file) => ({ file: norm(file), text: readFileSync(file, 'utf8') }))
  const violations = scanEntries(entries)
  if (violations.length === 0) {
    console.log('js-surface-gate: OK — no handwritten js-* role variants, no Student/Teacher js surface')
    return
  }
  console.error(`js-surface-gate: ${violations.length} 处违规`)
  for (const v of violations) {
    console.error(`  ${v.file}:${v.line}  ${v.kind}: ${v.token}  (${v.text})`)
  }
  process.exit(1)
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main()
}
