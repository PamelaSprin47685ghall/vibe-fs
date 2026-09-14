import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn as nodeSpawn } from 'node:child_process'

const MODULE_DIR = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(MODULE_DIR, '../..')

export const SCHEMA_VERSION = 'owner-compile-v3'
// WP5 cutover: the wrapper aggregate fsproj is physically deleted; every
// `aggregatePath` parameter below is an exclusion pattern (a path to skip
// during owner project discovery), never a live file a caller must open.
// The canonical source list + order live inside the compile-shard graph
// itself plus the compile-order.txt manifest.

export const DEFAULT_SCRATCH_ROOT = path.resolve(REPO_ROOT, '.fable-build/owner-compile')
export const DEFAULT_ROOT_PROPS_PATH = path.resolve(REPO_ROOT, 'Directory.Build.props')
export const DEFAULT_BUILD_MANIFEST_PATH = path.resolve(REPO_ROOT, '.fable-build/build-manifest.json')

function norm(filePath) {
  return path.resolve(filePath).replace(/\\/g, '/')
}

function escapeXmlAttr(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;')
}

function decodeXmlAttr(str, contextDesc = '') {
  if (typeof str !== 'string') {
    return str
  }

  if (!str.includes('&')) {
    return str
  }

  let result = ''
  let i = 0
  while (i < str.length) {
    const ampIdx = str.indexOf('&', i)
    if (ampIdx === -1) {
      result += str.slice(i)
      break
    }

    result += str.slice(i, ampIdx)

    const semiIdx = str.indexOf(';', ampIdx)
    const nextAmp = str.indexOf('&', ampIdx + 1)
    if (semiIdx === -1 || (nextAmp !== -1 && nextAmp < semiIdx)) {
      const raw = str.slice(ampIdx, nextAmp !== -1 ? nextAmp : undefined)
      const ctx = contextDesc ? ` in ${contextDesc}` : ''
      throw new Error(`Malformed XML entity reference "${raw}"${ctx}`)
    }

    const entity = str.slice(ampIdx + 1, semiIdx)
    const fullEntity = str.slice(ampIdx, semiIdx + 1)

    if (entity === 'amp') {
      result += '&'
    } else if (entity === 'lt') {
      result += '<'
    } else if (entity === 'gt') {
      result += '>'
    } else if (entity === 'quot') {
      result += '"'
    } else if (entity === 'apos') {
      result += "'"
    } else if (entity.startsWith('#')) {
      const isHex = entity.startsWith('#x') || entity.startsWith('#X')
      const numStr = isHex ? entity.slice(2) : entity.slice(1)

      if (!numStr || (isHex ? !/^[0-9a-fA-F]+$/.test(numStr) : !/^[0-9]+$/.test(numStr))) {
        const ctx = contextDesc ? ` in ${contextDesc}` : ''
        throw new Error(`Malformed XML character reference "${fullEntity}"${ctx}`)
      }

      const codePoint = Number.parseInt(numStr, isHex ? 16 : 10)
      if (
        Number.isNaN(codePoint) ||
        codePoint > 0x10ffff ||
        codePoint === 0 ||
        (codePoint >= 0xd800 && codePoint <= 0xdfff)
      ) {
        const ctx = contextDesc ? ` in ${contextDesc}` : ''
        throw new Error(`Invalid XML character reference "${fullEntity}" (code point: ${codePoint})${ctx}`)
      }

      try {
        result += String.fromCodePoint(codePoint)
      } catch {
        const ctx = contextDesc ? ` in ${contextDesc}` : ''
        throw new Error(`Invalid XML character reference "${fullEntity}" (code point: ${codePoint})${ctx}`)
      }
    } else {
      const ctx = contextDesc ? ` in ${contextDesc}` : ''
      throw new Error(`Unknown XML entity reference "${fullEntity}"${ctx}`)
    }

    i = semiIdx + 1
  }

  return result
}

function stripXmlComments(xml) {
  return xml.replace(/<!--[\s\S]*?-->/g, '')
}

function extractXmlItems(xmlText, tagName, contextPath = '') {
  const clean = stripXmlComments(xmlText)
  const regex = new RegExp(`<${tagName}\\b([^>]*?)(?:\\/>|>([\\s\\S]*?)<\\/${tagName}>)`, 'gi')
  const items = []
  let match
  while ((match = regex.exec(clean)) !== null) {
    const attrs = match[1]
    const incMatch = attrs.match(/\bInclude=(["'])(.*?)\1/i)
    if (incMatch) {
      items.push({
        rawInclude: decodeXmlAttr(incMatch[2], contextPath),
        fullTag: match[0],
        attrs,
      })
    }
  }
  return items
}

function parseProjectFile(projectPath) {
  const resolvedPath = norm(projectPath)
  if (!fs.existsSync(resolvedPath)) {
    throw new Error(`Project file does not exist: ${resolvedPath}`)
  }
  if (path.extname(resolvedPath).toLowerCase() !== '.fsproj') {
    throw new Error(`Project file must have .fsproj extension: ${resolvedPath}`)
  }

  const rawText = fs.readFileSync(resolvedPath, 'utf8')
  const cleanText = stripXmlComments(rawText)
  if (!/^\uFEFF?(?:\s*<\?xml[^>]*\?>)?\s*<Project\b/i.test(cleanText)) {
    throw new Error(`Project file lacks <Project> root: ${resolvedPath}`)
  }

  const dir = path.dirname(resolvedPath)

  // ProjectReference items
  const rawRefs = extractXmlItems(rawText, 'ProjectReference', resolvedPath)
  const seenRawRefs = new Set()
  const seenResolvedRefs = new Set()
  const references = []

  for (const item of rawRefs) {
    if (seenRawRefs.has(item.rawInclude)) {
      throw new Error(`Duplicate ProjectReference in ${resolvedPath}: "${item.rawInclude}"`)
    }
    seenRawRefs.add(item.rawInclude)

    const resolvedRef = norm(path.resolve(dir, item.rawInclude))
    if (seenResolvedRefs.has(resolvedRef)) {
      throw new Error(`Duplicate ProjectReference in ${resolvedPath}: "${item.rawInclude}" (resolved: ${resolvedRef})`)
    }
    seenResolvedRefs.add(resolvedRef)

    if (!fs.existsSync(resolvedRef)) {
      throw new Error(`Missing ProjectReference in ${resolvedPath}: "${item.rawInclude}" (file not found: ${resolvedRef})`)
    }
    if (resolvedRef === resolvedPath) {
      throw new Error(`Self ProjectReference in ${resolvedPath}`)
    }
    if (path.extname(resolvedRef).toLowerCase() !== '.fsproj') {
      throw new Error(`Invalid ProjectReference in ${resolvedPath}: "${item.rawInclude}" must reference a .fsproj file (resolved: ${resolvedRef})`)
    }
    references.push(resolvedRef)
  }

  // Compile items
  const rawCompiles = extractXmlItems(rawText, 'Compile', resolvedPath)
  const seenRawCompiles = new Set()
  const seenResolvedCompiles = new Set()
  const compileItems = []

  for (const item of rawCompiles) {
    if (seenRawCompiles.has(item.rawInclude)) {
      throw new Error(`Duplicate Compile item in ${resolvedPath}: "${item.rawInclude}"`)
    }
    seenRawCompiles.add(item.rawInclude)

    const resolvedSrc = norm(path.resolve(dir, item.rawInclude))
    if (seenResolvedCompiles.has(resolvedSrc)) {
      throw new Error(`Duplicate Compile item in ${resolvedPath}: "${item.rawInclude}" (resolved: ${resolvedSrc})`)
    }
    seenResolvedCompiles.add(resolvedSrc)

    if (!fs.existsSync(resolvedSrc)) {
      throw new Error(`Compile source file does not exist for ${resolvedPath}: "${item.rawInclude}" (resolved: ${resolvedSrc})`)
    }
    compileItems.push(resolvedSrc)
  }

  return {
    path: resolvedPath,
    dir,
    rawText,
    references,
    compileItems,
  }
}

function parseAggregateProject(aggregatePath) {
  const resolvedPath = norm(aggregatePath)
  if (!fs.existsSync(resolvedPath)) {
    throw new Error(`Aggregate project file does not exist: ${resolvedPath}`)
  }
  if (path.extname(resolvedPath).toLowerCase() !== '.fsproj') {
    throw new Error(`Aggregate project file must have .fsproj extension: ${resolvedPath}`)
  }

  const rawText = fs.readFileSync(resolvedPath, 'utf8')
  const cleanText = stripXmlComments(rawText)
  if (!/^\uFEFF?(?:\s*<\?xml[^>]*\?>)?\s*<Project\b/i.test(cleanText)) {
    throw new Error(`Aggregate project file lacks <Project> root: ${resolvedPath}`)
  }

  const dir = path.dirname(resolvedPath)

  // Aggregate project must NOT contain ProjectReference
  const refs = extractXmlItems(rawText, 'ProjectReference', resolvedPath)
  if (refs.length > 0) {
    throw new Error(`Aggregate project must not contain ProjectReference: ${resolvedPath} contains ${refs.length} reference(s)`)
  }

  const rawCompiles = extractXmlItems(rawText, 'Compile', resolvedPath)
  const seenRawCompiles = new Set()
  const seenResolved = new Set()
  const compileItems = []

  for (const item of rawCompiles) {
    if (seenRawCompiles.has(item.rawInclude)) {
      throw new Error(`Duplicate Compile item in aggregate project ${resolvedPath}: "${item.rawInclude}"`)
    }
    seenRawCompiles.add(item.rawInclude)

    const resolvedSrc = norm(path.resolve(dir, item.rawInclude))
    if (seenResolved.has(resolvedSrc)) {
      throw new Error(`Duplicate resolved Compile source in aggregate project ${resolvedPath}: "${resolvedSrc}"`)
    }
    seenResolved.add(resolvedSrc)

    if (!fs.existsSync(resolvedSrc)) {
      throw new Error(`Aggregate source file does not exist: ${resolvedSrc}`)
    }
    compileItems.push(resolvedSrc)
  }

  return {
    path: resolvedPath,
    dir,
    rawText,
    compileItems,
  }
}

/**
 * Pure, deterministic, side-effect free plan computation.
 *
 * Traverses ProjectReference reachability starting from candidate projectPath.
 * Validates DAG invariants (no cycles, no missing/duplicate refs, no duplicate compile items,
 * no aggregate drift).
 * Orders compile items strictly according to canonical aggregate document order.
 */
export function planOwnerCompile({ projectPath, aggregatePath = null } = {}) {
  if (!projectPath) {
    throw new Error('projectPath is required for planOwnerCompile')
  }

  const resolvedProjectPath = norm(projectPath)
  const resolvedAggregatePath = aggregatePath ? norm(aggregatePath) : null

  const aggregateExists = resolvedAggregatePath !== null && fs.existsSync(resolvedAggregatePath)
  const aggregate = aggregateExists
    ? parseAggregateProject(resolvedAggregatePath)
    : { path: resolvedAggregatePath, dir: resolvedAggregatePath ? path.dirname(resolvedAggregatePath) : null, rawText: '', compileItems: [], missing: true }
  const aggregateCompileSet = new Set(aggregate.compileItems)

  const closureProjects = new Map()
  const visiting = new Set()
  const visited = new Set()

  function visit(p, stack) {
    if (visiting.has(p)) {
      const cycleStart = stack.indexOf(p)
      const cycle = [...stack.slice(cycleStart), p]
      throw new Error(`ProjectReference cycle detected: ${cycle.join(' -> ')}`)
    }
    if (visited.has(p)) {
      return
    }

    visiting.add(p)
    stack.push(p)

    const parsed = parseProjectFile(p)
    closureProjects.set(p, parsed)

    for (const ref of parsed.references) {
      visit(ref, stack)
    }

    stack.pop()
    visiting.delete(p)
    visited.add(p)
  }

  visit(resolvedProjectPath, [])

  // Check compile items across all projects in closure
  const sourceToOwnerProject = new Map()
  for (const project of closureProjects.values()) {
    for (const src of project.compileItems) {
      const existing = sourceToOwnerProject.get(src)
      if (existing) {
        throw new Error(`Duplicate Compile item across closure projects: "${src}" is compiled by both ${existing} and ${project.path}`)
      }
      sourceToOwnerProject.set(src, project.path)
    }
  }

  if (aggregateExists) {
    // Wrapper still alive: enforce set parity between shard-declared items
    // and the aggregate (same shape compile-shards requires).
    for (const [src, owner] of sourceToOwnerProject.entries()) {
      if (!aggregateCompileSet.has(src)) {
        throw new Error(`Closure compile item absent from aggregate project: "${src}" (compiled in ${owner})`)
      }
    }
  }

  // WP2: compile order is canonical shard-DAG order, never the aggregate
  // document order once the wrapper file is gone. Order is identical this
  // run whether the aggregate exists or not — the difference is only whether
  // a file-side drift check ran.
  const closureSourcesSet = new Set(sourceToOwnerProject.keys())
  const orderedCompileItems = canonicalImpactOrder([...closureProjects.keys()], closureProjects)

  const sortedProjectPaths = [...closureProjects.keys()].sort()

  const projectContents = new Map()
  for (const [p, parsed] of closureProjects.entries()) {
    projectContents.set(p, parsed.rawText)
  }

  return {
    candidatePath: resolvedProjectPath,
    projectPath: resolvedProjectPath,
    candidateBasename: path.basename(resolvedProjectPath),
    aggregatePath: resolvedAggregatePath,
    projectPaths: sortedProjectPaths,
    compileItems: orderedCompileItems,
    projectContents,
    aggregateContent: aggregate.rawText,
  }
}

const FULL_IMPACT_BASENAMES = new Set([
  'Directory.Build.props',
  'Directory.Build.targets',
  'package.json',
  'package-lock.json',
  'pnpm-lock.yaml',
  'yarn.lock',
])

function requiresFullImpact(changedPath, aggregatePath) {
  const basename = path.basename(changedPath)
  return changedPath === aggregatePath
    || path.extname(changedPath).toLowerCase() === '.fsproj'
    || FULL_IMPACT_BASENAMES.has(basename)
    || /(?:^|\/)\.config\/dotnet-tools\.json$/.test(changedPath)
    || /(?:^|\/)scripts\/build\.mjs$/.test(changedPath)
    || /(?:^|\/)scripts\/lib\/owner-compile\.mjs$/.test(changedPath)
}

function discoverOwnerProjects(projectDirectory, aggregatePath) {
  const resolvedExclusion = aggregatePath ? norm(aggregatePath) : null
  const resolvedDirectory = norm(projectDirectory)
  if (!fs.existsSync(resolvedDirectory) || !fs.statSync(resolvedDirectory).isDirectory()) {
    throw new Error(`Owner project directory does not exist: ${resolvedDirectory}`)
  }

  return fs.readdirSync(resolvedDirectory, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith('.fsproj'))
    .map((entry) => norm(path.join(resolvedDirectory, entry.name)))
    // Exclude the wrapper aggregate by identity (caller may still pass its
    // path during the migration window) AND by content marker when the
    // caller did not name it (or passed a stale path while the physical
    // file is still on disk). Recognized emitter aliases cover the
    // production name and the two fixture conventions.
    .filter((projectPath) => resolvedExclusion === null || projectPath !== resolvedExclusion)
    .filter((projectPath) => !/\/(Wanxiangshu|Aggregate)\.fsproj$/.test(projectPath))
    .filter((projectPath) => {
      try {
        const text = fs.readFileSync(projectPath, 'utf8')
        if (text.includes('<WanxiangshuEmitProject>')) return false
        return true
      } catch {
        return true
      }
    })
    .sort()
}

function impactPlan({
  mode,
  aggregate,
  projects,
  roots,
  selectedProjects,
  changedPaths,
  reason,
}) {
  const selectedSources = new Set(
    [...selectedProjects].flatMap((projectPath) => projects.get(projectPath).compileItems),
  )
  const compileItems = mode === 'full'
    ? aggregate.compileItems
    : aggregate.compileItems.filter((sourcePath) => selectedSources.has(sourcePath))
  const projectPaths = [...selectedProjects].sort()

  return {
    mode,
    reason,
    changedPaths,
    rootProjectPaths: [...roots].sort(),
    // With the aggregate retired there is no fsproj anchoring the flat
    // impact project — synthesize a stable virtual path so fingerprinting
    // and scratch naming still work, and downstream consumers can
    // `path.dirname(candidatePath)` without special-casing null.
    candidatePath: aggregate.path ?? 'impact-generated/Wanxiangshu.Impact.fsproj',
    projectPath: aggregate.path ?? 'impact-generated/Wanxiangshu.Impact.fsproj',
    candidateBasename: 'Wanxiangshu.Impact.fsproj',
    aggregatePath: aggregate.path,
    projectPaths,
    compileItems,
    projectContents: new Map(projectPaths.map((projectPath) => [projectPath, projects.get(projectPath).rawText])),
    aggregateContent: aggregate.rawText,
  }
}

/**
 * Computes one flat compile input for a set of changed files.
 *
 * Implementation-only .fs changes select the owning locality and its forward
 * closure. Signature changes select every reverse consumer, then union each
 * selected root's forward closure. Toolchain/topology changes and impact sets
 * above fullThreshold select the aggregate input.
 */
export function planImpactCompile({
  changedPaths,
  projectDirectory,
  aggregatePath = null,
  fullThreshold = 0.6,
  isClean = false,
} = {}) {
  if (!Array.isArray(changedPaths) || changedPaths.length === 0) {
    throw new Error('changedPaths must contain at least one path for planImpactCompile')
  }
  if (!(fullThreshold > 0 && fullThreshold <= 1)) {
    throw new Error(`fullThreshold must be within (0, 1], got ${fullThreshold}`)
  }

  const inventory = readImpactInventory({ projectDirectory, aggregatePath })
  // Filesystem-facing pre-pass (the pure planner never touches disk):
  //  - production .fs/.fsi that exists but owns no shard → hard error;
  //    an unowned source must not be silently covered by a full compile.
  //  - changed sources that no longer exist at all (delete/rename) → full
  //    plan so retired JS outputs are cleared, not reused.
  //  - paired .fs bodies whose source contains `inline` / `[<Literal>]` or
  //    that lack a sibling .fsi are signature-risky — treat the change like
  //    an .fsi change and pull every reverse consumer.
  const signatureRiskPaths = new Set()
  const removedSources = []
  const unmappedExisting = []
  for (const changedPath of changedPaths) {
    const normalized = norm(changedPath)
    const extension = path.extname(normalized).toLowerCase()
    if (extension !== '.fs' && extension !== '.fsi') continue
    if (!fs.existsSync(normalized)) {
      removedSources.push(normalized)
      continue
  }
    if (!inventory.sourceOwner.has(normalized)) {
      unmappedExisting.push(normalized)
      continue
  }
    if (extension === '.fs') {
      const signaturePath = normalized.replace(/\.fs$/i, '.fsi')
      const text = fs.readFileSync(normalized, 'utf8')
      if (!inventory.sourceOwner.has(signaturePath)
        || SIGNATURE_RISK_SOURCE_PATTERN.test(text)) {
        signatureRiskPaths.add(normalized)
  }
    }
  }

  if (unmappedExisting.length > 0) {
    throw new Error(
      `Unmapped production source change (declared in no owner shard): ${unmappedExisting.join(', ')}. ` +
      'Register the file in an owner fsproj — a silent full compile must not cover missing ownership.',
    )
  }
  if (removedSources.length > 0) {
    return planImpactFromInventory({
      inventory,
  changedPaths,
      fullThreshold,
      isClean,
      forceFullReason: 'source-graph-change',
    })
  }
  return planImpactFromInventory({
    inventory,
  changedPaths,
    fullThreshold,
    isClean,
    signatureRiskPaths,
  })
   }

// An implementation body that still changes what callers compile against:
// `let inline ...`, `member inline`, `[<Literal>]` constants and `inline fun`
// lambdas are emitted at the call site even when the sibling .fsi is
// unchanged, so the reverse-consumer impact of a signature change applies.
const SIGNATURE_RISK_SOURCE_PATTERN = /\[\s*<\s*Literal[^\]>]*>\s*\]|\b(?:let|member|static\s+member|and)\s+inline\b|\binline\s+fun\b/

/**
 * Reads the on-disk owner topology into an immutable inventory for planning.
 *
 * Owns every filesystem touch of the impact planner: aggregate parse,
 * project discovery, per-project parse, duplicate-compile and
 * outside-topology validation, and the reverse-reference index. The returned
 * inventory is treated as immutable by planImpactFromInventory.
 */
export function readImpactInventory({ projectDirectory, aggregatePath = null } = {}) {
  const aggregateExists = aggregatePath != null && fs.existsSync(norm(aggregatePath))
  const aggregate = aggregateExists
    ? parseAggregateProject(aggregatePath)
    : {
        path: aggregatePath ? norm(aggregatePath) : null,
        dir: aggregatePath ? path.dirname(norm(aggregatePath)) : null,
        rawText: '',
        compileItems: [],
        missing: true,
      }
  const resolvedProjectDirectory = norm(
    projectDirectory ?? (aggregate.dir ?? process.cwd()),
  )
  const projectPaths = discoverOwnerProjects(resolvedProjectDirectory, aggregate.path)
  const projects = new Map(projectPaths.map((projectPath) => [projectPath, parseProjectFile(projectPath)]))
  const sourceOwner = new Map()

  for (const [projectPath, project] of projects) {
    for (const sourcePath of project.compileItems) {
      const existingOwner = sourceOwner.get(sourcePath)
      if (existingOwner) {
        throw new Error(
          `Duplicate Compile item across owner projects: "${sourcePath}" is compiled by both ${existingOwner} and ${projectPath}`,
        )
      }
      sourceOwner.set(sourcePath, projectPath)
    }

    for (const reference of project.references) {
      if (!projects.has(reference)) {
        throw new Error(`Owner project ${projectPath} references project outside owner topology: ${reference}`)
      }
    }
  }

  // WP2 cutover: the shard graph is the single source for the compile set and
  // its order. Even when the aggregate file still exists during migration its
  // job is reduced to drift-checking — the canonical sequence is always
  // derived from declared shard order so the inventory alone can re-emit a
  // correct flat project after the wrapper file is deleted.
  const canonicalItemsBase = canonicalImpactOrder(projectPaths, projects)
  const canonicalOrderFile = norm(path.join(resolvedProjectDirectory, 'compile-order.txt'))
  const canonicalItems = fs.existsSync(canonicalOrderFile)
    ? (() => {
        const lines = fs.readFileSync(canonicalOrderFile, 'utf8')
          .split(/\r?\n/)
          .map((line) => line.trim())
          .filter((line) => line && !line.startsWith('#'))
        const order = lines.map((line) => norm(path.resolve(resolvedProjectDirectory, line)))
        const known = new Set(order)
        const supplement = [...projects.values()]
          .flatMap((proj) => proj.compileItems)
          .filter((item) => !known.has(item))
        return [...order, ...supplement]
      })()
    : canonicalItemsBase
  const declaredAggregateSet = aggregateExists ? new Set(aggregate.compileItems) : null
  aggregate.compileItems = canonicalItems
  aggregate.missing = !aggregateExists

  const reverseReferences = new Map(projectPaths.map((projectPath) => [projectPath, new Set()]))
  for (const [consumerPath, project] of projects) {
    for (const providerPath of project.references) {
      reverseReferences.get(providerPath).add(consumerPath)
    }
  }

  return {
      aggregate,
      aggregateMissing: !aggregateExists,
      declaredAggregateSet,
    projectDirectory: resolvedProjectDirectory,
    projectPaths: Object.freeze([...projectPaths]),
      projects,
    sourceOwner,
    reverseReferences,
  }
  }

/**
 * Canonical compile-item order derived purely from the owner DAG, used when
 * no aggregate fsproj is available to dictate order. Deterministic: DFS
 * post-order over lexically-sorted project keys, each shard contributing its
 * declared `<Compile>` sequence.
 */
function canonicalImpactOrder(projectPaths, projects) {
  const orderedPaths = []
  const visited = new Set()
  const stackFrame = new Set()
  const visit = (projectPath) => {
    if (visited.has(projectPath)) return
    if (stackFrame.has(projectPath)) {
      throw new Error(`ProjectReference cycle detected via ${projectPath}`)
    }
    stackFrame.add(projectPath)
    const references = [...projects.get(projectPath).references].sort((a, b) => {
      const left = projects.get(a)?.path ?? a
      const right = projects.get(b)?.path ?? b
      return left.localeCompare(right)
    })
    for (const reference of references) visit(reference)
    stackFrame.delete(projectPath)
    visited.add(projectPath)
    orderedPaths.push(projectPath)
  }
  for (const projectPath of [...projectPaths].sort()) visit(projectPath)

  const seen = new Set()
  const items = []
  for (const projectPath of orderedPaths) {
    for (const item of projects.get(projectPath).compileItems) {
      if (!seen.has(item)) {
        seen.add(item)
        items.push(item)
      }
    }
  }
  return items
}

/**
 * Pure impact planning over an inventory from readImpactInventory.
 *
 * Performs no filesystem access: requiresFullImpact/toolchain check, signature
 * risk fan-out (reverse consumers), forward closure, aggregate-drift check,
 * and the fullThreshold/clean-build promotion. `signatureRiskPaths` is an
 * optional set of changed .fs paths that must behave like a signature change
 * (inline / [<Literal>] bodies, or a .fs with no paired .fsi). `forceFullReason`
 * promotes the plan to `full` with that reason without walking the graph — used
 * for deleted/renamed production sources whose ownership went stale.
 * Throws the same errors as planImpactCompile for bad
 * changedPaths/fullThreshold, cycles, and aggregate drift.
 */
export function planImpactFromInventory({ inventory, changedPaths, fullThreshold = 0.6, isClean = false, signatureRiskPaths, forceFullReason } = {}) {
  if (!Array.isArray(changedPaths) || changedPaths.length === 0) {
    throw new Error('changedPaths must contain at least one path for planImpactCompile')
  }
  if (!(fullThreshold > 0 && fullThreshold <= 1)) {
    throw new Error(`fullThreshold must be within (0, 1], got ${fullThreshold}`)
  }
  if (!inventory || !inventory.aggregate || !inventory.projects || !inventory.sourceOwner) {
    throw new Error('inventory from readImpactInventory is required for planImpactFromInventory')
  }

  const { aggregate, projects, sourceOwner } = inventory
  const projectPaths = inventory.projectPaths ?? [...projects.keys()]
  const reverseReferences = inventory.reverseReferences ?? (() => {
    const index = new Map(projectPaths.map((projectPath) => [projectPath, new Set()]))
  for (const [consumerPath, project] of projects) {
    for (const providerPath of project.references) {
        index.get(providerPath)?.add(consumerPath)
      }
    }
    return index
  })()

  const normalizedChanges = [...new Set(changedPaths.map((changedPath) => norm(changedPath)))].sort()
  const allProjects = new Set(projectPaths)
  if (normalizedChanges.some((changedPath) => requiresFullImpact(changedPath, aggregate.path))) {
    return impactPlan({
      mode: 'full',
      aggregate,
      projects,
      roots: allProjects,
      selectedProjects: allProjects,
      changedPaths: normalizedChanges,
      reason: 'toolchain-or-project-change',
    })
  }

  if (forceFullReason) {
    return impactPlan({
      mode: 'full',
      aggregate,
      projects,
      roots: allProjects,
      selectedProjects: allProjects,
      changedPaths: normalizedChanges,
      reason: forceFullReason,
    })
  }

  const roots = new Set()

  const addRoot = (projectPath) => {
    if (!roots.has(projectPath)) roots.add(projectPath)
  }

  const addReverseConsumers = (projectPath) => {
    const explored = new Set()
    const pending = [projectPath]
    while (pending.length > 0) {
      const current = pending.pop()
      if (explored.has(current)) {
        continue
      }
      explored.add(current)
      addRoot(current)
      pending.push(...reverseReferences.get(current))
    }
  }

  const signatureRisks = signatureRiskPaths ?? new Set()

  for (const changedPath of normalizedChanges) {
    const ownerProject = sourceOwner.get(changedPath)
    if (!ownerProject) {
      const extension = path.extname(changedPath).toLowerCase()
      if (extension === '.fs' || extension === '.fsi') {
        return impactPlan({
          mode: 'full',
          aggregate,
          projects,
          roots: allProjects,
          selectedProjects: allProjects,
          changedPaths: normalizedChanges,
          reason: 'unmapped-source-change',
        })
      }
      continue
    }

    addRoot(ownerProject)
    const extension = path.extname(changedPath).toLowerCase()
    if (extension === '.fsi' || signatureRisks.has(changedPath)) {
      addReverseConsumers(ownerProject)
    }
  }

  if (roots.size === 0) {
    return impactPlan({
      mode: 'none',
      aggregate,
      projects,
      roots,
      selectedProjects: new Set(),
      changedPaths: normalizedChanges,
      reason: 'no-production-impact',
    })
  }

  const selectedProjects = new Set()
  const visiting = new Set()

  const addForwardClosure = (projectPath, stack) => {
    if (visiting.has(projectPath)) {
      const cycleStart = stack.indexOf(projectPath)
      throw new Error(`ProjectReference cycle detected: ${[...stack.slice(cycleStart), projectPath].join(' -> ')}`)
    }
    if (selectedProjects.has(projectPath)) {
      return
    }

    visiting.add(projectPath)
    stack.push(projectPath)
    for (const reference of projects.get(projectPath).references) {
      addForwardClosure(reference, stack)
    }
    stack.pop()
    visiting.delete(projectPath)
    selectedProjects.add(projectPath)
  }

  for (const rootProject of roots) {
    addForwardClosure(rootProject, [])
  }

  // Membership check: every item in the compiled closure must be one this
  // planner actually derived from the shard graph — either through the
  // declared aggregate (kept while the wrapper file survives) or through the
  // canonical inventory order when the file is gone.
  const declaredSet = inventory.declaredAggregateSet
    ? inventory.declaredAggregateSet
    : new Set(aggregate.compileItems)
  for (const projectPath of selectedProjects) {
    for (const sourcePath of projects.get(projectPath).compileItems) {
      if (!declaredSet.has(sourcePath)) {
        throw new Error(`Impact compile item absent from production inventory: "${sourcePath}" (compiled in ${projectPath})`)
      }
    }
  }

  const selectedProductionCount = [...selectedProjects]
    .flatMap((projectPath) => projects.get(projectPath).compileItems)
    .filter((sourcePath) => sourcePath.endsWith('.fs')).length
  const aggregateProductionCount = aggregate.compileItems.filter((sourcePath) => sourcePath.endsWith('.fs')).length

  if (isClean || selectedProductionCount / aggregateProductionCount > fullThreshold) {
    return impactPlan({
      mode: 'full',
      aggregate,
      projects,
      roots,
      selectedProjects: allProjects,
      changedPaths: normalizedChanges,
      reason: isClean ? 'clean-build' : 'impact-exceeds-full-threshold',
    })
  }

  return impactPlan({
    mode: 'focused',
    aggregate,
    projects,
    roots,
    selectedProjects,
    changedPaths: normalizedChanges,
    reason: 'focused-impact',
  })
}

/**
 * Atomically writes content to filePath only if content changed.
 */
function writeIfChanged(filePath, content) {
  if (fs.existsSync(filePath)) {
    const existing = fs.readFileSync(filePath, 'utf8')
    if (existing === content) {
      return false
    }
  }
  fs.mkdirSync(path.dirname(filePath), { recursive: true })
  const tmpPath = `${filePath}.${process.pid}.${Date.now()}.${Math.random().toString(36).slice(2)}.tmp`
  fs.writeFileSync(tmpPath, content, 'utf8')
  fs.renameSync(tmpPath, filePath)
  return true
}

/**
 * Generates the flat fsproj XML by preserving the aggregate non-Compile XML shell
 * and rewriting kept Compile paths to absolute paths in aggregate document order.
 */
function generateFlatProjectXml(aggregateContent, aggregateDir, aggregatePathForContext, orderedCompileItems) {
  if (!aggregateContent || aggregateContent.trim().length === 0) {
    // Post-W5: no wrapper aggregate — emit a canonical, minimal flat project
    // that names its shared props partner explicitly. The props file next to
    // the shard graph (src/Wanxiangshu/Directory.Build.props) owns package
    // references and toolchain settings; importing it here lets the flat
    // materialized project inherit them without re-declaring.
    const includes = orderedCompileItems
      .map((abs) => `    <Compile Include="${escapeXmlAttr(abs)}"/>`)
      .join('\n')
    // Package imports flow through the project-referenced chain:<br/>
    // the generated Wanxiangshu.Impact name already matches the Wanxiangshu.*
    // item-group condition in src/Wanxiangshu/Directory.Build.props for the
    // repository-local scratch dir, or explicitly via the test-time
    // --props injection for fixture-only runs. Never Import that file inside
    // this XML — props item groups that fire on the MSBuildProjectName would
    // double-evaluate into NU1504 duplicate PackageReference failures.
    return `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Wanxiangshu</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference>
    <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>
  </PropertyGroup>

  <ItemGroup>
${includes}
  </ItemGroup>
</Project>`
  }

  const compileSet = new Set(orderedCompileItems)

  let xml = aggregateContent

  // Rewrite Compile items: keep only those in compileSet, replace Include with absolute path
  xml = xml.replace(/<Compile\b([^>]*?)(?:\/>|>([\s\S]*?)<\/Compile>)/gi, (match, attrs) => {
    const incMatch = attrs.match(/\bInclude=(["'])(.*?)\1/i)
    if (!incMatch) return ''
    const decodedInclude = decodeXmlAttr(incMatch[2], aggregatePathForContext)
    const abs = norm(path.resolve(aggregateDir, decodedInclude))
    if (!compileSet.has(abs)) return ''
    return `<Compile Include="${escapeXmlAttr(abs)}"/>`
  })

  // Strip any ProjectReference tags
  xml = xml.replace(/<ProjectReference\b([^>]*?)(?:\/>|>([\s\S]*?)<\/ProjectReference>)/gi, '')

  // Strip WanxiangshuEmitProject tag so scratch project does not claim emitter identity
  xml = xml.replace(/<WanxiangshuEmitProject\b(?:[^>]*\/>|[^>]*>[\s\S]*?<\/WanxiangshuEmitProject>)/gi, '')

  return xml
}

/**
 * Materializes the flat owner project into a fingerprint-isolated scratch directory.
 *
 * Fingerprint binds: runner schema, candidate path, all closure project paths+contents,
 * aggregate contents, root props, and exact ordered compile item bytes.
 */
export function materializeOwnerCompile(plan, {
  scratchRoot,
  rootPropsPath = DEFAULT_ROOT_PROPS_PATH,
  outputDir,
} = {}) {
  if (!plan || !plan.candidatePath || !plan.projectPaths || !plan.compileItems) {
    throw new Error('Valid plan object is required for materializeOwnerCompile')
  }

  const defaultScratch = outputDir
    ? path.resolve(REPO_ROOT, '.fable-build/output-compile')
    : DEFAULT_SCRATCH_ROOT
  const resolvedScratchRoot = norm(scratchRoot ?? defaultScratch)
  const resolvedRootPropsPath = norm(rootPropsPath)

  if (!fs.existsSync(resolvedRootPropsPath)) {
    throw new Error(`Root Directory.Build.props not found at: ${resolvedRootPropsPath}`)
  }
  const rootPropsContent = fs.readFileSync(resolvedRootPropsPath, 'utf8')

  // Compute SHA-256 fingerprint binding all inputs
  const hasher = crypto.createHash('sha256')
  hasher.update(`schema:${SCHEMA_VERSION}\n`)
  hasher.update(`candidate:${plan.candidatePath}\n`)
  hasher.update(`aggregatePath:${plan.aggregatePath}\n`)
  hasher.update(`aggregateContent:${plan.aggregateContent}\n`)
  hasher.update(`rootPropsPath:${resolvedRootPropsPath}\n`)
  hasher.update(`rootPropsContent:${rootPropsContent}\n`)

  for (const p of plan.projectPaths) {
    hasher.update(`projectPath:${p}\n`)
    const content = plan.projectContents?.get(p) ?? fs.readFileSync(p, 'utf8')
    hasher.update(`projectContent:${content}\n`)
  }

  for (const item of plan.compileItems) {
    if (!fs.existsSync(item)) {
      throw new Error(`Compile source file does not exist: ${item}`)
    }
    hasher.update(`compileItem:${item}\n`)
    const fileBytes = fs.readFileSync(item)
    hasher.update(fileBytes)
    hasher.update('\n')
  }

  const fingerprint = hasher.digest('hex')

  const fingerprintDir = norm(path.join(resolvedScratchRoot, fingerprint))
  const outputCompileInstance = crypto.createHash('sha256').update(fingerprintDir).digest('hex').slice(0, 16)
  // Anchor flat-project output under the source directory; with the wrapper
  // aggregate retired, we no longer have its path to anchor against.
  const flatAnchor = plan.aggregatePath ? path.dirname(plan.aggregatePath) : path.resolve(REPO_ROOT, 'src/Wanxiangshu')
  const generatedProjectDir = outputDir
    ? norm(path.join(flatAnchor, '.fable-build/output-compile', fingerprint, outputCompileInstance))
    : fingerprintDir
  const generatedProjectPath = norm(path.join(generatedProjectDir, plan.candidateBasename))
  const scratchPropsPath = norm(path.join(generatedProjectDir, 'Directory.Build.props'))

  const projectName = path.basename(plan.candidateBasename, path.extname(plan.candidateBasename))
  const assetsPath = norm(path.join(fingerprintDir, 'artifacts', 'obj', projectName, 'project.assets.json'))
  const finalOutputDir = outputDir ? norm(outputDir) : norm(path.join(fingerprintDir, 'out'))
  const markerPath = norm(path.join(fingerprintDir, '.success'))

  // Generate flat fsproj XML
  // With the aggregate retired, aggregateContent is empty: we emit a
  // synthetic flat project (Sdk + canonical absolute items). The aggregatePath
  // argument only feeds path resolution for rewritten `Include` attrs in the
  // legacy content branch — pass the scratch dir as the anchor either way.
  const flatXml = generateFlatProjectXml(plan.aggregateContent, flatAnchor, plan.aggregatePath ?? flatAnchor, plan.compileItems)

  // Generate scratch Directory.Build.props setting isolated ArtifactsDir then importing root props
  const artifactRoot = outputDir
    ? `${norm(path.join(fingerprintDir, 'artifacts'))}/`
    : '$(MSBuildThisFileDirectory)artifacts/'
  // W5: the canonical flat project sits under a scratch dir, far from
  // src/Wanxiangshu's walk-up props chain. Import the production-level
  // props explicitly so Wanxiangshu.Impact picks up the same package
  // references every shard does. When a test fixture supplies a custom
  // --props this file IS the shared source partner (no extra hop needed).
  const sourceRootDefaultProps = norm(path.resolve(REPO_ROOT, 'src/Wanxiangshu/Directory.Build.props'))
  const sourcePropsPath = resolvedRootPropsPath === norm(DEFAULT_ROOT_PROPS_PATH)
    ? sourceRootDefaultProps
    : resolvedRootPropsPath
  const sourcePropsImport = fs.existsSync(sourcePropsPath) && sourcePropsPath !== resolvedRootPropsPath
    ? `  <Import Project="${escapeXmlAttr(sourcePropsPath)}" />\n`
    : ''
  // W5: generated flat project sits under scratch — it does not inherit
  // the props chain that would normally resolve walk-up from src/Wanxiangshu.
  // Import the production-level props explicitly so Wanxiangshu.Impact picks
  // up packages (FSharp.Core/Fable.Core/etc.) the same way shard projects do.
  const scratchPropsContent = `<Project>
  <PropertyGroup>
    <ArtifactsDir>${escapeXmlAttr(artifactRoot)}</ArtifactsDir>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
${sourcePropsImport}  <Import Project="${escapeXmlAttr(resolvedRootPropsPath)}" />
</Project>
`
  // Write if changed
  writeIfChanged(generatedProjectPath, flatXml)
  writeIfChanged(scratchPropsPath, scratchPropsContent)

  return {
    projectPath: generatedProjectPath,
    outputPath: finalOutputDir,
    assetsPath,
    markerPath,
    successMarkerPath: markerPath,
    fingerprint,
    scratchDir: fingerprintDir,
    projectDir: generatedProjectDir,
    candidateBasename: plan.candidateBasename,
  }
}

/**
 * Recursively checks if a directory contains at least one emitted JavaScript file.
 */
export function hasEmittedJsFiles(dir) {
  if (!dir || !fs.existsSync(dir)) {
    return false
  }
  try {
    const entries = fs.readdirSync(dir, { withFileTypes: true })
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name)
      if (entry.isFile()) {
        const ext = path.extname(entry.name).toLowerCase()
        if (ext === '.js' || ext === '.mjs' || ext === '.cjs') {
          return true
        }
      } else if (entry.isDirectory()) {
        if (hasEmittedJsFiles(fullPath)) {
          return true
        }
      }
    }
  } catch {
    return false
  }
  return false
}

/**
 * Validates whether the fingerprint-bound success marker exists and matches.
 */
export function isSuccessMarkerValid(markerPath, expectedFingerprint, outputDir) {
  if (!markerPath || !fs.existsSync(markerPath)) {
    return false
  }
  try {
    const raw = fs.readFileSync(markerPath, 'utf8').trim()
    let parsed
    try {
      parsed = JSON.parse(raw)
    } catch {
      parsed = { fingerprint: raw }
    }
    if (parsed.fingerprint !== expectedFingerprint) {
      return false
    }
    if (parsed.schema && parsed.schema !== SCHEMA_VERSION) {
      return false
    }
  } catch {
    return false
  }
  return hasEmittedJsFiles(outputDir)
}

/**
 * Atomically writes the success marker for the given fingerprint.
 */
export function writeSuccessMarker(markerPath, fingerprint) {
  const content = JSON.stringify(
    {
      schema: SCHEMA_VERSION,
      fingerprint,
    },
    null,
    2,
  )
  fs.mkdirSync(path.dirname(markerPath), { recursive: true })
  const tmpPath = `${markerPath}.${process.pid}.${Date.now()}.${Math.random().toString(36).slice(2)}.tmp`
  fs.writeFileSync(tmpPath, content, 'utf8')
  fs.renameSync(tmpPath, markerPath)
}

/**
 * Removes the success marker file if it exists.
 */
export function removeSuccessMarker(markerPath) {
  if (markerPath && fs.existsSync(markerPath)) {
    try {
      fs.rmSync(markerPath, { force: true })
    } catch {
      // ignore
    }
  }
}

/**
 * Removes output directory recursively while retaining isolated restore assets.
 */
export function removeOutputDirectory(outputDir) {
  if (outputDir && fs.existsSync(outputDir)) {
    try {
      fs.rmSync(outputDir, { recursive: true, force: true })
    } catch {
      // ignore
    }
  }
}

export function resetOutputDirectory(outputDir) {
  if (!outputDir) throw new Error('outputDir is required')
  fs.rmSync(outputDir, { recursive: true, force: true })
  fs.mkdirSync(outputDir, { recursive: true })
}

/**
 * Compiles an owner project using the flattened projection.
 *
 * Spawns `dotnet tool run fable -- <generated> -c Debug -o <output> --noGitignore`;
 * appends `--noRestore` only when isolated assets exist.
 * Propagates compiler status and prints one concise success line on completion.
 */
export async function compileOwnerProject({
  projectPath,
  aggregatePath = null,
  scratchRoot,
  rootPropsPath = DEFAULT_ROOT_PROPS_PATH,
  outputDir,
  stdio = 'inherit',
  env = process.env,
  spawn = nodeSpawn,
  compilePlan,
} = {}) {
  const plan = compilePlan ?? planOwnerCompile({ projectPath, aggregatePath })
  const materialized = materializeOwnerCompile(plan, { scratchRoot, rootPropsPath, outputDir })

  // Check if success marker is valid for the computed fingerprint and output contains JS
  const isWarm = isSuccessMarkerValid(
    materialized.markerPath,
    materialized.fingerprint,
    materialized.outputPath,
  )
  const isScratchOutput = materialized.outputPath.startsWith(materialized.scratchDir)

  if (!isWarm) {
    // Missing or invalid marker: delete scratch output directory recursively while retaining isolated restore assets
    removeSuccessMarker(materialized.markerPath)
    if (isScratchOutput) {
      removeOutputDirectory(materialized.outputPath)
    }
    fs.mkdirSync(materialized.outputPath, { recursive: true })
  } else {
    fs.mkdirSync(materialized.outputPath, { recursive: true })
  }

  const hasAssets = fs.existsSync(materialized.assetsPath)

  // Cache discipline:
  // - Focused plans live under a fingerprint-addressed scratch dir — the
  //   input identity pins the cache correctly, so Fable's own cache is safe
  //   to reuse. Dropping --noCache here is the actual speedup the spec asks
  //   for: the fingerprint already rules out stale reuse.
  // - Forced `clean` builds still pass --noCache so a changed fingerprint
  //   plus cache rebuild can't sneak a stale cache entry past verification.
  const args = [
    'tool',
    'run',
    'fable',
    '--',
    materialized.projectPath,
    '-c',
    'Debug',
    '-o',
    materialized.outputPath,
    '--noGitignore',
  ]

  if (plan.forceCompileCache === false) {
    args.push('--noCache')
  }

  if (hasAssets) {
    args.push('--noRestore')
  }

  const startTime = Date.now()

  let stdout = ''
  let stderr = ''

  let result
  try {
    const child = spawn('dotnet', args, {
      cwd: REPO_ROOT,
      stdio,
      env,
    })

    if (child && typeof child.then === 'function') {
      const awaited = await child
      result = {
        code: typeof awaited?.code === 'number' ? awaited.code : (awaited?.status ?? 0),
        signal: awaited?.signal ?? null,
      }
      if (typeof awaited?.stdout === 'string') stdout = awaited.stdout
      if (typeof awaited?.stderr === 'string') stderr = awaited.stderr
    } else {
      result = await new Promise((resolve, reject) => {
        if (!child) {
          resolve({ code: 1, signal: null })
          return
        }

        if (child.stdout && typeof child.stdout.on === 'function') {
          child.stdout.setEncoding?.('utf8')
          child.stdout.on('data', (chunk) => {
            stdout += chunk
          })
        }
        if (child.stderr && typeof child.stderr.on === 'function') {
          child.stderr.setEncoding?.('utf8')
          child.stderr.on('data', (chunk) => {
            stderr += chunk
          })
        }

        if (typeof child.on === 'function') {
          child.on('error', reject)
          child.on('close', (code, signal) => {
            resolve({ code: code ?? (signal ? 1 : 0), signal: signal ?? null })
          })
        } else if (typeof child.status === 'number' || typeof child.code === 'number') {
          resolve({ code: child.code ?? child.status ?? 0, signal: child.signal ?? null })
        } else {
          resolve({ code: 0, signal: null })
        }
      })
    }
  } catch (err) {
    removeSuccessMarker(materialized.markerPath)
    if (isScratchOutput) {
      removeOutputDirectory(materialized.outputPath)
    }
    throw err
  }

  const elapsedMs = Date.now() - startTime

  let ok = result.code === 0 && !result.signal

  if (ok) {
    // Code 0: require at least one emitted .js recursively
    const hasJs = hasEmittedJsFiles(materialized.outputPath)
    if (!hasJs) {
      ok = false
      result.code = 1
      removeSuccessMarker(materialized.markerPath)
      if (isScratchOutput) {
        removeOutputDirectory(materialized.outputPath)
      }
    } else {
      // Atomically write marker
      writeSuccessMarker(materialized.markerPath, materialized.fingerprint)
    }
  } else {
    // Nonzero or signal: remove marker and partial scratch output
    removeSuccessMarker(materialized.markerPath)
    if (isScratchOutput) {
      removeOutputDirectory(materialized.outputPath)
    }
  }

  if (ok && stdio !== 'pipe') {
    const shortFp = materialized.fingerprint.slice(0, 12)
    const restoreNote = hasAssets ? 'noRestore' : 'restored'
    console.log(`[owner-compile] OK: ${materialized.candidateBasename} in ${elapsedMs}ms (${restoreNote}, fp:${shortFp})`)
  }

  return {
    ok,
    code: result.code ?? (ok ? 0 : 1),
    signal: result.signal ?? null,
    stdout,
    stderr,
    projectPath: materialized.projectPath,
    outputPath: materialized.outputPath,
    assetsPath: materialized.assetsPath,
    scratchDir: materialized.scratchDir,
    markerPath: materialized.markerPath,
    successMarkerPath: materialized.successMarkerPath,
    fingerprint: materialized.fingerprint,
    elapsedMs,
    cached: false,
  }
}

/**
 * Computes SHA-256 hash for a given file path.
 */
export function computeFileHash(filePath) {
  const content = fs.readFileSync(filePath)
  return crypto.createHash('sha256').update(content).digest('hex')
}

/**
 * Collects all tracked production and configuration inputs for incremental build tracking.
 */
export function collectTrackedInputs({
  root = REPO_ROOT,
  aggregatePath = null,
  projectDirectory,
} = {}) {
  const resolvedAggregate = aggregatePath ? norm(aggregatePath) : null
  const resolvedProjectDirectory = norm(
    projectDirectory ?? (resolvedAggregate ? path.dirname(resolvedAggregate) : path.resolve(root, 'src/Wanxiangshu')),
  )
  // WP2: the source list derives from the shard inventory — when the
  // aggregate wrapper file exists it still exists on disk and is hashed
  // (drift guard), but compile sources compile from shard declarations.
  const projectPaths = discoverOwnerProjects(resolvedProjectDirectory, resolvedAggregate)
  const projects = new Map(projectPaths.map((p) => [p, parseProjectFile(p)]))
  const sourcePaths = canonicalImpactOrder(projectPaths, projects)

  const tracked = new Set()
  if (resolvedAggregate && fs.existsSync(resolvedAggregate)) {
    tracked.add(resolvedAggregate)
  }

  for (const item of sourcePaths) {
    tracked.add(item)
  }

  for (const proj of projectPaths) {
    tracked.add(proj)
  }

  const configCandidates = [
    path.resolve(root, 'Directory.Build.props'),
    path.resolve(root, 'Directory.Build.targets'),
    path.resolve(root, 'package.json'),
    path.resolve(root, 'package-lock.json'),
    path.resolve(root, '.config/dotnet-tools.json'),
    path.resolve(root, 'scripts/build.mjs'),

    path.resolve(root, 'scripts/lib/owner-compile.mjs'),
    path.resolve(resolvedProjectDirectory, 'Directory.Build.props'),
  ]

  for (const config of configCandidates) {
    if (fs.existsSync(config)) {
      tracked.add(norm(config))
    }
  }

  return [...tracked].sort()
}

/**
 * Detects modified, added, or removed inputs by comparing against the recorded build manifest.
 */
export function detectChangedFiles({
  root = REPO_ROOT,
  aggregatePath = null,
  manifestPath = DEFAULT_BUILD_MANIFEST_PATH,
  outputDir,
} = {}) {
  const resolvedOutputDir = norm(outputDir ?? path.resolve(root, 'dist'))
  const resolvedManifestPath = norm(manifestPath)
  const trackedFiles = collectTrackedInputs({ root, aggregatePath })

  let manifest = null
  if (fs.existsSync(resolvedManifestPath)) {
    try {
      manifest = JSON.parse(fs.readFileSync(resolvedManifestPath, 'utf8'))
    } catch {
      manifest = null
    }
  }

  let hasOutputs = false
  if (fs.existsSync(resolvedOutputDir) && hasEmittedJsFiles(resolvedOutputDir)) {
    if (manifest && manifest.outputs && typeof manifest.outputs === 'object') {
      // Full dist walk comparison against manifest.outputs
      const currentOutputs = {}
      const walkFiles = (dir) => {
        const entries = fs.readdirSync(dir, { withFileTypes: true })
        for (const entry of entries) {
          const fullPath = path.join(dir, entry.name)
          if (entry.isDirectory()) {
            walkFiles(fullPath)
          } else if (entry.isFile()) {
            const rel = path.relative(resolvedOutputDir, fullPath).replace(/\\/g, '/')
            const stat = fs.statSync(fullPath)
            const hash = computeFileHash(fullPath)
            currentOutputs[rel] = [hash, stat.size, stat.mtimeMs]
          }
        }
      }
      try {
        walkFiles(resolvedOutputDir)
        const recordedKeys = Object.keys(manifest.outputs)
        const currentKeys = Object.keys(currentOutputs)
        if (
          recordedKeys.length > 0 &&
          recordedKeys.length === currentKeys.length &&
          recordedKeys.every((k) => currentOutputs[k] && currentOutputs[k][0] === manifest.outputs[k][0])
        ) {
          hasOutputs = true
        }
      } catch {
        hasOutputs = false
      }
    } else {
      const essentialOutputs = [
        path.join(resolvedOutputDir, 'OpenCode/Plugin/Plugin.js'),
        path.join(resolvedOutputDir, 'Sphinx/ServeEntry.js'),
      ]
      const isProductionOutput = resolvedOutputDir === norm(path.resolve(root, 'dist'))
      hasOutputs = !isProductionOutput || essentialOutputs.every((p) => fs.existsSync(p))
    }
  }

  if (!manifest || manifest.schema !== SCHEMA_VERSION || !hasOutputs) {
    const currentFiles = {}
    for (const file of trackedFiles) {
      if (fs.existsSync(file)) {
        const stat = fs.statSync(file)
        const hash = computeFileHash(file)
        currentFiles[file] = { mtimeMs: stat.mtimeMs, size: stat.size, hash }
      }
    }
    return {
      changedPaths: trackedFiles,
      isCleanBuild: true,
      manifest: null,
      currentFiles,
    }
  }

  const oldFiles = manifest.files ?? {}
  const currentFiles = {}
  const changedPaths = []

  for (const file of trackedFiles) {
    if (!fs.existsSync(file)) {
      changedPaths.push(file)
      continue
    }

    const stat = fs.statSync(file)
    const oldEntry = oldFiles[file]
    const hash = computeFileHash(file)

    currentFiles[file] = { mtimeMs: stat.mtimeMs, size: stat.size, hash }

    if (!oldEntry || oldEntry.hash !== hash) {
      changedPaths.push(file)
    }
  }

  // Check for deleted files that were in manifest
  for (const oldFile of Object.keys(oldFiles)) {
    if (!currentFiles[oldFile] && !fs.existsSync(oldFile)) {
      changedPaths.push(oldFile)
    }
  }

  return {
    changedPaths: [...new Set(changedPaths)].sort(),
    isCleanBuild: false,
    manifest,
    currentFiles,
  }
}

/**
 * Executes automatic freshness-driven incremental compilation.
 */
export async function compileIncremental({
  changedPaths,
  root = REPO_ROOT,
  aggregatePath = null,
  outputDir,
  scratchRoot,
  rootPropsPath = DEFAULT_ROOT_PROPS_PATH,
  fullThreshold = 0.6,
  stdio = 'inherit',
  env = process.env,
  spawn = nodeSpawn,
  manifestPath = DEFAULT_BUILD_MANIFEST_PATH,
} = {}) {
  const resolvedOutputDir = outputDir ? norm(outputDir) : undefined
  const targetOutputDir = resolvedOutputDir ?? norm(path.resolve(root, 'dist'))
  const resolvedManifestPath = norm(manifestPath)
  const resolvedAggregate = aggregatePath ? norm(aggregatePath) : null

  let effectiveChangedPaths
  let isClean = false

  if (Array.isArray(changedPaths)) {
    effectiveChangedPaths = [...new Set(changedPaths.map((p) => norm(p)))].sort()
  } else {
    const detection = detectChangedFiles({
      root,
      aggregatePath: resolvedAggregate,
      manifestPath: resolvedManifestPath,
      outputDir: targetOutputDir,
    })
    effectiveChangedPaths = detection.changedPaths
    isClean = detection.isCleanBuild
  }

  const buildSnapshot = () => {
    const tracked = collectTrackedInputs({ root, aggregatePath: resolvedAggregate })
    const map = {}
    for (const f of tracked) {
      if (fs.existsSync(f)) {
        const stat = fs.statSync(f)
        const sha256 = computeFileHash(f)
        map[f] = { path: f, sha256, hash: sha256, mtimeMs: stat.mtimeMs, size: stat.size }
      }
    }
    return map
  }

  // Fast no-op cache hit when no changed paths
  if (effectiveChangedPaths.length === 0) {
    const hasJs = hasEmittedJsFiles(targetOutputDir)
    if (hasJs) {
      return {
        ok: true,
        code: 0,
        signal: null,
        mode: 'cached',
        reason: 'no-changes-detected',
        changedPaths: [],
        compileItems: [],
        elapsedMs: 0,
        cached: true,
        outputPath: targetOutputDir,
        fingerprint: null,
        snapshot: buildSnapshot(),
      }
    }
    // If output is missing despite no changed paths, trigger clean compile
    isClean = true
    effectiveChangedPaths = collectTrackedInputs({ root, aggregatePath: resolvedAggregate })
  }

  const plan = planImpactCompile({
    changedPaths: effectiveChangedPaths,
    aggregatePath: resolvedAggregate,
    fullThreshold,
    projectDirectory: resolvedAggregate ? path.dirname(resolvedAggregate) : path.resolve(root, 'src/Wanxiangshu'),
    isClean,
  })

  // A `--clean` or `full` plan forces Fable to bypass its own cache: the
  // narrower a plan, the more a fingerprint-addressed scratch already pins
  // the inputs and the compiler cache is harmless; the broader the plan the
  // more we want a cold rebuild to re-verify the emitted bytes.
  if (isClean || plan.mode === 'full') {
    plan.forceCompileCache = false
  }

  if (plan.mode === 'none') {
    return {
      ok: true,
      code: 0,
      signal: null,
      mode: 'none',
      reason: plan.reason,
      changedPaths: effectiveChangedPaths,
      compileItems: [],
      elapsedMs: 0,
      cached: true,
      outputPath: targetOutputDir,
      fingerprint: null,
      snapshot: buildSnapshot(),
    }
  }

  // For clean build, wipe and recreate output directory so deleted files leave no stale JS
  if (isClean) {
    if (resolvedOutputDir && fs.existsSync(resolvedOutputDir)) {
      fs.rmSync(resolvedOutputDir, { recursive: true, force: true })
    }
    if (resolvedOutputDir) {
      fs.mkdirSync(resolvedOutputDir, { recursive: true })
    }
  } else if (resolvedOutputDir && !fs.existsSync(resolvedOutputDir)) {
    fs.mkdirSync(resolvedOutputDir, { recursive: true })
  }

  const result = await compileOwnerProject({
    compilePlan: plan,
    scratchRoot,
    rootPropsPath,
    outputDir: resolvedOutputDir,
    stdio,
    env,
    spawn,
  })

  return {
    ...result,
    mode: plan.mode,
    reason: plan.reason,
    changedPaths: effectiveChangedPaths,
    compileItems: plan.compileItems,
    snapshot: buildSnapshot(),
  }
}
