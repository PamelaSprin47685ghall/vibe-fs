import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { privateDotnetEnvironment } from './verification-dotnet-environment.mjs'
import { verificationInputExists } from './verification-input-path.mjs'
import { captureVerificationNugetArchives } from './verification-nuget-archives.mjs'
import { captureOrdinaryVerificationFiles } from './verification-ordinary-files.mjs'
import { runVerificationToolProbe } from './verification-tool-probe.mjs'

const hash = (algorithm, bytes, encoding = 'hex') => createHash(algorithm).update(bytes).digest(encoding)
const invalidEntry = message => Object.assign(new Error(message), { code: 'verification-nuget-project-entry-invalid' })
const integrityError = message => Object.assign(new Error(message), { code: 'verification-nuget-project-integrity-invalid' })
const unsupported = message => Object.assign(new Error(message), { code: 'verification-nuget-project-unsupported' })
const ordinaryPath = value => typeof value === 'string' && value !== '' && !path.posix.isAbsolute(value) && value.split('/').every(segment => segment && segment !== '.' && segment !== '..' && !segment.includes('\\') && !segment.includes(':'))
const objectMap = value => value !== null && typeof value === 'object' && !Array.isArray(value)

function dependencyEdges(value, selected) {
  if (value === undefined) return []
  if (!objectMap(value)) throw invalidEntry('Package dependencies require an object map')
  const edges = Object.entries(value).map(([id, version]) => {
    if (!selected.some(pkg => pkg.id === id.toLowerCase()) || typeof version !== 'string' || !version) throw invalidEntry('Package dependency edge escapes the selected closure')
    return [id.toLowerCase(), version]
  }).sort(([left], [right]) => left.localeCompare(right, 'en'))
  if (new Set(edges.map(([id]) => id)).size !== edges.length) throw invalidEntry('Package dependency identities must be distinct')
  return edges
}

function readJson(file) {
  let parsed
  try {
    parsed = JSON.parse(fs.readFileSync(file, 'utf8'))
  } catch (cause) {
    if (cause instanceof SyntaxError || cause.code === 'ENOENT') throw Object.assign(invalidEntry(`Restore requires complete JSON: ${file}`), { cause })
    throw cause
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw invalidEntry('Restore JSON requires an object')
  return parsed
}

function requireOwnedFile(root, relative) {
  if (!ordinaryPath(relative) || !verificationInputExists(root, path.join(root, relative)) || !fs.lstatSync(path.join(root, relative)).isFile()) throw invalidEntry(`Restore requires an ordinary owned file: ${relative}`)
  return path.join(root, relative)
}

function privateGraph(root, projectFile, assetsPath, lockPath, selected) {
  const assets = readJson(requireOwnedFile(root, assetsPath))
  const lock = readJson(requireOwnedFile(root, lockPath))
  const packagesRoot = path.join(root, 'packages')
  const feed = path.join(root, 'feed')
  const restore = assets.project?.restore
  if (![3, 4].includes(assets.version) || lock.version !== 1 || !objectMap(restore)) throw invalidEntry('Restore requires the supported assets and lock protocols')
  if (!objectMap(assets.targets) || !objectMap(lock.dependencies) || !objectMap(restore.frameworks)) throw invalidEntry('Restore requires target and framework dependency maps')
  const sole = (values, expected) => JSON.stringify(values) === JSON.stringify([expected])
  if (!sole(Object.keys(assets.packageFolders ?? {}), packagesRoot) || !sole(Object.keys(restore.sources ?? {}), feed) || !sole(restore.configFilePaths, path.join(root, 'NuGet.Config')) || (restore.fallbackFolders ?? []).length !== 0) throw invalidEntry('Restore borrows a source, configuration or package directory outside its owned inputs')
  if (restore.outputPath !== path.dirname(path.join(root, assetsPath)) + path.sep) throw invalidEntry('Restore output directory differs from its owned assets role')
  if (restore.projectPath !== projectFile || !sole(Object.keys(restore.frameworks ?? {}), 'net10.0') || !sole(Object.keys(assets.targets ?? {}), 'net10.0') || !sole(Object.keys(lock.dependencies ?? {}), 'net10.0')) throw unsupported('Restore supports only the evaluated single net10.0 project')
  if (!objectMap(assets.targets['net10.0']) || !objectMap(lock.dependencies['net10.0']) || !objectMap(restore.frameworks['net10.0'])) throw invalidEntry('Restore requires ordinary framework target and dependency maps')
  if (JSON.stringify(restore.frameworks['net10.0'].projectReferences) !== '{}') throw unsupported('Restore does not accept project references')
  const libraries = assets.libraries
  if (!libraries || typeof libraries !== 'object' || Array.isArray(libraries)) throw invalidEntry('Assets require a package library map')
  const expected = selected.map(pkg => `${pkg.id}/${pkg.version}`).sort()
  const actual = []
  for (const [name, library] of Object.entries(libraries)) {
    if (!objectMap(library)) throw invalidEntry('Assets require ordinary package library objects')
    if (library.type !== 'package') throw unsupported('Restore does not accept project libraries')
    const selectedPackage = selected.find(pkg => name.toLowerCase() === `${pkg.id}/${pkg.version}`)
    if (!selectedPackage || library.path !== `${selectedPackage.id}/${selectedPackage.version}`) throw invalidEntry('Assets package identity differs from the selected archives')
    const pkg = selectedPackage
    actual.push(`${pkg.id}/${pkg.version}`)
    const packageRelative = `packages/${pkg.id}/${pkg.version}`
    const archive = requireOwnedFile(root, `${packageRelative}/${pkg.id}.${pkg.version}.nupkg`)
    const sidecar = requireOwnedFile(root, `${packageRelative}/${pkg.id}.${pkg.version}.nupkg.sha512`)
    const metadata = readJson(requireOwnedFile(root, `${packageRelative}/.nupkg.metadata`))
    if (hash('sha512', fs.readFileSync(archive), 'base64') !== pkg.sha512 || fs.readFileSync(sidecar, 'utf8').trim() !== pkg.sha512) throw integrityError('Restored package archive differs from its selected full-byte identity')
    const locked = Object.entries(lock.dependencies['net10.0']).find(([id]) => id.toLowerCase() === pkg.id)?.[1]
    if (!locked || !['Direct', 'Transitive'].includes(locked.type) || locked.resolved !== pkg.version || typeof library.sha512 !== 'string' || !library.sha512 || locked.contentHash !== library.sha512 || metadata.contentHash !== library.sha512 || metadata.source !== feed) throw invalidEntry('Actual assets, lock and package metadata disagree on their content identity')
    if (!Array.isArray(library.files) || !library.files.length) throw invalidEntry('Assets require a complete library file list')
    for (const member of library.files) requireOwnedFile(root, `${packageRelative}/${member}`)
    const target = assets.targets['net10.0'][name]
    if (!objectMap(target) || target.type !== 'package') throw invalidEntry('Target package identity differs from its library')
    if (JSON.stringify(dependencyEdges(target.dependencies, selected)) !== JSON.stringify(dependencyEdges(locked.dependencies, selected))) throw invalidEntry('Target and lock disagree on package dependency edges')
    for (const kind of ['compile', 'runtime']) {
      if (target[kind] !== undefined && !objectMap(target[kind])) throw invalidEntry('Target assets require an object map')
      for (const member of Object.keys(target[kind] ?? {})) requireOwnedFile(root, `${packageRelative}/${member}`)
    }
  }
  if (JSON.stringify(actual.sort()) !== JSON.stringify(expected) || Object.keys(lock.dependencies['net10.0']).length !== expected.length || Object.keys(assets.targets['net10.0']).length !== expected.length) throw invalidEntry('Actual resolved package graph differs from the selected closure')
  const cached = []
  for (const id of fs.readdirSync(packagesRoot).sort()) {
    for (const version of fs.readdirSync(path.join(packagesRoot, id)).sort()) cached.push(`${id}/${version}`)
  }
  if (JSON.stringify(cached.sort()) !== JSON.stringify(expected)) throw invalidEntry('Actual package cache differs from the selected closure')
  return { targetFramework: 'net10.0', targets: assets.targets, libraries: assets.libraries, dependencies: lock.dependencies }
}

export async function prepareVerificationNugetProject({ source, sdk, projectPath, packageArchives, parentDirectory, signal }) {
  signal?.throwIfAborted()
  if (typeof source?.revalidate !== 'function' || typeof sdk?.revalidate !== 'function' || sdk.identityScope !== 'selected-dotnet-sdk-bundle') throw invalidEntry('Project restore requires prepared source and SDK owners')
  source.revalidate()
  sdk.revalidate()
  if (!ordinaryPath(projectPath) || !projectPath.endsWith('.fsproj')) throw invalidEntry('Project restore requires an ordinary source-relative F# project')
  const projectFile = requireOwnedFile(source.sourceRoot, projectPath)
  const globalBytes = fs.readFileSync(requireOwnedFile(source.sourceRoot, 'global.json'))
  if (hash('sha256', globalBytes) !== sdk.globalJsonSha256) throw invalidEntry('Project restore global.json differs from SDK selection')
  if (!ordinaryPath(sdk.dotnet?.path) || !sdk.dotnet.path.startsWith('dotnet-sdk/')) throw invalidEntry('Project restore requires the selected SDK executable')
  const executable = requireOwnedFile(sdk.toolRoot, sdk.dotnet.path)
  if (hash('sha256', fs.readFileSync(executable)) !== sdk.dotnet.sha256) throw integrityError('SDK executable differs from its selected role')
  const selected = captureVerificationNugetArchives(packageArchives, 'verification-nuget-project')
  const { sourceDigest, treeId } = source
  const sdkDigest = sdk.sdkDigest
  const sourceRoot = source.sourceRoot
  const sdkRoot = sdk.toolRoot
  const assertInputs = () => {
    source.revalidate()
    sdk.revalidate()
    if (source.sourceDigest !== sourceDigest || source.treeId !== treeId || source.sourceRoot !== sourceRoot || sdk.sdkDigest !== sdkDigest || sdk.toolRoot !== sdkRoot) throw invalidEntry('Project restore changed its selected input identities')
  }
  const parentRoot = fs.realpathSync(parentDirectory)
  if (!fs.lstatSync(parentRoot).isDirectory()) throw invalidEntry('Project restore requires a real parent directory')
  const projectRoot = fs.mkdtempSync(path.join(parentRoot, 'verification-nuget-project-'))
  try {
    const env = privateDotnetEnvironment(projectRoot, path.join(sdkRoot, 'dotnet-sdk'), executable)
    const options = { cwd: sourceRoot, env, signal }
    const evaluationOutput = await runVerificationToolProbe(executable, ['msbuild', projectFile, '-getItem:ProjectReference', '-getProperty:TargetFramework,TargetFrameworks,MSBuildProjectFullPath'], options)
    signal?.throwIfAborted()
    assertInputs()
    let evaluated
    try {
      evaluated = JSON.parse(evaluationOutput)
    } catch (cause) {
      throw Object.assign(invalidEntry('MSBuild evaluation requires JSON'), { cause })
    }
    if (evaluated?.Properties?.MSBuildProjectFullPath !== projectFile || !Array.isArray(evaluated?.Items?.ProjectReference)) throw invalidEntry('MSBuild evaluation differs from the selected project')
    if (evaluated.Properties.TargetFramework !== 'net10.0' || evaluated.Properties.TargetFrameworks !== '' || evaluated.Items.ProjectReference.length) throw unsupported('Project restore supports only a single net10.0 project without references')
    const feed = path.join(projectRoot, 'feed')
    fs.mkdirSync(feed)
    for (const pkg of selected) fs.writeFileSync(path.join(feed, `${pkg.id}.${pkg.version}.nupkg`), pkg.bytes, { flag: 'wx' })
    const configPath = path.join(projectRoot, 'NuGet.Config')
    const escapedFeed = feed.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    const configBytes = Buffer.from(`<configuration><packageSources><clear/><add key="selected" value="${escapedFeed}"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>\n`)
    fs.writeFileSync(configPath, configBytes, { flag: 'wx' })
    const assertBootstrap = () => {
      const feedFiles = selected.map(pkg => `${pkg.id}.${pkg.version}.nupkg`).sort()
      if (JSON.stringify(fs.readdirSync(feed).sort()) !== JSON.stringify(feedFiles) || !fs.readFileSync(requireOwnedFile(projectRoot, 'NuGet.Config')).equals(configBytes)) throw invalidEntry('Project restore changed its selected feed or configuration')
      for (const pkg of selected) {
        if (!fs.readFileSync(requireOwnedFile(projectRoot, `feed/${pkg.id}.${pkg.version}.nupkg`)).equals(pkg.bytes)) throw integrityError('Project restore changed selected feed bytes')
      }
    }
    const assetsPath = `artifacts/obj/${path.basename(projectPath, '.fsproj')}/project.assets.json`
    const lockPath = 'artifacts/packages.lock.json'
    const common = ['restore', projectFile, '--configfile', configPath, '--packages', path.join(projectRoot, 'packages'), '--disable-parallel', '--verbosity', 'minimal', `-p:ArtifactsDir=${path.join(projectRoot, 'artifacts')}${path.sep}`, `-p:NuGetLockFilePath=${path.join(projectRoot, lockPath)}`, '-p:RestorePackagesWithLockFile=true', '-p:NuGetAudit=false', `-p:RestoreSources=${feed}`, '-p:RestoreAdditionalProjectSources=', '-p:RestoreFallbackFolders=', '-p:RestoreAdditionalProjectFallbackFolders=', '-p:DisableImplicitLibraryPacksFolder=true', '-p:DisableImplicitNuGetFallbackFolder=true', '-p:EnableTargetingPackDownload=false', '-p:EnableRuntimePackDownload=false']
    assertBootstrap()
    await runVerificationToolProbe(executable, [...common, '--use-lock-file'], options)
    signal?.throwIfAborted()
    assertInputs()
    captureOrdinaryVerificationFiles(projectRoot, invalidEntry)
    assertBootstrap()
    const graph = privateGraph(projectRoot, projectFile, assetsPath, lockPath, selected)
    const expectedGraph = JSON.stringify(graph)
    const lockBytes = fs.readFileSync(path.join(projectRoot, lockPath))
    fs.rmSync(path.join(projectRoot, 'packages'), { recursive: true })
    fs.mkdirSync(path.join(projectRoot, 'packages'))
    assertBootstrap()
    await runVerificationToolProbe(executable, [...common, '--locked-mode', '--force'], options)
    signal?.throwIfAborted()
    assertInputs()
    captureOrdinaryVerificationFiles(projectRoot, invalidEntry)
    if (!fs.readFileSync(path.join(projectRoot, lockPath)).equals(lockBytes) || JSON.stringify(privateGraph(projectRoot, projectFile, assetsPath, lockPath, selected)) !== expectedGraph) throw invalidEntry('Fresh-cache locked restore changed the selected graph')
    assertBootstrap()
    const entries = captureOrdinaryVerificationFiles(projectRoot, invalidEntry)
    const expectedEntries = JSON.stringify(entries)
    const entriesDigest = hash('sha256', expectedEntries)
    const graphDigest = hash('sha256', expectedGraph)
    const packages = selected.map(({ id, version, sha512 }) => ({ id, version, rawSha512: sha512 }))
    const identityScope = 'selected-nuget-project-restore'
    const projectDigest = hash('sha256', JSON.stringify({ sourceDigest, treeId, sdkDigest, projectPath, packages, graphDigest, entriesDigest, identityScope }))
    const selection = { projectRoot, projectPath, targetFramework: 'net10.0', assetsPath, lockPath, sourceDigest, treeId, sdkDigest, packages, graph, graphDigest, entries, entriesDigest, projectDigest, identityScope }
    const capturedSelection = JSON.stringify(selection)
    const prepared = {
      ...selection,
      revalidate() {
        assertInputs()
        const actual = Object.fromEntries(Object.keys(selection).map(key => [key, prepared[key]]))
        if (JSON.stringify(actual) !== capturedSelection || JSON.stringify(captureOrdinaryVerificationFiles(projectRoot, invalidEntry)) !== expectedEntries) throw invalidEntry('Prepared project receipt differs from its owned inputs and outputs')
      },
      dispose() { fs.rmSync(projectRoot, { recursive: true, force: true }) },
    }
    return prepared
  } catch (error) {
    try {
      fs.rmSync(projectRoot, { recursive: true, force: true })
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Project restore and cleanup failed', { cause: error })
    }
    throw error
  }
}
