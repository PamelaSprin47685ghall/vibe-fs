#!/usr/bin/env node

import { readFileSync, readdirSync } from 'node:fs'
import { dirname, isAbsolute, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import {
  PROOF_LEVELS,
  buildTraceGraph,
  resolveProofLevel,
  validateProofLevelRegistry,
  whatHeadings,
} from '../lib/requirement-trace.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const DEFAULT_ROOT = resolve(HERE, '..', '..')

export const REQUIRED_EFFECT_IDS = Object.freeze([
  'canonical-append',
  'writer-sync',
  'worktree-create',
  'branch-fast-forward',
  'prompt-dispatch',
  'blogger-request',
  'todo-write',
  'js-transaction',
  'provider-execution',
  'managed-child',
  'managed-attempt-interrupt',
  'bounded-process',
])

export const EXTERNAL_EFFECT_SCHEMA = Object.freeze({
  schema_version: 1,
  phases: Object.freeze(['intent', 'admission', 'physical_receipt', 'durable_outcome']),
  mechanisms: Object.freeze(['idempotency', 'query', 'compensation', 'finite-fail-closed']),
  retry: Object.freeze(['proven-not-applied', 'never']),
  proof_levels: PROOF_LEVELS,
})

const PHASE_KINDS = Object.freeze({
  intent: new Set(['durable-intent', 'process-local-intent', 'not-applicable']),
  admission: new Set(['process-local-admission', 'not-applicable']),
  physical_receipt: new Set(['transport-receipt', 'physical-identity', 'typed-result', 'physical-observation', 'not-applicable']),
  durable_outcome: new Set(['durable-outcome', 'not-applicable']),
})

const EXTERNAL_BOUNDARIES = new Set([
  'writer-sync',
  'worktree-create',
  'branch-fast-forward',
  'prompt-dispatch',
  'blogger-request',
  'todo-write',
  'provider-execution',
  'managed-child',
  'managed-attempt-interrupt',
  'bounded-process',
])
const PROOF_LEVEL_SET = new Set(EXTERNAL_EFFECT_SCHEMA.proof_levels)
const MECHANISMS = new Set(EXTERNAL_EFFECT_SCHEMA.mechanisms)
const RECOVERY_PC = new Set(['ResumeAt', 'RecoveryStage', 'RecoveryStep', 'NextAction'])
const ROW_KEYS = new Set([
  'id',
  'owner',
  'what_ids',
  'intent',
  'admission',
  'physical_receipt',
  'durable_outcome',
  'effect_identity',
  'ambiguity',
  'reentry',
  'proof_portfolio',
])

const norm = (value) => String(value).replace(/\\/g, '/').replace(/^\.\//, '')
const lineOf = (text, index) => text.slice(0, index).split('\n').length
const nonempty = (value) => typeof value === 'string' && value.trim().length > 0
const plainObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value)
const unique = (values) => new Set(values).size === values.length
const escapeRe = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
const hasExactSymbol = (source, symbol) => new RegExp(`(?<![A-Za-z0-9_'])${escapeRe(symbol)}(?![A-Za-z0-9_'])`, 'u').test(source)

const diagnostic = (code, row, law, message, path = null) => ({
  code,
  effect: row?.id ?? '<registry>',
  owner: row?.owner ?? '<unknown>',
  law,
  path,
  message: `${row?.id ?? 'registry'} owner=${row?.owner ?? '<unknown>'} law=${law}: ${message}`,
})

const stripStringsAndComments = (source) => {
  let out = ''
  let i = 0
  let block = false
  let quote = null
  while (i < source.length) {
    const ch = source[i]
    const next = source[i + 1]
    if (block) {
      if (ch === '*' && next === '/') {
        block = false
        out += '  '
        i += 2
      } else {
        out += ch === '\n' ? '\n' : ' '
        i++
      }
    } else if (quote) {
      if (ch === '\\') {
        out += '  '
        i += 2
      } else if (ch === quote) {
        quote = null
        out += ' '
        i++
      } else {
        out += ch === '\n' ? '\n' : ' '
        i++
      }
    } else if (ch === '/' && next === '*') {
      block = true
      out += '  '
      i += 2
    } else if (ch === '/' && next === '/') {
      const end = source.indexOf('\n', i + 2)
      if (end < 0) {
        out += ' '.repeat(source.length - i)
        break
      }
      out += ' '.repeat(end - i) + '\n'
      i = end + 1
    } else if (ch === '"' || ch === "'" || ch === '`') {
      quote = ch
      out += ' '
      i++
    } else {
      out += ch
      i++
    }
  }
  return out
}

/** Pure lexical scanner for forbidden durable recovery program counters. */
export function scanRecoveryProgramCounters(source, path = '<source>') {
  const code = stripStringsAndComments(String(source))
  const findings = []
  for (const match of code.matchAll(/\b(?:ResumeAt|RecoveryStage|RecoveryStep|NextAction)\b/g)) {
    if (RECOVERY_PC.has(match[0])) findings.push({ path: norm(path), line: lineOf(code, match.index), symbol: match[0] })
  }
  return findings
}

const referencedSourcePaths = (document) => {
  const paths = new Set()
  for (const row of document?.effects ?? []) {
    for (const phase of EXTERNAL_EFFECT_SCHEMA.phases) if (nonempty(row?.[phase]?.path)) paths.add(norm(row[phase].path))
    if (nonempty(row?.ambiguity?.query_or_compensation?.path)) paths.add(norm(row.ambiguity.query_or_compensation.path))
    if (nonempty(row?.reentry?.path)) paths.add(norm(row.reentry.path))
  }
  return [...paths]
}

const validatePath = (row, law, path, findings) => {
  if (!nonempty(path) || isAbsolute(path) || norm(path).split('/').includes('..')) {
    findings.push(diagnostic('EFFECT_PATH', row, law, 'path must be a nonempty workspace-relative path', path ?? null))
    return false
  }
  return true
}

const validateSourceAnchor = (row, law, path, symbols, context, findings) => {
  if (!validatePath(row, law, path, findings)) return
  const source = context.sources?.get(norm(path))
  if (source === undefined) {
    findings.push(diagnostic('EFFECT_SOURCE_MISSING', row, law, `source path does not exist: ${norm(path)}`, norm(path)))
    return
  }
  if (!Array.isArray(symbols) || symbols.length === 0 || symbols.some((symbol) => !nonempty(symbol)) || !unique(symbols)) {
    findings.push(diagnostic('EFFECT_SYMBOLS', row, law, 'symbols must be a nonempty unique string array', norm(path)))
    return
  }
  for (const symbol of symbols) {
    if (!hasExactSymbol(source, symbol)) {
      findings.push(diagnostic('EFFECT_SOURCE_SYMBOL', row, law, `exact source symbol is stale or absent: ${symbol}`, norm(path)))
    }
  }
}

const validatePhase = (row, phaseName, context, findings) => {
  const phase = row[phaseName]
  const law = `phase.${phaseName}`
  if (!plainObject(phase) || !nonempty(phase.kind) || !PHASE_KINDS[phaseName].has(phase.kind)) {
    findings.push(diagnostic('EFFECT_PHASE_KIND', row, law, `kind must be one of ${[...PHASE_KINDS[phaseName]].join('|')}`))
    return
  }
  if (phase.kind === 'not-applicable') {
    if (!nonempty(phase.reason) || Object.keys(phase).some((key) => !['kind', 'reason'].includes(key))) {
      findings.push(diagnostic('EFFECT_PHASE_NA', row, law, 'not-applicable requires only a nonempty reason'))
    }
    return
  }
  if (Object.keys(phase).some((key) => !['kind', 'path', 'symbols'].includes(key))) {
    findings.push(diagnostic('EFFECT_PHASE_SHAPE', row, law, 'applicable phase permits only kind, path, and symbols'))
  }
  validateSourceAnchor(row, law, phase.path, phase.symbols, context, findings)
}

const validatePhysicalReceiptTruth = (row, findings) => {
  const receipt = row.physical_receipt
  const anchored = (path, symbols) => norm(receipt?.path) === path && symbols.every((symbol) => receipt?.symbols?.includes(symbol))
  if (row.id === 'worktree-create' && !anchored('src/Wanxiangshu/Change/Runtime.fs', ['WorktreeResource.Create', 'WorktreeResource.Adopt'])) {
    findings.push(diagnostic('EFFECT_PHASE_TRUTH', row, 'phase.physical_receipt', 'worktree receipt must anchor the ordinary runtime Create/Adopt effect callsites', norm(receipt?.path ?? '')))
  }
  if (row.id === 'todo-write' && !anchored('src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs', ['runAfterTodo', 'PhysicalSuccessEvidence.LiveAfterSuccess'])) {
    findings.push(diagnostic('EFFECT_PHASE_TRUTH', row, 'phase.physical_receipt', 'Todo receipt must anchor the Host After physical-success observation', norm(receipt?.path ?? '')))
  }
}

const validateAmbiguity = (row, context, findings) => {
  const value = row.ambiguity
  if (!plainObject(value)) {
    findings.push(diagnostic('EFFECT_RESTART_LAW', row, 'ambiguity', 'acknowledged effect has no restart ambiguity law'))
    return
  }
  if (Object.keys(value).some((key) => !['mechanisms', 'states', 'retry_when', 'query_or_compensation'].includes(key))) {
    findings.push(diagnostic('EFFECT_AMBIGUITY_SHAPE', row, 'ambiguity', 'ambiguity has unknown fields'))
  }
  if (!Array.isArray(value.mechanisms) || value.mechanisms.length === 0 || !unique(value.mechanisms) || value.mechanisms.some((item) => !MECHANISMS.has(item))) {
    findings.push(diagnostic('EFFECT_AMBIGUITY_MECHANISM', row, 'ambiguity.mechanisms', `mechanisms must be a nonempty unique subset of ${[...MECHANISMS].join('|')}`))
  }
  if (!Array.isArray(value.states) || value.states.length === 0 || value.states.length > 12 || !unique(value.states) || value.states.some((item) => !nonempty(item) || /\*|unbounded|infinite/i.test(item))) {
    findings.push(diagnostic('EFFECT_AMBIGUITY_UNBOUNDED', row, 'ambiguity.states', 'states must be a finite unique named set (1..12, no wildcard/unbounded state)'))
  }
  if (!EXTERNAL_EFFECT_SCHEMA.retry.includes(value.retry_when)) {
    findings.push(diagnostic('EFFECT_RETRY_UNSAFE', row, 'ambiguity.retry_when', 'retry_when must be proven-not-applied or never'))
  }
  if (value.retry_when === 'proven-not-applied') {
    if (!value.mechanisms?.includes('query') || !value.states?.includes('proven-not-applied')) {
      findings.push(diagnostic('EFFECT_RETRY_UNSAFE', row, 'ambiguity.retry_when', 'proven-not-applied retry requires query and an explicit proven-not-applied state'))
    }
  }
  const query = value.query_or_compensation
  if (!plainObject(query) || !nonempty(query.path) || !nonempty(query.symbol)) {
    findings.push(diagnostic('EFFECT_AMBIGUITY_ANCHOR', row, 'ambiguity.query_or_compensation', 'finite ambiguity requires an exact query or compensation source anchor'))
  } else {
    validateSourceAnchor(row, 'ambiguity.query_or_compensation', query.path, [query.symbol], context, findings)
  }
}

const validateReentry = (row, context, findings) => {
  const value = row.reentry
  if (!plainObject(value)) {
    findings.push(diagnostic('EFFECT_RESTART_LAW', row, 'reentry', 'acknowledged effect has no ordinary CE re-entry law'))
    return
  }
  if (value.ordinary_ce !== true) findings.push(diagnostic('EFFECT_REENTRY_CE', row, 'reentry', 'ordinary_ce must be exactly true'))
  validateSourceAnchor(row, 'reentry', value.path, [value.symbol], context, findings)
  const source = context.sources?.get(norm(value.path))
  if (source !== undefined) {
    for (const hit of scanRecoveryProgramCounters(source, value.path)) {
      findings.push(diagnostic('EFFECT_RECOVERY_PC', row, 'reentry', `forbidden recovery program counter ${hit.symbol} at line ${hit.line}`, hit.path))
    }
  }
}

const validateProofs = (row, context, findings) => {
  if (!Array.isArray(row.proof_portfolio) || row.proof_portfolio.length === 0) {
    findings.push(diagnostic('EFFECT_PROOF_MISSING', row, 'proof_portfolio', 'at least one exact executable proof is required'))
    return
  }
  const seen = new Set()
  const verifiedLevels = new Set()
  for (const proof of row.proof_portfolio) {
    const law = `proof.${proof?.what_id ?? '<unknown>'}`
    if (!plainObject(proof) || !nonempty(proof.what_id) || !nonempty(proof.path) || !nonempty(proof.title) || !PROOF_LEVEL_SET.has(proof.level)) {
      findings.push(diagnostic('EFFECT_PROOF_SHAPE', row, law, 'proof requires what_id, workspace-relative test path, exact title, and a valid level'))
      continue
    }
    const classifiedLevel = resolveProofLevel(context.proofLevelRegistry, proof)
    if (classifiedLevel === null) {
      findings.push(diagnostic('EFFECT_PROOF_CLASSIFICATION_UNKNOWN', row, law, 'exact (path, title, what_id) proof has no independent classification', norm(proof.path)))
    } else if (classifiedLevel !== proof.level) {
      findings.push(diagnostic('EFFECT_PROOF_LEVEL_MISMATCH', row, law, `self-declared level ${proof.level} does not match independent level ${classifiedLevel}`, norm(proof.path)))
    } else {
      verifiedLevels.add(classifiedLevel)
    }
    const key = `${norm(proof.path)}\u0000${proof.title}`
    if (seen.has(key)) findings.push(diagnostic('EFFECT_PROOF_DUPLICATE', row, law, 'duplicate proof title anchor', norm(proof.path)))
    seen.add(key)
    if (!row.what_ids?.includes(proof.what_id)) findings.push(diagnostic('EFFECT_PROOF_FOREIGN_WHAT', row, law, `${proof.what_id} is not declared by this row`, norm(proof.path)))
    if (!validatePath(row, law, proof.path, findings)) continue
    const matches = (context.tests ?? []).filter((test) => norm(test.file) === norm(proof.path) && test.title === proof.title)
    if (matches.length !== 1) {
      findings.push(diagnostic('EFFECT_PROOF_STALE', row, law, `exact active test title resolves ${matches.length} times`, norm(proof.path)))
      continue
    }
    const test = matches[0]
    if (test.state !== 'active' || test.whatIds.length !== 1 || test.whatIds[0] !== proof.what_id) {
      findings.push(diagnostic('EFFECT_PROOF_OWNERSHIP', row, law, `test must be active with sole primary WHAT[${proof.what_id}]`, norm(proof.path)))
    }
  }
  if (EXTERNAL_BOUNDARIES.has(row.id)) {
    if (!verifiedLevels.has('adapter') && !verifiedLevels.has('long-stroke')) {
      findings.push(diagnostic('EFFECT_ADAPTER_PROOF', row, 'proof_portfolio', 'Host/provider/Git/process boundary requires independently classified Adapter or Long-Stroke evidence'))
    }
    if (!verifiedLevels.has('pure') && !verifiedLevels.has('temporal')) {
      findings.push(diagnostic('EFFECT_AMBIGUITY_PROOF', row, 'proof_portfolio', 'external boundary requires independently classified deterministic Pure or Temporal ambiguity evidence'))
    }
  }
}

/** Pure registry validator. All source and trace inputs are supplied by the caller. */
export function validateExternalEffectRegistry(document, context = {}, options = {}) {
  const findings = []
  if (!plainObject(document) || document.schema_version !== EXTERNAL_EFFECT_SCHEMA.schema_version || !Array.isArray(document.effects)) {
    return [diagnostic('EFFECT_REGISTRY_SHAPE', null, 'schema', 'expected { schema_version: 1, effects: [...] }')]
  }
  for (const proofFinding of context.proofLevelRegistryFindings ?? []) {
    findings.push(diagnostic('EFFECT_PROOF_REGISTRY_INVALID', null, 'proof-level-registry', proofFinding.message, proofFinding.key ?? null))
  }
  for (const duplicate of context.duplicateWhatIds ?? []) {
    findings.push(diagnostic('EFFECT_WHAT_DUPLICATE', null, `WHAT[${duplicate.id}]`, `duplicate WHAT id appears at ${duplicate.locations.join(', ')}`))
  }
  const rows = document.effects
  const ids = rows.map((row) => row?.id).filter(nonempty)
  for (const id of new Set(ids)) {
    if (ids.filter((value) => value === id).length > 1) findings.push(diagnostic('EFFECT_ROW_DUPLICATE', rows.find((row) => row?.id === id), 'row.id', 'effect id must be unique'))
  }
  if (options.requireCensus !== false) {
    for (const id of REQUIRED_EFFECT_IDS) if (!ids.includes(id)) findings.push(diagnostic('EFFECT_CENSUS_MISSING', null, 'census', `missing required effect row ${id}`))
    for (const id of ids) if (!REQUIRED_EFFECT_IDS.includes(id)) findings.push(diagnostic('EFFECT_CENSUS_UNKNOWN', rows.find((row) => row?.id === id), 'census', 'effect id is outside the closed 12-row census'))
  }
  for (const row of rows) {
    if (!plainObject(row) || !nonempty(row.id) || !nonempty(row.owner)) {
      findings.push(diagnostic('EFFECT_ROW_SHAPE', row, 'row', 'row requires nonempty id and owner'))
      continue
    }
    if (Object.keys(row).some((key) => !ROW_KEYS.has(key))) findings.push(diagnostic('EFFECT_ROW_SHAPE', row, 'row', 'row has unknown fields'))
    if (!Array.isArray(row.what_ids) || row.what_ids.length === 0 || row.what_ids.some((id) => !nonempty(id)) || !unique(row.what_ids)) {
      findings.push(diagnostic('EFFECT_WHAT_IDS', row, 'what_ids', 'what_ids must be a nonempty unique string array'))
    } else {
      for (const id of row.what_ids) {
        const what = context.whats?.get(id)
        if (!what) findings.push(diagnostic('EFFECT_WHAT_UNKNOWN', row, `WHAT[${id}]`, 'WHAT proposition does not exist'))
        else if (what.package !== row.owner) findings.push(diagnostic('EFFECT_WHAT_FOREIGN', row, `WHAT[${id}]`, `WHAT is owned by ${what.package}, not ${row.owner}`, norm(what.file)))
      }
    }
    for (const phase of EXTERNAL_EFFECT_SCHEMA.phases) validatePhase(row, phase, context, findings)
    validatePhysicalReceiptTruth(row, findings)
    if (!nonempty(row.effect_identity)) findings.push(diagnostic('EFFECT_IDENTITY', row, 'effect_identity', 'physical effect identity must be explicit and nonempty'))
    validateAmbiguity(row, context, findings)
    validateReentry(row, context, findings)
    validateProofs(row, context, findings)
  }
  return findings.sort((a, b) => a.effect.localeCompare(b.effect) || a.code.localeCompare(b.code) || a.message.localeCompare(b.message))
}

const walkWhatFiles = (directory) => {
  const files = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) files.push(...walkWhatFiles(path))
    else if (entry.name === 'WHAT.md') files.push(path)
  }
  return files
}

const duplicateWhats = (requirementsRoot) => {
  const byId = new Map()
  for (const file of walkWhatFiles(requirementsRoot)) {
    const text = readFileSync(file, 'utf8')
    for (const heading of whatHeadings(text)) {
      const location = `${norm(relative(DEFAULT_ROOT, file))}:${heading.line}`
      if (!byId.has(heading.id)) byId.set(heading.id, [])
      byId.get(heading.id).push(location)
    }
  }
  return [...byId].filter(([, locations]) => locations.length > 1).map(([id, locations]) => ({ id, locations }))
}

export function loadValidationContext(root, document) {
  const workspace = resolve(root)
  const requirementsRoot = join(workspace, 'requirements')
  const graph = buildTraceGraph(requirementsRoot)
  const proofLevelRegistry = JSON.parse(readFileSync(join(workspace, 'scripts/checks/proof-levels.json'), 'utf8'))
  const sources = new Map()
  for (const path of referencedSourcePaths(document)) {
    try {
      sources.set(path, readFileSync(resolve(workspace, path), 'utf8'))
    } catch {
      // Missing paths remain absent so the pure validator owns the diagnostic.
    }
  }
  return {
    whats: graph.whats,
    tests: graph.tests.map((test) => ({ ...test, file: norm(relative(workspace, test.file)) })),
    sources,
    duplicateWhatIds: duplicateWhats(requirementsRoot),
    proofLevelRegistry,
    proofLevelRegistryFindings: validateProofLevelRegistry(proofLevelRegistry),
  }
}

export function validateWorkspace(root = DEFAULT_ROOT) {
  const registryPath = join(resolve(root), 'scripts/checks/external-effect-contracts.json')
  const document = JSON.parse(readFileSync(registryPath, 'utf8'))
  return validateExternalEffectRegistry(document, loadValidationContext(root, document))
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  let findings
  try {
    findings = validateWorkspace(DEFAULT_ROOT)
  } catch (error) {
    console.error(`[EFFECT_REGISTRY_LOAD] ${error.message}`)
    process.exitCode = 1
  }
  if (findings) {
    for (const finding of findings) console.error(`[${finding.code}] ${finding.message}${finding.path ? ` (${finding.path})` : ''}`)
    if (findings.length > 0) process.exitCode = 1
    else console.log(`external-effect reconciliation: ${REQUIRED_EFFECT_IDS.length} contracts valid`)
  }
}
