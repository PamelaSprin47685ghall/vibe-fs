import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'

const MODULE_DIR = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(MODULE_DIR, '../..')

export const SCHEMA_VERSION = 'owner-compile-v2'
export const DEFAULT_AGGREGATE_PATH = path.resolve(REPO_ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj')
export const DEFAULT_SCRATCH_ROOT = path.resolve(REPO_ROOT, '.fable-build/owner-compile')
export const DEFAULT_ROOT_PROPS_PATH = path.resolve(REPO_ROOT, 'Directory.Build.props')

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
  scratchRoot = DEFAULT_SCRATCH_ROOT,
  rootPropsPath = DEFAULT_ROOT_PROPS_PATH,
  outputDir,
} = {}) {
  if (!plan || !plan.candidatePath || !plan.projectPaths || !plan.compileItems) {
    throw new Error('Valid plan object is required for materializeOwnerCompile')
  }

  const resolvedScratchRoot = norm(scratchRoot)
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
    fingerprint,
    scratchDir: fingerprintDir,
    candidateBasename: plan.candidateBasename,
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
  scratchRoot = DEFAULT_SCRATCH_ROOT,
  rootPropsPath = DEFAULT_ROOT_PROPS_PATH,
  outputDir,
  stdio = 'inherit',
  env = process.env,
} = {}) {
  const plan = planOwnerCompile({ projectPath, aggregatePath })
  const materialized = materializeOwnerCompile(plan, { scratchRoot, rootPropsPath, outputDir })

  fs.mkdirSync(materialized.outputPath, { recursive: true })

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
  ]

  if (hasAssets) {
    args.push('--noRestore')
  }

  const startTime = Date.now()

  let stdout = ''
  let stderr = ''

  const result = await new Promise((resolve, reject) => {
    const child = spawn('dotnet', args, {
      cwd: REPO_ROOT,
      stdio,
      env,
    })

    if (child.stdout) {
      child.stdout.setEncoding('utf8')
      child.stdout.on('data', (chunk) => {
        stdout += chunk
      })
    }
    if (child.stderr) {
      child.stderr.setEncoding('utf8')
      child.stderr.on('data', (chunk) => {
        stderr += chunk
      })
    }

    child.on('error', reject)
    child.on('close', (code, signal) => {
      resolve({ code: code ?? (signal ? 1 : 0), signal })
    })
  })

  const elapsedMs = Date.now() - startTime
  const ok = result.code === 0

  if (ok && stdio !== 'pipe') {
    const shortFp = materialized.fingerprint.slice(0, 12)
    const restoreNote = hasAssets ? 'noRestore' : 'restored'
    console.log(`[owner-compile] OK: ${materialized.candidateBasename} in ${elapsedMs}ms (${restoreNote}, fp:${shortFp})`)
  }

  return {
    ok,
    code: result.code,
    signal: result.signal,
    stdout,
    stderr,
    projectPath: materialized.projectPath,
    outputPath: materialized.outputPath,
    assetsPath: materialized.assetsPath,
    fingerprint: materialized.fingerprint,
    elapsedMs,
  }
}
