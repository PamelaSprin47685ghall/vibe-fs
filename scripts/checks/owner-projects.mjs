#!/usr/bin/env node

import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')
const SHARED_PROPS = join(SOURCE_ROOT, 'Directory.Build.props')
const OWNERS = join(ROOT, 'scripts/checks/semantic-owners.json')
const CONTRACTS = join(ROOT, 'scripts/checks/published-contracts.json')
const OWNER_PROJECT = /^Wanxiangshu\.Owner\..+\.fsproj$/
const LOCALITY_KINDS = new Set(['contract', 'runtime', 'adapter', 'composition'])

const norm = (value) => value.replace(/\\/g, '/')
const repoPath = (value) => norm(relative(ROOT, value))
const sorted = (values) => [...values].sort()

function parseProject(projectPath) {
  const text = readFileSync(projectPath, 'utf8')
  const owner = text.match(/<WanxiangshuSemanticOwner>([^<]+)<\/WanxiangshuSemanticOwner>/)?.[1]?.trim() ?? ''
  const locality = text.match(/<WanxiangshuOwnerLocality>([^<]+)<\/WanxiangshuOwnerLocality>/)?.[1]?.trim() ?? ''
  const kind = text.match(/<WanxiangshuOwnerLocalityKind>([^<]+)<\/WanxiangshuOwnerLocalityKind>/)?.[1]?.trim() ?? ''
  const compile = [...text.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/?\s*>/g)]
    .map((match) => repoPath(resolve(dirname(projectPath), match[1])))
  const signatures = [...text.matchAll(/<Compile\s+Include="([^"]+\.fsi)"\s*\/?\s*>/g)]
    .map((match) => repoPath(resolve(dirname(projectPath), match[1])))
  const references = [...text.matchAll(/<ProjectReference\s+Include="([^"]+\.fsproj)"\s*\/?\s*>/g)]
    .map((match) => resolve(dirname(projectPath), match[1]))
  return { projectPath, text, owner, locality, kind, compile, signatures, references }
}

function cycleOf(projects) {
  const visiting = new Set()
  const visited = new Set()
  const stack = []
  let cycle = null
  const visit = (projectPath) => {
    if (visited.has(projectPath) || cycle) return
    if (visiting.has(projectPath)) {
      const start = stack.indexOf(projectPath)
      cycle = [...stack.slice(start), projectPath]
      return
    }
    visiting.add(projectPath)
    stack.push(projectPath)
    for (const next of projects.get(projectPath)?.references ?? []) {
      if (projects.has(next)) visit(next)
      if (cycle) return
    }
    stack.pop()
    visiting.delete(projectPath)
    visited.add(projectPath)
  }
  for (const projectPath of projects.keys()) visit(projectPath)
  return cycle
}

function projectClosure(projects, roots) {
  const closure = new Set()
  const pending = [...roots]
  while (pending.length > 0) {
    const projectPath = pending.pop()
    if (closure.has(projectPath)) continue
    closure.add(projectPath)
    for (const reference of projects.get(projectPath)?.references ?? []) {
      if (projects.has(reference) && !closure.has(reference)) pending.push(reference)
    }
  }
  return closure
}

export function projectArchitectureViolations(projects, { contractSourceBudget = 100 } = {}) {
  const violations = []

  for (const project of projects.values()) {
    const label = repoPath(project.projectPath)

    if (!project.kind) {
      violations.push(`locality-kind: ${label}: missing WanxiangshuOwnerLocalityKind`)
      continue
    }

    if (!LOCALITY_KINDS.has(project.kind)) {
      violations.push(`locality-kind: ${label}: unknown WanxiangshuOwnerLocalityKind '${project.kind}'`)
    }
  }

  for (const project of projects.values()) {
    if (project.kind === 'contract') {
      const closure = projectClosure(projects, [project.projectPath])
      const sources = new Set()

      for (const projectPath of closure) {
        const dependency = projects.get(projectPath)
        for (const source of dependency?.compile ?? []) sources.add(source)

        if (dependency && dependency.kind !== 'contract') {
          violations.push(
            `contract-runtime-direction: ${repoPath(project.projectPath)}: contract closure contains non-contract ${repoPath(projectPath)} (${dependency.kind || 'missing'})`,
          )
        }
      }

      if (sources.size > contractSourceBudget) {
        violations.push(
          `contract-closure-budget: ${repoPath(project.projectPath)}: contract closure has ${sources.size} production .fs; budget is ${contractSourceBudget}`,
        )
      }
    }

    for (const reference of project.references) {
      const provider = projects.get(reference)
      const foreignImplementation = provider
        && provider.owner !== project.owner
        && provider.kind === 'runtime'

      if (!foreignImplementation) continue

      if (project.kind !== 'composition') {
        violations.push(
          `foreign-runtime-reference/composition-only-runtime-binding: ${repoPath(project.projectPath)} -> ${repoPath(reference)}: only composition may reference foreign ${provider.kind}`,
        )
      }
    }
  }

  return violations
}

function allowedForeignReferences(projects, projectOfSource, contracts) {
  const allowed = new Set()
  const byOwner = new Map()
  for (const project of projects.values()) {
    const entries = byOwner.get(project.owner) ?? []
    entries.push(project.projectPath)
    byOwner.set(project.owner, entries)
  }
  const add = (consumerProject, providerProject) => {
    if (consumerProject && providerProject) allowed.add(`${consumerProject}\0${providerProject}`)
  }
  for (const contract of contracts.contracts ?? []) {
    const providerProject = projectOfSource.get(contract.path)
    for (const consumerOwner of contract.consumers ?? []) {
      for (const consumerProject of byOwner.get(consumerOwner) ?? []) add(consumerProject, providerProject)
    }
  }
  for (const adapter of contracts.physical_adapters ?? []) {
    const consumerProject = projectOfSource.get(adapter.path)
    for (const port of adapter.ports ?? []) add(consumerProject, projectOfSource.get(port.path))
  }
  for (const root of contracts.composition_roots ?? []) {
    const consumerProject = projectOfSource.get(root.path)
    for (const wire of root.wires ?? []) add(consumerProject, projectOfSource.get(wire.path))
  }
  return allowed
}

export function checkOwnerProjects() {
  const violations = []
  const fail = (message) => violations.push(message)
  if (!existsSync(AGGREGATE)) return { ok: false, violations: [`${repoPath(AGGREGATE)}: missing aggregate project`] }
  if (!existsSync(SHARED_PROPS)) return { ok: false, violations: [`${repoPath(SHARED_PROPS)}: missing owner-project props`] }

  const ownerManifest = JSON.parse(readFileSync(OWNERS, 'utf8'))
  const contractManifest = JSON.parse(readFileSync(CONTRACTS, 'utf8'))
  const semanticOwner = new Map(ownerManifest.ownership.map((entry) => [entry.path, entry.owner]))
  const projectPaths = readdirSync(SOURCE_ROOT)
    .filter((name) => OWNER_PROJECT.test(name))
    .map((name) => join(SOURCE_ROOT, name))
    .sort()
  if (projectPaths.length < 2) fail('57.15 requires more than one owner-locality project')

  const projects = new Map(projectPaths.map((projectPath) => [projectPath, parseProject(projectPath)]))
  const projectOfSource = new Map()
  for (const project of projects.values()) {
    const label = repoPath(project.projectPath)
    if (!project.owner) fail(`${label}: missing WanxiangshuSemanticOwner`)
    if (!project.locality) fail(`${label}: missing WanxiangshuOwnerLocality`)
    if (/^(?:phase|slice|part)[-_]?\d+$/i.test(project.locality)) fail(`${label}: numbered phase/slice locality is forbidden`)
    if (project.compile.length === 0) fail(`${label}: owner locality compiles no production source`)
    if (new Set(project.compile).size !== project.compile.length) fail(`${label}: duplicate Compile entry`)
    if (new Set(project.references).size !== project.references.length) fail(`${label}: duplicate ProjectReference`)
    for (const source of project.compile) {
      if (!existsSync(join(ROOT, source))) fail(`${label}: missing source ${source}`)
      const declaredOwner = semanticOwner.get(source)
      if (!declaredOwner) fail(`${label}: ${source} has no semantic owner`)
      else if (declaredOwner !== project.owner) fail(`${label}: ${source} belongs to ${declaredOwner}, not ${project.owner}`)
      const previous = projectOfSource.get(source)
      if (previous) fail(`${source}: compiled by both ${repoPath(previous)} and ${label}`)
      else projectOfSource.set(source, project.projectPath)
    }
    for (const reference of project.references) {
      if (!projects.has(reference)) fail(`${label}: stale/non-owner ProjectReference ${repoPath(reference)}`)
      if (reference === project.projectPath) fail(`${label}: self ProjectReference`)
    }
  }

  for (const [source, owner] of semanticOwner) {
    if (!projectOfSource.has(source)) fail(`${source}: semantic owner ${owner} has no compile locality`)
  }
  for (const source of projectOfSource.keys()) {
    if (!semanticOwner.has(source)) fail(`${source}: compiled production source is absent from semantic-owners.json`)
  }

  const cycle = cycleOf(projects)
  if (cycle) fail(`owner project graph contains SCC/cycle: ${cycle.map(repoPath).join(' -> ')}`)

  const allowed = allowedForeignReferences(projects, projectOfSource, contractManifest)
  for (const violation of projectArchitectureViolations(projects)) fail(violation)

  const contractPaths = new Set((contractManifest.contracts ?? []).map((entry) => entry.path))
  const contractSupportPaths = new Set()
  for (const entry of contractManifest.compile_contract_support ?? []) {
    const source = entry?.path ?? ''
    if (!source || /[*?\[\]]/.test(source) || !semanticOwner.has(source)) {
      fail(`compile contract support must name one exact production source: ${source || '<missing>'}`)
      continue
    }
    if (contractPaths.has(source)) fail(`${source}: compile contract support duplicates a published contract source`)
    if (contractSupportPaths.has(source)) fail(`${source}: duplicate compile contract support declaration`)
    if (semanticOwner.get(source) !== entry.owner) {
      fail(`${source}: compile contract support owner ${entry.owner ?? '<missing>'} != ${semanticOwner.get(source)}`)
    }
    if (typeof entry.justification !== 'string' || entry.justification.trim().length < 16) {
      fail(`${source}: compile contract support requires an architectural justification`)
    }
    const signature = source.replace(/\.fs$/, '.fsi')
    const projectPath = projectOfSource.get(source)
    const project = projectPath ? projects.get(projectPath) : null
    if (!project || !project.signatures.includes(signature) || !existsSync(join(ROOT, signature))) {
      fail(`${source}: compile contract support requires a sibling .fsi compiled by the same owner locality`)
    }
    contractSupportPaths.add(source)
  }
  const contractSafePaths = new Set([...contractPaths, ...contractSupportPaths])
  const compilerBoundaryProjects = new Set()
  const compilerBoundaryKeys = new Set()
  for (const entry of contractManifest.compiler_boundary_localities ?? []) {
    const owner = entry?.owner ?? ''
    const locality = entry?.locality ?? ''
    const key = `${owner}\0${locality}`
    if (!owner || !locality || compilerBoundaryKeys.has(key)) {
      fail(`compiler boundary locality is missing or duplicated: ${owner || '<missing>'}/${locality || '<missing>'}`)
      continue
    }
    if (typeof entry.justification !== 'string' || entry.justification.trim().length < 16) {
      fail(`${owner}/${locality}: compiler boundary locality requires an architectural justification`)
    }
    compilerBoundaryKeys.add(key)
    const ownedProjects = [...projects.values()].filter((project) => project.owner === owner && project.locality === locality)
    if (ownedProjects.length !== 1) {
      fail(`${owner}/${locality}: compiler boundary locality must resolve to exactly one owner project, found ${ownedProjects.length}`)
      continue
    }
    const project = ownedProjects[0]
    compilerBoundaryProjects.add(project.projectPath)
    for (const source of project.compile) {
      const signature = source.replace(/\.fs$/, '.fsi')
      if (!project.signatures.includes(signature) || !existsSync(join(ROOT, signature))) {
        fail(`${source}: compiler boundary locality '${owner}/${locality}' requires a sibling .fsi in the same locality`)
      }
      if (!contractSafePaths.has(source)) {
        fail(`${source}: compiler boundary locality '${owner}/${locality}' source is neither published contract nor signed support`)
      }
    }
  }
  const contractClosure = projectClosure(projects, compilerBoundaryProjects)
  for (const projectPath of contractClosure) {
    if (!compilerBoundaryProjects.has(projectPath)) {
      const project = projects.get(projectPath)
      fail(
        `${repoPath(projectPath)}: compiler boundary closure dependency ${project?.owner ?? '<unknown>'}/${project?.locality ?? '<unknown>'} is not itself a graduated compiler-boundary locality`,
      )
    }
  }
  const contractLeakSources = [...contractClosure]
    .flatMap((projectPath) => projects.get(projectPath)?.compile ?? [])
    .filter((source) => !contractSafePaths.has(source))
    .sort()
  if (contractLeakSources.length > 0) {
    fail(
      `published contract compile closure contains ${contractLeakSources.length} runtime/private source(s): ${contractLeakSources
        .slice(0, 12)
        .join(', ')}`,
    )
  }

  for (const project of projects.values()) {
    for (const reference of project.references) {
      const provider = projects.get(reference)
      if (!provider || provider.owner === project.owner) continue
      if (!allowed.has(`${project.projectPath}\0${reference}`)) {
        fail(`${repoPath(project.projectPath)} -> ${repoPath(reference)}: foreign ProjectReference has no published-contract/adapter/root authorization`)
      }
    }
  }

  const emit = parseProject(AGGREGATE)
  if (!/<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/.test(emit.text)) {
    fail(`${repoPath(AGGREGATE)}: flattened Fable emitter must declare WanxiangshuEmitProject=true`)
  }
  if (emit.references.length > 0) fail(`${repoPath(AGGREGATE)}: flattened Fable emitter must not ProjectReference owner localities`)
  const emitSources = sorted(emit.compile)
  const ownerSources = sorted(projectOfSource.keys())
  if (JSON.stringify(emitSources) !== JSON.stringify(ownerSources)) {
    const actual = new Set(emitSources)
    const expected = new Set(ownerSources)
    const missing = ownerSources.filter((value) => !actual.has(value))
    const extra = emitSources.filter((value) => !expected.has(value))
    fail(`${repoPath(AGGREGATE)}: emit/owner compile-set drift missing=[${missing.slice(0, 8).join(', ')}] extra=[${extra.slice(0, 8).join(', ')}]`)
  }
  for (const projectPath of compilerBoundaryProjects) {
    const project = projects.get(projectPath)
    for (const source of project?.compile ?? []) {
      const signature = source.replace(/\.fs$/, '.fsi')
      if (!emit.signatures.includes(signature)) {
        fail(`${signature}: compiler boundary locality '${project.owner}/${project.locality}' signature is missing from flattened Fable emit`)
      }
    }
  }

  const props = readFileSync(SHARED_PROPS, 'utf8')
  if (!/<DisableTransitiveProjectReferences>true<\/DisableTransitiveProjectReferences>/.test(props)) {
    fail(`${repoPath(SHARED_PROPS)}: DisableTransitiveProjectReferences must be true`)
  }

  return {
    ok: violations.length === 0,
    violations,
    projectCount: projects.size,
    sourceCount: projectOfSource.size,
    projectReferenceCount: [...projects.values()].reduce((count, project) => count + project.references.length, 0),
    compilerBoundaryLocalityCount: compilerBoundaryProjects.size,
    contractSupportSourceCount: contractSupportPaths.size,
    contractLeakSourceCount: contractLeakSources.length,
  }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = checkOwnerProjects()
  if (!result.ok) {
    console.error(`owner-projects: FAILED — ${result.violations.length} violation(s)`)
    for (const violation of result.violations) console.error(`  ${violation}`)
    process.exit(1)
  }
  console.log(`owner-projects: OK — ${result.projectCount} localities, ${result.sourceCount} sources, ${result.projectReferenceCount} refs, DAG`)
}
