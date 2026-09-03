import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn as nodeSpawn } from 'node:child_process'

const MODULE_DIR = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(MODULE_DIR, '../..')

export const SCHEMA_VERSION = 'owner-compile-v3'
export const DEFAULT_AGGREGATE_PATH = path.resolve(REPO_ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj')
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
export function planOwnerCompile({ projectPath, aggregatePath = DEFAULT_AGGREGATE_PATH } = {}) {
  if (!projectPath) {
    throw new Error('projectPath is required for planOwnerCompile')
  }

  const resolvedProjectPath = norm(projectPath)
  const resolvedAggregatePath = norm(aggregatePath)

  const aggregate = parseAggregateProject(resolvedAggregatePath)
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

  // Check that every closure item exists in aggregate
  for (const [src, owner] of sourceToOwnerProject.entries()) {
    if (!aggregateCompileSet.has(src)) {
      throw new Error(`Closure compile item absent from aggregate project: "${src}" (compiled in ${owner})`)
    }
  }

  // Stable-filter aggregate compile document order
  const closureSourcesSet = new Set(sourceToOwnerProject.keys())
  const orderedCompileItems = aggregate.compileItems.filter((src) => closureSourcesSet.has(src))

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
    || /(?:^|\/)scripts\/(?:build|compile-impact)\.mjs$/.test(changedPath)
    || /(?:^|\/)scripts\/lib\/owner-compile\.mjs$/.test(changedPath)
}

function discoverOwnerProjects(projectDirectory, aggregatePath) {
  const resolvedDirectory = norm(projectDirectory)
  if (!fs.existsSync(resolvedDirectory) || !fs.statSync(resolvedDirectory).isDirectory()) {
    throw new Error(`Owner project directory does not exist: ${resolvedDirectory}`)
  }

  return fs.readdirSync(resolvedDirectory, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith('.fsproj'))
    .map((entry) => norm(path.join(resolvedDirectory, entry.name)))
    .filter((projectPath) => projectPath !== aggregatePath)
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
    candidatePath: aggregate.path,
    projectPath: aggregate.path,
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
  aggregatePath = DEFAULT_AGGREGATE_PATH,
  fullThreshold = 0.6,
  isClean = false,
} = {}) {
  if (!Array.isArray(changedPaths) || changedPaths.length === 0) {
    throw new Error('changedPaths must contain at least one path for planImpactCompile')
  }
  if (!(fullThreshold > 0 && fullThreshold <= 1)) {
    throw new Error(`fullThreshold must be within (0, 1], got ${fullThreshold}`)
  }

  const aggregate = parseAggregateProject(aggregatePath)
  const resolvedProjectDirectory = norm(projectDirectory ?? path.dirname(aggregate.path))
  const normalizedChanges = [...new Set(changedPaths.map((changedPath) => norm(changedPath)))].sort()
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

  const reverseReferences = new Map(projectPaths.map((projectPath) => [projectPath, new Set()]))
  for (const [consumerPath, project] of projects) {
    for (const providerPath of project.references) {
      reverseReferences.get(providerPath).add(consumerPath)
    }
  }

  const changedSet = new Set(normalizedChanges)
  const roots = new Set()

  const addReverseConsumers = (projectPath) => {
    const pending = [projectPath]
    while (pending.length > 0) {
      const current = pending.pop()
      if (roots.has(current)) {
        continue
      }
      roots.add(current)
      pending.push(...reverseReferences.get(current))
    }
  }

  for (const changedPath of normalizedChanges) {
    const ownerProject = sourceOwner.get(changedPath)
    if (!ownerProject) {
      if (['.fs', '.fsi'].includes(path.extname(changedPath).toLowerCase())) {
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

    const extension = path.extname(changedPath).toLowerCase()
    const siblingSignature = extension === '.fs' ? `${changedPath.slice(0, -3)}.fsi` : null
    const signatureChanged = extension === '.fsi'
      || (siblingSignature !== null && changedSet.has(siblingSignature))
      || (siblingSignature !== null && !fs.existsSync(siblingSignature))

    if (signatureChanged) {
      addReverseConsumers(ownerProject)
    } else {
      roots.add(ownerProject)
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

  const aggregateSet = new Set(aggregate.compileItems)
  for (const projectPath of selectedProjects) {
    for (const sourcePath of projects.get(projectPath).compileItems) {
      if (!aggregateSet.has(sourcePath)) {
        throw new Error(`Impact compile item absent from aggregate project: "${sourcePath}" (compiled in ${projectPath})`)
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
function generateFlatProjectXml(aggregateContent, aggregatePath, orderedCompileItems) {
  const aggregateDir = path.dirname(aggregatePath)
  const compileSet = new Set(orderedCompileItems)

  let xml = aggregateContent

  // Rewrite Compile items: keep only those in compileSet, replace Include with absolute path
  xml = xml.replace(/<Compile\b([^>]*?)(?:\/>|>([\s\S]*?)<\/Compile>)/gi, (match, attrs) => {
    const incMatch = attrs.match(/\bInclude=(["'])(.*?)\1/i)
    if (!incMatch) return ''
    const decodedInclude = decodeXmlAttr(incMatch[2], aggregatePath)
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
    ? path.resolve(path.dirname(plan.aggregatePath), '.fable-build')
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
  const generatedProjectPath = norm(path.join(fingerprintDir, plan.candidateBasename))
  const scratchPropsPath = norm(path.join(fingerprintDir, 'Directory.Build.props'))

  const projectName = path.basename(plan.candidateBasename, path.extname(plan.candidateBasename))
  const assetsPath = norm(path.join(fingerprintDir, 'artifacts', 'obj', projectName, 'project.assets.json'))
  const finalOutputDir = outputDir ? norm(outputDir) : norm(path.join(fingerprintDir, 'out'))
  const markerPath = norm(path.join(fingerprintDir, '.success'))

  // Generate flat fsproj XML
  const flatXml = generateFlatProjectXml(plan.aggregateContent, plan.aggregatePath, plan.compileItems)

  // Generate scratch Directory.Build.props setting isolated ArtifactsDir then importing root props
  const scratchPropsContent = `<Project>
  <PropertyGroup>
    <ArtifactsDir>$(MSBuildThisFileDirectory)artifacts/</ArtifactsDir>
  </PropertyGroup>
  <Import Project="${escapeXmlAttr(resolvedRootPropsPath)}" />
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

/**
 * Compiles an owner project using the flattened projection.
 *
 * Spawns `dotnet tool run fable -- <generated> -c Debug -o <output> --noGitignore`;
 * appends `--noRestore` only when isolated assets exist.
 * Propagates compiler status and prints one concise success line on completion.
 */
export async function compileOwnerProject({
  projectPath,
  aggregatePath = DEFAULT_AGGREGATE_PATH,
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
    '--noCache',
  ]

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
  aggregatePath = DEFAULT_AGGREGATE_PATH,
  projectDirectory,
} = {}) {
  const resolvedAggregate = norm(aggregatePath)
  const resolvedProjectDirectory = norm(projectDirectory ?? path.dirname(resolvedAggregate))
  const aggregate = parseAggregateProject(resolvedAggregate)
  const projectPaths = discoverOwnerProjects(resolvedProjectDirectory, resolvedAggregate)

  const tracked = new Set()
  tracked.add(resolvedAggregate)

  for (const item of aggregate.compileItems) {
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
    path.resolve(root, 'scripts/compile-impact.mjs'),
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
  aggregatePath = DEFAULT_AGGREGATE_PATH,
  manifestPath = DEFAULT_BUILD_MANIFEST_PATH,
  outputDir,
} = {}) {
  const resolvedOutputDir = norm(outputDir ?? path.resolve(root, 'dist'))
  const resolvedManifestPath = norm(manifestPath)
  const trackedFiles = collectTrackedInputs({ root, aggregatePath })

  const essentialOutputs = [
    path.join(resolvedOutputDir, 'OpenCode/Plugin/Plugin.js'),
    path.join(resolvedOutputDir, 'Sphinx/McpServer.js'),
  ]

  const isProductionOutput = resolvedOutputDir === norm(path.resolve(root, 'dist'))
  const hasOutputs = fs.existsSync(resolvedOutputDir)
    && hasEmittedJsFiles(resolvedOutputDir)
    && (!isProductionOutput || essentialOutputs.every((p) => fs.existsSync(p)))

  let manifest = null
  if (fs.existsSync(resolvedManifestPath)) {
    try {
      manifest = JSON.parse(fs.readFileSync(resolvedManifestPath, 'utf8'))
    } catch {
      manifest = null
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
  aggregatePath = DEFAULT_AGGREGATE_PATH,
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
  const resolvedAggregate = norm(aggregatePath)

  let effectiveChangedPaths
  let isClean = false
  let currentFilesCache = null

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
    currentFilesCache = detection.currentFiles
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
    projectDirectory: path.dirname(resolvedAggregate),
    isClean,
  })

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

  if (result.ok) {
    // Record successful build manifest
    try {
      const files = currentFilesCache ?? (() => {
        const tracked = collectTrackedInputs({ root, aggregatePath: resolvedAggregate })
        const map = {}
        for (const f of tracked) {
          if (fs.existsSync(f)) {
            const stat = fs.statSync(f)
            map[f] = { mtimeMs: stat.mtimeMs, size: stat.size, hash: computeFileHash(f) }
          }
        }
        return map
      })()

      const manifestPayload = JSON.stringify({
        schema: SCHEMA_VERSION,
        timestamp: Date.now(),
        aggregatePath: resolvedAggregate,
        outputDir: result.outputPath,
        mode: plan.mode,
        files,
      }, null, 2)

      fs.mkdirSync(path.dirname(resolvedManifestPath), { recursive: true })
      const tmpPath = `${resolvedManifestPath}.${process.pid}.${Date.now()}.${Math.random().toString(36).slice(2)}.tmp`
      fs.writeFileSync(tmpPath, manifestPayload, 'utf8')
      fs.renameSync(tmpPath, resolvedManifestPath)
    } catch {
      // Manifest write non-fatal
    }
  }

  return {
    ...result,
    mode: plan.mode,
    reason: plan.reason,
    changedPaths: effectiveChangedPaths,
    compileItems: plan.compileItems,
  }
}
