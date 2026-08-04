#!/usr/bin/env node
// Normalizes Markdown prose typography, leaving fenced code blocks untouched.
// AGENTS.md 「思考和输出」forbids decorative formatting and requires correct
// full-width punctuation; docs are prose, so a mechanical pass is legitimate
// here (unlike code, which is edited by hand).
//
// Rules:
//   1. strip ** bold
//   2. collapse the ASCII space that bold removal leaves after a full-width
//      colon — 「禁止： x」 becomes 「禁止：x」. Full-width punctuation already
//      carries its own spacing.
//
//   node scripts/strip-doc-bold.mjs            report
//   node scripts/strip-doc-bold.mjs --write    rewrite in place

import { readFileSync, writeFileSync } from 'node:fs'
import { walk } from './repo-scan.mjs'

// Active prose only. Historical evidence/archive retains original markup.
const ROOTS = [
  'spec',
  'docs/architecture.md',
  'docs/development.md',
  'docs/releasing.md',
  'docs/decisions',
  'docs/rfcs',
  'AGENTS.md',
  'README.md',
  'CHANGELOG.md',
]

const RULES = [
  { name: 'bold', pattern: /\*\*(?=\S)([^*]+?)(?<=\S)\*\*/g, replacement: '$1' },
  { name: 'fullwidth-colon-space', pattern: /：[ \t]+/g, replacement: '：' },
]

const write = process.argv.includes('--write')
const files = ROOTS.flatMap((root) => walk(root, ['.md']))

const counts = new Map(RULES.map((rule) => [rule.name, 0]))
let totalHits = 0
let touched = 0

for (const file of files) {
  const original = readFileSync(file, 'utf8')
  let inFence = false
  let hits = 0

  const normalized = original.split('\n').map((line) => {
    if (line.trimStart().startsWith('```')) {
      inFence = !inFence
      return line
    }
    if (inFence) return line

    let next = line
    for (const rule of RULES) {
      const applied = next.replace(rule.pattern, rule.replacement)
      if (applied !== next) counts.set(rule.name, counts.get(rule.name) + 1)
      next = applied
    }
    if (next !== line) hits += 1
    return next
  })

  if (hits === 0) continue
  totalHits += hits
  touched += 1
  console.log(`${file}: ${hits} line(s)`)
  if (write) writeFileSync(file, normalized.join('\n'))
}

for (const [name, count] of counts) {
  if (count > 0) console.log(`  rule ${name}: ${count} line(s)`)
}

console.log(
  totalHits === 0
    ? 'strip-doc-bold: clean'
    : `strip-doc-bold: ${totalHits} line(s) in ${touched} file(s)${write ? ' rewritten' : ' — pass --write to apply'}`,
)
process.exit(write || totalHits === 0 ? 0 : 1)
