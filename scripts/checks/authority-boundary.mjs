#!/usr/bin/env node
/** Static authority-contract gate: exact declarations, issuance, use and non-durability. */
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { buildTraceGraph } from '../lib/requirement-trace.mjs'
import { scanProjectSymbolUses } from './owner-dependencies.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
export const DEFAULT_MANIFEST = resolve(HERE, 'authority-contracts.json')
const SEMANTIC_OWNERS = resolve(HERE, 'semantic-owners.json')
const REQUIREMENTS = resolve(HERE, '../../requirements')
const AUTHORITY_FCS_ROOT = resolve(HERE, '../../.fable-build/authority-fcs')
const AUTHORITY_FCS_RESULT = resolve(AUTHORITY_FCS_ROOT, 'symbol-uses.json')
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
const authorityRegistry = () => {
  if (canonicalRegistry) return canonicalRegistry
  const semanticOwners = JSON.parse(readFileSync(SEMANTIC_OWNERS, 'utf8'))
  const trace = buildTraceGraph(REQUIREMENTS)
  canonicalRegistry = {
    owners: new Set(semanticOwners.owners ?? []),
    ownership: new Map((semanticOwners.ownership ?? []).map((entry) => [norm(entry.path), entry.owner])),
    whats: trace.whats,
  }
  return canonicalRegistry
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

const methodMatches = (application, contract) =>
  symbolMatches(application.resolvedTarget ?? application.symbol ?? '', contract.symbol ?? '')
  && (!contract.file || [...(application.declarationPaths ?? []), ...(application.providerPaths ?? [])].map(norm).includes(norm(contract.file)))

const sourcePosition = (line, column = 0) => line * 1_000_000 + column
const rangeContains = (outer, inner) => sourcePosition(outer.startLine, outer.startColumn) <= sourcePosition(inner.startLine, inner.startColumn)
  && sourcePosition(inner.endLine, inner.endColumn) <= sourcePosition(outer.endLine, outer.endColumn)
const successPattern = (result) => result === 'Option' ? 'Some' : 'Ok'
const resultContinuationMatches = (application) => {
  const target = (application.resolvedTarget ?? '').replace(/Module\./g, '.')
  return application.sourceAnchor === 'Result.map'
    || application.sourceAnchor === 'Result.bind'
    || target.endsWith('.Result.Map')
    || target.endsWith('.Result.Bind')
}

const admittedProducerKeys = (entries, applications, admissionContracts, controlFlow) => {
  const declarations = entries.flatMap((entry) => declarationSpans(entry.text).map((span) => ({ file: norm(entry.file), ...span })))
  const keyOf = (declaration) => `${declaration.file}#${declaration.symbol}`
  const targetKeys = (application) => {
    const symbol = (application.resolvedTarget ?? '').split('.').at(-1)
    return [...new Set([...(application.declarationPaths ?? []), ...(application.providerPaths ?? [])].map((file) => `${norm(file)}#${symbol}`))]
  }
  const callsByDeclaration = new Map(declarations.map((declaration) => [
    keyOf(declaration),
    applications.filter((application) =>
      norm(application.consumerPath ?? '') === declaration.file
      && application.startLine - 1 >= declaration.start
      && application.startLine - 1 < declaration.end),
  ]))
  const successfulCarrier = (application) =>
    (controlFlow.bindExpressions ?? []).some((bind) => bind.builderKind === 'TaskResult' && rangeContains(bind.binding, application))
    || (controlFlow.matchExpressions ?? []).some((expression) => rangeContains(expression.scrutinee, application))
    || applications.some((outer) => resultContinuationMatches(outer)
      && rangeContains(outer, application)
      && (controlFlow.lambdaExpressions ?? []).some((lambda) => rangeContains(outer, lambda.body) && rangeContains(lambda.body, application)))

  const producers = new Set(declarations.filter((declaration) =>
    callsByDeclaration.get(keyOf(declaration)).some((application) =>
      admissionContracts.some((contract) => methodMatches(application, contract)) && successfulCarrier(application))).map(keyOf))
  let changed = true
  while (changed) {
    changed = false
    for (const declaration of declarations) {
      if (producers.has(keyOf(declaration))) continue
      const wrapsProducer = callsByDeclaration.get(keyOf(declaration)).some((application) =>
        successfulCarrier(application)
        && targetKeys(application).some((key) => producers.has(key)))
      if (wrapsProducer) { producers.add(keyOf(declaration)); changed = true }
    }
  }
  return {
    matches: (application) => targetKeys(application).some((key) => producers.has(key)),
  }
}

const admittedEffectIsDominated = (span, row, applications, admissionContracts, effectContracts, controlFlow, producerCalls) => {
  const witnessBindings = [...span.code.matchAll(new RegExp(`\\b([a-z_][A-Za-z0-9_']*)\\s*:\\s*${escapeRe(row.symbol)}\\b`, 'g'))]
    .map((match) => match[1])
  const admissionUses = applications.filter((application) =>
    admissionContracts.some((contract) => methodMatches(application, contract)) || producerCalls.matches(application))
  const effectUses = applications.filter((application) => effectContracts.some((contract) => methodMatches(application, contract)))
  for (const effect of effectUses) {
    for (const admission of admissionUses) {
      if (admission.startLine > effect.startLine) continue
      const admittedWitnesses = (admission.argumentIdentifiers ?? []).filter((argument) =>
        witnessBindings.includes(argument)
        || Object.values(admission.argumentTypes ?? {}).some((type) => new RegExp(`\\b${escapeRe(row.symbol)}\\b`).test(type)))
      if (admittedWitnesses.length === 0) continue
      const effectWitnesses = (effect.argumentIdentifiers ?? []).filter((argument) =>
        new RegExp(`\\b${escapeRe(row.symbol)}\\b`).test(effect.argumentTypes?.[argument] ?? ''))
      const sameWitness = effectWitnesses.length === 0
        || effectWitnesses.some((argument) => admittedWitnesses.includes(argument))
      if (!sameWitness) continue

      const admissionContract = admissionContracts.find((contract) => methodMatches(admission, contract))
      const matchedSuccessArm = (controlFlow.matchExpressions ?? []).some((expression) =>
        rangeContains(expression.scrutinee, admission)
        && expression.clauses.some((clause) => clause.patternKind === successPattern(admissionContract?.result) && rangeContains(clause, effect)))
      const successfulBind = (controlFlow.bindExpressions ?? []).some((bind) =>
        bind.builderKind === 'TaskResult' && rangeContains(bind.binding, admission) && rangeContains(bind.body, effect))
      const resultContinuation = applications.some((application) =>
        resultContinuationMatches(application)
        && rangeContains(application, admission)
        && (controlFlow.lambdaExpressions ?? []).some((lambda) =>
          rangeContains(application, lambda.body) && rangeContains(lambda.body, effect)))
      if (matchedSuccessArm || successfulBind || resultContinuation) return true
    }
  }
  return false
}

const whatIds = (value) => typeof value === 'string'
  ? value.split(',').map((id) => id.trim()).filter(Boolean)
  : []

const symbolMatches = (actual, expected) => {
  const normalized = actual.replace(/Module\./g, '.')
  return normalized === expected || normalized.endsWith(`.${expected}`)
}

const lineOf = (text, offset) => text.slice(0, offset).split('\n').length
const problem = (id, file, line, text) => ({ id, file: norm(file), line, text })

/**
 * @param {{file:string,text:string}[]} entries
 * @param {{version:number,contracts:object[]}} manifest
 */
export const scanEntries = (entries, manifest, evidence = {}) => {
  const problems = []
  const byFile = new Map(entries.map((entry) => [norm(entry.file), entry.text]))
  const registry = evidence.registry ?? authorityRegistry()
  const compilerUses = Array.isArray(evidence.symbolUses) ? evidence.symbolUses : null
  const applicationUses = Array.isArray(evidence.applicationUses) ? evidence.applicationUses : []
  const controlFlow = {
    matchExpressions: Array.isArray(evidence.matchExpressions) ? evidence.matchExpressions : [],
    bindExpressions: Array.isArray(evidence.bindExpressions) ? evidence.bindExpressions : [],
    lambdaExpressions: Array.isArray(evidence.lambdaExpressions) ? evidence.lambdaExpressions : [],
  }
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
    if (!registry.owners.has(method.owner)) problems.push(problem('unregistered-authority-owner', method.file ?? '<manifest>', 0, `${method.symbol}: ${method.owner}`))
    const declarationOwner = registry.ownership?.get(norm(method.file ?? ''))
    if ((declarationOwner !== undefined || norm(method.file ?? '').startsWith('src/')) && declarationOwner !== method.owner) {
      problems.push(problem('authority-owner-mismatch', method.file, 0, `${method.symbol}: declaration owner is ${declarationOwner ?? '<missing>'}, contract says ${method.owner}`))
    }
    for (const id of whatIds(method.what)) {
      const definition = registry.whats.get(id)
      if (!definition || !registry.owners.has(definition.package)) problems.push(problem('unregistered-authority-what', method.file, 0, `${method.symbol}: ${id}`))
      else if (method.whatOwners?.[id] !== definition.package) problems.push(problem('authority-what-owner-mismatch', method.file, 0, `${method.symbol}: ${id}`))
    }
  }
  const effectContracts = methodContracts.filter((method) => method.classification === 'Effect')
  const admissionContracts = methodContracts.filter((method) => method.classification === 'Admission')
  const durableSinkContracts = methodContracts.filter((method) => method.classification === 'DurableSink')
  const producerCallsByWitness = new Map(contracts
    .filter((row) => row.class === 'Witness')
    .map((row) => {
      const rowAdmissions = admissionContracts.filter((contract) => (row.admissions ?? []).some((admission) =>
        symbolMatches(contract.symbol ?? '', admission.symbol ?? '') && norm(contract.file ?? '') === norm(admission.file ?? '')))
      return [`${norm(row.file ?? '')}#${row.symbol ?? ''}`, admittedProducerKeys(entries, applicationUses, rowAdmissions, controlFlow)]
    }))

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
    if (!registry.owners.has(row.owner)) problems.push(problem('unregistered-authority-owner', row.file, 0, `${row.symbol}: ${row.owner ?? '<missing>'}`))
    const declarationOwner = registry.ownership?.get(norm(row.file ?? ''))
    if ((declarationOwner !== undefined || norm(row.file ?? '').startsWith('src/')) && declarationOwner !== row.owner) {
      problems.push(problem('authority-owner-mismatch', row.file, 0, `${row.symbol}: declaration owner is ${declarationOwner ?? '<missing>'}, contract says ${row.owner ?? '<missing>'}`))
    }
    for (const issuer of row.issuers ?? []) {
      const issuerOwner = registry.ownership?.get(norm(issuer.file ?? ''))
      if ((issuerOwner !== undefined || norm(issuer.file ?? '').startsWith('src/')) && issuer.owner !== issuerOwner) {
        problems.push(problem('authority-issuer-owner-mismatch', issuer.file ?? row.file, 0, `${row.symbol}: issuer owner is ${issuerOwner ?? '<missing>'}, contract says ${issuer.owner ?? '<missing>'}`))
      }
    }
    for (const id of whatIds(row.what)) {
      const definition = registry.whats.get(id)
      if (!definition || !registry.owners.has(definition.package)) {
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
      if (compilerUses) {
        const reportedMintLines = new Set()
        for (const use of compilerUses) {
          if (norm(use.consumerPath ?? '') !== file || use.isFromPattern || use.isFromType) continue
          const declarationPaths = [...(use.declarationPaths ?? []), ...(use.providerPaths ?? [])].map(norm)
          if (!declarationPaths.includes(norm(row.file))) continue
          const unionConstruction = use.symbolKind === 'FSharpUnionCase' && symbolMatches(use.symbol ?? '', row.symbol)
          const sourceLine = code.split('\n')[use.line - 1] ?? ''
          const fieldName = (use.symbol ?? '').split('.').at(-1)
          const fieldAtUse = sourceLine.slice(Math.max(0, use.column ?? 0))
          const recordFieldConstruction = use.symbolKind === 'FSharpField'
            && (use.symbol ?? '').includes(`.${row.symbol}.`)
            && new RegExp(`^${escapeRe(fieldName)}\\s*=`).test(fieldAtUse)
          if (!unionConstruction && !recordFieldConstruction) continue
          if (!isInsideIssuer(use.line) && !reportedMintLines.has(use.line)) {
            reportedMintLines.add(use.line)
            problems.push(problem('foreign-issuance', file, use.line, row.symbol))
          }
        }
      } else {
        const mintPatterns = [
          new RegExp(`\\b${symbol}\\s*(?:\\(|\\{\\|)`, 'g'),
          new RegExp(`\\b${symbol}\\.issue\\b`, 'g'),
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
      }
      const boolConsume = new RegExp(`\\blet\\s+(?:try)?(?:consume|release|use)[A-Za-z0-9_']*[^=\\n]*\\b${symbol}\\b[^=\\n]*:\\s*bool\\b`, 'i')
      if (boolConsume.test(code)) problems.push(problem('bool-one-shot-consume', file, 0, row.symbol))

      if (row.class === 'Capability') {
        const capabilityReference = new RegExp(`\\b${symbol}\\b`)
        const durablePayload = conventionallyDurableTypeSpans(entry.text).some((span) => capabilityReference.test(span.code))
        const serializerSurface = new RegExp(`(?:Json|JSON|serialize|deserialize|encode|decode)[^\\n]*\\b${symbol}\\b|\\b${symbol}\\b[^\\n]*(?:Json|JSON|serialize|deserialize|encode|decode)`, 'i').test(code)
        const spans = typeSpans(entry.text)
        const payloadTypes = new Set()
        for (const span of spans) {
          if (span.symbol === row.symbol) continue
          const typedCapabilityField = compilerUses?.some((use) =>
            norm(use.consumerPath ?? '') === file
            && use.line - 1 >= span.start
            && use.line - 1 < span.end
            && use.isFromType
            && use.symbolKind === 'FSharpEntity'
            && [...(use.declarationPaths ?? []), ...(use.providerPaths ?? [])].map(norm).includes(norm(row.file))
            && symbolMatches(use.symbol ?? '', row.symbol)
            && (/\{[\s\S]*\b[A-Za-z_][A-Za-z0-9_']*\s*:\s*/.test(span.code)
              || /(?:^|\n)\s*\|\s*[A-Za-z_][A-Za-z0-9_']*(?:\s+of)?\b/.test(span.code)))
          const lexicalCapabilityField = new RegExp(`(?:\\{|;)\\s*[A-Za-z_][A-Za-z0-9_']*\\s*:\\s*${symbol}\\b|(?:^|\\n)\\s*\\|[^\\n]*\\b${symbol}\\b`).test(span.code)
          if (typedCapabilityField || (!compilerUses && lexicalCapabilityField)) payloadTypes.add(span.symbol)
        }
        let changed = true
        while (changed) {
          changed = false
          for (const span of spans) {
            if (payloadTypes.has(span.symbol)) continue
            const nested = [...payloadTypes].some((payloadType) => new RegExp(`\\b${escapeRe(payloadType)}\\b`).test(span.code))
            if (nested) { payloadTypes.add(span.symbol); changed = true }
          }
        }
        const durableDataflow = applicationUses.some((application) =>
          norm(application.consumerPath ?? '') === file
          && durableSinkContracts.some((contract) => methodMatches(application, contract))
          && Object.values(application.argumentTypes ?? {}).some((type) =>
            [...payloadTypes, row.symbol].some((payloadType) => new RegExp(`\\b${escapeRe(payloadType)}\\b`).test(type))))
        if (durablePayload || serializerSurface || durableDataflow) problems.push(problem('capability-persistence', file, 0, row.symbol))
      }

      if (row.class === 'Witness' && norm(row.file) !== file) {
        const witnessReference = new RegExp(`\\b${symbol}\\b`)
        for (const span of declarationSpans(entry.text)) {
          const typedWitnessReference = compilerUses?.some((use) =>
            norm(use.consumerPath ?? '') === file
            && use.line - 1 >= span.start
            && use.line - 1 < span.end
            && use.symbolKind === 'FSharpEntity'
            && (use.providerPaths ?? []).map(norm).includes(norm(row.file))
            && symbolMatches(use.symbol ?? '', row.symbol))
          if (compilerUses?.length > 0 ? !typedWitnessReference : !witnessReference.test(span.code)) continue
          const applicationsInSpan = applicationUses.filter((use) =>
            norm(use.consumerPath ?? '') === file
            && use.startLine - 1 >= span.start
            && use.startLine - 1 < span.end)
          const directEffect = applicationsInSpan.some((application) => effectContracts.some((contract) => methodMatches(application, contract)))
          const rowAdmissions = admissionContracts.filter((contract) => (row.admissions ?? []).some((admission) =>
            symbolMatches(contract.symbol ?? '', admission.symbol ?? '') && norm(contract.file ?? '') === norm(admission.file ?? '')))
          const producerCalls = producerCallsByWitness.get(`${norm(row.file ?? '')}#${row.symbol ?? ''}`) ?? { matches: () => false }
          if (directEffect && !admittedEffectIsDominated(span, row, applicationsInSpan, rowAdmissions, effectContracts, controlFlow, producerCalls)) {
            problems.push(problem('witness-direct-effect-without-admission', file, span.start + 1, row.symbol))
          }
        }
      }
    }
  }
  return problems
}

export const scanRepo = (repoRoot = process.cwd(), manifest = readManifest()) => {
  const entries = collectEntries(repoRoot, manifest)
  const methodNames = [...new Set((manifest.methods ?? []).map((method) => method.symbol?.split('.').at(-1)).filter(Boolean))]
  const applicationConsumerPaths = entries
    .filter((entry) => methodNames.some((name) => new RegExp(`\\b${escapeRe(name)}\\b`).test(stripFSharpNonCode(entry.text))))
    .map((entry) => resolve(repoRoot, entry.file))
  const { symbolUses, applicationUses, matchExpressions, bindExpressions, lambdaExpressions } = scanProjectSymbolUses({
    scratchRoot: AUTHORITY_FCS_ROOT,
    resultPath: AUTHORITY_FCS_RESULT,
    applicationConsumerPaths,
  })
  const problems = scanEntries(entries, manifest, { symbolUses, applicationUses, matchExpressions, bindExpressions, lambdaExpressions })
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
