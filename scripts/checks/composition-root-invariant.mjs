#!/usr/bin/env node
// Composition roots are positive, closed inventories. Every declaration and
// executable control expression must be registered as wiring, representation,
// or typed-mode under an owner law. An unregistered site is a semantic addition,
// regardless of its spelling.

import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { buildTraceGraph } from '../lib/requirement-trace.mjs'
import { scanProjectSymbolUses } from './owner-dependencies.mjs'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
const REGISTRY_PATH = 'scripts/checks/composition-root-contracts.json'
const FCS_SCRATCH = join(ROOT, '.fable-build/composition-root-fcs')
const FCS_RESULT = join(FCS_SCRATCH, 'applications.json')
const CLASSIFICATIONS = new Set(['wiring', 'representation', 'typed-mode'])
let traceGraph
const requirementGraph = () => {
  traceGraph ??= buildTraceGraph(join(ROOT, 'requirements'))
  return traceGraph
}

const registryDocument = JSON.parse(readFileSync(join(ROOT, REGISTRY_PATH), 'utf8'))

export const ROOT_CONTRACTS = Object.freeze((registryDocument.roots ?? []).map((root) => Object.freeze({
  name: root.name,
  file: root.file,
  owner: root.owner,
  sites: Object.freeze((root.sites ?? []).map((site) => Object.freeze({ ...site }))),
})))

const contractFor = (contractOrFile) => {
  if (typeof contractOrFile === 'object' && contractOrFile !== null) return contractOrFile
  return ROOT_CONTRACTS.find((contract) => contract.file === contractOrFile || contract.name === contractOrFile)
    ?? { name: String(contractOrFile), file: String(contractOrFile), sites: [] }
}

const executableText = (text) => {
  let blockDepth = 0
  let inString = false
  let inCharacter = false
  let escaped = false
  let lineComment = false
  let result = ''
  for (let index = 0; index < text.length; index++) {
    const character = text[index]
    const next = text[index + 1]
    if (character === '\n') {
      result += '\n'
      lineComment = false
      escaped = false
      continue
    }
    if (lineComment) {
      result += ' '
      continue
    }
    if (blockDepth > 0) {
      if (character === '(' && next === '*') {
        blockDepth++
        result += '  '
        index++
      } else if (character === '*' && next === ')') {
        blockDepth--
        result += '  '
        index++
      } else result += ' '
      continue
    }
    if (inString || inCharacter) {
      result += ' '
      if (escaped) escaped = false
      else if (character === '\\') escaped = true
      else if ((inString && character === '"') || (inCharacter && character === "'")) {
        inString = false
        inCharacter = false
      }
      continue
    }
    if (character === '/' && next === '/') {
      lineComment = true
      result += '  '
      index++
    } else if (character === '(' && next === '*') {
      blockDepth = 1
      result += '  '
      index++
    } else if (character === '"') {
      inString = true
      result += ' '
    } else if (character === "'" && /^(?:'[^'\\]|'\\.)'/.test(text.slice(index))) {
      inCharacter = true
      result += ' '
    } else result += character
  }
  return result
}

const STRUCTURES = Object.freeze([
  ['declaration', /\b(?:let|use)!?\b/g],
  ['if', /\bif\b/g],
  ['elif', /\belif\b/g],
  ['match', /\bmatch!?\b/g],
  ['try', /\btry\b/g],
  ['lambda', /\b(?:fun|function)\b/g],
  ['loop', /\b(?:for|while)\b/g],
])

const QUALIFIED_TARGET = /\b[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+\b/g

const applicationTargets = (line) => {
  const targets = []
  QUALIFIED_TARGET.lastIndex = 0
  for (let match; (match = QUALIFIED_TARGET.exec(line)) !== null;) {
    const before = line.slice(0, match.index)
    const after = line.slice(QUALIFIED_TARGET.lastIndex)
    const piped = /\|>\s*$/.test(before)
    const applied = /^\s*(?:\(|[A-Za-z_][\w']*|\{|\[|\"|\d)/.test(after)
    if (piped || applied) targets.push(match[0])
  }
  return targets
}

/** Exact structural inventory from executable F# tokens and resolved FCS applications. */
export const compositionSites = (text, applicationUses) => {
  const occurrences = new Map()
  const sites = []
  const add = (kind, anchor, line) => {
    const occurrenceKey = `${kind}\u0000${anchor}`
    const occurrence = (occurrences.get(occurrenceKey) ?? 0) + 1
    occurrences.set(occurrenceKey, occurrence)
    sites.push({ kind, anchor, occurrence, line })
  }

  const rawLines = text.split('\n')
  const codeLines = executableText(text).split('\n')
  for (const [index, structure] of codeLines.entries()) {
    if (!structure.trim()) continue
    const anchor = rawLines[index].slice(0, structure.trimEnd().length).trim()
    for (const [kind, pattern] of STRUCTURES) {
      pattern.lastIndex = 0
      while (pattern.exec(structure) !== null) add(kind, anchor, index + 1)
    }
    if (applicationUses === undefined) {
      for (const target of applicationTargets(structure)) add('application', target, index + 1)
    }
  }
  const orderedApplications = [...(applicationUses ?? [])].sort((left, right) =>
    left.startLine - right.startLine || left.startColumn - right.startColumn
      || left.endLine - right.endLine || left.endColumn - right.endColumn
      || left.resolvedTarget.localeCompare(right.resolvedTarget))
  for (const application of orderedApplications) {
    const occurrenceKey = `application\u0000${application.sourceAnchor}`
    const occurrence = (occurrences.get(occurrenceKey) ?? 0) + 1
    occurrences.set(occurrenceKey, occurrence)
    sites.push({
      kind: 'application',
      anchor: application.sourceAnchor,
      occurrence,
      line: application.startLine,
      column: application.startColumn,
      endLine: application.endLine,
      endColumn: application.endColumn,
      resolvedTarget: application.resolvedTarget,
      declarationPaths: [...new Set(application.declarationPaths ?? [])].sort(),
    })
  }
  return sites
}

const siteKey = (site) => site.kind === 'application' && site.resolvedTarget
  ? [site.kind, site.anchor, site.occurrence ?? 1, site.line ?? site.startLine, site.column ?? site.startColumn,
      site.endLine, site.endColumn, site.resolvedTarget, ...(site.declarationPaths ?? [])].join('\u0000')
  : `${site.kind}\u0000${site.anchor}\u0000${site.occurrence ?? 1}`

/** @returns {{root:string,file:string,kind:string,message:string,line?:number}[]} */
export const scanCompositionRoot = (text, contractOrFile = '<synthetic>', applicationUses) => {
  const contract = contractFor(contractOrFile)
  const violations = []
  const registered = new Map()

  for (const site of contract.sites ?? []) {
    const key = siteKey(site)
    if (registered.has(key)) {
      violations.push({ root: contract.name, file: contract.file, kind: 'duplicate-registration', message: `duplicate registry site: ${site.kind} ${site.anchor} #${site.occurrence}` })
      continue
    }
    registered.set(key, site)
    if (!CLASSIFICATIONS.has(site.classification)) {
      violations.push({ root: contract.name, file: contract.file, kind: 'invalid-classification', message: `${site.kind} ${site.anchor} has invalid classification '${site.classification}'` })
    }
    if (site.kind === 'application' && (!site.resolvedTarget || !Number.isInteger(site.line) || !Number.isInteger(site.column)
      || !Number.isInteger(site.endLine) || !Number.isInteger(site.endColumn) || !Array.isArray(site.declarationPaths))) {
      violations.push({ root: contract.name, file: contract.file, kind: 'invalid-application-registration', message: `application ${site.anchor} lacks exact resolved target, source range, or declaration paths` })
    }
    if (!/^[A-Z][A-Z0-9-]*-\d{3}$/.test(site.ownerLaw ?? '')) {
      violations.push({ root: contract.name, file: contract.file, kind: 'missing-owner-law', message: `${site.kind} ${site.anchor} has no exact owner law` })
    } else {
      const graph = requirementGraph()
      const law = graph.whats.get(site.ownerLaw)
      if (!law || graph.whatDefinitions.get(site.ownerLaw)?.length !== 1 || law.package !== contract.owner) {
        violations.push({ root: contract.name, file: contract.file, kind: 'foreign-owner-law', message: `${site.kind} ${site.anchor} law ${site.ownerLaw} is not uniquely owned by ${contract.owner}` })
      }
    }
  }

  const actual = new Map(compositionSites(text, applicationUses).map((site) => [siteKey(site), site]))
  for (const [key, site] of actual) {
    if (!registered.has(key)) {
      const resolution = site.resolvedTarget ? ` -> ${site.resolvedTarget}${site.declarationPaths.length > 0 ? ` [${site.declarationPaths.join(', ')}]` : ''}` : ''
      violations.push({ root: contract.name, file: contract.file, line: site.line, kind: 'unregistered-site', message: `unregistered ${site.kind}: ${site.anchor}${resolution} #${site.occurrence}` })
    }
  }
  for (const [key, site] of registered) {
    if (!actual.has(key)) {
      violations.push({ root: contract.name, file: contract.file, kind: 'stale-anchor', message: `registered ${site.kind} anchor is stale: ${site.anchor} #${site.occurrence}` })
    }
  }
  return violations
}

/** @param {{file:string,text:string}[]} entries */
export const scanTexts = (entries, applicationUses) => {
  const byFile = new Map(entries.map((entry) => [entry.file.replace(/\\/g, '/'), entry.text]))
  const violations = []
  for (const contract of ROOT_CONTRACTS) {
    const text = byFile.get(contract.file)
    if (text === undefined) {
      violations.push({ root: contract.name, file: contract.file, kind: 'missing-root', message: 'registered composition root is missing' })
    } else {
      violations.push(...scanCompositionRoot(text, contract, applicationUses?.filter((use) => use.consumerPath === contract.file)))
    }
  }
  return violations
}

export const scanCompositionRootApplications = () => scanProjectSymbolUses({
  scratchRoot: FCS_SCRATCH,
  resultPath: FCS_RESULT,
  applicationConsumerPaths: ROOT_CONTRACTS.map(({ file }) => file),
}).applicationUses

export const scanRepo = (root = ROOT, resolvedApplications) => {
  const applicationUses = resolvedApplications ?? scanCompositionRootApplications()
  return scanTexts(ROOT_CONTRACTS.map((contract) => ({
    file: contract.file,
    text: readFileSync(join(root, contract.file), 'utf8'),
  })), applicationUses)
}

const runCli = () => {
  const violations = scanRepo()
  if (violations.length > 0) {
    console.error('composition-root-invariant: VIOLATIONS')
    for (const violation of violations) console.error(`  ${violation.file}${violation.line ? `:${violation.line}` : ''} [${violation.kind}] ${violation.message}`)
    process.exit(1)
  }
  console.log(`composition-root-invariant: OK — ${ROOT_CONTRACTS.map((root) => root.name).join(', ')} exactly match the positive operation registry`)
}

const isMain = process.argv[1] !== undefined && resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])
if (isMain) runCli()
