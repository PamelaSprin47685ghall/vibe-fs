#!/usr/bin/env node
// scripts/toml-format.mjs — one indentation convention for every scenario file.
//
// VERIFY-003 §10. Indentation is meaningless to TOML: the two spaces before
// `[[turn.step]]` exist purely so a reader sees the nesting. Meaningless-to-the-parser
// means nothing keeps it consistent, so it drifts — and a scenario is the one place in
// this architecture that describes how a model replies, so its readability decides
// whether it can be trusted.
//
// LINE-BASED, not parse-and-restringify. `parse` → `stringify` would produce canonical
// TOML and destroy every comment, and comments are the main reason §10 chose TOML over
// JSON: `# REVIEW-003: two PERFECT verdicts…` sits next to the two steps it constrains.
// A formatter that discards them removes the format's whole advantage.
//
//   node scripts/toml-format.mjs                 check every scenario, exit 1 on drift
//   node scripts/toml-format.mjs --write         rewrite in place
//   node scripts/toml-format.mjs path/a.toml     restrict to given files

import { readFileSync, writeFileSync } from 'node:fs'
import { walk } from './repo-scan.mjs'

const SCENARIO_ROOT = 'testkit/opencode/scripts'
const INDENT = '  '

/** `[[turn.step]]` nests one level deeper than `[[turn]]`. */
const headerDepth = (text) => {
  const path = text.replace(/^\[+/, '').replace(/\]+$/, '')
  return path.split('.').length - 1
}

const isHeader = (text) => text.startsWith('[')

/**
 * How many `"""` / `'''` delimiters a line opens or closes.
 *
 * Odd means the line toggles multi-line-string state. Counting rather than testing for
 * a prefix because `user = """text"""` opens and closes on one line, and `"""` can also
 * appear at the end of a line that started inside the block.
 */
const delimiterCount = (line) => (line.match(/"""|'''/g) ?? []).length

/**
 * Re-indent one scenario.
 *
 * Lines INSIDE a multi-line string are copied byte for byte. Trimming them changes the
 * string's value: measured on §10's own example, `"  Read AGENTS.md.\n    Then fix…"`
 * became `"Read AGENTS.md.\nThen fix…"` — a formatter silently rewriting the prompt a
 * scenario declares, which would then no longer match what production sends.
 *
 * A comment takes the indent of the content it introduces, so a clause reference stays
 * visually attached to the step it explains.
 */
export function formatToml(source) {
  const raw = source.split('\n')

  // Which lines are interior to a multi-line string, and therefore untouchable.
  const literal = []
  let inside = false
  for (const line of raw) {
    literal.push(inside)
    if (delimiterCount(line) % 2 === 1) inside = !inside
  }

  const lines = raw.map((line, index) => (literal[index] ? line : line.trim()))

  // First pass: the indent each line will get, ignoring comments.
  const indents = []
  let depth = 0
  lines.forEach((text, index) => {
    if (literal[index]) {
      indents.push(null)
      return
    }
    if (text === '' || text.startsWith('#')) {
      indents.push(null)
      return
    }
    if (isHeader(text)) depth = headerDepth(text)
    indents.push(depth)
  })

  // Second pass: a comment adopts the next content line's indent.
  for (let index = indents.length - 1; index >= 0; index -= 1) {
    if (indents[index] === null && !literal[index] && lines[index].startsWith('#')) {
      indents[index] = indents[index + 1] ?? depth
    }
  }

  const out = []
  lines.forEach((text, index) => {
    if (literal[index]) {
      out.push(text)
      return
    }

    if (text === '') {
      // Collapse blank runs; a leading blank line is dropped entirely.
      if (out.length > 0 && out[out.length - 1] !== '') out.push('')
      return
    }

    // One blank line before a top-level header, so turns read as blocks. Nested
    // headers stay attached to their parent.
    if (isHeader(text) && headerDepth(text) === 0 && out.length > 0 && out[out.length - 1] !== '') {
      out.push('')
    }

    out.push(`${INDENT.repeat(indents[index] ?? 0)}${text}`)
  })

  while (out.length > 0 && out[out.length - 1] === '') out.pop()
  return `${out.join('\n')}\n`
}

// ── cli ─────────────────────────────────────────────────────────────────────

const isMain = process.argv[1] !== undefined && import.meta.url.endsWith(process.argv[1].replace(/^.*?(scripts\/)/, '$1'))

if (isMain) {
  const write = process.argv.includes('--write')
  const explicit = process.argv.slice(2).filter((argument) => argument.endsWith('.toml'))
  const files = explicit.length > 0 ? explicit : walk(SCENARIO_ROOT, ['.toml'])

  const drifted = []
  for (const file of files) {
    const source = readFileSync(file, 'utf8')
    const formatted = formatToml(source)
    if (formatted === source) continue

    drifted.push(file)
    if (write) writeFileSync(file, formatted)
  }

  if (files.length === 0) {
    console.log(`toml-format: no .toml under ${SCENARIO_ROOT}/ yet`)
    process.exit(0)
  }
  if (drifted.length === 0) {
    console.log(`toml-format: clean — ${files.length} file(s)`)
    process.exit(0)
  }
  if (write) {
    console.log(`toml-format: rewrote ${drifted.length} of ${files.length} file(s)`)
    process.exit(0)
  }

  console.error(`toml-format: ${drifted.length} file(s) need formatting — run with --write\n`)
  for (const file of drifted) console.error(`  ${file}`)
  process.exit(1)
}
