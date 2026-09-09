#!/usr/bin/env node
/** Static authority-contract gate: exact declarations, issuance, use and non-durability. */
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { readCompileShardInventory } from '../lib/compile-shards.mjs'
import { buildTraceGraph } from '../lib/requirement-trace.mjs'
import { buildSubsystemInventory, readSubsystemPolicy } from './subsystems.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
export const DEFAULT_MANIFEST = resolve(HERE, 'authority-contracts.json')
const REPOSITORY_ROOT = resolve(HERE, '../..')
export const AUTHORITY_CLASSES = Object.freeze([
  'Evidence',
  'Decision',
  'Witness',
  'Capability',
  'Receipt',
  'PhysicalHandle',
])
const CLASS_SET = new Set(AUTHORITY_CLASSES)
const norm = (path) => path.replace(/\\/g, '/')
const escapeRe = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

let canonicalRegistry
const authorityRegistry = (repoRoot = REPOSITORY_ROOT) => {
  const normalized = resolve(repoRoot)
  if (normalized === REPOSITORY_ROOT && canonicalRegistry) return canonicalRegistry
  const compileInventory = readCompileShardInventory({ repositoryRoot: normalized })
  const policyState = readSubsystemPolicy(resolve(normalized, 'scripts/checks/subsystems.json'))
  const subsystemInventory = buildSubsystemInventory({ compileInventory, policyState })
  const subsystems = new Set(policyState.ids)
  const ownership = new Map()
  for (const project of subsystemInventory.projects.values()) {
    if (!project.subsystem) continue
    for (const file of project.implementationFiles) {
      ownership.set(norm(relative(normalized, file)), project.subsystem)
    }
  }
  const trace = buildTraceGraph(resolve(normalized, 'requirements'))
  const packages = new Set([...trace.whats.values()].map((what) => what.package))
  const registry = {
    subsystems,
    ownership,
    whats: trace.whats,
    packages,
  }
  if (normalized === REPOSITORY_ROOT) canonicalRegistry = registry
  return registry
}

/** Remove F# comments and string/char literal content while preserving newlines. */
export const stripFSharpNonCode = (text) => {
  let code = ''
  let index = 0
  let blockDepth = 0
  const blank = (char) => char === '\n' ? '\n' : ' '

  while (index < text.length) {
    if (blockDepth > 0) {
      if (text.startsWith('(*', index)) { blockDepth += 1; code += '  '; index += 2; continue }
      if (text.startsWith('*)', index)) { blockDepth -= 1; code += '  '; index += 2; continue }
      code += blank(text[index]); index += 1; continue
    }
    if (text.startsWith('//', index)) {
      while (index < text.length && text[index] !== '\n') { code += ' '; index += 1 }
      continue
    }
    if (text.startsWith('(*', index)) { blockDepth = 1; code += '  '; index += 2; continue }

    const verbatim = text.startsWith('@"', index)
    const triple = text.startsWith('"""', index)
    if (verbatim || triple || text[index] === '"') {
      const delimiter = triple ? '"""' : '"'
      if (verbatim) { code += ' '; index += 1 }
      code += ' '.repeat(delimiter.length); index += delimiter.length
      while (index < text.length) {
        if (!verbatim && !triple && text[index] === '\\') {
          code += ' '; index += 1
          if (index < text.length) { code += blank(text[index]); index += 1 }
          continue
        }
        if (verbatim && text.startsWith('""', index)) { code += '  '; index += 2; continue }
        if (text.startsWith(delimiter, index)) { code += ' '.repeat(delimiter.length); index += delimiter.length; break }
        code += blank(text[index]); index += 1
      }
      continue
    }
    if (text[index] === "'" && index + 2 < text.length && text[index + 2] === "'") {
      code += '   '; index += 3; continue
    }
    code += text[index]
    index += 1
  }
  return code
}

export const readManifest = (path = DEFAULT_MANIFEST) => JSON.parse(readFileSync(path, 'utf8'))

/** Walk the production tree. The manifest classifies authority; it never defines scan scope. */
export const collectEntries = (repoRoot) => {
  const sourceRoot = resolve(repoRoot, 'src')
  const files = []
  const walk = (directory) => {
    for (const item of readdirSync(directory, { withFileTypes: true })) {
      const path = resolve(directory, item.name)
      if (item.isDirectory()) walk(path)
      else if (item.isFile() && item.name.endsWith('.fs')) files.push(path)
    }
  }
  walk(sourceRoot)
  return files.sort().map((path) => ({
    file: norm(relative(repoRoot, path)),
    text: readFileSync(path, 'utf8'),
  }))
}

const declarationRows = (file, text) => {
  const lines = text.split('\n')
  const codeLines = stripFSharpNonCode(text).split('\n')
  const rows = []
  for (let index = 0; index < lines.length; index += 1) {
    const line = codeLines[index]
    const hit = /^\s*type\s+(?:private\s+|internal\s+)?([A-Z][A-Za-z0-9_']*)\b/.exec(line)
    if (!hit) continue
    const prelude = lines.slice(Math.max(0, index - 3), index + 1).join('\n')
    const dsl = /\/\/\s*DSL-AUTHORITY:\s*(Evidence|Decision|Witness|Capability|Receipt|PhysicalHandle|Vocabulary)\b/.exec(prelude)?.[1]
    const privateConstruction = /\bprivate\b/.test(line) || /^\s*private\s*[|{]/.test(codeLines[index + 1] ?? '')
    const suffix = /(Evidence|Decision|Witness|Capability|Receipt|Permit|PhysicalHandle)$/.exec(hit[1])?.[1]
    const inherentlySensitive = /(Receipt|Permit|Witness|PhysicalHandle)$/.test(hit[1])
      || privateConstruction && suffix === 'Capability'
    rows.push({ file, line: index + 1, symbol: hit[1], dsl, sensitive: dsl !== 'Vocabulary' && (dsl !== undefined || inherentlySensitive) })
  }
  return rows
}

const declarationSpans = (text) => {
  const lines = stripFSharpNonCode(text).split('\n')
  const markers = []
  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index]
    const hit = /^\s*(?:let\s+(?:(?:private|internal|inline)\s+)*|member\s+(?:(?:private|internal)\s+)*(?:[A-Za-z_][A-Za-z0-9_']*\.)?)([A-Za-z_][A-Za-z0-9_']*)\b/.exec(line)
    if (hit) {
      markers.push({ symbol: hit[1], start: index, indent: line.search(/\S/), declaration: true })
      continue
    }
    if (/^\s*(?:namespace|module|type)\b/.test(line)) {
      markers.push({ start: index, indent: line.search(/\S/), declaration: false })
    }
  }
  return markers.flatMap((decl, index) => {
    if (!decl.declaration) return []
    let end = lines.length
    for (let next = index + 1; next < markers.length; next += 1) {
      if (markers[next].indent <= decl.indent) { end = markers[next].start; break }
    }
    return [{ symbol: decl.symbol, start: decl.start, indent: decl.indent, end, code: lines.slice(decl.start, end).join('\n') }]
  })
}

const typeSpans = (text) => {
  const lines = stripFSharpNonCode(text).split('\n')
  const declarations = []
  for (let index = 0; index < lines.length; index += 1) {
    const hit = /^(\s*)type\s+(?:private\s+|internal\s+)?([A-Z][A-Za-z0-9_']*)\b/.exec(lines[index])
    if (hit) declarations.push({ symbol: hit[2], start: index, indent: hit[1].length })
  }
  return declarations.map((decl, index) => {
    let end = lines.length
    for (let next = index + 1; next < declarations.length; next += 1) {
      if (declarations[next].indent <= decl.indent) { end = declarations[next].start; break }
    }
    return { ...decl, end, code: lines.slice(decl.start, end).join('\n') }
  })
}

const conventionallyDurableTypeSpans = (text) =>
  typeSpans(text).filter((span) =>
    /(?:Snapshot|Projection|Fact|Event|Codec|Payload)$/i.test(span.symbol)
    || /(?:^|\n)\s*\|\s*[A-Za-z0-9_']*(?:Snapshot|Projection|Fact|Event|Codec|Payload)\b/i.test(span.code))

const whatIds = (value) => typeof value === 'string'
  ? value.split(',').map((id) => id.trim()).filter(Boolean)
  : []

const lineOf = (text, offset) => text.slice(0, offset).split('\n').length
const problem = (id, file, line, text) => ({ id, file: norm(file), line, text })

/**
 * @param {{file:string,text:string}[]} entries
 * @param {{version:number,contracts:object[]}} manifest
 */
export const scanEntries = (entries, manifest, registry = authorityRegistry()) => {
  const problems = []
  const byFile = new Map(entries.map((entry) => [norm(entry.file), entry.text]))
  const contracts = manifest?.contracts
  if (!Array.isArray(contracts)) return [problem('invalid-manifest', '<manifest>', 0, 'contracts must be an array')]
  const methodContracts = manifest?.methods
  if (!Array.isArray(methodContracts)) return [problem('invalid-manifest', '<manifest>', 0, 'methods must be an array')]
  const methodKinds = new Set(['Effect', 'Admission', 'DurableSink'])
  for (const method of methodContracts) {
    if (!methodKinds.has(method.classification)) {
      problems.push(problem('invalid-method-classification', method.file ?? '<manifest>', 0, `${method.symbol ?? '<missing>'}: ${method.classification ?? '<missing>'}`))
    }
    for (const field of ['file', 'symbol', 'owner', 'what']) {
      if (typeof method[field] !== 'string' || method[field].trim() === '') problems.push(problem('incomplete-method-contract', method.file ?? '<manifest>', 0, `${method.symbol ?? '<missing>'}: ${field}`))
    }
    if (method.classification === 'Admission' && !['Result', 'Option', 'Capability'].includes(method.result)) {
      problems.push(problem('incomplete-method-contract', method.file ?? '<manifest>', 0, `${method.symbol ?? '<missing>'}: result`))
    }
    if (method.classification === 'Admission' && (typeof method.resultSymbol !== 'string' || method.resultSymbol.trim() === '')) {
      problems.push(problem('incomplete-method-contract', method.file ?? '<manifest>', 0, `${method.symbol ?? '<missing>'}: resultSymbol`))
    }
    if (!registry.subsystems.has(method.owner)) problems.push(problem('unregistered-authority-owner', method.file ?? '<manifest>', 0, `${method.symbol}: ${method.owner}`))
    const methodSubsystem = registry.ownership.get(norm(method.file ?? ''))
    if ((methodSubsystem !== undefined || norm(method.file ?? '').startsWith('src/')) && methodSubsystem !== method.owner) {
      const displayOwner = methodSubsystem ?? '<missing>'
      problems.push(problem('authority-owner-mismatch', method.file, 0, `${method.symbol}: declaration owner is ${displayOwner}, contract says ${method.owner}`))
    }
    for (const id of whatIds(method.what)) {
      const definition = registry.whats.get(id)
      if (!definition || !registry.packages.has(definition.package)) problems.push(problem('unregistered-authority-what', method.file, 0, `${method.symbol}: ${id}`))
      else if (method.whatOwners?.[id] !== definition.package) problems.push(problem('authority-what-owner-mismatch', method.file, 0, `${method.symbol}: ${id}`))
    }
  }
  const byKey = new Map()
  const issuerSpansByContract = new Map()
  for (const row of contracts) {
    const key = `${norm(row.file ?? '')}#${row.symbol ?? ''}`
    if (byKey.has(key)) {
      problems.push(problem('duplicate-contract', row.file ?? '<manifest>', 0, row.symbol ?? ''))
      continue
    }
    byKey.set(key, row)
    const text = byFile.get(norm(row.file ?? ''))
    if (text === undefined) {
      problems.push(problem('stale-owner-file', row.file ?? '<manifest>', 0, row.symbol ?? ''))
      continue
    }
    const ownerCode = stripFSharpNonCode(text)
    const declared = declarationRows(norm(row.file), text).some((decl) => decl.symbol === row.symbol)
    if (!row.anchor || ownerCode.split(row.anchor).length !== 2 || !declared) {
      problems.push(problem('stale-manifest-anchor', row.file, 0, `${row.symbol}: declaration anchor must occur exactly once`))
    }
    const registeredIssuerSpans = []
    for (const issuer of row.issuers ?? []) {
      const issuerFile = norm(issuer.file ?? '')
      const issuerText = byFile.get(issuerFile)
      const issuerCode = issuerText === undefined ? undefined : stripFSharpNonCode(issuerText)
      const anchorCount = issuerCode && issuer.anchor ? issuerCode.split(issuer.anchor).length - 1 : 0
      const declarationSymbol = typeof issuer.symbol === 'string' ? issuer.symbol.split('.').at(-1) : undefined
      const spans = issuerText === undefined || declarationSymbol === undefined
        ? []
        : declarationSpans(issuerText).filter((span) => span.symbol === declarationSymbol && span.code.includes(issuer.anchor))
      if (anchorCount !== 1 || spans.length !== 1) {
        problems.push(problem('stale-issuance-anchor', issuer.file ?? row.file, 0, `${row.symbol}: ${issuer.symbol ?? '<missing symbol>'} at ${issuer.anchor ?? ''}`))
      } else {
        registeredIssuerSpans.push({ file: issuerFile, ...spans[0] })
      }
    }
    issuerSpansByContract.set(key, registeredIssuerSpans)
    if (!registry.subsystems.has(row.owner)) problems.push(problem('unregistered-authority-owner', row.file, 0, `${row.symbol}: ${row.owner ?? '<missing>'}`))
    const rowSubsystem = registry.ownership.get(norm(row.file ?? ''))
    if ((rowSubsystem !== undefined || norm(row.file ?? '').startsWith('src/')) && rowSubsystem !== row.owner) {
      const displayOwner = rowSubsystem ?? '<missing>'
      problems.push(problem('authority-owner-mismatch', row.file, 0, `${row.symbol}: declaration owner is ${displayOwner}, contract says ${row.owner ?? '<missing>'}`))
    }
    for (const issuer of row.issuers ?? []) {
      const issuerSubsystem = registry.ownership.get(norm(issuer.file ?? ''))
      if ((issuerSubsystem !== undefined || norm(issuer.file ?? '').startsWith('src/')) && issuerSubsystem !== issuer.owner) {
        const displayIssuer = issuerSubsystem ?? '<missing>'
        problems.push(problem('authority-issuer-owner-mismatch', issuer.file ?? row.file, 0, `${row.symbol}: issuer owner is ${displayIssuer}, contract says ${issuer.owner ?? '<missing>'}`))
      }
    }
    for (const id of whatIds(row.what)) {
      const definition = registry.whats.get(id)
      if (!definition || !registry.packages.has(definition.package)) {
        problems.push(problem('unregistered-authority-what', row.file, 0, `${row.symbol}: ${id}`))
      } else if ((norm(row.file ?? '').startsWith('src/') || row.whatOwners !== undefined) && row.whatOwners?.[id] !== definition.package) {
        problems.push(problem('authority-what-owner-mismatch', row.file, 0, `${row.symbol}: ${id} owner is ${definition.package}, contract says ${row.whatOwners?.[id] ?? '<missing>'}`))
      }
    }
    if (row.classification === 'Authority') {
      if (!CLASS_SET.has(row.class)) problems.push(problem('invalid-authority-class', row.file, 0, `${row.symbol}: ${row.class}`))
      for (const field of ['owner', 'what', 'scope', 'freshness', 'multiplicity', 'consume', 'durability']) {
        if (typeof row[field] !== 'string' || row[field].trim() === '') problems.push(problem('incomplete-contract', row.file, 0, `${row.symbol}: ${field}`))
      }
      if (row.class === 'Witness' && !Array.isArray(row.admissions)) {
        problems.push(problem('incomplete-contract', row.file, 0, `${row.symbol}: admissions`))
      }
    } else if (row.classification !== 'Vocabulary') {
      problems.push(problem('invalid-classification', row.file, 0, `${row.symbol}: ${row.classification}`))
    }
  }

  for (const entry of entries) {
    const file = norm(entry.file)
    const code = stripFSharpNonCode(entry.text)
    for (const decl of declarationRows(file, entry.text)) {
      const row = byKey.get(`${file}#${decl.symbol}`)
      if (decl.sensitive && row === undefined) problems.push(problem('unclassified-sensitive-declaration', file, decl.line, decl.symbol))
      if (decl.dsl && decl.dsl !== 'Vocabulary' && row?.class !== decl.dsl) {
        problems.push(problem('dsl-class-mismatch', file, decl.line, `${decl.symbol}: ${decl.dsl}`))
      }
    }

    for (const match of entry.text.matchAll(/\/\/\s*DSL-ISSUE:\s*([A-Z][A-Za-z0-9_']*)\b/g)) {
      const symbol = match[1]
      const row = contracts.find((candidate) => candidate.symbol === symbol)
      const line = entry.text.slice(0, match.index).split('\n').length
      const registeredFiles = row ? new Set([norm(row.file), ...(row.issuers ?? []).map((issuer) => norm(issuer.file))]) : new Set()
      if (!row || !registeredFiles.has(file)) problems.push(problem('foreign-issuance', file, line, symbol))
    }

    for (const row of contracts.filter((candidate) => candidate.classification === 'Authority')) {
      const symbol = escapeRe(row.symbol)
      const key = `${norm(row.file ?? '')}#${row.symbol ?? ''}`
      const issuerSpans = issuerSpansByContract.get(key) ?? []
      const isInsideIssuer = (line) => issuerSpans.some((span) => span.file === file && line - 1 >= span.start && line - 1 < span.end)
      const mintPatterns = [
        new RegExp(`(?<!\\.)\\b${symbol}\\s*(?:\\(|\\{\\|)`, 'g'),
        new RegExp(`(?<!\\.)\\b${symbol}\\.issue\\b`, 'g'),
      ]
      for (const mintPattern of mintPatterns) {
        for (const match of code.matchAll(mintPattern)) {
          const line = lineOf(code, match.index)
          const sourceLine = code.split('\n')[line - 1] ?? ''
          const typeDeclaration = new RegExp(`^\\s*type\\s+(?:private\\s+|internal\\s+)?${symbol}\\b`).test(sourceLine)
          const destructuringPattern = new RegExp(`^\\s*(?:let|function|match|\\|)[^=]*\\b${symbol}\\b`).test(sourceLine)
          if (!typeDeclaration && !destructuringPattern && !isInsideIssuer(line)) {
            problems.push(problem('foreign-issuance', file, line, row.symbol))
          }
        }
      }
      const boolConsume = new RegExp(`\\blet\\s+(?:try)?(?:consume|release|use)[A-Za-z0-9_']*[^=\\n]*\\b${symbol}\\b[^=\\n]*:\\s*bool\\b`, 'i')
      if (boolConsume.test(code)) problems.push(problem('bool-one-shot-consume', file, 0, row.symbol))

      if (row.class === 'Capability') {
        const capabilityReference = new RegExp(`\\b${symbol}\\b`)
        const durablePayload = conventionallyDurableTypeSpans(entry.text).some((span) => capabilityReference.test(span.code))
        const serializerSurface = new RegExp(`(?:Json|JSON|serialize|deserialize|encode|decode)[^\\n]*\\b${symbol}\\b|\\b${symbol}\\b[^\\n]*(?:Json|JSON|serialize|deserialize|encode|decode)`, 'i').test(code)
        if (durablePayload || serializerSurface) problems.push(problem('capability-persistence', file, 0, row.symbol))
      }
    }
  }
  return problems
}

export const scanRepo = (repoRoot = process.cwd(), manifest = readManifest()) => {
  const entries = collectEntries(repoRoot)
  const problems = scanEntries(entries, manifest, authorityRegistry(repoRoot))
  return { ok: problems.length === 0, problems }
}

const isMain = process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))
if (isMain) {
  const result = scanRepo(process.cwd())
  if (result.ok) console.log('authority-boundary: OK')
  else {
    console.error(`authority-boundary: ${result.problems.length} violation(s)`)
    for (const hit of result.problems) console.error(`  ${hit.file}${hit.line ? `:${hit.line}` : ''}: ${hit.id} — ${hit.text}`)
    process.exitCode = 1
  }
}
