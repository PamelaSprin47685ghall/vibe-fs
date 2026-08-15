// requirement-trace.mjs — pure data graph for the test ↔ WHAT closure.
//
//   WhatNode { id, package, file, line, heading }
//   TestNode { file, line, title, state: active|skip|todo, whatIds: string[] }
//   Edge    { test → what }
//
// The scanner is a lightweight tokenizer over JS sources: it distinguishes
// string literals, comments, template literals (incl. ${} nesting), and
// call sites `test(` / `t.test(` / `test.skip(` / `test.todo(`. A bare regex
// would count `test(` inside strings and comments; this scanner does not.
//
// Machine contract (REQUIREMENT-SYSTEM-018): a test declares exactly one
// primary WHAT via `WHAT[<CURRENT-WHAT-ID>]` in its title. Historic IDs,
// path-implicit ownership, and comment prose are not recognized.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'

import { walk } from './walk.mjs'

export const WHAT_TAG_RE = /WHAT\[([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9-]+)?)\]/g

const CALLABLE = new Set(['test', 'describe', 'it'])
const STATE_MODIFIER = new Set(['skip', 'todo'])

const isIdentStart = (ch) => /[A-Za-z_$]/.test(ch)
const isIdentPart = (ch) => /[A-Za-z0-9_$]/.test(ch)
const isSpace = (ch) => ch === ' ' || ch === '\t' || ch === '\r' || ch === '\n'

/**
 * Scan one JS test source for test call sites.
 * Accepts a source string (for unit tests) or a file path.
 */
export function scanTestSource(file, source) {
  const text = source === undefined ? readFileSync(file, 'utf8') : source
  const calls = []

  let i = 0
  let line = 1
  const n = text.length

  const skipLineComment = () => {
    while (i < n && text[i] !== '\n') i++
  }
  const skipBlockComment = () => {
    i += 2
    while (i < n) {
      if (text[i] === '\n') line++
      else if (text[i] === '*' && text[i + 1] === '/') {
        i += 2
        return
      }
      i++
    }
  }
  const skipString = (quote) => {
    i++
    while (i < n) {
      const ch = text[i]
      if (ch === '\\') i += 2
      else if (ch === quote) {
        i++
        return
      } else {
        if (ch === '\n') line++
        i++
      }
    }
  }
  const skipTemplate = () => {
    // i points at the opening backtick. Track ${ ... } nesting so a backtick
    // inside an embedded expression does not terminate the template.
    i++
    let depth = 0
    while (i < n) {
      const ch = text[i]
      if (ch === '\\') i += 2
      else if (ch === '`' && depth === 0) {
        i++
        return
      } else if (ch === '$' && text[i + 1] === '{') {
        depth++
        i += 2
      } else if (ch === '}' && depth > 0) {
        depth--
        i++
      } else {
        if (ch === '\n') line++
        i++
      }
    }
  }

  const skipWs = () => {
    while (i < n && isSpace(text[i])) {
      if (text[i] === '\n') line++
      i++
    }
  }

  /** Read the first argument literal at the call site; returns { title, dynamic }. */
  const readTitle = () => {
    skipWs()
    const ch = text[i]
    if (ch === '"' || ch === "'") {
      let j = i + 1
      let title = ''
      while (j < n && text[j] !== ch) {
        if (text[j] === '\\') {
          title += text[j + 1] ?? ''
          j += 2
        } else {
          title += text[j]
          j++
        }
      }
      return { title, dynamic: false }
    }
    if (ch === '`') {
      const start = i
      skipTemplate()
      const raw = text.slice(start, i)
      return { title: raw.replace(/^\`|\`$/g, ''), dynamic: raw.includes('${') }
    }
    return { title: null, dynamic: false }
  }

  while (i < n) {
    const ch = text[i]
    if (ch === '/' && text[i + 1] === '/') skipLineComment()
    else if (ch === '/' && text[i + 1] === '*') skipBlockComment()
    else if (ch === '"' || ch === "'") skipString(ch)
    else if (ch === '`') skipTemplate()
    else if (isIdentStart(ch)) {
      const start = i
      const startLine = line
      while (i < n && isIdentPart(text[i])) i++
      const word = text.slice(start, i)
      const callLine = startLine

      // Recognize `foo.test(` / `test.skip(` / `test(` chains from the end:
      // walk back over `.modifier` pieces and the base identifier.
      const chain = []
      let j = i
      let base = word
      // After the identifier we may have `.skip` / `.todo` / `.test` etc.
      while (true) {
        let k = j
        while (k < n && isSpace(text[k])) k++
        if (text[k] === '.' && k + 1 < n && isIdentStart(text[k + 1])) {
          let m = k + 1
          while (m < n && isIdentPart(text[m])) m++
          chain.push(text.slice(k + 1, m))
          j = m
        } else break
      }
      // `test.skip(` -> base test + chain [skip]. `t.test(` -> base t + chain [test].
      const baseName = base
      const members = chain
      const isCall =
        (CALLABLE.has(baseName) && (members.length === 0 || (members.length === 1 && STATE_MODIFIER.has(members[0])))) ||
        (members[0] === 'test' && (members.length === 1 || (members.length === 2 && STATE_MODIFIER.has(members[1]))))
      if (!isCall) continue

      // Confirm a `(` follows (after whitespace).
      let k = j
      while (k < n && isSpace(text[k])) {
        if (text[k] === '\n') line++
        k++
      }
      if (text[k] !== '(') continue

      i = k + 1
      const { title, dynamic } = readTitle()
      if (title === null) continue
      // Machine contract: the primary WHAT tag must lead the title
      // (`WHAT[<CURRENT-WHAT-ID>] ...`). Anything later in the title is prose
      // or an embedded example, not a declaration.
      const leading = title.trimStart()
      const whatIds = leading.startsWith('WHAT[') ? [...leading.matchAll(WHAT_TAG_RE)].map((m) => m[1]) : []
      const state =
        members.includes('todo') || baseName === 'todo'
          ? 'todo'
          : members.includes('skip') || baseName === 'skip'
            ? 'skip'
            : 'active'
      calls.push({ file, line: callLine, title, dynamic, state, whatIds })
    } else {
      if (ch === '\n') line++
      i++
    }
  }
  return calls
}

/** Collect every test file under requirements/<pkg>/tests/** (excl. e2e/integration). */
export function findTestFiles(requirementsRoot) {
  const files = walk(requirementsRoot, ['.mjs']).filter(
    (f) =>
      f.includes('/tests/') &&
      f.endsWith('.test.mjs') &&
      !f.includes('/tests/e2e/') &&
      !f.includes('/tests/integration/') &&
      !f.includes('/tests/support/'),
  )
  return files.sort()
}

/**
 * Build the full graph for one requirements tree.
 * Returns { whats: Map<id, WhatNode>, tests: TestNode[], orphans, unknownWhat, multiPrimary }
 */
export function buildTraceGraph(requirementsRoot) {
  const whats = new Map()
  const whatFiles = walk(requirementsRoot, ['.md']).filter((f) => f.endsWith('/WHAT.md'))
  for (const file of whatFiles) {
    const pkg = file.split('/').slice(-2)[0]
    const text = readFileSync(file, 'utf8')
    for (const { id, line } of whatHeadings(text)) {
      const heading = whatHeadingLine(text, line)
      whats.set(id, { id, package: pkg, file, line, heading, deleted: isDeletedProposition(heading) })
    }
  }

  const tests = []
  for (const file of findTestFiles(requirementsRoot)) {
    for (const node of scanTestSource(file)) tests.push(node)
  }

  const unknownWhat = new Set()
  const multiPrimary = []
  const orphans = []
  for (const t of tests) {
    const declared = [...new Set(t.whatIds)]
    if (declared.length === 0) {
      if (t.state === 'active') orphans.push(t)
      continue
    }
    if (declared.length > 1) multiPrimary.push({ test: t, whats: declared })
    for (const id of declared) if (!whats.has(id)) unknownWhat.add(id)
  }

  const unproved = [...whats.values()].filter((w) => !tests.some((t) => t.whatIds.includes(w.id) && t.state === 'active'))

  return { whats, tests, orphans, unknownWhat: [...unknownWhat], multiPrimary, unproved }
}

/** WHAT.md headings: `## PREFIX-NNN[:：—–-]? 标题` with 1-based line. */
export const whatHeadings = (text) => {
  const findings = []
  const re = /^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9-]+)?)\b(?:\s*[：:—–-]?\s*(.*))?$/gm
  for (const match of text.matchAll(re)) {
    findings.push({ id: match[1], title: match[2] ?? '', line: text.slice(0, match.index).split('\n').length })
  }
  return findings
}

/** A deleted proposition (`已删除` tombstone) keeps its number but has no proof obligation. */
export const isDeletedProposition = (title) => /已删除|deleted/i.test(title)

/** Package name from a requirements path: `<...>/requirements/<pkg>/tests/<...>`. */
export const packageOf = (file) => {
  const match = /(?:^|\/)requirements\/([a-z0-9-]+)\/tests\//.exec(String(file).replace(/\\/g, '/'))
  return match?.[1] ?? null
}

const whatHeadingLine = (text, lineNumber) => text.split('\n')[lineNumber - 1]?.trim() ?? ''
