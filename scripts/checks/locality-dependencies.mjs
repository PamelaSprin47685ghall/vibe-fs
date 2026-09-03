#!/usr/bin/env node

import { spawnSync } from 'node:child_process'
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs'
import { homedir, tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { analyzeLocalityDependencies } from '../lib/locality-dependencies.mjs'
import { materializeOwnerCompile, planImpactCompile } from '../lib/owner-compile.mjs'
import { parseProject } from './owner-projects.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')
const SCANNER = join(ROOT, 'scripts/checks/locality-symbol-uses.fsx')
const FABLE_LIBRARY = join(ROOT, 'node_modules/@fable-org/fable-library-js')
const OWNER_PROJECT = /^Wanxiangshu\.Owner\..+\.fsproj$/

const normalizePath = (value) => value.replace(/\\/g, '/')
const repositoryPath = (value) => normalizePath(relative(ROOT, resolve(value)))

function fableToolDirectory() {
  const manifest = JSON.parse(readFileSync(join(ROOT, '.config/dotnet-tools.json'), 'utf8'))
  const version = manifest?.tools?.fable?.version
  if (typeof version !== 'string' || version.length === 0) throw new Error('dotnet tool manifest has no pinned Fable version')
  const directory = join(homedir(), '.nuget/packages/fable', version, 'tools', 'net10.0', 'any')
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

export function scanCompilerUses({ aggregate = AGGREGATE, productionRoot = SOURCE_ROOT } = {}) {
  const scratch = mkdtempSync(join(tmpdir(), 'wanxiangshu-locality-dependencies-'))
  const resultPath = join(scratch, 'symbol-uses.json')
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
    return {
      elapsedMilliseconds: parsed.elapsedMilliseconds,
      productionFiles: parsed.productionFiles.map(repositoryPath),
      symbolUses: parsed.symbolUses.map((entry) => ({
        ...entry,
        consumerPath: repositoryPath(entry.consumerPath),
        providerPaths: entry.providerPaths.map(repositoryPath),
      })),
    }
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
}

export function runLocalityDependencyScan(options = {}) {
  const compiler = scanCompilerUses(options)
  const analysis = analyzeLocalityDependencies({
    localities: readLocalities(options.productionRoot),
    compilerUses: compiler.symbolUses,
  })
  return { compiler, analysis }
}

function printResult(result) {
  const { census } = result.analysis
  process.stdout.write(
    `locality-dependencies: ${result.analysis.violations.length === 0 ? 'OK' : 'BLOCKED'} — ${census.localities} localities, ${census.sources} sources, ${census.actualSourceEdges} actual source edges, ${census.missingClosureEdges} missing closure edges, compiler ${result.compiler.elapsedMilliseconds}ms\n`,
  )
  for (const violation of result.analysis.violations.slice(0, 50))
    process.stderr.write(
      `${violation.code}: ${violation.consumerSource}:${violation.line}:${violation.column} (${violation.consumerLocality}) -> ${violation.providerSource} (${violation.providerLocality}) ${violation.symbol}\n`,
    )
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
