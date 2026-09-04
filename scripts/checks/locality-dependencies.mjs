#!/usr/bin/env node

import { spawnSync } from 'node:child_process'
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs'
import { homedir, tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { analyzeLocalityDependencies } from '../lib/locality-dependencies.mjs'
import { materializeOwnerCompile, planImpactCompile } from '../lib/owner-compile.mjs'
import { parseProject, readOwnerProjectInventoryV1 } from './owner-projects.mjs'

export { readOwnerProjectInventoryV1 } from './owner-projects.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')
const SCANNER = join(ROOT, 'scripts/checks/locality-symbol-uses.fsx')
const FABLE_LIBRARY = join(ROOT, 'node_modules/@fable-org/fable-library-js')
const OWNER_PROJECT = /^Wanxiangshu\.Owner\..+\.fsproj$/


const normalizePath = (value) => value.replace(/\\/g, '/')
const repositoryPath = (value) => normalizePath(relative(ROOT, resolve(value)))
const semanticAnchor = (value) => value.startsWith('source:') ? `source:${repositoryPath(value.slice('source:'.length))}` : value
const exactKeys = (value, keys) => value !== null
  && typeof value === 'object'
  && !Array.isArray(value)
  && Object.keys(value).sort().join('\0') === [...keys].sort().join('\0')
const siteValid = (site) => exactKeys(site, ['sourcePath', 'semanticDeclarationAnchor', 'sameAnchorOccurrenceOrdinal'])
  && [site.sourcePath, site.semanticDeclarationAnchor].every((value) => typeof value === 'string' && value.length > 0)
  && Number.isSafeInteger(site.sameAnchorOccurrenceOrdinal)
  && site.sameAnchorOccurrenceOrdinal >= 0
const compilerObservationsValid = (value) => {
  const topLevelKeys = ['schemaVersion', 'projectFile', 'productionFiles', 'signatureFiles', 'declarationUses', 'externalSymbolUses', 'fsharpNodes', 'fableInterop', 'signatureExports', 'diagnostics', 'elapsedMilliseconds']
  const arrays = topLevelKeys.slice(2, -1)
  if (!exactKeys(value, topLevelKeys)
    || value.schemaVersion !== 1
    || typeof value.projectFile !== 'string'
    || !Number.isSafeInteger(value.elapsedMilliseconds)
    || value.elapsedMilliseconds < 0
    || arrays.some((key) => !Array.isArray(value[key]))) return false
  const declarationKeys = ['consumerPath', 'providerPaths', 'symbol', 'symbolKind', 'assembly', 'isNamespace', 'isModule', 'line', 'column', 'isFromOpenStatement', 'isFromPattern', 'isFromType', 'isFromUse']
  if (!value.declarationUses.every((row) => exactKeys(row, declarationKeys)
    && typeof row.consumerPath === 'string'
    && Array.isArray(row.providerPaths))) return false
  if (!value.externalSymbolUses.every((row) => exactKeys(row, ['assembly', 'fullyQualifiedSymbol', 'symbolKind', 'site']) && siteValid(row.site))) return false
  if (!value.fsharpNodes.every((row) => exactKeys(row, ['nodeKind', 'semanticIdentity', 'site']) && siteValid(row.site))) return false
  if (!value.signatureExports.every((row) => exactKeys(row, ['exportKind', 'declarationIdentity', 'site']) && siteValid(row.site))) return false
  if (!value.fableInterop.every((row) => {
    if (row?.kind === 'fable-import') return exactKeys(row, ['kind', 'moduleSpecifier', 'selector', 'site']) && siteValid(row.site)
    if (['fable-emit', 'emit-js-expr'].includes(row?.kind)) return exactKeys(row, ['kind', 'expression', 'site']) && siteValid(row.site)
    return false
  })) return false
  return value.diagnostics.every((row) => exactKeys(row, ['code', 'sourcePath', 'semanticDeclarationAnchor', 'syntaxKind', 'line', 'column', 'rawIdentity']))
}

function fableToolDirectory() {
  const manifest = JSON.parse(readFileSync(join(ROOT, '.config/dotnet-tools.json'), 'utf8'))
  const version = manifest?.tools?.fable?.version
  if (typeof version !== 'string' || version.length === 0) throw new Error('dotnet tool manifest has no pinned Fable version')
  const packageRoot = process.env.NUGET_PACKAGES ?? join(process.env.DOTNET_CLI_HOME ?? homedir(), '.nuget/packages')
  const directory = join(packageRoot, 'fable', version, 'tools', 'net10.0', 'any')
  for (const assembly of ['Fable.Compiler.dll', 'Fable.AST.dll', 'FSharp.Compiler.Service.dll'])
    if (!existsSync(join(directory, assembly))) throw new Error(`missing Fable ${version} compiler assembly: ${assembly}`)
  return directory
}

export function readLocalities(sourceRoot = SOURCE_ROOT) {
  const projects = readdirSync(sourceRoot)
    .filter((name) => OWNER_PROJECT.test(name))
    .map((name) => parseProject(join(sourceRoot, name)))
    .sort((left, right) => left.locality.localeCompare(right.locality))
  const localityByProject = new Map(projects.map((project) => [project.projectPath, project.locality]))
  return projects.map((project) => ({
    id: project.locality,
    owner: project.owner,
    sources: project.compile,
    references: project.references.map((path) => localityByProject.get(path) ?? repositoryPath(path)),
  }))
}

export function scanCompilerObservationsV1({ aggregate = AGGREGATE, productionRoot = SOURCE_ROOT } = {}) {
  const scratch = mkdtempSync(join(tmpdir(), 'wanxiangshu-locality-dependencies-'))
  const resultPath = join(scratch, 'compiler-observations-v1.json')
  try {
    const compilePlan = planImpactCompile({
      changedPaths: [aggregate],
      projectDirectory: dirname(aggregate),
      aggregatePath: aggregate,
    })
    const materialized = materializeOwnerCompile(compilePlan, { scratchRoot: join(scratch, 'compile') })
    const restore = spawnSync(
      'dotnet',
      ['restore', materialized.projectPath, '--nologo', '-p:NuGetAudit=false'],
      { cwd: ROOT, encoding: 'utf8', env: { ...process.env, NuGetAudit: 'false' }, maxBuffer: 64 * 1024 * 1024 },
    )
    if (restore.status !== 0)
      throw new Error(`locality analyzer restore failed (${restore.status ?? restore.signal})\n${restore.stdout}\n${restore.stderr}`)
    const scan = spawnSync(
      'dotnet',
      [
        'fsi',
        SCANNER,
        materialized.projectPath,
        productionRoot,
        join(scratch, 'obj'),
        fableToolDirectory(),
        FABLE_LIBRARY,
        materialized.assetsPath,
        resultPath,
      ],
      {
        cwd: ROOT,
        encoding: 'utf8',
        env: { ...process.env, NuGetAudit: 'false' },
        maxBuffer: 64 * 1024 * 1024,
      },
    )
    if (scan.status !== 0)
      throw new Error(`compiler-resolved locality scan failed (${scan.status ?? scan.signal})\n${scan.stdout}\n${scan.stderr}`)
    const parsed = JSON.parse(readFileSync(resultPath, 'utf8'))
    if (!compilerObservationsValid(parsed)) {
      throw new Error(`compiler observation scan returned an invalid CompilerObservationsV1 payload: ${Object.keys(parsed).join(',')}`)
    }
    const mapSite = (site) => ({
      ...site,
      sourcePath: repositoryPath(site.sourcePath),
      semanticDeclarationAnchor: semanticAnchor(site.semanticDeclarationAnchor),
    })
    return {
      schemaVersion: 1,
      projectFile: repositoryPath(aggregate),
      elapsedMilliseconds: parsed.elapsedMilliseconds,
      productionFiles: parsed.productionFiles.map(repositoryPath),
      signatureFiles: parsed.signatureFiles.map(repositoryPath),
      declarationUses: parsed.declarationUses.map((entry) => ({
        ...entry,
        consumerPath: repositoryPath(entry.consumerPath),
        providerPaths: entry.providerPaths.map(repositoryPath),
      })),
      externalSymbolUses: parsed.externalSymbolUses.map((entry) => ({ ...entry, site: mapSite(entry.site) })),
      fsharpNodes: parsed.fsharpNodes.map((entry) => ({ ...entry, site: mapSite(entry.site) })),
      fableInterop: parsed.fableInterop.map((entry) => ({ ...entry, site: mapSite(entry.site) })),
      signatureExports: parsed.signatureExports.map((entry) => ({ ...entry, site: mapSite(entry.site) })),
      diagnostics: parsed.diagnostics.map((entry) => ({
        ...entry,
        sourcePath: entry.sourcePath === null ? null : repositoryPath(entry.sourcePath),
        semanticDeclarationAnchor: entry.semanticDeclarationAnchor === null
          ? null
          : semanticAnchor(entry.semanticDeclarationAnchor),
      })),
    }
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
}

export function runLocalityDependencyScan(options = {}) {
  const inventory = readOwnerProjectInventoryV1({
    sourceRoot: options.productionRoot ?? SOURCE_ROOT,
    aggregate: options.aggregate ?? join(options.productionRoot ?? SOURCE_ROOT, 'Wanxiangshu.fsproj'),
  })
  const compilerObservations = scanCompilerObservationsV1(options)
  const analysis = analyzeLocalityDependencies({
    localities: inventory.localities.map((locality) => ({
      id: locality.id,
      owner: locality.owner,
      sources: locality.sources.map(({ implementationPath }) => implementationPath),
      references: locality.references,
    })),
    declarationUses: compilerObservations.declarationUses,
  })
  return { compilerObservations, analysis }
}

function printResult(result) {
  const { census } = result.analysis
  const observations = result.compilerObservations
  process.stdout.write(
    `locality-dependencies: ${result.analysis.violations.length === 0 ? 'OK' : 'BLOCKED'} — ${census.localities} localities, ${census.sources} sources, ${census.actualSourceEdges} actual source edges, ${census.missingClosureEdges} missing closure edges; observations ${observations.declarationUses.length} declaration / ${observations.externalSymbolUses.length} external / ${observations.fsharpNodes.length} F# nodes / ${observations.fableInterop.length} interop / ${observations.signatureExports.length} exports / ${observations.diagnostics.length} diagnostics; compiler ${observations.elapsedMilliseconds}ms\n`,
  )
  for (const violation of result.analysis.violations.slice(0, 50))
    process.stderr.write(
      `${violation.code}: ${violation.consumerSource}:${violation.line}:${violation.column} (${violation.consumerLocality}) -> ${violation.providerSource} (${violation.providerLocality}) ${violation.symbol}\n`,
    )
  for (const diagnostic of observations.diagnostics.slice(0, 50)) {
    process.stderr.write(
      `${diagnostic.code}: ${diagnostic.sourcePath ?? '<unknown>'}:${diagnostic.line}:${diagnostic.column} ${diagnostic.syntaxKind} ${diagnostic.rawIdentity}\n`,
    )
  }
  if (result.analysis.violations.length > 50)
    process.stderr.write(`... ${result.analysis.violations.length - 50} more missing closure edges\n`)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const result = runLocalityDependencyScan()
    printResult(result)
    if (result.analysis.violations.length > 0 && !process.argv.includes('--report-only')) process.exitCode = 1
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`)
    process.exitCode = 1
  }
}
