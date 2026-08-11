#!/usr/bin/env node
// Kolmogorov file-size ratchet (refactor proposal Wave 0).
// Structural alarm only — line counts are symptoms, ownership is the disease.
//
// Modes:
//   node scripts/checks/kolmogorov-size.mjs --baseline=<json-string|path> [--root=<dir>]
//       exit 1 when:
//         - a grandfathered file (in baseline) exceeds its recorded line count
//         - any other file exceeds SOFT_LIMIT (200) lines
//   node scripts/checks/kolmogorov-size.mjs --generate [--out=<file>] [--root=<dir>]
//       write a baseline of current line counts for every file > SOFT_LIMIT
//
// Baseline JSON: { "<path>": <lines>, ..., "_exceptions": { "<path>": {
//   "owner": "...", "reason": "..." } } }. Exceptions document WHY a file may
// stay large (ports, entry points, pure vocabulary, generated/declarative
// tables, compile-order seams) — they do not lift the ratchet for files that
// are already in the baseline.
//
// Warnings only (never fail):
//   - ordinary function > 60 lines (F# heuristic, advisory)
//   - implementation file <= 15 lines not covered by an exception

import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const SOFT_LIMIT = 200
const FUNCTION_WARN = 60
const SMALL_WARN = 15

const SCOPES = ['src/Wanxiangshu', 'tests', 'scripts']
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

const check = (root, baseline) => {
  const failures = []
  const warnings = []
  const exceptions = baseline._exceptions ?? {}
  const counts = baseline.baseline ?? baseline
  for (const path of scopedFiles(root)) {
    const abs = join(root, path)
    const content = readFileSync(abs, 'utf8')
    const lines = lineCount(content)
    const grandfathered = typeof counts[path] === 'number'
    if (grandfathered) {
      if (lines > counts[path]) {
        failures.push(`${path}: ${lines} lines exceeds baseline ${counts[path]} (ratchet)`)
      }
    } else if (lines > SOFT_LIMIT) {
      const exception = exceptions[path]
      if (exception) {
        warnings.push(`${path}: ${lines} lines (exception: ${exception.owner} — ${exception.reason})`)
      } else {
        failures.push(`${path}: ${lines} lines exceeds ${SOFT_LIMIT} (new or previously-small file; split or grandparent it)`)
      }
    }
    if (!grandfathered && lines <= SMALL_WARN && !exceptions[path] && path.endsWith('.fs')) {
      warnings.push(`${path}: ${lines} lines — small implementation, review: delete / re-own / legal seam?`)
    }
    for (const fn of oversizedFunctions(abs)) {
      warnings.push(`${path}: function '${fn.name}' spans ${fn.lines} lines (> ${FUNCTION_WARN}, advisory)`)
    }
  }
  return { failures, warnings }
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
  if (!baselineArg) {
    console.error('kolmogorov-size: --baseline=<json|path> required (or --generate)')
    process.exit(2)
  }
  const baseline = parseBaseline(baselineArg)
  const { failures, warnings } = check(root, baseline)
  for (const warning of warnings) console.log(`warning: ${warning}`)
  for (const failure of failures) console.error(`FAIL: ${failure}`)
  console.log(`kolmogorov-size: ${failures.length} failure(s), ${warnings.length} warning(s)`)
  process.exit(failures.length > 0 ? 1 : 0)
}

main()
