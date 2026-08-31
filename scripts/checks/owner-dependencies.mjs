#!/usr/bin/env node

import { spawnSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { homedir, userInfo } from 'node:os'
import { dirname, isAbsolute, join, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const FSPROJ = join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj')
const PRODUCTION_ROOT = join(ROOT, 'src/Wanxiangshu')
const OWNERS = join(ROOT, 'scripts/checks/semantic-owners.json')
const CONTRACTS = join(ROOT, 'scripts/checks/published-contracts.json')
const MIGRATION_LEDGER = join(ROOT, 'scripts/checks/migration-ledger.json')
const SYMBOL_SCANNER = join(ROOT, 'scripts/checks/owner-symbol-uses.fsx')
const FCS_SCRATCH = join(ROOT, '.fable-build/owner-dependencies-fcs')
const FCS_RESULT = join(FCS_SCRATCH, 'symbol-uses.json')
const FABLE_LIBRARY = join(ROOT, 'node_modules/@fable-org/fable-library-js')
export const FCS_REUSE_PATH_ENV = 'OMP_FCS_REUSE_PATH'
export const FCS_REUSE_RUN_ID_ENV = 'OMP_FCS_REUSE_RUN_ID'
export const FCS_NORMALIZED_OUTPUT_ENV = 'OMP_FCS_NORMALIZED_OUTPUT_PATH'
export const FCS_NORMALIZED_SCHEMA_VERSION = 2
const PATH_GLOB = /[*?\[\]]/
const PUBLICISH_PATH = /(?:Surface|Contract|Port|Api)\.fs$/
const EXECUTION_POSITION = /(?:^|[._/])(Stage|Step|Cursor|Registry|NextAction|ResumeAt)(?:$|[A-Z._/])/i

const norm = (path) => path.replace(/\\/g, '/')
const meaningful = (value) => typeof value === 'string' && value.trim().length >= 16

function repositoryPath(path, label) {
  const normalized = norm(relative(ROOT, resolve(path)))
  if (normalized === '..' || normalized.startsWith('../')) throw new Error(`${label} is outside the repository: ${path}`)
  return normalized
}

function readCompilePaths(projectFile, productionRoot) {
  const project = resolve(projectFile)
  const root = resolve(productionRoot)
  const prefix = `${norm(root).replace(/\/$/, '')}/`
  const text = readFileSync(project, 'utf8')
  const paths = [...text.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/>/g)]
    .map((match) => resolve(dirname(project), match[1]))
    .filter((path) => `${norm(path)}/`.startsWith(prefix))
    .map((path) => repositoryPath(path, 'compile source'))

  if (paths.length === 0) throw new Error(`${repositoryPath(project, 'project file')}: no production Compile entries found`)
  if (new Set(paths).size !== paths.length) throw new Error(`${repositoryPath(project, 'project file')}: duplicate Compile entry`)
  return paths
}

function findFiles(directory, name, found = []) {
  if (!existsSync(directory)) return found
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) findFiles(path, name, found)
    else if (entry.isFile() && entry.name === name) found.push(path)
  }
  return found
}

function fableToolDirectory() {
  const manifest = JSON.parse(readFileSync(join(ROOT, '.config/dotnet-tools.json'), 'utf8'))
  const version = manifest?.tools?.fable?.version
  if (typeof version !== 'string' || version.length === 0) throw new Error('dotnet tool manifest has no pinned Fable version')
  const packageRoots = [
    process.env.NUGET_PACKAGES,
    join(userInfo().homedir, '.nuget/packages'),
    join(homedir(), '.nuget/packages'),
  ]
    .filter(Boolean)
    .map((path) => resolve(path))
    .filter((path, index, paths) => paths.indexOf(path) === index)
  const directories = [
    ...new Set(
      packageRoots
        .flatMap((packageRoot) => findFiles(join(packageRoot, 'fable', version), 'Fable.Compiler.dll'))
        .map(dirname)
        .map((path) => resolve(path))
        .filter((path) => existsSync(join(path, 'Fable.AST.dll')) && existsSync(join(path, 'FSharp.Compiler.Service.dll'))),
    ),
  ]
  if (directories.length !== 1)
    throw new Error(
      `Fable ${version}: expected one compiler tool directory, found ${directories.length} under ${packageRoots.join(', ')}`,
    )
  return directories[0]
}

function comparePathSets(expected, actual, label) {
  const expectedSet = new Set(expected)
  const actualSet = new Set(actual)
  const missing = expected.filter((path) => !actualSet.has(path))
  const extra = actual.filter((path) => !expectedSet.has(path))
  if (missing.length > 0 || extra.length > 0)
    throw new Error(
      `${label} differs from the .fsproj compile set: missing=[${missing.slice(0, 8).join(', ')}] extra=[${extra.slice(0, 8).join(', ')}]`,
    )
}

function normalizeSymbolUse(record, productionFiles) {
  if (!record || typeof record !== 'object') throw new Error('FCS scanner emitted a non-object symbol use')
  if (typeof record.consumerPath !== 'string') throw new Error('FCS scanner emitted a symbol use without consumerPath')
  if (!Array.isArray(record.providerPaths)) throw new Error('FCS scanner emitted a symbol use without providerPaths')
  if (typeof record.symbol !== 'string' || record.symbol.length === 0)
    throw new Error('FCS scanner emitted a symbol use without symbol')

  const consumerPath = repositoryPath(record.consumerPath, 'symbol consumer')
  const providerPaths = [...new Set(record.providerPaths.map((path) => repositoryPath(path, 'symbol provider')))].sort()
  const declarationPaths = [...new Set((record.declarationPaths ?? record.providerPaths).map((path) => repositoryPath(path, 'symbol declaration')))].sort()
  if (!productionFiles.has(consumerPath)) throw new Error(`${consumerPath}: FCS consumer is outside the production compile set`)
  for (const path of providerPaths)
    if (!productionFiles.has(path)) throw new Error(`${path}: FCS provider is outside the production compile set`)
  for (const path of declarationPaths)
    if (!productionFiles.has(path)) throw new Error(`${path}: FCS declaration is outside the production compile set`)

  return {
    consumerPath,
    providerPaths,
    declarationPaths,
    symbol: record.symbol,
    symbolKind: typeof record.symbolKind === 'string' ? record.symbolKind : 'Unknown',
    assembly: typeof record.assembly === 'string' ? record.assembly : '',
    line: Number.isInteger(record.line) ? record.line : 0,
    column: Number.isInteger(record.column) ? record.column : 0,
    endLine: Number.isInteger(record.endLine) ? record.endLine : (Number.isInteger(record.line) ? record.line : 0),
    endColumn: Number.isInteger(record.endColumn) ? record.endColumn : (Number.isInteger(record.column) ? record.column : 0),
    inferredType: typeof record.inferredType === 'string' ? record.inferredType : '',
    isNamespace: record.isNamespace === true,
    isModule: record.isModule === true,
    isFromOpenStatement: record.isFromOpenStatement === true,
    isFromPattern: record.isFromPattern === true,
    isFromType: record.isFromType === true,
    isFromUse: record.isFromUse === true,
    missingDeclaration: record.missingDeclaration === true,
  }
}

function normalizeApplicationRange(record, productionFiles) {
  if (!record || typeof record.consumerPath !== 'string') throw new Error('FCS scanner emitted an invalid application range')
  const consumerPath = repositoryPath(record.consumerPath, 'application consumer')
  if (!productionFiles.has(consumerPath)) throw new Error(`${consumerPath}: FCS application is outside the production compile set`)
  return {
    consumerPath,
    targetStartLine: record.targetStartLine,
    targetStartColumn: record.targetStartColumn,
    targetEndLine: record.targetEndLine,
    targetEndColumn: record.targetEndColumn,
    startLine: record.startLine,
    startColumn: record.startColumn,
    endLine: record.endLine,
    endColumn: record.endColumn,
  }
}

const position = (line, column) => line * 1_000_000 + column

// Range evidence is queried millions of times (application target ↔ symbol use ↔ control-flow
// region). Group it by its exact lookup key and keep each group sorted by (start, end, arrival)
// so containment queries are a binary search plus a bounded window instead of a full scan.
const rangeGroups = (records, keyOf, boundsOf) => {
  const groups = new Map()
  records.forEach((value, order) => {
    const key = keyOf(value)
    const [start, end] = boundsOf(value)
    const group = groups.get(key)
    if (group) group.push({ value, order, start, end })
    else groups.set(key, [{ value, order, start, end }])
  })
  for (const group of groups.values())
    group.sort((left, right) => left.start - right.start || left.end - right.end || left.order - right.order)
  return groups
}

const lowerBound = (group, start) => {
  let low = 0
  let high = group.length
  while (low < high) {
    const middle = (low + high) >> 1
    if (group[middle].start < start) low = middle + 1
    else high = middle
  }
  return low
}

const enclosedBy = (groups, key, start, end) => {
  const group = groups.get(key) ?? []
  const enclosed = []
  for (let index = lowerBound(group, start); index < group.length && group[index].start <= end; index += 1)
    if (group[index].end <= end) enclosed.push(group[index])
  return enclosed
}

const firstEnclosedBy = (groups, key, start, end) => {
  const group = groups.get(key) ?? []
  for (let index = lowerBound(group, start); index < group.length && group[index].start <= end; index += 1)
    if (group[index].end <= end) return group[index]
  return undefined
}

const byArrival = (left, right) => left.start - right.start || left.order - right.order
const byOrder = (left, right) => left.order - right.order
const bySpanDescending = (left, right) => right.end - right.start - (left.end - left.start) || left.order - right.order
const useBounds = (use) => [position(use.line, use.column), position(use.endLine, use.endColumn)]
const applicationBounds = (application) => [
  position(application.startLine, application.startColumn),
  position(application.endLine, application.endColumn),
]

const normalizedConsumer = (record, productionFiles, label) => {
  if (!record || typeof record.consumerPath !== 'string') throw new Error(`FCS scanner emitted an invalid ${label}`)
  const consumerPath = repositoryPath(record.consumerPath, `${label} consumer`)
  if (!productionFiles.has(consumerPath)) throw new Error(`${consumerPath}: FCS ${label} is outside the production compile set`)
  return consumerPath
}

const integer = (record, field, label) => {
  if (!Number.isInteger(record[field])) throw new Error(`FCS scanner emitted ${label} without ${field}`)
  return record[field]
}

const normalizeControlFlow = (parsed, productionFiles, applicationUses, applicationRanges = []) => {
  const field = (prefix, suffix) => prefix ? `${prefix}${suffix}` : `${suffix[0].toLowerCase()}${suffix.slice(1)}`
  const range = (record, prefix, label) => ({
    startLine: integer(record, field(prefix, 'StartLine'), label),
    startColumn: integer(record, field(prefix, 'StartColumn'), label),
    endLine: integer(record, field(prefix, 'EndLine'), label),
    endColumn: integer(record, field(prefix, 'EndColumn'), label),
  })
  const applicationsByConsumer = rangeGroups(applicationUses, (application) => application.consumerPath, applicationBounds)
  const applicationsByTarget = rangeGroups(
    applicationUses,
    (application) => `${application.consumerPath}\0${application.resolvedTarget}`,
    applicationBounds,
  )
  const rangesByConsumer = new Map()
  for (const application of applicationRanges) {
    const group = rangesByConsumer.get(application.consumerPath)
    if (group) group.push(application)
    else rangesByConsumer.set(application.consumerPath, [application])
  }

  const matchExpressions = (parsed.matchExpressions ?? []).map((record) => {
    const consumerPath = normalizedConsumer(record, productionFiles, 'match expression')
    const matchRange = range(record, '', 'match expression')
    const scrutineeRange = range(record, 'scrutinee', 'match expression')
    const scrutineeApplication = enclosedBy(
      applicationsByConsumer,
      consumerPath,
      position(scrutineeRange.startLine, scrutineeRange.startColumn),
      position(scrutineeRange.endLine, scrutineeRange.endColumn),
    ).sort(bySpanDescending)[0]?.value
    if (!Array.isArray(record.clauses)) throw new Error('FCS scanner emitted a match expression without clauses')
    return {
      consumerPath,
      ...matchRange,
      scrutinee: {
        ...scrutineeRange,
        resolvedTarget: scrutineeApplication?.resolvedTarget ?? '',
        declarationPaths: scrutineeApplication?.declarationPaths ?? [],
        providerPaths: scrutineeApplication?.providerPaths ?? [],
      },
      clauses: record.clauses.map((clause) => ({
        patternKind: typeof clause.patternKind === 'string' ? clause.patternKind : 'Other',
        ...range(clause, '', 'match clause'),
      })),
    }
  })
  const bindExpressions = (parsed.bindExpressions ?? []).map((record) => ({
    consumerPath: normalizedConsumer(record, productionFiles, 'bind expression'),
    builderKind: typeof record.builderKind === 'string' ? record.builderKind : 'Unknown',
    binding: range(record, 'binding', 'bind expression'),
    body: range(record, 'body', 'bind expression'),
  }))
  const lambdaExpressions = (parsed.lambdaExpressions ?? []).map((record) => {
    const consumerPath = normalizedConsumer(record, productionFiles, 'lambda expression')
    const lambdaRange = range(record, '', 'lambda expression')
    const lambdaStart = position(lambdaRange.startLine, lambdaRange.startColumn)
    const lambdaEnd = position(lambdaRange.endLine, lambdaRange.endColumn)
    return {
      consumerPath,
      ...lambdaRange,
      body: range(record, 'body', 'lambda expression'),
      invokedBy: (rangesByConsumer.get(consumerPath) ?? []).filter((application) =>
        position(application.targetStartLine, application.targetStartColumn) <= lambdaStart
        && lambdaEnd <= position(application.targetEndLine, application.targetEndColumn))
        .map((application) => ({
          startLine: application.startLine,
          startColumn: application.startColumn,
          endLine: application.endLine,
          endColumn: application.endColumn,
        })),
    }
  })
  const conditionalExpressions = (parsed.conditionalExpressions ?? []).map((record) => {
    if (!Array.isArray(record.branches)) throw new Error('FCS scanner emitted a conditional expression without branches')
    return {
      consumerPath: normalizedConsumer(record, productionFiles, 'conditional expression'),
      condition: range(record, 'condition', 'conditional expression'),
      branches: record.branches.map((branch) => ({
        kind: typeof branch.kind === 'string' ? branch.kind : 'Branch',
        ...range(branch, '', 'conditional branch'),
      })),
    }
  })
  const tryExpressions = (parsed.tryExpressions ?? []).map((record) => {
    if (!Array.isArray(record.continuations)) throw new Error('FCS scanner emitted a try expression without continuations')
    return {
      consumerPath: normalizedConsumer(record, productionFiles, 'try expression'),
      kind: typeof record.kind === 'string' ? record.kind : 'Unknown',
      body: range(record, 'body', 'try expression'),
      continuations: record.continuations.map((continuation) => ({
        kind: typeof continuation.kind === 'string' ? continuation.kind : 'Continuation',
        ...range(continuation, '', 'try continuation'),
      })),
    }
  })
  const loopExpressions = (parsed.loopExpressions ?? []).map((record) => ({
    consumerPath: normalizedConsumer(record, productionFiles, 'loop expression'),
    kind: typeof record.kind === 'string' ? record.kind : 'Unknown',
    body: range(record, 'body', 'loop expression'),
  }))
  const functionDefinitions = (parsed.functionDefinitions ?? []).map((record) => ({
    consumerPath: normalizedConsumer(record, productionFiles, 'function definition'),
    name: typeof record.name === 'string' ? record.name : '',
    fullSymbol: typeof record.symbol === 'string' ? record.symbol : '',
    startLine: integer(record, 'line', 'function definition'),
    startColumn: integer(record, 'column', 'function definition'),
    endLine: integer(record, 'endLine', 'function definition'),
    endColumn: integer(record, 'endColumn', 'function definition'),
  }))
  const definitionsByName = rangeGroups(
    functionDefinitions,
    (definition) => `${definition.consumerPath}\0${definition.name}`,
    applicationBounds,
  )
  const localFunctionBindings = (parsed.localFunctionBindings ?? []).map((record) => {
    const consumerPath = normalizedConsumer(record, productionFiles, 'local function binding')
    const bindingRange = range(record, '', 'local function binding')
    const scope = range(record, 'scope', 'local function binding')
    const definition = typeof record.name !== 'string'
      ? undefined
      : enclosedBy(
        definitionsByName,
        `${consumerPath}\0${record.name}`,
        position(bindingRange.startLine, bindingRange.startColumn),
        position(bindingRange.endLine, bindingRange.endColumn),
      ).sort(byOrder)[0]?.value
    return {
      consumerPath,
      name: typeof record.name === 'string' ? record.name : '',
      fullSymbol: definition?.fullSymbol ?? '',
      ...bindingRange,
      body: range(record, 'body', 'local function binding'),
      scope,
      invokedBy: definition
        ? enclosedBy(
          applicationsByTarget,
          `${consumerPath}\0${definition.fullSymbol}`,
          position(scope.startLine, scope.startColumn),
          position(scope.endLine, scope.endColumn),
        ).sort(byOrder).map((entry) => ({
          startLine: entry.value.startLine,
          startColumn: entry.value.startColumn,
          endLine: entry.value.endLine,
          endColumn: entry.value.endColumn,
        }))
        : [],
    }
  }).filter((binding) => binding.fullSymbol !== '')
  return {
    matchExpressions,
    bindExpressions,
    lambdaExpressions,
    conditionalExpressions,
    tryExpressions,
    loopExpressions,
    localFunctionBindings,
  }
}

function resolvedApplicationUses(symbolUses, applicationRanges) {
  const textByFile = new Map()
  const offsetsByFile = new Map()
  const source = (file) => {
    if (!textByFile.has(file)) {
      const text = readFileSync(join(ROOT, file), 'utf8')
      textByFile.set(file, text)
      const offsets = [0]
      for (let index = 0; index < text.length; index += 1) if (text[index] === '\n') offsets.push(index + 1)
      offsetsByFile.set(file, offsets)
    }
    return [textByFile.get(file), offsetsByFile.get(file)]
  }

  const rangeByOwnRange = new Map()
  for (const candidate of applicationRanges) {
    const key = `${candidate.consumerPath}\0${candidate.startLine}\0${candidate.startColumn}\0${candidate.endLine}\0${candidate.endColumn}`
    if (!rangeByOwnRange.has(key)) rangeByOwnRange.set(key, candidate)
  }
  const semanticTargetOf = (application) => {
    let semantic = application
    const seen = new Set()
    while (!seen.has(semantic)) {
      seen.add(semantic)
      const nested = rangeByOwnRange.get(
        `${semantic.consumerPath}\0${semantic.targetStartLine}\0${semantic.targetStartColumn}\0${semantic.targetEndLine}\0${semantic.targetEndColumn}`,
      )
      if (!nested || seen.has(nested)) break
      semantic = nested
    }
    return semantic
  }

  const addressable = (candidate) =>
    !candidate.isFromType && !candidate.isFromPattern && !candidate.isFromOpenStatement && !candidate.isNamespace && !candidate.isModule
  const consumerOf = (use) => use.consumerPath
  const callableByConsumer = rangeGroups(
    symbolUses.filter((candidate) =>
      addressable(candidate) && (candidate.inferredType.includes('->') || candidate.symbolKind === 'FSharpUnionCase')),
    consumerOf,
    useBounds,
  )
  const addressableByConsumer = rangeGroups(symbolUses.filter(addressable), consumerOf, useBounds)

  const resolved = applicationRanges.flatMap((parsed) => {
    const semanticTarget = semanticTargetOf(parsed)
    const targetStart = position(semanticTarget.targetStartLine, semanticTarget.targetStartColumn)
    const targetEnd = position(semanticTarget.targetEndLine, semanticTarget.targetEndColumn)
    const use = firstEnclosedBy(callableByConsumer, parsed.consumerPath, targetStart, targetEnd)?.value
    if (!use) return []
    const syntacticTargetStart = position(parsed.targetStartLine, parsed.targetStartColumn)
    const syntacticTargetEnd = position(parsed.targetEndLine, parsed.targetEndColumn)
    const applicationStart = position(parsed.startLine, parsed.startColumn)
    const applicationEnd = position(parsed.endLine, parsed.endColumn)

    const [text, offsets] = source(parsed.consumerPath)
    const targetOffset = (offsets[semanticTarget.targetStartLine - 1] ?? 0) + semanticTarget.targetStartColumn
    const useEndOffset = (offsets[use.endLine - 1] ?? 0) + use.endColumn
    const targetText = text.slice(targetOffset, useEndOffset)
    const sourceAnchor = /(?:[A-Za-z_][A-Za-z0-9_']*\s*\.\s*)*[A-Za-z_][A-Za-z0-9_']*\s*$/.exec(targetText)?.[0]
      ?.replace(/\s+/g, '') ?? targetText
    const orderedArgumentUses = []
    const distinctArgument = new Set()
    for (const entry of enclosedBy(addressableByConsumer, parsed.consumerPath, applicationStart, applicationEnd)
      .filter((entry) => !(syntacticTargetStart <= entry.start && entry.end <= syntacticTargetEnd))
      .sort(byArrival)) {
      const candidate = entry.value
      const identity = `${candidate.symbol}\0${candidate.line}\0${candidate.column}\0${candidate.endLine}\0${candidate.endColumn}`
      if (distinctArgument.has(identity)) continue
      distinctArgument.add(identity)
      orderedArgumentUses.push(candidate)
    }
    const identifierOf = (candidate) => candidate.symbol.split('.').at(-1)
    const typeByIdentifier = new Map()
    for (const candidate of orderedArgumentUses) {
      const identifier = identifierOf(candidate)
      if (!typeByIdentifier.has(identifier)) typeByIdentifier.set(identifier, candidate.inferredType)
    }
    const argumentIdentifiers = orderedArgumentUses.map(identifierOf).filter(Boolean)
    const argumentTypes = Object.fromEntries(
      argumentIdentifiers.map((name) => [name, typeByIdentifier.get(name) ?? '']),
    )
    return [{
      consumerPath: parsed.consumerPath,
      sourceAnchor,
      resolvedTarget: use.symbol,
      declarationPaths: use.declarationPaths,
      providerPaths: use.providerPaths,
      startLine: parsed.startLine,
      startColumn: parsed.startColumn,
      endLine: parsed.endLine,
      endColumn: parsed.endColumn,
      isApplication: true,
      argumentIdentifiers,
      argumentTypes,
      inferredType: use.inferredType,
      targetStartLine: parsed.targetStartLine,
      targetStartColumn: parsed.targetStartColumn,
      targetEndLine: parsed.targetEndLine,
      targetEndColumn: parsed.targetEndColumn,
    }]
  })

  const resolvedByTarget = rangeGroups(
    resolved,
    (application) => `${application.consumerPath}\0${application.resolvedTarget}`,
    applicationBounds,
  )
  const innerOfCurried = new Set()
  const mergedArguments = new Map()
  const merge = (application) => {
    if (mergedArguments.has(application)) return mergedArguments.get(application)
    const inner = enclosedBy(
      resolvedByTarget,
      `${application.consumerPath}\0${application.resolvedTarget}`,
      position(application.targetStartLine, application.targetStartColumn),
      position(application.targetEndLine, application.targetEndColumn),
    ).filter((entry) =>
      entry.value.startLine !== application.startLine || entry.value.startColumn !== application.startColumn
      || entry.value.endLine !== application.endLine || entry.value.endColumn !== application.endColumn)
      .sort(bySpanDescending)[0]?.value
    if (inner) innerOfCurried.add(inner)
    const inherited = inner ? merge(inner) : { identifiers: [], types: {} }
    const value = {
      identifiers: [...inherited.identifiers, ...application.argumentIdentifiers],
      types: Object.assign({}, inherited.types, application.argumentTypes),
    }
    mergedArguments.set(application, value)
    return value
  }
  for (const application of resolved) merge(application)
  return resolved.filter((application) => !innerOfCurried.has(application)).map((application) => {
    const merged = merge(application)
    return { ...application, argumentIdentifiers: merged.identifiers, argumentTypes: merged.types }
  })
}

export function scanProjectSymbolUses({
  projectFile = FSPROJ,
  productionRoot = PRODUCTION_ROOT,
  scratchRoot = FCS_SCRATCH,
  resultPath = FCS_RESULT,
  fableLibrary = FABLE_LIBRARY,
  applicationConsumerPaths,
} = {}) {
  const project = resolve(projectFile)
  const production = resolve(productionRoot)
  const scratch = resolve(scratchRoot)
  const result = resolve(resultPath)
  const library = resolve(fableLibrary)
  const expectedPaths = readCompilePaths(project, production)
  const applicationConsumers = applicationConsumerPaths === undefined
    ? undefined
    : [...new Set(applicationConsumerPaths.map((path) => resolve(path)))]
  if (applicationConsumers?.some((path) => !expectedPaths.includes(repositoryPath(path, 'application consumer')))) {
    throw new Error('application consumer filter contains a file outside the production compile set')
  }

  let parsed
  const defaultProductionScan = project === resolve(FSPROJ) && production === resolve(PRODUCTION_ROOT)
  const reusePath = process.env[FCS_REUSE_PATH_ENV]
  const reuseRunId = process.env[FCS_REUSE_RUN_ID_ENV]
  if (defaultProductionScan && (reusePath !== undefined || reuseRunId !== undefined)) {
    if (!reusePath || !reuseRunId) throw new Error('FCS evidence reuse requires both absolute path and run ID')
    if (!isAbsolute(reusePath)) throw new Error('FCS evidence reuse path must be absolute')
    if (!existsSync(reusePath)) throw new Error(`FCS evidence reuse file is missing: ${reusePath}`)
    try {
      parsed = JSON.parse(readFileSync(reusePath, 'utf8'))
    } catch (error) {
      throw new Error(`FCS reused evidence is invalid JSON: ${error.message}`)
    }
    if (parsed?.schemaVersion !== FCS_NORMALIZED_SCHEMA_VERSION)
      throw new Error('FCS normalized evidence schema version does not match')
    if (typeof parsed.runId !== 'string' || parsed.runId !== reuseRunId)
      throw new Error('FCS evidence reuse run ID does not match')
    const normalizedArrays = [
      'symbolUses',
      'applicationUses',
      'matchExpressions',
      'bindExpressions',
      'lambdaExpressions',
      'conditionalExpressions',
      'tryExpressions',
      'loopExpressions',
      'localFunctionBindings',
    ]
    if (!Array.isArray(parsed.productionFiles) || normalizedArrays.some((key) => !Array.isArray(parsed[key])))
      throw new Error('FCS normalized evidence has an invalid shape')
    const productionFiles = parsed.productionFiles.map((path) => {
      if (typeof path !== 'string') throw new Error('FCS normalized evidence has an invalid production path')
      return norm(path)
    }).sort()
    comparePathSets([...expectedPaths].sort(), productionFiles, 'FCS production file set')
    const applicationConsumerSet = applicationConsumers
      ? new Set(applicationConsumers.map((path) => repositoryPath(path, 'application consumer')))
      : null
    const filtered = (records) => records.filter((record) => {
      if (!record || typeof record.consumerPath !== 'string') throw new Error('FCS normalized evidence record has no consumerPath')
      return !applicationConsumerSet || applicationConsumerSet.has(norm(record.consumerPath))
    })
    return {
      projectAssembly: typeof parsed.projectAssembly === 'string' ? parsed.projectAssembly : '',
      productionFiles,
      symbolUses: filtered(parsed.symbolUses),
      applicationUses: filtered(parsed.applicationUses),
      matchExpressions: filtered(parsed.matchExpressions),
      bindExpressions: filtered(parsed.bindExpressions),
      lambdaExpressions: filtered(parsed.lambdaExpressions),
      conditionalExpressions: filtered(parsed.conditionalExpressions),
      tryExpressions: filtered(parsed.tryExpressions),
      loopExpressions: filtered(parsed.loopExpressions),
      localFunctionBindings: filtered(parsed.localFunctionBindings),
    }
  } else {
    if (!existsSync(SYMBOL_SCANNER)) throw new Error(`missing FCS scanner: ${repositoryPath(SYMBOL_SCANNER, 'scanner')}`)
    if (!existsSync(library)) throw new Error(`missing Fable library: ${repositoryPath(library, 'Fable library')}`)
    mkdirSync(scratch, { recursive: true })
    mkdirSync(dirname(result), { recursive: true })
    rmSync(result, { force: true })

    const scan = spawnSync(
      'dotnet',
      ['fsi', '--exec', SYMBOL_SCANNER, project, production, scratch, fableToolDirectory(), library, result],
      {
        cwd: ROOT,
        encoding: 'utf8',
        maxBuffer: 64 * 1024 * 1024,
        env: {
          ...process.env,
          ...(applicationConsumers ? { OMP_FCS_APPLICATION_CONSUMERS: applicationConsumers.join('\n') } : {}),
        },
      },
    )
    if (scan.error) throw scan.error
    if (scan.status !== 0) {
      const output = [scan.stdout, scan.stderr].filter(Boolean).join('\n').trim()
      throw new Error(`FCS owner dependency scan failed with exit ${scan.status ?? 'unknown'}${output ? `\n${output}` : ''}`)
    }
    if (!existsSync(result)) throw new Error('FCS owner dependency scan succeeded without producing its result')
    try {
      parsed = JSON.parse(readFileSync(result, 'utf8'))
    } catch (error) {
      throw new Error(`FCS owner dependency result is invalid JSON: ${error.message}`)
    }
  }
  if (!parsed || typeof parsed !== 'object' || !Array.isArray(parsed.productionFiles) || !Array.isArray(parsed.symbolUses))
    throw new Error('FCS owner dependency result has an invalid shape')

  const productionFiles = parsed.productionFiles.map((path) => repositoryPath(path, 'FCS production source')).sort()
  comparePathSets([...expectedPaths].sort(), productionFiles, 'FCS production file set')
  const productionSet = new Set(productionFiles)
  const symbolUses = parsed.symbolUses.map((record) => normalizeSymbolUse(record, productionSet))
  const applicationConsumerSet = applicationConsumers
    ? new Set(applicationConsumers.map((path) => repositoryPath(path, 'application consumer')))
    : null
  const forRequestedConsumers = (record) => !applicationConsumerSet
    || applicationConsumerSet.has(repositoryPath(record.consumerPath, 'application consumer'))
  const applicationCandidates = (parsed.applicationCandidates ?? parsed.symbolUses).filter(forRequestedConsumers)
    .map((record) => normalizeSymbolUse(record, productionSet))
  const applicationRanges = (parsed.applicationRanges ?? []).filter(forRequestedConsumers)
    .map((record) => normalizeApplicationRange(record, productionSet))
  const applicationUses = resolvedApplicationUses(applicationCandidates, applicationRanges)
  const filteredControlFlow = Object.fromEntries(Object.entries(parsed).map(([key, value]) => [
    key,
    ['matchExpressions', 'bindExpressions', 'lambdaExpressions', 'conditionalExpressions', 'tryExpressions', 'loopExpressions', 'functionDefinitions', 'localFunctionBindings'].includes(key)
      && Array.isArray(value) ? value.filter(forRequestedConsumers) : value,
  ]))
  const normalizedEvidence = {
    projectAssembly: typeof parsed.projectAssembly === 'string' ? parsed.projectAssembly : '',
    productionFiles,
    symbolUses,
    applicationUses,
    ...normalizeControlFlow(filteredControlFlow, productionSet, applicationUses, applicationRanges),
  }
  const normalizedOutput = process.env[FCS_NORMALIZED_OUTPUT_ENV]
  if (defaultProductionScan && normalizedOutput !== undefined) {
    if (!normalizedOutput || !isAbsolute(normalizedOutput)) throw new Error('FCS normalized evidence output path must be absolute')
    const runId = process.env.OMP_FCS_EVIDENCE_RUN_ID
    if (!runId || parsed.runId !== runId) throw new Error('FCS normalized evidence producer run ID does not match')
    const artifact = {
      schemaVersion: FCS_NORMALIZED_SCHEMA_VERSION,
      runId,
      ...normalizedEvidence,
    }
    mkdirSync(dirname(normalizedOutput), { recursive: true })
    const temporary = `${normalizedOutput}.${process.pid}.tmp`
    try {
      writeFileSync(temporary, JSON.stringify(artifact))
      renameSync(temporary, normalizedOutput)
    } finally {
      rmSync(temporary, { force: true })
    }
  }
  return normalizedEvidence
}

function stronglyConnectedComponents(nodes, edges) {
  const adjacency = new Map(nodes.map((node) => [node, []]))
  for (const { consumer, provider } of edges) adjacency.get(consumer)?.push(provider)
  const indexByNode = new Map()
  const lowLink = new Map()
  const stack = []
  const onStack = new Set()
  const components = []
  let nextIndex = 0

  const visit = (node) => {
    indexByNode.set(node, nextIndex)
    lowLink.set(node, nextIndex++)
    stack.push(node)
    onStack.add(node)

    for (const target of adjacency.get(node) ?? []) {
      if (!indexByNode.has(target)) {
        visit(target)
        lowLink.set(node, Math.min(lowLink.get(node), lowLink.get(target)))
      } else if (onStack.has(target)) lowLink.set(node, Math.min(lowLink.get(node), indexByNode.get(target)))
    }

    if (lowLink.get(node) !== indexByNode.get(node)) return
    const component = []
    let member
    do {
      member = stack.pop()
      onStack.delete(member)
      component.push(member)
    } while (member !== node)
    components.push(component.sort())
  }

  for (const node of nodes) if (!indexByNode.has(node)) visit(node)
  return components.filter((component) => component.length > 1)
}

function authorizationOf(value, label, fail) {
  const symbols = value?.symbols ?? []
  const symbolRoots = value?.symbol_roots ?? []
  if (!Array.isArray(symbols) || !Array.isArray(symbolRoots)) {
    fail('invalid-symbol-authorization', `${label}: symbols and symbol_roots must be arrays`)
    return null
  }
  const invalid = [...symbols, ...symbolRoots].filter(
    (symbol) => typeof symbol !== 'string' || symbol.trim().length === 0 || PATH_GLOB.test(symbol),
  )
  if (invalid.length > 0 || symbols.length + symbolRoots.length === 0) {
    fail('invalid-symbol-authorization', `${label}: declare at least one exact symbol or symbol root without globs`)
    return null
  }
  const normalizedSymbols = symbols.map((symbol) => symbol.trim())
  const normalizedRoots = symbolRoots.map((symbol) => symbol.trim().replace(/\.$/, ''))
  if (new Set(normalizedSymbols).size !== normalizedSymbols.length || new Set(normalizedRoots).size !== normalizedRoots.length) {
    fail('duplicate-symbol-authorization', `${label}: duplicate symbol authorization`)
    return null
  }
  return { symbols: normalizedSymbols, symbolRoots: normalizedRoots }
}

const authorizes = (authorization, symbol) =>
  authorization.symbols.includes(symbol) ||
  authorization.symbolRoots.some((root) => symbol === root || symbol.startsWith(`${root}.`))

const useKind = (use) =>
  use.isFromPattern ? 'pattern' : use.isFromType ? 'type' : use.isFromUse ? 'use' : 'symbol'

const isExecutionPosition = (edge) =>
  !(edge.symbolKind === 'FSharpUnionCase' && /(?:Rejection|Error)\.[^.]*Cursor/.test(edge.symbol)) &&
  (EXECUTION_POSITION.test(edge.providerPath) ||
    (edge.symbolKind !== 'FSharpField' && EXECUTION_POSITION.test(edge.symbol)))

const semanticEvidenceMetadata = (entry, fail) => {
  const lawMatch = /^WHAT\[([A-Z0-9]+(?:-[A-Z0-9]+)*)\]$/.exec(entry?.law ?? '')
  const proof = norm(entry?.proof ?? '')
  const proofMatch = /^requirements\/([^/]+)\/tests\/.+\.test\.mjs$/.exec(proof)
  const proofPath = resolve(ROOT, proof)
  const proofExists =
    proofMatch &&
    proof === entry.proof &&
    !isAbsolute(entry.proof) &&
    existsSync(proofPath) &&
    statSync(proofPath).isFile()
  const whatPath = proofMatch ? join(ROOT, 'requirements', proofMatch[1], 'WHAT.md') : ''
  const lawId = lawMatch?.[1]
  const normative =
    proofExists &&
    lawId &&
    existsSync(whatPath) &&
    new RegExp(`^##\\s+${lawId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}:`, 'm').test(readFileSync(whatPath, 'utf8')) &&
    readFileSync(proofPath, 'utf8').includes(`WHAT[${lawId}]`)
  if (!normative) {
    fail(
      'invalid-semantic-evidence-metadata',
      `${entry?.path ?? ''}: semantic-evidence needs an exact normative WHAT law and existing proof that cites it`,
      { path: entry?.path },
    )
    return false
  }
  return true
}

export function analyzeOwnerDependencies({
  compilePaths,
  semanticOwners,
  publishedContracts,
  symbolUses,
  migrationState,
}) {
  const violations = []
  const fail = (code, message, details = {}) => violations.push({ code, message, ...details })
  const compiled = compilePaths.map(norm)
  const compiledSet = new Set(compiled)
  if (compiledSet.size !== compiled.length) fail('duplicate-compile-entry', 'production compile set contains duplicate paths')
  if (!Array.isArray(symbolUses)) fail('missing-compiler-symbol-uses', 'owner dependency analysis requires FCS symbol-use evidence')

  const ownerClaims = new Map()
  for (const entry of semanticOwners?.ownership ?? []) {
    const path = norm(entry.path)
    const claims = ownerClaims.get(path) ?? []
    claims.push(entry.owner)
    ownerClaims.set(path, claims)
  }
  for (const path of compiled) {
    const claims = ownerClaims.get(path) ?? []
    if (claims.length === 0) fail('unowned-production-module', `${path}: production module has no primary owner`, { path })
    if (claims.length > 1)
      fail('duplicate-primary-owner', `${path}: primary owner declared ${claims.length} times (${claims.join(', ')})`, { path })
  }
  for (const [path, claims] of ownerClaims) {
    if (!compiledSet.has(path)) fail('stale-owner-entry', `${path}: semantic owner entry is outside the compile set`, { path })
    if (claims.length > 1 && !compiledSet.has(path))
      fail('duplicate-primary-owner', `${path}: primary owner declared ${claims.length} times (${claims.join(', ')})`, { path })
  }

  const ownerOf = new Map([...ownerClaims].filter(([, claims]) => claims.length === 1).map(([path, claims]) => [path, claims[0]]))
  const declaredOwners = new Set(ownerOf.values())
  const sourceEdgeMap = new Map()
  for (const use of Array.isArray(symbolUses) ? symbolUses : []) {
    const consumerPath = norm(use.consumerPath ?? '')
    if (!compiledSet.has(consumerPath)) {
      fail('invalid-symbol-consumer', `${consumerPath || '<missing>'}: FCS symbol consumer is outside the compile set`, { consumerPath })
      continue
    }
    if (use.isFromOpenStatement || use.isNamespace || use.isModule) continue
    if (use.missingDeclaration) {
      fail(
        'missing-symbol-declaration',
        `${consumerPath}:${use.line ?? 0}:${use.column ?? 0}: project symbol '${use.symbol ?? ''}' has no declaration location`,
        { consumerPath, symbol: use.symbol },
      )
      continue
    }
    const providers = [...new Set((use.providerPaths ?? []).map(norm))]
    const invalidProviders = providers.filter((path) => !compiledSet.has(path))
    if (invalidProviders.length > 0) {
      fail(
        'invalid-symbol-provider',
        `${consumerPath}: symbol '${use.symbol ?? ''}' resolves outside the production compile set (${invalidProviders.join(', ')})`,
        { consumerPath, providerPaths: invalidProviders },
      )
      continue
    }
    if (providers.length > 1) {
      fail(
        'ambiguous-symbol-declaration',
        `${consumerPath}: symbol '${use.symbol ?? ''}' resolves to multiple production files (${providers.join(', ')})`,
        { consumerPath, providerPaths: providers, symbol: use.symbol },
      )
      continue
    }
    if (providers.length === 0 || providers[0] === consumerPath) continue
    const providerPath = providers[0]
    const consumerOwner = ownerOf.get(consumerPath)
    const providerOwner = ownerOf.get(providerPath)
    if (!consumerOwner || !providerOwner || consumerOwner === providerOwner) continue
    const edge = {
      consumerPath,
      providerPath,
      consumerOwner,
      providerOwner,
      symbol: use.symbol ?? '',
      symbolKind: use.symbolKind ?? 'Unknown',
      line: use.line ?? 0,
      column: use.column ?? 0,
      useKind: useKind(use),
      isFromPattern: use.isFromPattern === true,
    }
    const key = `${edge.consumerPath}\0${edge.providerPath}\0${edge.symbol}\0${edge.line}\0${edge.column}\0${edge.useKind}`
    sourceEdgeMap.set(key, edge)
  }
  const sourceEdges = [...sourceEdgeMap.values()].sort((left, right) =>
    `${left.consumerPath}/${left.line}/${left.column}/${left.providerPath}/${left.symbol}`.localeCompare(
      `${right.consumerPath}/${right.line}/${right.column}/${right.providerPath}/${right.symbol}`,
    ),
  )

  const closedPaths = migrationState ? new Set(migrationState.closedPaths ?? []) : null
  const migrationNodeByPath = new Map(migrationState?.nodeByPath ?? [])
  const migrationNodes = new Map((migrationState?.nodes ?? []).map((node) => [node.id, node]))
  const registry = publishedContracts ?? {}
  const contractsByPath = new Map()
  const contractEntries = []
  const adapterEntries = []
  const rootEntries = []

  const validateOwnedPath = (entry, kind, publication) => {
    const path = norm(entry?.path ?? '')
    if (!path || PATH_GLOB.test(path) || !compiledSet.has(path)) {
      fail('invalid-contract-declaration', `${kind}: '${path}' must be one exact compiled path`, { path })
      return null
    }
    if (ownerOf.get(path) !== entry.owner) {
      fail('contract-owner-mismatch', `${path}: registry owner '${entry.owner}' does not match '${ownerOf.get(path) ?? 'unowned'}'`, {
        path,
      })
      return null
    }
    if (!meaningful(entry.justification)) {
      fail('missing-architectural-justification', `${path}: ${kind} needs a written architectural justification`, { path })
      return null
    }
    if (closedPaths) {
      if (!closedPaths.has(path)) {
        fail('contract-before-cutover', `${path}: ${kind} cannot be declared before its migration node is DONE`, { path })
        return null
      }
      const nodeId = migrationNodeByPath.get(path)
      const node = nodeId ? migrationNodes.get(nodeId) : null
      if (!entry.node || entry.node !== nodeId || !node || node.state !== 'DONE') {
        fail('contract-node-mismatch', `${path}: ${kind} must reference its exact DONE migration node '${nodeId ?? 'none'}'`, {
          path,
        })
        return null
      }
      const proofs = node.proofs
      const invalidProofs = Array.isArray(proofs)
        ? proofs.filter((proof) => {
            if (typeof proof !== 'string' || proof.length === 0 || proof !== norm(proof) || isAbsolute(proof)) return true
            if (!/^requirements\/[^/]+\/tests\/.+\.test\.mjs$/.test(proof)) return true
            const resolved = resolve(ROOT, proof)
            const repositoryRelative = norm(relative(ROOT, resolved))
            return (
              repositoryRelative === '..' ||
              repositoryRelative.startsWith('../') ||
              repositoryRelative !== proof ||
              !existsSync(resolved) ||
              !statSync(resolved).isFile()
            )
          })
        : []
      if (!Array.isArray(proofs) || proofs.length === 0 || invalidProofs.length > 0) {
        fail(
          'contract-without-proof',
          `${path}: migration node '${nodeId}' must have existing executable proofs under requirements/<package>/tests/*.test.mjs`,
          { path, invalidProofs },
        )
        return null
      }
      if (publication && (!entry.contract || !node.publishes?.includes(entry.contract))) {
        fail('contract-vocabulary-mismatch', `${path}: '${entry.contract ?? ''}' is not published by migration node '${nodeId}'`, {
          path,
        })
        return null
      }
    }
    return path
  }

  const contractKeys = new Set()
  for (const entry of registry.contracts ?? []) {
    if (!['published-contract', 'physical-port', 'semantic-evidence'].includes(entry?.kind)) {
      fail('invalid-contract-kind', `${entry?.path ?? ''}: illegal contract kind '${entry?.kind ?? ''}'`, { path: entry?.path })
      continue
    }
    const path = validateOwnedPath(entry, 'contract', true)
    const authorization = authorizationOf(entry, `contract ${entry?.path ?? ''}`, fail)
    const semanticEvidence = entry.kind !== 'semantic-evidence' || semanticEvidenceMetadata(entry, fail)
    if (
      entry.kind === 'semantic-evidence' &&
      authorization &&
      (authorization.symbols.length === 0 || authorization.symbolRoots.length > 0)
    ) {
      fail(
        'invalid-semantic-evidence-authorization',
        `${entry?.path ?? ''}: semantic-evidence must authorize exact symbols and forbids symbol roots`,
        { path: entry?.path },
      )
    }
    const consumers = [...new Set(entry?.consumers ?? [])]
    if (
      consumers.length === 0 ||
      consumers.some((owner) => typeof owner !== 'string' || owner === entry.owner || !declaredOwners.has(owner))
    ) {
      fail('invalid-contract-consumers', `${entry?.path ?? ''}: contract consumers must be exact, foreign, existing owners`, {
        path: entry?.path,
      })
      continue
    }
    if (
      !path ||
      !authorization ||
      !semanticEvidence ||
      (entry.kind === 'semantic-evidence' && (authorization.symbols.length === 0 || authorization.symbolRoots.length > 0))
    ) continue
    const key = `${path}\0${entry.kind}\0${[...consumers].sort().join('\0')}\0${authorization.symbols.join('\0')}\0${authorization.symbolRoots.join('\0')}`
    if (contractKeys.has(key)) {
      fail('duplicate-contract-declaration', `${path}: exact contract declaration is duplicated`, { path })
      continue
    }
    contractKeys.add(key)
    const normalized = { ...entry, path, consumers: new Set(consumers), authorization }
    contractEntries.push(normalized)
    const entries = contractsByPath.get(path) ?? []
    entries.push(normalized)
    contractsByPath.set(path, entries)
  }

  const validateTargets = (entry, field, kind, code) => {
    const values = entry?.[field]
    if (!Array.isArray(values) || values.length === 0) {
      fail(code, `${entry?.path ?? ''}: ${kind} must declare exact symbol-bearing targets`, { path: entry?.path })
      return null
    }
    const targets = []
    for (const value of values) {
      if (!value || typeof value !== 'object' || Array.isArray(value)) {
        fail(code, `${entry?.path ?? ''}: ${kind} targets must be objects, not bare paths`, { path: entry?.path })
        continue
      }
      const path = norm(value.path ?? '')
      const authorization = authorizationOf(value, `${kind} target ${path}`, fail)
      if (!path || PATH_GLOB.test(path) || !compiledSet.has(path) || !authorization) {
        fail(code, `${entry?.path ?? ''}: ${kind} target '${path}' must be one exact compiled path with exact symbols`, {
          path: entry?.path,
          targetPath: path,
        })
        continue
      }
      targets.push({ path, authorization })
    }
    return targets.length === values.length ? targets : null
  }

  for (const entry of registry.physical_adapters ?? []) {
    const path = validateOwnedPath(entry, 'physical adapter', false)
    const targets = validateTargets(entry, 'ports', 'physical adapter', 'invalid-physical-adapter')
    if (!path || !targets) continue
    const undeclaredTargets = targets.filter(
      (target) =>
        !(contractsByPath.get(target.path) ?? []).some(
          (contract) =>
            contract.kind === 'physical-port' &&
            contract.consumers.has(entry.owner) &&
            target.authorization.symbols.every((symbol) => authorizes(contract.authorization, symbol)) &&
            target.authorization.symbolRoots.every((root) =>
              contract.authorization.symbolRoots.some(
                (contractRoot) => root === contractRoot || root.startsWith(`${contractRoot}.`),
              ),
            ),
        ),
    )
    for (const target of undeclaredTargets)
      fail(
        'undeclared-physical-port',
        `${path} → ${target.path}: physical adapter target must be a declared physical port consumed by '${entry.owner}'`,
        { path, targetPath: target.path },
      )
    if (undeclaredTargets.length === 0) adapterEntries.push({ ...entry, path, targets })
  }
  for (const entry of registry.composition_roots ?? []) {
    const path = validateOwnedPath(entry, 'composition root', false)
    const targets = validateTargets(entry, 'wires', 'composition root', 'invalid-composition-root')
    if (path && targets) rootEntries.push({ ...entry, path, targets })
  }

  const targetAllows = (entries, consumerPath, providerPath, symbol) =>
    entries.some(
      (entry) =>
        entry.path === consumerPath &&
        entry.targets.some((target) => target.path === providerPath && authorizes(target.authorization, symbol)),
    )

  const pendingEdges = []
  const strictEdges = []
  const allowedEdges = []
  for (const edge of sourceEdges) {
    if (closedPaths && !closedPaths.has(edge.providerPath)) {
      pendingEdges.push(edge)
      continue
    }
    strictEdges.push(edge)
    const entries = contractsByPath.get(edge.providerPath) ?? []
    const symbolContracts = entries.filter((entry) => authorizes(entry.authorization, edge.symbol))
    const contractEdge = symbolContracts.some((entry) => entry.consumers.has(edge.consumerOwner))
    const semanticEvidenceEdge = symbolContracts.some(
      (entry) => entry.kind === 'semantic-evidence' && entry.consumers.has(edge.consumerOwner),
    )
    const physicalPortEdge = symbolContracts.some(
      (entry) => entry.kind === 'physical-port' && entry.consumers.has(edge.consumerOwner),
    )
    const adapterEdge = targetAllows(adapterEntries, edge.consumerPath, edge.providerPath, edge.symbol)
    const rootEdge = targetAllows(rootEntries, edge.consumerPath, edge.providerPath, edge.symbol)

    if (isExecutionPosition(edge) && !semanticEvidenceEdge && !physicalPortEdge && !adapterEdge) {
      fail(
        'foreign-execution-position',
        `${edge.consumerPath}:${edge.line}:${edge.column} → ${edge.providerPath}: foreign execution-position '${edge.symbol}' is forbidden`,
        edge,
      )
      continue
    }
    if (edge.isFromPattern && rootEdge && !contractEdge) {
      fail(
        'composition-root-foreign-policy',
        `${edge.consumerPath}:${edge.line}:${edge.column} → ${edge.providerPath}: composition root matches uncontracted foreign symbol '${edge.symbol}'`,
        edge,
      )
      continue
    }
    if (!contractEdge && !adapterEdge && !rootEdge) {
      const code =
        entries.length === 0
          ? PUBLICISH_PATH.test(edge.providerPath)
            ? 'undeclared-published-contract'
            : 'cross-owner-private-import'
          : symbolContracts.length === 0
            ? 'unauthorized-contract-symbol'
            : 'unauthorized-contract-consumer'
      fail(
        code,
        `${edge.consumerPath}:${edge.line}:${edge.column} → ${edge.providerPath}: ${edge.consumerOwner} may not consume ${edge.providerOwner} symbol '${edge.symbol}'`,
        edge,
      )
      continue
    }
    allowedEdges.push({
      ...edge,
      authorizationKind: adapterEdge
        ? 'physical-adapter'
        : rootEdge
          ? 'composition-root'
          : physicalPortEdge
            ? 'physical-port'
            : 'contract',
    })
  }

  const assertAuthorizationIsLive = (authorization, edges, label, details) => {
    for (const symbol of authorization.symbols)
      if (!edges.some((edge) => edge.symbol === symbol))
        fail('stale-symbol-authorization', `${label}: exact symbol '${symbol}' has no matching compiler-resolved edge`, details)
    for (const root of authorization.symbolRoots)
      if (!edges.some((edge) => edge.symbol === root || edge.symbol.startsWith(`${root}.`)))
        fail('stale-symbol-authorization', `${label}: symbol root '${root}' has no matching compiler-resolved edge`, details)
  }

  for (const entry of contractEntries) {
    const live = strictEdges.filter(
      (edge) => edge.providerPath === entry.path && authorizes(entry.authorization, edge.symbol),
    )
    assertAuthorizationIsLive(entry.authorization, live, entry.path, { path: entry.path })
    for (const consumer of entry.consumers)
      if (!live.some((edge) => edge.consumerOwner === consumer))
        fail('stale-contract-consumer', `${entry.path}: declared consumer '${consumer}' has no matching compiler-resolved edge`, {
          path: entry.path,
          consumer,
        })
  }
  for (const entry of adapterEntries)
    for (const target of entry.targets) {
      const live = strictEdges.filter(
        (edge) =>
          edge.consumerPath === entry.path && edge.providerPath === target.path && authorizes(target.authorization, edge.symbol),
      )
      assertAuthorizationIsLive(target.authorization, live, `${entry.path} → ${target.path}`, {
        path: entry.path,
        targetPath: target.path,
      })
    }
  for (const entry of rootEntries)
    for (const target of entry.targets) {
      const live = strictEdges.filter(
        (edge) =>
          edge.consumerPath === entry.path && edge.providerPath === target.path && authorizes(target.authorization, edge.symbol),
      )
      assertAuthorizationIsLive(target.authorization, live, `${entry.path} → ${target.path}`, {
        path: entry.path,
        targetPath: target.path,
      })
    }

  const projectOwnerEdges = (edges) => {
    const ownerEdgeMap = new Map()
    for (const edge of edges) {
      const key = `${edge.consumerOwner}\0${edge.providerOwner}`
      if (!ownerEdgeMap.has(key)) ownerEdgeMap.set(key, { consumer: edge.consumerOwner, provider: edge.providerOwner, uses: [] })
      ownerEdgeMap.get(key).uses.push({
        consumerPath: edge.consumerPath,
        providerPath: edge.providerPath,
        symbol: edge.symbol,
        line: edge.line,
        column: edge.column,
        useKind: edge.useKind,
      })
    }
    return [...ownerEdgeMap.values()].sort((left, right) =>
      `${left.consumer}/${left.provider}`.localeCompare(`${right.consumer}/${right.provider}`),
    )
  }

  const allSourceOwnerEdges = projectOwnerEdges(sourceEdges)
  const sourceOwnerEdges = projectOwnerEdges(strictEdges)

  const requirementOwnerEdges = []
  for (const edge of registry.requirement_dependencies ?? []) {
    if (!edge?.consumer || !edge?.provider || edge.consumer === edge.provider || !meaningful(edge.justification)) {
      fail(
        'invalid-requirement-dependency',
        `requirement dependency '${edge?.consumer ?? ''}' → '${edge?.provider ?? ''}' needs distinct packages and written justification`,
        { consumer: edge?.consumer, provider: edge?.provider },
      )
      continue
    }
    requirementOwnerEdges.push({ consumer: edge.consumer, provider: edge.provider, justification: edge.justification.trim() })
  }

  const cycleJustifications = new Map()
  for (const entry of registry.owner_cycle_justifications ?? []) {
    const owners = [...new Set(entry?.owners ?? [])].sort()
    const key = owners.join('\0')
    if (owners.length < 2 || !meaningful(entry?.justification)) {
      fail('invalid-cycle-justification', `owner cycle '${owners.join(' → ')}' needs exact members and written justification`, {
        owners,
      })
      continue
    }
    if (cycleJustifications.has(key)) {
      fail('duplicate-cycle-justification', `owner cycle justification is duplicated: ${owners.join(' → ')}`, { owners })
      continue
    }
    cycleJustifications.set(key, entry.justification.trim())
  }
  const semanticContractEdges = strictEdges.filter((edge) =>
    (contractsByPath.get(edge.providerPath) ?? []).some(
      (entry) =>
        ['published-contract', 'semantic-evidence'].includes(entry.kind) &&
        entry.consumers.has(edge.consumerOwner) &&
        authorizes(entry.authorization, edge.symbol),
    ),
  )
  const cycleOwnerEdges = projectOwnerEdges(semanticContractEdges)
  const cycleOwners = [...declaredOwners]
  const cycles = stronglyConnectedComponents(cycleOwners, cycleOwnerEdges)
  const liveCycleKeys = new Set()
  for (const owners of cycles) {
    const key = owners.join('\0')
    liveCycleKeys.add(key)
    if (!cycleJustifications.has(key))
      fail('unjustified-owner-cycle', `owner dependency cycle lacks exact justification: ${owners.join(' → ')}`, { owners })
  }
  for (const [key] of cycleJustifications)
    if (!liveCycleKeys.has(key))
      fail('stale-cycle-justification', `cycle justification has no matching live SCC: ${key.split('\0').join(' → ')}`, {
        owners: key.split('\0'),
      })

  violations.sort((left, right) => `${left.code}/${left.message}`.localeCompare(`${right.code}/${right.message}`))
  return {
    ok: violations.length === 0,
    violations,
    sourceEdges,
    pendingEdges,
    strictEdges,
    allowedEdges,
    semanticContractEdges,
    allSourceOwnerEdges,
    sourceOwnerEdges,
    cycleOwnerEdges,
    requirementOwnerEdges,
    cycles,
    contracts: contractEntries.length,
  }
}

function readMigrationState(semanticOwners) {
  if (!existsSync(MIGRATION_LEDGER)) return undefined
  const ledger = JSON.parse(readFileSync(MIGRATION_LEDGER, 'utf8'))
  const nodes = ledger.nodes ?? []
  const nodeByPath = []
  const closedPaths = []
  for (const node of nodes)
    for (const path of node.files ?? []) {
      nodeByPath.push([norm(path), node.id])
      if (node.state === 'DONE') closedPaths.push(norm(path))
    }
  const ownerByPath = new Map((semanticOwners?.ownership ?? []).map((entry) => [norm(entry.path), entry.owner]))
  const pendingOwners = new Set(
    Object.values(ledger.coverage_backlog ?? {})
      .flat()
      .map(norm)
      .map((path) => ownerByPath.get(path))
      .filter(Boolean),
  )
  const closedOwners = [...new Set(ownerByPath.values())].filter((owner) => !pendingOwners.has(owner)).sort()
  return { nodes, nodeByPath, closedPaths, closedOwners }
}

function readProductionInput() {
  const scan = scanProjectSymbolUses()
  const semanticOwners = JSON.parse(readFileSync(OWNERS, 'utf8'))
  return {
    compilePaths: scan.productionFiles,
    semanticOwners,
    publishedContracts: JSON.parse(readFileSync(CONTRACTS, 'utf8')),
    symbolUses: scan.symbolUses,
    migrationState: readMigrationState(semanticOwners),
  }
}

function runCli() {
  try {
    const result = analyzeOwnerDependencies(readProductionInput())
    if (process.argv.includes('--json')) console.log(JSON.stringify(result, null, 2))
    else if (result.ok)
      console.log(
        `owner-dependencies: OK — ${result.sourceEdges.length} FCS cross-owner uses, ${result.pendingEdges.length} pending-provider uses, ${result.strictEdges.length} strict uses, ${result.sourceOwnerEdges.length} owner edges, ${result.contracts} contracts, ${result.cycles.length} justified cycles`,
      )
    else {
      console.error(`owner-dependencies: ${result.violations.length} violation(s)`)
      for (const violation of result.violations) console.error(`  ${violation.code}: ${violation.message}`)
    }
    process.exitCode = result.ok ? 0 : 1
  } catch (error) {
    console.error(`owner-dependencies: ${error.message}`)
    process.exitCode = 1
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) runCli()
