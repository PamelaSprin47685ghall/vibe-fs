#!/usr/bin/env node
// Kolmogorov size advisory (refactor proposal Wave 0).
// Structural signal only — line counts are symptoms, ownership is the disease.
// This check is intentionally NON-BLOCKING: no file/function size threshold may
// make `npm run check` fail. The baseline is comparative context, not a budget.
//
// Modes:
//   node scripts/checks/kolmogorov-size.mjs [--baseline=<json-string|path>] [--root=<dir>]
//       report suggestions only; always exit 0 for size findings.
//       With a baseline, growth is called out as a stronger refactor suggestion.
//   node scripts/checks/kolmogorov-size.mjs --generate [--out=<file>] [--root=<dir>]
//       write a snapshot of current line counts for every file > SOFT_LIMIT
//
// Baseline JSON: { "<path>": <lines>, ..., "_exceptions": { "<path>": {
//   "owner": "...", "reason": "..." } } }. Exceptions document why a large file
// may be coherent (ports, entry points, pure vocabulary, generated/declarative
// tables, compile-order seams). They are explanatory only.
//
// Advisory signals include:
//   - file > 200 lines
//   - growth beyond a recorded baseline
//   - ordinary function > 60 lines (F# heuristic)
//   - implementation file <= 15 lines not covered by an exception

import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const SOFT_LIMIT = 200
const FUNCTION_WARN = 60
const SMALL_WARN = 15

const SCOPES = ['src/Wanxiangshu', 'requirements', 'scripts']
const EXTENSIONS = ['.fs', '.mjs', '.js']
const DEFAULT_OUT = join(dirname(fileURLToPath(import.meta.url)), 'kolmogorov-size-baseline.json')

const norm = (p) => p.replace(/\\/g, '/')

const args = process.argv.slice(2)
const argValue = (flag) => {
  const inline = args.find((a) => a.startsWith(`${flag}=`))
  if (inline) return inline.slice(flag.length + 1)
  const index = args.indexOf(flag)
  return index >= 0 ? args[index + 1] : undefined
}

const parseBaseline = (arg) => {
  try {
    return JSON.parse(arg)
  } catch {
    // Not inline JSON — treat as a file path.
  }
  return JSON.parse(readFileSync(arg, 'utf8'))
}

/** Physical line count (wc -l semantics: number of newline-terminated lines). */
const lineCount = (content) => content.split('\n').length - 1

/** Scoped files as root-relative POSIX paths, sorted. */
const scopedFiles = (root) => {
  const files = []
  for (const scope of SCOPES) {
    for (const abs of walk(join(root, scope), EXTENSIONS)) {
      files.push(norm(relative(root, abs)))
    }
  }
  return files.sort()
}

/**
 * Advisory F# function-size heuristic: a binding line at indentation N is
 * "open" until the next non-blank line at indentation <= N (or EOF). Returns
 * [{ name, lines }] for bindings whose body exceeds FUNCTION_WARN.
 */
const oversizedFunctions = (path) => {
  if (!path.endsWith('.fs')) return []
  const lines = readFileSync(path, 'utf8').split('\n')
  const out = []
  let open = null // { name, indent, start }
  let lastIndent = 0
  const close = (index) => {
    if (open && index - open.start > FUNCTION_WARN) {
      out.push({ name: open.name, lines: index - open.start })
    }
    open = null
  }
  for (let i = 0; i < lines.length; i++) {
    const text = lines[i]
    if (text.trim() === '') continue
    if (text.trim().startsWith('//') || text.trim().startsWith('(*')) {
      if (open && text.trim().startsWith('//') && text.trimStart().length > open.indent + 4) continue
      close(i)
      continue
    }
    const indent = text.length - text.trimStart().length
    const trimmed = text.trimStart()
    if (open) {
      if (indent <= open.indent) {
        close(i)
      } else {
        lastIndent = indent
        continue
      }
    }
    const binding = trimmed.match(/^(?:let\s+(?:rec\s+|inline\s+|private\s+|internal\s+)*[A-Za-z_][\w']*|(?:static\s+)?member\s+(?:this\.|_\.)?[A-Za-z_][\w']*)/)
    if (binding && !trimmed.includes('=')) {
      // Multi-line signature: keep scanning the current binding.
      if (!open) open = { name: binding[0].replace(/^(let|member|static member)\s+/, ''), indent, start: i }
      continue
    }
    if (binding) {
      if (open) close(i)
      open = { name: binding[0].replace(/^(let|member|static member)\s+/, ''), indent, start: i }
    }
    lastIndent = indent
  }
  close(lines.length)
  return out
}

const check = (root, baseline = {}) => {
  const suggestions = []
  const exceptions = baseline._exceptions ?? {}
  const counts = baseline.baseline ?? baseline
  for (const path of scopedFiles(root)) {
    const abs = join(root, path)
    const content = readFileSync(abs, 'utf8')
    const lines = lineCount(content)
    const grandfathered = typeof counts[path] === 'number'
    if (grandfathered && lines > counts[path]) {
      suggestions.push(`${path}: ${lines} lines grew beyond baseline ${counts[path]} — consider re-owning or splitting if cohesion improved`)
    } else if (!grandfathered && lines > SOFT_LIMIT) {
      const exception = exceptions[path]
      if (exception) {
        suggestions.push(`${path}: ${lines} lines (documented ownership: ${exception.owner} — ${exception.reason})`)
      } else {
        suggestions.push(`${path}: ${lines} lines exceeds advisory ${SOFT_LIMIT} — review ownership/cohesion; split only if it clarifies the model`)
      }
    }
    if (!grandfathered && lines <= SMALL_WARN && !exceptions[path] && path.endsWith('.fs')) {
      suggestions.push(`${path}: ${lines} lines — small implementation; review whether it should be deleted, re-owned, or remain a legal seam`)
    }
    for (const fn of oversizedFunctions(abs)) {
      suggestions.push(`${path}: function '${fn.name}' spans ${fn.lines} lines (> advisory ${FUNCTION_WARN})`)
    }
  }
  return { suggestions }
}

const main = () => {
  const root = resolve(argValue('--root') ?? '.')
  const baselineArg = argValue('--baseline')
  if (args.includes('--generate')) {
    const counts = {}
    for (const path of scopedFiles(root)) {
      const lines = lineCount(readFileSync(join(root, path), 'utf8'))
      if (lines > SOFT_LIMIT) counts[path] = lines
    }
    const out = argValue('--out') ?? DEFAULT_OUT
    writeFileSync(out, `${JSON.stringify({ baseline: counts, _exceptions: {} }, null, 2)}\n`)
    const total = Object.keys(counts).length
    console.log(`kolmogorov-size: baseline written to ${out} — ${total} grandfathered file(s)`)
    return
  }
  const baseline = baselineArg ? parseBaseline(baselineArg) : {}
  const { suggestions } = check(root, baseline)
  for (const suggestion of suggestions) console.log(`suggestion: ${suggestion}`)
  console.log(`kolmogorov-size: advisory only — ${suggestions.length} suggestion(s), 0 blocking finding(s)`)
  process.exit(0)
}

main()
