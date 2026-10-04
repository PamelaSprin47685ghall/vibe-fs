import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { privateDotnetEnvironment } from './verification-dotnet-environment.mjs'
import { verificationInputExists } from './verification-input-path.mjs'
import { captureOrdinaryVerificationFiles } from './verification-ordinary-files.mjs'
import { runVerificationToolProbe } from './verification-tool-probe.mjs'

const hash = bytes => createHash('sha256').update(bytes).digest('hex')
const invalidEntry = message => Object.assign(new Error(message), { code: 'verification-fable-project-entry-invalid' })
const ordinaryPath = value => typeof value === 'string' && value !== '' && !path.posix.isAbsolute(value) && value.split('/').every(segment => segment && segment !== '.' && segment !== '..' && !segment.includes('\\') && !segment.includes(':'))

function ownedFile(root, relative) {
  if (!ordinaryPath(relative) || !verificationInputExists(root, path.join(root, relative)) || !fs.lstatSync(path.join(root, relative)).isFile()) throw invalidEntry(`Compilation requires an ordinary owned file: ${relative}`)
  return path.join(root, relative)
}

export async function compileVerificationFableProject({ source, sdk, tools, project, parentDirectory, signal }) {
  signal?.throwIfAborted()
  if ([source, sdk, tools, project].some(owner => typeof owner?.revalidate !== 'function') || sdk.identityScope !== 'selected-dotnet-sdk-bundle' || tools.identityScope !== 'selected-dotnet-tool-restore' || project.identityScope !== 'selected-nuget-project-restore') throw invalidEntry('Compilation requires prepared source, SDK, tools and project owners')
  for (const owner of [source, sdk, tools, project]) owner.revalidate()
  const { sourceDigest, treeId, sourceRoot } = source
  const { sdkDigest, toolRoot: sdkRoot } = sdk
  const { toolsDigest, toolRoot: toolsRoot } = tools
  const { projectDigest, projectPath, projectRoot } = project
  if (project.sourceDigest !== sourceDigest || project.treeId !== treeId || project.sdkDigest !== sdkDigest || tools.sdkDigest !== sdkDigest || project.targetFramework !== 'net10.0') throw invalidEntry('Compilation owners refer to different selected inputs')
  const globalJsonSha256 = hash(fs.readFileSync(ownedFile(sourceRoot, 'global.json')))
  if (globalJsonSha256 !== sdk.globalJsonSha256 || globalJsonSha256 !== tools.globalJsonSha256 || hash(fs.readFileSync(ownedFile(sourceRoot, '.config/dotnet-tools.json'))) !== tools.toolManifestSha256) throw invalidEntry('Compilation tools differ from the selected source manifests')
  const projectFile = ownedFile(sourceRoot, projectPath)
  if (!projectPath.endsWith('.fsproj') || !ordinaryPath(sdk.dotnet?.path) || !sdk.dotnet.path.startsWith('dotnet-sdk/')) throw invalidEntry('Compilation requires the selected F# project and SDK executable')
  const executable = ownedFile(sdkRoot, sdk.dotnet.path)
  if (hash(fs.readFileSync(executable)) !== sdk.dotnet.sha256) throw invalidEntry('Compilation SDK executable differs from its selected role')
  if (!Array.isArray(tools.tools)) throw invalidEntry('Compilation requires selected tool command roles')
  const commands = tools.tools.filter(tool => tool?.command === 'fable')
  if (commands.length !== 1 || typeof commands[0].executable !== 'string' || !commands[0].executable.endsWith('.dll')) throw invalidEntry('Compilation requires exactly one selected Fable DLL role')
  const fable = { ...commands[0] }
  const compiler = ownedFile(toolsRoot, fable.executable)
  const capturedInputs = JSON.stringify({ sourceDigest, treeId, sourceRoot, sdkDigest, sdkRoot, toolsDigest, toolsRoot, projectDigest, projectPath, projectRoot })
  const assertInputs = () => {
    for (const owner of [source, sdk, tools, project]) owner.revalidate()
    const actual = { sourceDigest: source.sourceDigest, treeId: source.treeId, sourceRoot: source.sourceRoot, sdkDigest: sdk.sdkDigest, sdkRoot: sdk.toolRoot, toolsDigest: tools.toolsDigest, toolsRoot: tools.toolRoot, projectDigest: project.projectDigest, projectPath: project.projectPath, projectRoot: project.projectRoot }
    if (JSON.stringify(actual) !== capturedInputs) throw invalidEntry('Compilation changed its selected input identities')
  }
  const artifacts = path.join(projectRoot, 'artifacts')
  const artifactEntries = captureOrdinaryVerificationFiles(artifacts, invalidEntry)
  const selectedArtifacts = JSON.stringify(artifactEntries)
  const parentRoot = fs.realpathSync(parentDirectory)
  const parentIdentity = fs.lstatSync(parentRoot, { bigint: true })
  if (!parentIdentity.isDirectory()) throw invalidEntry('Cannot verify owned identity of the compilation parent')
  const compileRoot = fs.mkdtempSync(path.join(parentRoot, 'verification-fable-project-'))
  const rootIdentity = fs.lstatSync(compileRoot, { bigint: true })
  function assertOwnedRoot(allowMissing = false) {
    for (const [directory, identity] of [[parentRoot, parentIdentity], [compileRoot, rootIdentity]]) {
      let current
      try {
        current = fs.lstatSync(directory, { bigint: true })
      } catch (cause) {
        if (allowMissing && directory === compileRoot && cause.code === 'ENOENT') return false
        throw Object.assign(invalidEntry(`Cannot verify owned identity of the compilation directory: ${directory}`), { cause })
      }
      if (!current.isDirectory() || current.dev !== identity.dev || current.ino !== identity.ino) throw invalidEntry(`Cannot verify owned identity of the compilation directory: ${directory}`)
    }
    return true
  }
  function dispose() {
    if (assertOwnedRoot(true)) fs.rmSync(compileRoot, { recursive: true, force: true })
  }
  try {
    assertOwnedRoot()
    const env = privateDotnetEnvironment(compileRoot, path.join(sdkRoot, 'dotnet-sdk'), executable)
    const seed = path.join(compileRoot, 'artifacts')
    fs.cpSync(artifacts, seed, { recursive: true, verbatimSymlinks: true, preserveTimestamps: true })
    const copiedEntries = captureOrdinaryVerificationFiles(seed, invalidEntry)
    const withoutModes = entries => entries.map(({ mode, ...entry }) => entry)
    if (JSON.stringify(withoutModes(copiedEntries)) !== JSON.stringify(withoutModes(artifactEntries))) throw invalidEntry('Compilation artifacts seed differs from its selected restore members and bytes')
    for (const entry of artifactEntries) fs.chmodSync(path.join(seed, entry.path), entry.mode)
    if (JSON.stringify(captureOrdinaryVerificationFiles(seed, invalidEntry)) !== selectedArtifacts) throw invalidEntry('Compilation artifacts seed differs from its selected restore outputs')
    env.ArtifactsDir = seed + path.sep
    env.NUGET_PACKAGES = path.join(projectRoot, 'packages')
    const outputPath = 'js'
    fs.mkdirSync(path.join(compileRoot, outputPath))
    assertInputs()
    assertOwnedRoot()
    await runVerificationToolProbe(executable, [compiler, projectFile, '--noRestore', '--noCache', '--noGitignore', '--outDir', path.join(compileRoot, outputPath)], { cwd: sourceRoot, env, signal })
    signal?.throwIfAborted()
    assertInputs()
    assertOwnedRoot()
    const entries = captureOrdinaryVerificationFiles(compileRoot, invalidEntry)
    if (!entries.some(entry => entry.type === 'File' && entry.path.startsWith('js/') && entry.path.endsWith('.js'))) throw invalidEntry('Compilation produced no owned JavaScript files')
    const expectedEntries = JSON.stringify(entries)
    const entriesDigest = hash(expectedEntries)
    const identityScope = 'selected-fable-project-compile'
    const compileDigest = hash(JSON.stringify({ sourceDigest, treeId, sdkDigest, toolsDigest, projectDigest, projectPath, fable, entriesDigest, identityScope }))
    const selection = { compileRoot, projectPath, outputPath, treeId, sourceDigest, sdkDigest, toolsDigest, projectDigest, fable, entries, entriesDigest, compileDigest, identityScope }
    const capturedSelection = JSON.stringify(selection)
    const prepared = {
      ...selection,
      revalidate() {
        assertInputs()
        assertOwnedRoot()
        const actual = Object.fromEntries(Object.keys(selection).map(key => [key, prepared[key]]))
        if (JSON.stringify(actual) !== capturedSelection || JSON.stringify(captureOrdinaryVerificationFiles(compileRoot, invalidEntry)) !== expectedEntries) throw invalidEntry('Compiled receipt differs from its selected inputs and complete outputs')
        assertOwnedRoot()
      },
      dispose,
    }
    assertOwnedRoot()
    return prepared
  } catch (error) {
    try {
      dispose()
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Compilation and cleanup failed', { cause: error })
    }
    throw error
  }
}
