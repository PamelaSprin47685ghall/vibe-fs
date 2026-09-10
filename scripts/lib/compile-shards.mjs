import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { dirname, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const MODULE_DIR = dirname(fileURLToPath(import.meta.url))
export const REPOSITORY_ROOT = resolve(MODULE_DIR, '../..')
export const SOURCE_ROOT = resolve(REPOSITORY_ROOT, 'src/Wanxiangshu')
export const AGGREGATE_PROJECT = resolve(SOURCE_ROOT, 'Wanxiangshu.fsproj')

const SHARD_PROJECT = /^Wanxiangshu\.(?:Owner|Shard)\..+\.fsproj$/

const norm = (value) => value.replace(/\\/g, '/')
const repoPath = (root, value) => norm(relative(root, value))
const property = (text, name) => text.match(new RegExp(`<${name}>([^<]+)</${name}>`))?.[1]?.trim() ?? ''

function includes(text, tag) {
  return [...text.matchAll(new RegExp(`<${tag}\\s+Include="([^"]+)"\\s*\\/?\\s*>`, 'g'))]
    .map((match) => match[1])
}

export function parseCompileShardProject(projectPath, { repositoryRoot = REPOSITORY_ROOT } = {}) {
  const text = readFileSync(projectPath, 'utf8')
  const projectDirectory = dirname(projectPath)
  const compileFiles = includes(text, 'Compile').map((entry) => resolve(projectDirectory, entry))
  const references = includes(text, 'ProjectReference').map((entry) => resolve(projectDirectory, entry))
  return {
    projectPath,
    projectRepoPath: repoPath(repositoryRoot, projectPath),
    text,
    explicitSubsystem: property(text, 'WanxiangshuSubsystem'),
    explicitCompileShard: property(text, 'WanxiangshuCompileShard'),
    legacyOwner: property(text, 'WanxiangshuSemanticOwner'),
    legacyLocality: property(text, 'WanxiangshuOwnerLocality'),
    legacyKind: property(text, 'WanxiangshuOwnerLocalityKind'),
    implementationFiles: compileFiles.filter((entry) => entry.endsWith('.fs')),
    signatureFiles: compileFiles.filter((entry) => entry.endsWith('.fsi')),
    references,
  }
}

function productionSources(root) {
  const result = []
  const visit = (directory) => {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const fullPath = resolve(directory, entry.name)
      if (entry.isDirectory()) {
        if (entry.name.startsWith('.') || entry.name === 'obj' || entry.name === 'bin') continue
        visit(fullPath)
      }
      else if (entry.isFile() && entry.name.endsWith('.fs') && !entry.name.endsWith('.fsi')) result.push(fullPath)
    }
  }
  visit(root)
  return result.sort()
}

function assertDag(projects) {
  const visiting = new Set()
  const visited = new Set()
  const visit = (projectPath) => {
    if (visited.has(projectPath)) return
    if (visiting.has(projectPath)) throw new Error(`compile-shard graph contains a cycle at ${projectPath}`)
    visiting.add(projectPath)
    for (const reference of projects.get(projectPath).references) visit(reference)
    visiting.delete(projectPath)
    visited.add(projectPath)
  }
  for (const projectPath of projects.keys()) visit(projectPath)
}

function parseAggregate(aggregatePath) {
  const text = readFileSync(aggregatePath, 'utf8')
  const directory = dirname(aggregatePath)
  return {
    text,
    implementationFiles: includes(text, 'Compile').filter((entry) => entry.endsWith('.fs')).map((entry) => resolve(directory, entry)),
    signatureFiles: includes(text, 'Compile').filter((entry) => entry.endsWith('.fsi')).map((entry) => resolve(directory, entry)),
    references: includes(text, 'ProjectReference').map((entry) => resolve(directory, entry)),
  }
}

function sameSet(left, right) {
  if (left.size !== right.size) return false
  for (const value of left) if (!right.has(value)) return false
  return true
}

export function readCompileShardInventory({
  repositoryRoot = REPOSITORY_ROOT,
  sourceRoot = resolve(repositoryRoot, 'src/Wanxiangshu'),
  aggregatePath = resolve(sourceRoot, 'Wanxiangshu.fsproj'),
} = {}) {
  if (!existsSync(aggregatePath)) throw new Error(`${repoPath(repositoryRoot, aggregatePath)}: aggregate project is missing`)
  const projectPaths = readdirSync(sourceRoot)
    .filter((name) => SHARD_PROJECT.test(name))
    .map((name) => resolve(sourceRoot, name))
    .sort()
  const projects = new Map(projectPaths.map((projectPath) => [projectPath, parseCompileShardProject(projectPath, { repositoryRoot })]))
  if (projects.size === 0) throw new Error('no compile-shard projects found')

  const sourceProject = new Map()
  for (const project of projects.values()) {
    if (project.implementationFiles.length === 0) throw new Error(`${project.projectRepoPath}: compile shard has no production .fs`)
    if (new Set(project.implementationFiles).size !== project.implementationFiles.length) throw new Error(`${project.projectRepoPath}: duplicate .fs entry`)
    if (new Set(project.signatureFiles).size !== project.signatureFiles.length) throw new Error(`${project.projectRepoPath}: duplicate .fsi entry`)
    if (new Set(project.references).size !== project.references.length) throw new Error(`${project.projectRepoPath}: duplicate ProjectReference`)

    const signatures = new Set(project.signatureFiles)
    for (const implementationPath of project.implementationFiles) {
      if (!existsSync(implementationPath)) throw new Error(`${repoPath(repositoryRoot, implementationPath)}: source is missing`)
      const signaturePath = implementationPath.replace(/\.fs$/, '.fsi')
      if (!signatures.has(signaturePath) || !existsSync(signaturePath)) {
        throw new Error(`${repoPath(repositoryRoot, implementationPath)}: compile shard must carry its sibling .fsi`)
      }
      const previous = sourceProject.get(implementationPath)
      if (previous) throw new Error(`${repoPath(repositoryRoot, implementationPath)}: compiled by both ${previous.projectRepoPath} and ${project.projectRepoPath}`)
      sourceProject.set(implementationPath, project)
    }
    for (const signaturePath of project.signatureFiles) {
      const implementationPath = signaturePath.slice(0, -1)
      if (!project.implementationFiles.includes(implementationPath)) {
        throw new Error(`${repoPath(repositoryRoot, signaturePath)}: signature has no sibling implementation in the same compile shard`)
      }
    }
    for (const reference of project.references) {
      if (!projects.has(reference)) throw new Error(`${project.projectRepoPath}: ProjectReference is not a compile shard: ${repoPath(repositoryRoot, reference)}`)
      if (reference === project.projectPath) throw new Error(`${project.projectRepoPath}: compile shard references itself`)
    }
  }
  assertDag(projects)

  const aggregate = parseAggregate(aggregatePath)
  if (aggregate.references.length !== 0) throw new Error(`${repoPath(repositoryRoot, aggregatePath)}: aggregate emitter must not contain ProjectReference`)
  const shardImplementations = new Set([...sourceProject.keys()])
  const shardSignatures = new Set([...projects.values()].flatMap((project) => project.signatureFiles))
  if (!sameSet(shardImplementations, new Set(aggregate.implementationFiles))) throw new Error('aggregate .fs compile set differs from compile-shard union')
  if (!sameSet(shardSignatures, new Set(aggregate.signatureFiles))) throw new Error('aggregate .fsi compile set differs from compile-shard union')
  const discoveredSources = new Set(productionSources(sourceRoot))
  if (!sameSet(shardImplementations, discoveredSources)) {
    const unassigned = [...discoveredSources].filter((source) => !shardImplementations.has(source)).map((source) => repoPath(repositoryRoot, source))
    const stale = [...shardImplementations].filter((source) => !discoveredSources.has(source)).map((source) => repoPath(repositoryRoot, source))
    throw new Error(`production source coverage mismatch unassigned=[${unassigned.slice(0, 12).join(', ')}] stale=[${stale.slice(0, 12).join(', ')}]`)
  }

  return {
    repositoryRoot,
    sourceRoot,
    aggregatePath,
    projects,
    sourceProject,
    sourceCount: sourceProject.size,
    projectReferenceCount: [...projects.values()].reduce((sum, project) => sum + project.references.length, 0),
  }
}

export function stronglyConnectedComponents(nodes, edges) {
  const adjacency = new Map([...nodes].map((node) => [node, []]))
  for (const [from, to] of edges) adjacency.get(from)?.push(to)
  let index = 0
  const indices = new Map()
  const lowlink = new Map()
  const stack = []
  const onStack = new Set()
  const components = []
  const visit = (node) => {
    indices.set(node, index)
    lowlink.set(node, index++)
    stack.push(node)
    onStack.add(node)
    for (const next of adjacency.get(node) ?? []) {
      if (!indices.has(next)) {
        visit(next)
        lowlink.set(node, Math.min(lowlink.get(node), lowlink.get(next)))
      } else if (onStack.has(next)) lowlink.set(node, Math.min(lowlink.get(node), indices.get(next)))
    }
    if (lowlink.get(node) !== indices.get(node)) return
    const component = []
    while (stack.length > 0) {
      const current = stack.pop()
      onStack.delete(current)
      component.push(current)
      if (current === node) break
    }
    components.push(component.sort())
  }
  for (const node of nodes) if (!indices.has(node)) visit(node)
  return components.sort((left, right) => right.length - left.length || left[0].localeCompare(right[0]))
}
