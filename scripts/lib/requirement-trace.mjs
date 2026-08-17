// requirement-trace.mjs — pure data graph for the test ↔ WHAT closure.
//
//   WhatNode { id, package, file, line, heading }
//   TestNode { file, line, title, state: active|skip|todo, whatIds }
//   ProofEdge { proofFile, proofLine, whatId, file, line, title, state, anchor }
//
// The scanner is a lexical parser, not a source-text regex. It tokenizes
// identifiers and call punctuation while skipping comments, quoted strings,
// regular-expression literals, and template bodies. Template expressions are
// tokenized recursively, so a nested t.test() remains visible without making
// text that merely resembles code visible.

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'

import { walk } from './walk.mjs'

export const WHAT_TAG_RE = /WHAT\[([A-Z][A-Z0-9-]*-\d{3})\]/g

const TEST_MODIFIER = new Set(['fails', 'only', 'skip', 'todo'])
const STATE_MODIFIER = new Set(['skip', 'todo'])
const IDENT_START_RE = /[A-Za-z_$]/
const IDENT_PART_RE = /[A-Za-z0-9_$]/
const REGEX_PRECEDING_KEYWORDS = new Set([
  'await',
  'case',
  'delete',
  'else',
  'in',
  'instanceof',
  'of',
  'return',
  'throw',
  'typeof',
  'void',
  'yield',
])

const isIdentStart = (ch) => ch !== undefined && IDENT_START_RE.test(ch)
const isIdentPart = (ch) => ch !== undefined && IDENT_PART_RE.test(ch)
const isSpace = (ch) => ch === ' ' || ch === '\t' || ch === '\r' || ch === '\n'

const lineStartsOf = (text) => {
  const starts = [0]
  for (let i = 0; i < text.length; i++) if (text[i] === '\n') starts.push(i + 1)
  return starts
}

const lineOf = (starts, index) => {
  let low = 0
  let high = starts.length
  while (low + 1 < high) {
    const middle = (low + high) >> 1
    if (starts[middle] <= index) low = middle
    else high = middle
  }
  return low + 1
}

const skipQuoted = (text, start, quote) => {
  let i = start + 1
  while (i < text.length) {
    if (text[i] === '\\') {
      i += 2
      continue
    }
    if (text[i] === quote) return i + 1
    i++
  }
  return text.length
}

const skipLineComment = (text, start) => {
  const end = text.indexOf('\n', start + 2)
  return end < 0 ? text.length : end
}

const skipBlockComment = (text, start) => {
  const end = text.indexOf('*/', start + 2)
  return end < 0 ? text.length : end + 2
}

/**
 * Heuristic: is the slash at index i a regular-expression literal? The
 * previous token is unavailable while finding a template expression, so use
 * the preceding significant character plus the small set of expression
 * keywords that can precede a regex. Division cannot contain a test call in a
 * valid expression, while a regex can contain arbitrary quote-like text.
 */
const isRegexSlash = (text, i) => {
  if (text[i + 1] === '/' || text[i + 1] === '*') return false
  let j = i - 1
  while (j >= 0 && isSpace(text[j])) j--
  if (j < 0) return true
  const previous = text[j]
  if (/[A-Za-z0-9_$)\]}]/.test(previous)) {
    let end = j + 1
    let begin = j
    while (begin >= 0 && isIdentPart(text[begin])) begin--
    if (REGEX_PRECEDING_KEYWORDS.has(text.slice(begin + 1, end))) return true
    return false
  }
  return true
}

const skipRegex = (text, start) => {
  let i = start + 1
  let inClass = false
  while (i < text.length) {
    const ch = text[i]
    if (ch === '\\') i += 2
    else if (ch === '\n' || ch === '\r') return start + 1
    else if (ch === '[') {
      inClass = true
      i++
    } else if (ch === ']') {
      inClass = false
      i++
    } else if (ch === '/' && !inClass) {
      i++
      while (isIdentPart(text[i])) i++
      return i
    } else i++
  }
  return start + 1
}

/** Find the closing `}` for a `${ ... }` expression. */
const findExpressionEnd = (text, start) => {
  let i = start
  let braces = 0
  while (i < text.length) {
    const ch = text[i]
    if (ch === '/' && text[i + 1] === '/') i = skipLineComment(text, i)
    else if (ch === '/' && text[i + 1] === '*') i = skipBlockComment(text, i)
    else if (ch === '"' || ch === "'") i = skipQuoted(text, i, ch)
    else if (ch === '`') i = parseTemplate(text, i).end
    else if (ch === '/' && isRegexSlash(text, i)) {
      const next = skipRegex(text, i)
      i = next === i + 1 ? i + 1 : next
    } else if (ch === '{') {
      braces++
      i++
    } else if (ch === '}') {
      if (braces === 0) return i
      braces--
      i++
    } else i++
  }
  return text.length
}

/** Parse a template literal, returning static title text and expression spans. */
const parseTemplate = (text, start) => {
  let i = start + 1
  let value = ''
  const expressions = []
  while (i < text.length) {
    const ch = text[i]
    if (ch === '\\') {
      value += text.slice(i, Math.min(i + 2, text.length))
      i += 2
    } else if (ch === '`') {
      return { end: i + 1, value, dynamic: expressions.length > 0, expressions }
    } else if (ch === '$' && text[i + 1] === '{') {
      const expressionStart = i + 2
      const expressionEnd = findExpressionEnd(text, expressionStart)
      expressions.push({ start: expressionStart, end: expressionEnd })
      value += '${}'
      i = expressionEnd < text.length ? expressionEnd + 1 : text.length
    } else {
      value += ch
      i++
    }
  }
  return { end: text.length, value, dynamic: expressions.length > 0, expressions }
}

const decodeQuoted = (text, start, end) => {
  let value = ''
  for (let i = start + 1; i < end - 1; i++) {
    if (text[i] === '\\' && i + 1 < end - 1) {
      value += text[i + 1]
      i++
    } else value += text[i]
  }
  return value
}

const tokenize = (text, file) => {
  const starts = lineStartsOf(text)
  const tokens = []

  const tokenizeRange = (start, end) => {
    let i = start
    while (i < end) {
      const ch = text[i]
      if (isSpace(ch)) {
        i++
        continue
      }
      if (ch === '/' && text[i + 1] === '/') {
        i = Math.min(skipLineComment(text, i), end)
        continue
      }
      if (ch === '/' && text[i + 1] === '*') {
        i = Math.min(skipBlockComment(text, i), end)
        continue
      }
      if (ch === '"' || ch === "'") {
        const tokenEnd = Math.min(skipQuoted(text, i, ch), end)
        tokens.push({ kind: 'string', value: decodeQuoted(text, i, tokenEnd), start: i, end: tokenEnd, line: lineOf(starts, i), file })
        i = tokenEnd
        continue
      }
      if (ch === '`') {
        const template = parseTemplate(text, i)
        const tokenEnd = Math.min(template.end, end)
        tokens.push({ kind: 'template', value: template.value, dynamic: template.dynamic, start: i, end: tokenEnd, line: lineOf(starts, i), file })
        for (const expression of template.expressions) {
          const expressionEnd = Math.min(expression.end, end)
          if (expression.start < expressionEnd) tokenizeRange(expression.start, expressionEnd)
        }
        i = tokenEnd
        continue
      }
      if (ch === '/' && isRegexSlash(text, i)) {
        const tokenEnd = Math.min(skipRegex(text, i), end)
        if (tokenEnd > i + 1) {
          i = tokenEnd
          continue
        }
      }
      if (isIdentStart(ch)) {
        let tokenEnd = i + 1
        while (tokenEnd < end && isIdentPart(text[tokenEnd])) tokenEnd++
        tokens.push({ kind: 'identifier', value: text.slice(i, tokenEnd), start: i, end: tokenEnd, line: lineOf(starts, i), file })
        i = tokenEnd
        continue
      }
      tokens.push({ kind: 'punct', value: ch, start: i, end: i + 1, line: lineOf(starts, i), file })
      i++
    }
  }

  tokenizeRange(0, text.length)
  return tokens.sort((a, b) => a.start - b.start || a.end - b.end)
}

const isPropertyToken = (tokens, index) => {
  const previous = tokens[index - 1]
  return previous?.value === '.' || previous?.value === '?' || previous?.value === '#'
}

// Names that look like test calls in declarations/constructors are not proof cases.
const isDeclarationOrConstructorTarget = (tokens, index) => {
  const previous = tokens[index - 1]?.value
  if (previous === 'function' || previous === 'new' || previous === 'class') return true
  return previous === '*' && tokens[index - 2]?.value === 'function'
}

const closesWithMethodBody = (tokens, open) => {
  let depth = 0
  for (let index = open; index < tokens.length; index++) {
    const value = tokens[index].value
    if (value === '(') depth++
    else if (value === ')') {
      depth--
      if (depth === 0) return tokens[index + 1]?.value === '{'
    }
  }
  return false
}

const callShapeAt = (tokens, index) => {
  const token = tokens[index]
  if (!token || token.kind !== 'identifier' || isPropertyToken(tokens, index) || isDeclarationOrConstructorTarget(tokens, index)) return null

  const next = tokens[index + 1]
  if (token.value === 'test') {
    if (next?.value === '(') return { open: index + 1, modifier: null }
    if (next?.value === '.' && tokens[index + 2]?.kind === 'identifier' && TEST_MODIFIER.has(tokens[index + 2].value) && tokens[index + 3]?.value === '(') {
      return { open: index + 3, modifier: tokens[index + 2].value }
    }
    return null
  }

  if (token.value !== 't' || next?.value !== '.' || tokens[index + 2]?.value !== 'test') return null
  const afterTest = tokens[index + 3]
  if (afterTest?.value === '(') return { open: index + 3, modifier: null }
  if (afterTest?.value === '.' && tokens[index + 4]?.kind === 'identifier' && TEST_MODIFIER.has(tokens[index + 4].value) && tokens[index + 5]?.value === '(') {
    return { open: index + 5, modifier: tokens[index + 4].value }
  }
  return null
}

const readTitleToken = (tokens, open) => {
  const first = tokens[open + 1]
  if (first?.kind === 'string' || first?.kind === 'template') return first
  return null
}

const titleParts = (title) => {
  if (title === null) return { whatIds: [], anchor: null }
  const leading = title.trimStart()
  if (!leading.startsWith('WHAT[')) return { whatIds: [], anchor: title.trim() }
  const whatIds = [...leading.matchAll(WHAT_TAG_RE)].map((match) => match[1])
  const first = leading.match(/^WHAT\[[A-Z][A-Z0-9-]*-\d{3}\]\s*/)?.[0] ?? ''
  return { whatIds, anchor: leading.slice(first.length).trim() }
}

/**
 * Scan one JS test source for actual `test()` / `test.skip()` /
 * `test.todo()` / `t.test()` call sites. Accepts a source string (for unit
 * tests) or a file path.
 */
export function scanTestSource(file, source) {
  const text = source === undefined ? readFileSync(file, 'utf8') : source
  const tokens = tokenize(text, file)
  const calls = []
  for (let index = 0; index < tokens.length; index++) {
    const shape = callShapeAt(tokens, index)
    if (!shape) continue
    const titleToken = readTitleToken(tokens, shape.open)
    if (!titleToken && closesWithMethodBody(tokens, shape.open)) continue
    const call = tokens[index]
    const title = titleToken?.value ?? null
    const { whatIds, anchor } = titleParts(title)
    calls.push({
      file,
      line: call.line,
      title,
      anchor,
      dynamic: titleToken?.kind === 'template' ? titleToken.dynamic : false,
      state: STATE_MODIFIER.has(shape.modifier) ? shape.modifier : 'active',
      whatIds,
    })
  }
  return calls
}

/** Collect every semantic test module under requirements/<pkg>/tests/**. */
export function findTestFiles(requirementsRoot) {
  return walk(requirementsRoot, ['.mjs']).filter((file) => file.includes('/tests/') && file.endsWith('.test.mjs')).sort()
}

const proofIdTokens = (text) => {
  const ids = []
  for (const match of text.matchAll(/\b([A-Z][A-Z0-9-]*-\d{3})(?:\/(\d{3}(?:\/\d{3})*))?\b/g)) {
    ids.push(match[1])
    for (const tail of (match[2] ?? '').split('/').filter(Boolean)) ids.push(tail)
  }
  return ids
}

const normalizeProofPath = (raw, proofFile, requirementsRoot) => {
  const withoutAnchor = raw.split('::', 1)[0]
  if (withoutAnchor.startsWith('requirements/')) return resolve(dirnameOf(requirementsRoot), withoutAnchor)
  if (withoutAnchor.startsWith('/')) return resolve(withoutAnchor)
  return resolve(proofFile, '..', withoutAnchor)
}

const dirnameOf = (path) => path.replace(/[\\/]requirements[\\/]?$/, '')

const pathReferences = (text, proofFile, requirementsRoot) => {
  const refs = []
  const pathPattern = /`([^`]*?\.test\.mjs(?:::[^`]*)?)`|((?:requirements\/|tests\/)[A-Za-z0-9_./-]+\.test\.mjs(?:::[A-Za-z0-9_$.-]+)?)/g
  for (const match of text.matchAll(pathPattern)) {
    const raw = match[1] ?? match[2]
    const separator = raw.indexOf('::')
    refs.push({
      raw,
      path: normalizeProofPath(raw, proofFile, requirementsRoot),
      anchor: separator >= 0 ? raw.slice(separator + 2) : null,
      explicit: separator >= 0,
      index: match.index,
      end: match.index + match[0].length,
    })
  }
  return refs
}

const anchorMatches = (test, anchor) => {
  if (!anchor) return false
  const normalized = anchor.replace(/^test:\s*/, '').trim()
  return normalized === test.title || normalized === test.anchor || normalized === test.title?.replace(/^WHAT\[[^\]]+\]\s*/, '')
}

const proofEdgesForRow = ({ proofFile, proofLine, rowText, whatIds, testsByFile, requirementsRoot }) => {
  const edges = []
  for (const reference of pathReferences(rowText, proofFile, requirementsRoot)) {
    const candidates = testsByFile.get(reference.path) ?? []
    const explicit = reference.explicit
    if (candidates.length === 0) {
      if (explicit) edges.push({ proofFile, proofLine, whatId: whatIds[0] ?? null, file: reference.path, line: null, title: null, state: 'dangling', anchor: reference.anchor, reason: 'test file does not exist' })
      continue
    }

    let matched = reference.anchor ? candidates.filter((test) => anchorMatches(test, reference.anchor)) : []
    if (!reference.anchor) {
      // Existing PROOF prose often places an exact title beside a backticked
      // path. A title match is an exact edge; a bare file path remains a
      // structural reference owned by the meta-verifier and is not guessed.
      matched = candidates.filter((test) => {
        const aliases = [test.title, test.anchor].filter(Boolean)
        return aliases.some((alias) => alias.length > 0 && rowText.includes(alias))
      })
    }
    if (matched.length === 0) {
      if (explicit) edges.push({ proofFile, proofLine, whatId: whatIds[0] ?? null, file: reference.path, line: null, title: null, state: 'dangling', anchor: reference.anchor, reason: 'test anchor does not exist' })
      continue
    }

    const valid = matched.filter((test) => {
      const whatId = whatIds.find((id) => test.whatIds.includes(id))
      return Boolean(whatId && test.whatIds.length === 1 && test.whatIds[0] === whatId && test.state === 'active')
    })
    if (valid.length === 0) {
      // A non-explicit cross-package REUSE note may name a test owned by a
      // different proposition. It is not an executable edge for this row, but
      // it is also not a stale anchor. Explicit `::anchor` syntax is strict.
      if (explicit) {
        const test = matched[0]
        edges.push({
          proofFile,
          proofLine,
          whatId: whatIds[0] ?? null,
          file: test.file,
          line: test.line,
          title: test.title,
          state: test.state,
          anchor: test.anchor,
          reason: test.state !== 'active' ? 'skip/todo is not executable proof' : 'test WHAT does not match PROOF proposition',
        })
      }
      continue
    }

    if (valid.length !== 1) {
      if (explicit) {
        const test = valid[0] ?? matched[0]
        edges.push({
          proofFile,
          proofLine,
          whatId: whatIds[0] ?? null,
          file: test.file,
          line: test.line,
          title: test.title,
          state: 'dangling',
          anchor: reference.anchor ?? test.anchor,
          reason: valid.length === 0 ? 'anchor does not match an active WHAT-owned test' : 'anchor resolves to multiple active tests',
        })
      }
      continue
    }

    for (const test of valid) {
      const whatId = whatIds.find((id) => test.whatIds.includes(id))
      edges.push({ proofFile, proofLine, whatId, file: test.file, line: test.line, title: test.title, state: test.state, anchor: test.anchor })
    }
  }
  return edges
}

const readProofRows = (requirementsRoot, tests) => {
  const proofFiles = walk(requirementsRoot, ['.md']).filter((file) => file.endsWith('/PROOF.md'))
  const testsByFile = new Map()
  for (const test of tests) {
    if (!testsByFile.has(test.file)) testsByFile.set(test.file, [])
    testsByFile.get(test.file).push(test)
  }

  const proofIds = new Set()
  const proofEdges = []
  const proseOnlyProof = []
  for (const file of proofFiles) {
    const packageName = file.split('/').slice(-2)[0]
    const whatFile = join(file, '..', 'WHAT.md')
    const whatText = readFileSync(whatFile, 'utf8')
    const packageIds = whatHeadings(whatText).map(({ id }) => id)
    const byTail = new Map(packageIds.map((id) => [id.slice(-3), id]))
    const lines = readFileSync(file, 'utf8').split('\n')
    for (let index = 0; index < lines.length; index++) {
      const line = lines[index]
      if (!line.startsWith('|')) continue
      // Skip header separator rows (|---|---|) and column header rows.
      if (/^\|[-:\s|]+\|?$/.test(line)) continue
      const cells = line.split('|')
      const rawCell1 = cells[1] ?? ''
      const rawCell2 = cells[2] ?? ''
      const cell1 = rawCell1.replace(/`/g, '')
      const cell2 = rawCell2.replace(/`/g, '')
      const ids = []
      for (const token of proofIdTokens(cell1)) {
        if (/^\d{3}$/.test(token) && byTail.has(token)) ids.push(byTail.get(token))
        else if (!/^\d{3}$/.test(token)) ids.push(token)
      }
      for (const token of proofIdTokens(cell2)) {
        if (/^\d{3}$/.test(token) && byTail.has(token)) ids.push(byTail.get(token))
        else if (!/^\d{3}$/.test(token)) ids.push(token)
      }
      for (const match of cell1.matchAll(/(?:^|[\s,、/–—])([0-9]{3})(?=$|[\s,、/–—])/g)) {
        if (byTail.has(match[1])) ids.push(byTail.get(match[1]))
      }
      const uniqueIds = [...new Set(ids)]
      // The full row text (all cells) is needed for pathReferences and
      // anchor matching: PROOF.md formats vary, and test paths may appear in
      // any column beyond the first two.
      const rowText = cells.slice(1, -1).join(' | ').replace(/`/g, '')
      const rawRowText = cells.slice(1, -1).join(' | ')
      const refs = pathReferences(rawRowText, file, requirementsRoot)
      // A PROOF row is executable evidence only when it references at least
      // one .test.mjs path. A prose-only row (law ID + narrative, no test
      // path) is not proof — it is a false green waiting to happen.
      if (refs.length > 0) {
        for (const id of uniqueIds) proofIds.add(`${packageName}:${id}`)
      } else if (uniqueIds.length > 0) {
        proseOnlyProof.push({ proofFile: file, proofLine: index + 1, whatIds: uniqueIds, rowText: rawRowText.trim() })
      }
      proofEdges.push(...proofEdgesForRow({ proofFile: file, proofLine: index + 1, rowText: rawRowText, whatIds: uniqueIds, testsByFile, requirementsRoot }))
    }
  }
  return { proofIds, proofEdges, proseOnlyProof }
}

/**
 * Build the full graph for one requirements tree.
 * Returns WHAT nodes, all test-zone call sites, exact proof edges, and closure
 * classifications consumed by the requirement-trace gate.
 */
export function buildTraceGraph(requirementsRoot) {
  const whats = new Map()
  const whatFiles = walk(requirementsRoot, ['.md']).filter((file) => file.endsWith('/WHAT.md'))
  for (const file of whatFiles) {
    const packageName = file.split('/').slice(-2)[0]
    const text = readFileSync(file, 'utf8')
    for (const { id, line } of whatHeadings(text)) {
      const heading = whatHeadingLine(text, line)
      whats.set(id, { id, package: packageName, file, line, heading, deleted: isDeletedProposition(heading) })
    }
  }

  const tests = []
  for (const file of findTestFiles(requirementsRoot)) tests.push(...scanTestSource(file))

  const unknownWhat = new Set()
  const multiPrimary = []
  const orphans = []
  for (const test of tests) {
    if (test.whatIds.length === 0) {
      orphans.push(test)
      continue
    }
    if (test.whatIds.length !== 1) multiPrimary.push({ test, whats: test.whatIds })
    for (const id of new Set(test.whatIds)) if (!whats.has(id)) unknownWhat.add(id)
  }

  const unproved = [...whats.values()].filter(
    (what) => !what.deleted && tests.some((test) => test.state === 'active' && test.whatIds.length === 1 && test.whatIds[0] === what.id),
  )
  const missingProof = [...whats.values()].filter((what) => !what.deleted)
  const proof = readProofRows(requirementsRoot, tests)
  const unprovedWhats = [...whats.values()].filter(
    (what) => !what.deleted && !tests.some((test) => test.state === 'active' && test.whatIds.length === 1 && test.whatIds[0] === what.id),
  )
  const proofMissing = missingProof.filter((what) => !proof.proofIds.has(`${what.package}:${what.id}`))
  const danglingProof = proof.proofEdges.filter((edge) => edge.state === 'dangling' || edge.reason)

  return {
    whats,
    tests,
    edges: tests.flatMap((test) => test.whatIds.map((what) => ({ test, what: whats.get(what) ?? null }))),
    proofEdges: proof.proofEdges,
    orphans,
    unknownWhat: [...unknownWhat],
    multiPrimary,
    unproved: unprovedWhats,
    proofMissing,
    danglingProof,
    proseOnlyProof: proof.proseOnlyProof,
  }
}

/** WHAT.md headings: `## PREFIX-NNN[:：—–-]? title` with 1-based line. */
export const whatHeadings = (text) => {
  const findings = []
  const re = /^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3})\b(?:\s*[：:—–-]?\s*(.*))?$/gm
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
