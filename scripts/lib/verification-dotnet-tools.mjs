import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { privateDotnetEnvironment } from './verification-dotnet-environment.mjs'
import { verificationInputExists } from './verification-input-path.mjs'
import { runVerificationToolProbe } from './verification-tool-probe.mjs'

const hash = (algorithm, bytes, encoding = 'hex') => createHash(algorithm).update(bytes).digest(encoding)
const invalidEntry = message => Object.assign(new Error(message), { code: 'verification-dotnet-tools-entry-invalid' })
const integrityError = message => Object.assign(new Error(message), { code: 'verification-dotnet-tools-integrity-invalid' })
const exactVersion = value => typeof value === 'string' && /^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$/.test(value)
const ordinaryName = value => typeof value === 'string' && /^[0-9A-Za-z][0-9A-Za-z._-]*$/.test(value)

function requireFile(file, message) {
  try {
    if (!fs.lstatSync(file).isFile()) throw invalidEntry(message)
  } catch (cause) {
    if (cause.code === 'ENOENT') throw Object.assign(invalidEntry(message), { cause })
    throw cause
  }
}

function sourceFile(root, relative) {
  const file = path.join(root, relative)
  if (!verificationInputExists(root, file) || !fs.lstatSync(file).isFile()) throw invalidEntry(`Selected input requires a real file: ${relative}`)
  return fs.readFileSync(file)
}

function parseManifest(bytes) {
  let manifest
  try {
    manifest = JSON.parse(bytes)
  } catch (cause) {
    throw Object.assign(invalidEntry('Selected tool manifest must be JSON'), { cause })
  }
  if (manifest?.version !== 1 || manifest.isRoot !== true || !manifest.tools || Array.isArray(manifest.tools) || typeof manifest.tools !== 'object') throw invalidEntry('Selected tools require a root version1 manifest')
  const tools = Object.entries(manifest.tools).map(([id, tool]) => {
    if (!ordinaryName(id) || id !== id.toLowerCase() || !exactVersion(tool?.version) || (tool.rollForward !== undefined && tool.rollForward !== false) || !Array.isArray(tool.commands) || !tool.commands.length || tool.commands.some(command => !ordinaryName(command))) throw invalidEntry(`Tool requires an exact version and ordinary commands: ${id}`)
    return { id, version: tool.version, commands: [...tool.commands] }
  })
  const commands = tools.flatMap(tool => tool.commands)
  if (!tools.length || new Set(commands).size !== commands.length) throw invalidEntry('Selected tools require distinct commands')
  return tools
}

function capturePackages(packageArchives, tools) {
  if (!Array.isArray(packageArchives) || !packageArchives.length) throw invalidEntry('Tool restore requires explicitly identified package archives')
  const packages = packageArchives.map(selected => {
    const { id, version, archivePath, sha512 } = selected ?? {}
    if (!ordinaryName(id) || id !== id.toLowerCase() || !exactVersion(version)) throw invalidEntry('Package requires an ordinary lowercase ID and exact version')
    if (typeof sha512 !== 'string' || !/^[A-Za-z0-9+/]{86}==$/.test(sha512) || Buffer.from(sha512, 'base64').toString('base64') !== sha512) throw integrityError('Package requires an explicit SHA512 identity')
    if (!fs.lstatSync(archivePath).isFile()) throw invalidEntry('Selected package archive requires a real file')
    const bytes = fs.readFileSync(archivePath)
    if (hash('sha512', bytes, 'base64') !== sha512) throw integrityError(`Package differs from its selected identity: ${id}/${version}`)
    return { id, version, sha512, bytes }
  }).sort((left, right) => `${left.id}/${left.version}`.localeCompare(`${right.id}/${right.version}`, 'en'))
  if (new Set(packages.map(pkg => `${pkg.id}/${pkg.version}`)).size !== packages.length) throw invalidEntry('Selected package identities must be distinct')
  if (tools.some(tool => !packages.some(pkg => pkg.id === tool.id && pkg.version === tool.version))) throw invalidEntry('Selected archives must include each manifest tool version')
  return packages
}

function selectedSdk(sdk) {
  if (sdk?.identityScope !== 'selected-dotnet-sdk-bundle' || typeof sdk.revalidate !== 'function' || !Array.isArray(sdk.entries)) throw invalidEntry('Tool restore requires a prepared selected SDK')
  sdk.revalidate()
  const entriesDigest = hash('sha256', JSON.stringify(sdk.entries))
  const digest = hash('sha256', JSON.stringify({ archiveSha256: sdk.archiveSha256, entriesDigest, globalJsonSha256: sdk.globalJsonSha256, dotnet: sdk.dotnet, sdk: sdk.sdk, runtimes: sdk.runtimes, identityScope: sdk.identityScope }))
  if (entriesDigest !== sdk.entriesDigest || digest !== sdk.sdkDigest) throw invalidEntry('Selected SDK receipt metadata differs from its identity')
  const relative = sdk.dotnet?.path
  if (typeof relative !== 'string' || !relative.startsWith('dotnet-sdk/') || relative.split('/').some(segment => !segment || segment === '.' || segment === '..' || segment.includes('\\'))) throw invalidEntry('Selected SDK executable must belong to its bundle')
  const entry = sdk.entries.find(entry => entry.path === relative)
  const executable = path.join(sdk.toolRoot, relative)
  requireFile(executable, 'Selected SDK executable requires a real file')
  if (entry?.type !== 'File' || (entry.mode & 0o111) === 0 || entry.sha256 !== sdk.dotnet.sha256 || hash('sha256', fs.readFileSync(executable)) !== entry.sha256) throw invalidEntry('Selected SDK executable differs from its captured role')
  return { sdkDigest: digest, executable, sdkRoot: path.join(sdk.toolRoot, 'dotnet-sdk') }
}

function inventory(root) {
  const entries = []
  function inspect(relative) {
    const file = path.join(root, relative)
    const stat = fs.lstatSync(file)
    if (stat.isDirectory()) {
      entries.push({ path: relative, type: 'Directory', mode: stat.mode & 0o777 })
      for (const name of fs.readdirSync(file).sort()) inspect(relative === '.' ? name : `${relative}/${name}`)
    } else if (stat.isFile()) {
      const bytes = fs.readFileSync(file)
      entries.push({ path: relative, type: 'File', mode: stat.mode & 0o777, size: bytes.length, sha256: hash('sha256', bytes) })
    } else {
      throw invalidEntry(`Restored tools require ordinary owned members: ${relative}`)
    }
  }
  inspect('.')
  return entries
}

function restoredPackages(root, selected) {
  const packagesRoot = path.join(root, 'packages')
  const actual = []
  for (const id of fs.readdirSync(packagesRoot).sort()) {
    if (!fs.lstatSync(path.join(packagesRoot, id)).isDirectory()) throw invalidEntry('Restored package IDs require directories')
    for (const version of fs.readdirSync(path.join(packagesRoot, id)).sort()) {
      if (!fs.lstatSync(path.join(packagesRoot, id, version)).isDirectory()) throw invalidEntry('Restored package versions require directories')
      actual.push(`${id}/${version}`)
    }
  }
  if (JSON.stringify(actual.sort()) !== JSON.stringify(selected.map(pkg => `${pkg.id}/${pkg.version}`).sort())) throw invalidEntry('Actual restored package closure differs from selected archives')
  for (const pkg of selected) {
    const archive = path.join(packagesRoot, pkg.id, pkg.version, `${pkg.id}.${pkg.version}.nupkg`)
    if (!fs.lstatSync(archive).isFile() || hash('sha512', fs.readFileSync(archive), 'base64') !== pkg.sha512) throw integrityError(`Restored package differs from selected bytes: ${pkg.id}/${pkg.version}`)
  }
}

function restoredCommands(root, selected) {
  const cache = path.join(root, 'cli', '.dotnet', 'toolResolverCache', '1')
  const commands = selected.flatMap(tool => tool.commands).sort()
  if (JSON.stringify(fs.readdirSync(cache).sort()) !== JSON.stringify(commands)) throw invalidEntry('Actual tool resolver inventory differs from manifest commands')
  return commands.map(command => {
    const tool = selected.find(tool => tool.commands.includes(command))
    const file = path.join(cache, command)
    if (!fs.lstatSync(file).isFile()) throw invalidEntry('Tool resolver requires an ordinary file')
    let rows
    try {
      rows = JSON.parse(fs.readFileSync(file, 'utf8'))
    } catch (cause) {
      throw Object.assign(invalidEntry('Tool resolver requires JSON'), { cause })
    }
    if (!Array.isArray(rows) || rows.length !== 1) throw invalidEntry('Each manifest command requires one restored resolver entry')
    const row = rows[0]
    const packageRoot = path.join(root, 'packages', tool.id, tool.version)
    if (row?.Name !== command || row.Version !== tool.version || row.Runner !== 'dotnet' || typeof row.PathToExecutable !== 'string' || !path.isAbsolute(row.PathToExecutable)) throw invalidEntry('Actual tool resolver differs from the selected command identity')
    const executable = fs.realpathSync(row.PathToExecutable)
    if (!executable.startsWith(`${packageRoot}${path.sep}`) || !fs.lstatSync(row.PathToExecutable).isFile()) throw invalidEntry('Tool command borrows an executable outside its selected package')
    validateRuntimeFiles(executable)
    return { id: tool.id, version: tool.version, command, executable: path.relative(root, executable).split(path.sep).join('/') }
  })
}

function validateRuntimeFiles(executable) {
  if (!executable.endsWith('.dll')) throw invalidEntry('Selected dotnet commands require a DLL entry point')
  const depsFile = executable.slice(0, -4) + '.deps.json'
  requireFile(depsFile, 'Selected tool requires an ordinary dependency manifest')
  let deps
  try {
    deps = JSON.parse(fs.readFileSync(depsFile, 'utf8'))
  } catch (cause) {
    throw Object.assign(invalidEntry('Selected tool dependency manifest requires JSON'), { cause })
  }
  const targetName = deps?.runtimeTarget?.name
  const target = typeof targetName === 'string' ? deps?.targets?.[targetName] : undefined
  if (!target || typeof target !== 'object' || Array.isArray(target)) throw invalidEntry('Selected tool requires its declared runtime target')
  let runtimeCount = 0
  for (const library of Object.values(target)) {
    if (library?.runtime === undefined) continue
    if (!library.runtime || typeof library.runtime !== 'object' || Array.isArray(library.runtime)) throw invalidEntry('Selected tool runtime files require an asset map')
    for (const asset of Object.keys(library.runtime)) {
      const segments = asset.split('/')
      if (!asset || path.posix.isAbsolute(asset) || segments.some(segment => !segment || segment === '.' || segment === '..' || segment.includes('\\') || segment.includes(':'))) throw invalidEntry('Selected tool runtime asset requires an ordinary relative path')
      const file = path.join(path.dirname(executable), segments.at(-1))
      requireFile(file, 'Selected tool runtime file requires a real owned file')
      runtimeCount += 1
    }
  }
  if (!runtimeCount) throw invalidEntry('Selected tool requires runtime assets')
}

const xmlText = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')

export async function prepareVerificationDotnetTools({ sdk, sourceRoot, packageArchives, parentDirectory, signal }) {
  signal?.throwIfAborted()
  const { sdkDigest, executable, sdkRoot } = selectedSdk(sdk)
  const selectedRoot = path.resolve(sourceRoot)
  if (!fs.lstatSync(selectedRoot).isDirectory()) throw invalidEntry('Selected source requires a real directory')
  const globalBytes = sourceFile(selectedRoot, 'global.json')
  const globalJsonSha256 = hash('sha256', globalBytes)
  if (globalJsonSha256 !== sdk.globalJsonSha256) throw invalidEntry('Tool restore global.json differs from SDK selection')
  const manifestBytes = sourceFile(selectedRoot, '.config/dotnet-tools.json')
  const selectedTools = parseManifest(manifestBytes)
  const selectedPackages = capturePackages(packageArchives, selectedTools)
  signal?.throwIfAborted()
  const parentRoot = fs.realpathSync(parentDirectory)
  if (!fs.lstatSync(parentRoot).isDirectory()) throw invalidEntry('Tool preparation requires a real parent directory')
  const allocatedRoot = fs.mkdtempSync(path.join(parentRoot, 'verification-dotnet-tools-'))
  try {
    const toolRoot = fs.realpathSync(allocatedRoot)
    const env = privateDotnetEnvironment(toolRoot, sdkRoot, executable)
    fs.mkdirSync(path.join(toolRoot, '.config'))
    fs.writeFileSync(path.join(toolRoot, 'global.json'), globalBytes, { flag: 'wx' })
    const manifestPath = path.join(toolRoot, '.config', 'dotnet-tools.json')
    fs.writeFileSync(manifestPath, manifestBytes, { flag: 'wx' })
    const feed = path.join(toolRoot, 'feed')
    fs.mkdirSync(feed)
    for (const pkg of selectedPackages) fs.writeFileSync(path.join(feed, `${pkg.id}.${pkg.version}.nupkg`), pkg.bytes, { flag: 'wx' })
    const configFile = path.join(toolRoot, 'NuGet.Config')
    const configBytes = Buffer.from(`<configuration><packageSources><clear/><add key="selected" value="${xmlText(feed)}"/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><disabledPackageSources><clear/></disabledPackageSources></configuration>\n`)
    fs.writeFileSync(configFile, configBytes, { flag: 'wx' })
    await runVerificationToolProbe(executable, ['tool', 'restore', '--tool-manifest', manifestPath, '--configfile', configFile, '--disable-parallel', '--verbosity', 'minimal'], { cwd: toolRoot, env, signal })
    signal?.throwIfAborted()
    if (selectedSdk(sdk).sdkDigest !== sdkDigest) throw invalidEntry('Tool restore changed the selected SDK identity')
    if (!fs.readFileSync(path.join(toolRoot, 'global.json')).equals(globalBytes) || !fs.readFileSync(manifestPath).equals(manifestBytes) || !fs.readFileSync(configFile).equals(configBytes)) throw invalidEntry('Tool restore changed captured selection inputs')
    inventory(toolRoot)
    restoredPackages(toolRoot, selectedPackages)
    const tools = restoredCommands(toolRoot, selectedTools)
    const selectedFeed = selectedPackages.map(pkg => `${pkg.id}.${pkg.version}.nupkg`).sort()
    if (JSON.stringify(fs.readdirSync(feed).sort()) !== JSON.stringify(selectedFeed)) throw invalidEntry('Tool restore changed the selected feed inventory')
    for (const pkg of selectedPackages) {
      if (!fs.readFileSync(path.join(feed, `${pkg.id}.${pkg.version}.nupkg`)).equals(pkg.bytes)) throw integrityError('Tool restore changed selected feed bytes')
    }
    const entries = inventory(toolRoot)
    const expectedEntries = JSON.stringify(entries)
    const entriesDigest = hash('sha256', expectedEntries)
    const packages = selectedPackages.map(({ id, version, sha512 }) => ({ id, version, sha512 }))
    const toolManifestSha256 = hash('sha256', manifestBytes)
    const identityScope = 'selected-dotnet-tool-restore'
    const toolsDigest = hash('sha256', JSON.stringify({ sdkDigest, globalJsonSha256, toolManifestSha256, packages, entriesDigest, identityScope }))
    const selection = { toolRoot, sdkDigest, globalJsonSha256, toolManifestSha256, packages, tools, entries, entriesDigest, toolsDigest, identityScope }
    const capturedSelection = JSON.stringify(selection)
    const prepared = {
      ...selection,
      revalidate() {
        const actual = Object.fromEntries(Object.keys(selection).map(key => [key, prepared[key]]))
        if (JSON.stringify(actual) !== capturedSelection) throw invalidEntry('Restored tool receipt differs from its owned selection')
        if (selectedSdk(sdk).sdkDigest !== sdkDigest) throw invalidEntry('Restored tools no longer use their selected SDK identity')
        if (JSON.stringify(inventory(toolRoot)) !== expectedEntries) throw invalidEntry('Restored tool inventory differs from its captured identity')
      },
      dispose() { fs.rmSync(allocatedRoot, { recursive: true, force: true }) },
    }
    return prepared
  } catch (error) {
    try {
      fs.rmSync(allocatedRoot, { recursive: true, force: true })
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Tool restore and cleanup failed', { cause: error })
    }
    throw error
  }
}
