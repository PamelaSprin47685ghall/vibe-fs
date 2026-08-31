// requirement-trace.mjs — pure data graph for the test ↔ WHAT closure.
//
//   WhatNode { id, package, file, line, heading }
//   WhatConflict { id, kind: duplicate|multi-owner, definitions: WhatNode[] }
//   TestNode { file, line, title, state: active|skip|todo, whatIds }
//   ProofEdge { proofFile, proofLine, whatId, file, line, title, state, anchor }
//
// Test declarations are parsed through the shared JavaScript syntax core.
// Only statically bound node:test registrations can own proof edges.

import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

import { isFunction, parseModule, patternNames, walkSyntax } from './js-syntax.mjs'
import { walk } from './walk.mjs'

export const WHAT_TAG_RE = /WHAT\[([A-Z][A-Z0-9-]*-\d{3})\]/g
export const PROOF_LEVELS = Object.freeze(['static', 'pure', 'temporal', 'adapter', 'long-stroke'])

const PROOF_LEVEL_SET = new Set(PROOF_LEVELS)
const normalizeWorkspacePath = (value) => String(value).replace(/\\/g, '/').replace(/^\.\//, '')

export const proofLevelKey = ({ path, title, what_id: whatId }) =>
  `${normalizeWorkspacePath(path)}\u0000${title}\u0000${whatId}`

/** Validate the verification-owned proof classification registry without consulting external rows. */
export function validateProofLevelRegistry(document) {
  const findings = []
  const add = (code, key, message) => findings.push({ code, key, message })
  if (document === null || typeof document !== 'object' || Array.isArray(document) || document.schema_version !== 1 || !Array.isArray(document.levels) || !Array.isArray(document.proofs)) {
    add('PROOF_LEVEL_REGISTRY_SHAPE', null, 'expected { schema_version: 1, levels: [...], proofs: [...] }')
    return findings
  }
  if (document.levels.length !== PROOF_LEVELS.length || document.levels.some((level, index) => level !== PROOF_LEVELS[index])) {
    add('PROOF_LEVEL_LADDER', null, `levels must be exactly ${PROOF_LEVELS.join(', ')}`)
  }
  const seen = new Set()
  for (const proof of document.proofs) {
    if (proof === null || typeof proof !== 'object' || Array.isArray(proof)) {
      add('PROOF_LEVEL_SHAPE', null, 'proof entry must be an object')
      continue
    }
    const keys = Object.keys(proof).sort()
    if (keys.join(',') !== 'level,path,title,what_id' || typeof proof.path !== 'string' || proof.path.length === 0 || proof.path.startsWith('/') || normalizeWorkspacePath(proof.path).split('/').includes('..') || typeof proof.title !== 'string' || proof.title.length === 0 || typeof proof.what_id !== 'string' || !/^[A-Z][A-Z0-9-]*-\d{3}$/.test(proof.what_id) || !PROOF_LEVEL_SET.has(proof.level)) {
      add('PROOF_LEVEL_SHAPE', null, 'proof requires only workspace-relative path, exact title, what_id, and canonical level')
      continue
    }
    const key = proofLevelKey(proof)
    if (seen.has(key)) add('PROOF_LEVEL_DUPLICATE', key, 'proof classification key is ambiguous')
    seen.add(key)
  }
  return findings
}

/** Resolve one exact proof classification. Invalid, absent, and ambiguous registries fail closed. */
export function resolveProofLevel(document, proof) {
  if (validateProofLevelRegistry(document).length > 0 || proof === null || typeof proof !== 'object') return null
  const key = proofLevelKey(proof)
  const matches = document.proofs.filter((candidate) => proofLevelKey(candidate) === key)
  return matches.length === 1 ? matches[0].level : null
}

const TEST_MODIFIER = new Set(['fails', 'only', 'skip', 'todo'])
const STATE_MODIFIER = new Set(['skip', 'todo'])

const titleParts = (title) => {
  if (title === null) return { whatIds: [], anchor: null }
  const leading = title.trimStart()
  if (!leading.startsWith('WHAT[')) return { whatIds: [], anchor: title.trim() }
  const whatIds = [...leading.matchAll(WHAT_TAG_RE)].map((match) => match[1])
  const first = leading.match(/^WHAT\[[A-Z][A-Z0-9-]*-\d{3}\]\s*/)?.[0] ?? ''
  return { whatIds, anchor: leading.slice(first.length).trim() }
}

const titleOf = (node) => {
  if (node?.type === 'Literal' && typeof node.value === 'string') return { title: node.value, dynamic: false }
  if (node?.type !== 'TemplateLiteral') return { title: null, dynamic: false }
  return {
    title: node.quasis
      .map((quasi, index) => `${quasi.value.cooked ?? quasi.value.raw}${index < node.expressions.length ? '${}' : ''}`)
      .join(''),
    dynamic: node.expressions.length > 0,
  }
}

const nodeTestBound = (program) => program.body.some((statement) =>
  statement.type === 'ImportDeclaration'
  && statement.source.value === 'node:test'
  && statement.specifiers.some((specifier) =>
    specifier.local.name === 'test'
    && (specifier.type === 'ImportDefaultSpecifier' || specifier.imported?.name === 'test')))

const testCallShape = (call) => {
  const callee = call.callee
  if (callee.type === 'Identifier' && callee.name === 'test') {
    return { kind: 'root', modifier: null, contextName: null, unsupported: false }
  }
  if (callee.type !== 'MemberExpression' || callee.computed || callee.property.type !== 'Identifier') return null
  if (callee.object.type === 'Identifier' && callee.object.name === 'test') {
    const modifier = callee.property.name
    if (!TEST_MODIFIER.has(modifier)) return null
    return { kind: 'root', modifier, contextName: null, unsupported: modifier === 'fails' }
  }
  if (callee.object.type === 'Identifier' && callee.object.name === 't' && callee.property.name === 'test') {
    return { kind: 'context', modifier: null, contextName: 't', unsupported: false }
  }
  const context = callee.object
  if (
    context.type === 'MemberExpression'
    && !context.computed
    && context.object.type === 'Identifier'
    && context.object.name === 't'
    && context.property.type === 'Identifier'
    && context.property.name === 'test'
  ) {
    return { kind: 'context', modifier: callee.property.name, contextName: 't', unsupported: true }
  }
  return null
}

const directRegistration = (call, container, ancestors) => {
  if (call === container) return true
  let current = call
  for (let index = ancestors.length - 1; index >= 0; index--) {
    const parent = ancestors[index]
    if (parent === container) return parent.body?.includes(current) === true
    if (parent.type === 'AwaitExpression' && parent.argument === current) current = parent
    else if (parent.type === 'ExpressionStatement' && parent.expression === current) current = parent
    else return false
  }
  return false
}

const declarationNames = (statement) => {
  if (statement?.type === 'VariableDeclaration') return statement.declarations.flatMap(({ id }) => patternNames(id))
  if (statement?.type === 'FunctionDeclaration' || statement?.type === 'ClassDeclaration') return statement.id ? [statement.id.name] : []
  return []
}

const shadows = (name, ancestors) => ancestors.some((ancestor) => {
  if (isFunction(ancestor) && ancestor.params.flatMap(patternNames).includes(name)) return true
  if (ancestor.type === 'CatchClause' && patternNames(ancestor.param).includes(name)) return true
  if (ancestor.type === 'ForStatement' && declarationNames(ancestor.init).includes(name)) return true
  if ((ancestor.type === 'ForInStatement' || ancestor.type === 'ForOfStatement') && declarationNames(ancestor.left).includes(name)) return true
  if (ancestor.type !== 'BlockStatement') return false
  return ancestor.body.some((statement) => declarationNames(statement).includes(name))
})

const callbackOf = (call) => {
  const callback = call.arguments.at(-1)
  return callback?.type === 'ArrowFunctionExpression' || callback?.type === 'FunctionExpression' ? callback : null
}

const callbackBody = (callback) => callback === null
  ? { bodyStart: null, bodyEnd: null, contextName: null }
  : {
      bodyStart: callback.body.start,
      bodyEnd: callback.body.end,
      contextName: callback.params[0]?.type === 'Identifier' ? callback.params[0].name : null,
    }

const staticState = (node) => {
  if (node?.type !== 'ObjectExpression') return { state: 'invalid', issue: 'DynamicTestState' }
  const states = []
  for (const property of node.properties) {
    if (property.type !== 'Property' || property.computed || property.kind !== 'init') {
      return { state: 'invalid', issue: 'DynamicTestState' }
    }
    const name = property.key.type === 'Identifier' ? property.key.name : property.key.value
    if (name !== 'skip' && name !== 'todo') continue
    if (property.value.type !== 'Literal') return { state: 'invalid', issue: 'DynamicTestState' }
    if (property.value.value !== false) states.push(name)
  }
  if (new Set(states).size > 1) return { state: 'invalid', issue: 'DynamicTestState' }
  return { state: states[0] ?? 'active', issue: null }
}

export function scanTestSource(file, source, syntax) {
  const text = source === undefined ? readFileSync(file, 'utf8') : source
  const program = syntax ?? parseModule(text, file)
  const hasBinding = nodeTestBound(program)
  const candidates = []
  walkSyntax(program, (node, _parent, _key, ancestors) => {
    if (node.type !== 'CallExpression') return
    const shape = testCallShape(node)
    if (shape) candidates.push({ node, shape, ancestors })
  })
  candidates.sort((left, right) => left.node.start - right.node.start)

  const contexts = []
  const declarations = []
  for (const { node, shape, ancestors } of candidates) {
    const { title, dynamic } = titleOf(node.arguments[0])
    const { whatIds, anchor } = titleParts(title)
    const callback = callbackOf(node)
    const body = callbackBody(callback)
    let state = 'active'
    let issue = null
    let owner = null

    if (shape.unsupported) {
      state = 'invalid'
      issue = 'UnsupportedModifier'
    } else if (shape.kind === 'root' && !hasBinding) {
      state = 'invalid'
      issue = 'UnboundTestBinding'
    } else if (shape.kind === 'root' && shadows('test', ancestors)) {
      state = 'invalid'
      issue = 'ShadowedTestBinding'
    } else if (shape.kind === 'root' && !directRegistration(node, program, ancestors)) {
      state = 'invalid'
      issue = 'IndirectRegistration'
    } else if (shape.kind === 'context') {
      owner = contexts
        .filter((context) => context.contextName === shape.contextName && context.bodyStart <= node.start && node.end <= context.bodyEnd)
        .sort((left, right) => right.bodyStart - left.bodyStart)[0]
      if (!owner) {
        state = 'invalid'
        issue = 'UnboundTestContext'
      } else if (!directRegistration(node, owner.callback.body, ancestors)) {
        state = 'invalid'
        issue = 'IndirectRegistration'
      } else state = owner.state
    }

    if (!issue && callback === null) {
      state = 'invalid'
      issue = 'MissingCallback'
    }
    if (!issue && node.arguments.length >= 3) {
      const options = staticState(node.arguments[1])
      state = options.state
      issue = options.issue
    }
    if (!issue && shape.modifier && STATE_MODIFIER.has(shape.modifier)) state = shape.modifier
    if (!issue && owner && owner.state !== 'active') state = owner.state

    const declaration = {
      file,
      line: node.loc.start.line,
      start: node.start,
      end: node.end,
      bodyStart: body.bodyStart,
      bodyEnd: body.bodyEnd,
      title,
      anchor,
      dynamic,
      state,
      issue,
      whatIds,
    }
    declarations.push(declaration)
    if (!issue && callback !== null && body.contextName !== null) contexts.push({ ...body, callback, state })
  }
  return declarations
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

/**
 * Resolve an explicit HOW.md proof title against scanned tests. Resolution is
 * exact after accepting the documented optional `test:` prefix and the
 * scanner's primary-WHAT-stripped title alias. Callers decide whether zero or
 * multiple matches are admissible; this helper never guesses from a filename.
 */
export const resolveExactProofTitle = (tests, anchor) => {
  if (!anchor) return []
  const normalized = anchor.replace(/^test:\s*/, '').trim()
  return tests.filter(
    (test) => normalized === test.title || normalized === test.anchor || normalized === test.title?.replace(/^WHAT\[[^\]]+\]\s*/, ''),
  )
}

const proofEdgesForRow = ({ proofFile, proofLine, rowText, whatIds, testsByFile, requirementsRoot }) => {
  const edges = []
  for (const reference of pathReferences(rowText, proofFile, requirementsRoot)) {
    const candidates = testsByFile.get(reference.path) ?? []
    if (candidates.length === 0) {
      edges.push({ proofFile, proofLine, whatId: whatIds[0] ?? null, file: reference.path, line: null, title: null, state: 'dangling', anchor: reference.anchor, reason: 'test file does not exist' })
      continue
    }
    if (!reference.explicit) {
      edges.push({ proofFile, proofLine, whatId: whatIds[0] ?? null, file: reference.path, line: null, title: null, state: 'dangling', anchor: null, reason: 'bare test path has no exact title anchor' })
      continue
    }

    const matched = resolveExactProofTitle(candidates, reference.anchor)
    if (matched.length === 0) {
      edges.push({ proofFile, proofLine, whatId: whatIds[0] ?? null, file: reference.path, line: null, title: null, state: 'dangling', anchor: reference.anchor, reason: 'test anchor does not exist' })
      continue
    }
    if (matched.length !== 1) {
      const test = matched[0]
      edges.push({ proofFile, proofLine, whatId: whatIds[0] ?? null, file: test.file, line: test.line, title: test.title, state: 'dangling', anchor: reference.anchor, reason: 'anchor resolves to multiple tests' })
      continue
    }

    const valid = matched.filter((test) => {
      const whatId = whatIds.find((id) => test.whatIds.includes(id))
      return Boolean(whatId && test.whatIds.length === 1 && test.whatIds[0] === whatId && test.state === 'active')
    })
    if (valid.length === 0) {
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
      continue
    }

    for (const test of valid) {
      const whatId = whatIds.find((id) => test.whatIds.includes(id))
      edges.push({ proofFile, proofLine, whatId, file: test.file, line: test.line, title: test.title, state: test.state, anchor: test.anchor })
    }
  }
  return edges
}

const readProofRows = (requirementsRoot, tests, uniqueWhats) => {
  const proofFiles = walk(requirementsRoot, ['.md']).filter((file) => file.endsWith('/HOW.md'))
  const testsByFile = new Map()
  for (const test of tests) {
    if (!testsByFile.has(test.file)) testsByFile.set(test.file, [])
    testsByFile.get(test.file).push(test)
  }

  const idsByPackage = new Map()
  for (const what of uniqueWhats.values()) {
    if (!idsByPackage.has(what.package)) idsByPackage.set(what.package, [])
    idsByPackage.get(what.package).push(what.id)
  }

  const proofIds = new Set()
  const proofEdges = []
  const proseOnlyProof = []
  for (const file of proofFiles) {
    const packageName = file.split('/').slice(-2)[0]
    const packageIds = idsByPackage.get(packageName) ?? []
    const byTail = new Map(packageIds.map((id) => [id.slice(-3), id]))
    const lines = readFileSync(file, 'utf8').split('\n')
    let inProofSection = false

    for (let index = 0; index < lines.length; index++) {
      const line = lines[index]
      const headerMatch = line.match(/^#{1,6}\s+(.*)$/)
      if (headerMatch) {
        const headerText = headerMatch[1].trim()
        if (/验证|落点|证明|命题\s*→|Focused acceptance|PROOF/i.test(headerText)) {
          inProofSection = true
        } else if (/^##\s+/.test(line)) {
          inProofSection = false
        }
      }

      if (!line.startsWith('|')) continue
      if (/^\|\s*(?:命题|WHAT|ID)\s*\|/i.test(line) || /落点|测试/i.test(line)) {
        inProofSection = true
      }
      // Skip header separator rows (|---|---|) and column header rows.
      if (/^\|[-:\s|]+\|?$/.test(line)) continue
      const cells = line.split('|')
      const rawRowText = cells.slice(1, -1).join(' | ')
      const proofCellIndexes = cells
        .map((cell, cellIndex) => pathReferences(cell, file, requirementsRoot).length > 0 ? cellIndex : -1)
        .filter((cellIndex) => cellIndex >= 0)
      // A wide semantic table may place WHAT ownership before a later proof
      // column. Read every non-proof cell that precedes at least one proof,
      // but never acquire ownership from WHAT text inside the proof title.
      const ownershipCells = proofCellIndexes.length === 0
        ? cells.slice(1, -1)
        : cells.filter((_, cellIndex) => cellIndex > 0 && !proofCellIndexes.includes(cellIndex) && proofCellIndexes.some((proofIndex) => cellIndex < proofIndex))
      const ids = []
      for (const rawCell of ownershipCells) {
        const cell = rawCell.replace(/`/g, '')
        for (const token of proofIdTokens(cell)) {
          if (/^\d{3}$/.test(token) && byTail.has(token)) ids.push(byTail.get(token))
          else if (!/^\d{3}$/.test(token) && packageIds.includes(token)) ids.push(token)
        }
        for (const match of cell.matchAll(/(?:^|[\s,、/–—])([0-9]{3})(?=$|[\s,、/–—])/g)) {
          if (byTail.has(match[1])) ids.push(byTail.get(match[1]))
        }
      }
      const uniqueIds = [...new Set(ids)]
      const refs = pathReferences(rawRowText, file, requirementsRoot)

      if (refs.length === 0 && uniqueIds.length > 0 && inProofSection) {
        proseOnlyProof.push({ proofFile: file, proofLine: index + 1, whatIds: uniqueIds, rowText: rawRowText.trim() })
      }
      const rowEdges = proofEdgesForRow({ proofFile: file, proofLine: index + 1, rowText: rawRowText, whatIds: uniqueIds, testsByFile, requirementsRoot })
      for (const edge of rowEdges) {
        if (edge.state === 'active' && !edge.reason && edge.whatId) proofIds.add(`${packageName}:${edge.whatId}`)
      }
      proofEdges.push(...rowEdges)
    }
  }
  return { proofIds, proofEdges, proseOnlyProof }
}

export function buildTraceGraph(requirementsRoot) {
  const whatDefinitions = new Map()
  const whatFiles = walk(requirementsRoot, ['.md']).filter((file) => file.endsWith('/WHAT.md'))
  for (const file of whatFiles) {
    const packageName = file.split('/').slice(-2)[0]
    const text = readFileSync(file, 'utf8')
    for (const { id, line } of whatHeadings(text)) {
      const heading = whatHeadingLine(text, line)
      const definition = { id, package: packageName, file, line, heading, deleted: isDeletedProposition(heading) }
      if (!whatDefinitions.has(id)) whatDefinitions.set(id, [])
      whatDefinitions.get(id).push(definition)
    }
  }

  // Ambiguous definitions have no authority. Keep every location for an exact
  // diagnostic, but expose only genuinely unique IDs through the ownership
  // map consumed by tests and proof edges.
  const whats = new Map()
  const duplicateWhats = []
  for (const [id, definitions] of whatDefinitions) {
    if (definitions.length === 1) whats.set(id, definitions[0])
    else {
      duplicateWhats.push({
        id,
        definitions,
        kind: new Set(definitions.map((definition) => definition.package)).size > 1 ? 'multi-owner' : 'duplicate',
      })
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
    for (const id of new Set(test.whatIds)) if (!whatDefinitions.has(id)) unknownWhat.add(id)
  }

  const missingProof = [...whats.values()].filter((what) => !what.deleted)
  const proof = readProofRows(requirementsRoot, tests, whats)
  const unprovedWhats = [...whats.values()].filter(
    (what) => !what.deleted && !tests.some((test) => test.state === 'active' && test.whatIds.length === 1 && test.whatIds[0] === what.id),
  )
  const proofMissing = missingProof.filter((what) => !proof.proofIds.has(`${what.package}:${what.id}`))
  const danglingProof = proof.proofEdges.filter((edge) => edge.state === 'dangling' || edge.reason)

  return {
    whats,
    whatDefinitions,
    duplicateWhats,
    tests,
    edges: tests.filter((test) => test.state !== 'invalid').flatMap((test) => test.whatIds.map((what) => ({ test, what: whats.get(what) ?? null }))),
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
