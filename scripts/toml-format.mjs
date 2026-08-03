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

const SCENARIO_ROOT = 'tests/e2e/scripts'
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
 * Net unclosed `[` / `{` on one line, ignoring brackets inside strings and comments.
 *
 * The first real conversion showed the formatter flattening a multi-line `flow = [`
 * array to column zero. Balance has to be counted rather than pattern-matched: a header
 * line `[[turn]]` is balanced, inline tables nest, and a bracket inside
 * `command = "sh -lc '[...]'"` must not count at all.
 */
const bracketDelta = (line) => {
  let delta = 0
  let quote = null

  for (let index = 0; index < line.length; index += 1) {
    const char = line[index]

    if (quote !== null) {
      if (char === '\\') index += 1
      else if (char === quote) quote = null
      continue
    }
    if (char === '"' || char === "'") {
      quote = char
      continue
    }
    if (char === '#') break
    if (char === '[' || char === '{') delta += 1
    if (char === ']' || char === '}') delta -= 1
  }

  return delta
}

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
  //
  // `open` tracks unclosed brackets so the continuation lines of a multi-line
  // `flow = [...]` array indent one level past the key that opened it. Without it the
  // first real conversion flattened every flow step to column zero.
  const indents = []
  let depth = 0
  let open = 0
  lines.forEach((text, index) => {
    if (literal[index] || text === '' || text.startsWith('#')) {
      indents.push(null)
      return
    }

    if (isHeader(text) && open === 0) depth = headerDepth(text)

    // A line that STARTS by closing belongs to the level its opener was on, so a
    // multi-line array's `]` returns to the column of `flow = [`.
    const level = /^[\]}]/.test(text) ? Math.max(0, open - 1) : open
    indents.push(depth + (level > 0 ? 1 : 0))
    open = Math.max(0, open + bracketDelta(text))
  })

  // Second pass: a comment adopts the indent of the next CONTENT line, skipping blanks
  // and other comments. Falling back to the running depth put a file-header comment at
  // the depth the file happened to end on.
  let nextContent = 0
  for (let index = indents.length - 1; index >= 0; index -= 1) {
    if (indents[index] !== null) {
      nextContent = indents[index]
      continue
    }
    if (!literal[index] && lines[index].startsWith('#')) indents[index] = nextContent
  }

  // Third pass: where a block begins.
  //
  // A top-level header starts one — but comments sitting directly above it INTRODUCE it,
  // and pass 2 already gave them its indent. So the blank line belongs before the first
  // of those comments, not between the comment and the header it explains. Caught on the
  // third real conversion, where `# Attempt 1, at Offset 0 → SideA.` was pushed one
  // blank line away from its `[[turn]]`.
  const blockStart = lines.map(() => false)
  lines.forEach((text, index) => {
    if (literal[index] || !isHeader(text) || headerDepth(text) !== 0) return

    let start = index
    while (start > 0 && !literal[start - 1] && lines[start - 1].startsWith('#')) start -= 1
    blockStart[start] = true
  })

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

    // One blank line before each turn, so they read as blocks.
    if (blockStart[index] && out.length > 0 && out[out.length - 1] !== '') out.push('')

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
