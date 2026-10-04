import { createHash } from 'node:crypto'
import fs from 'node:fs'
import { allocateVerificationDirectory } from './verification-directory-owner.mjs'
import path from 'node:path'
import { materializeVerificationArchive } from './verification-archive.mjs'
import { runVerificationToolProbe } from './verification-tool-probe.mjs'
import { privateDotnetEnvironment } from './verification-dotnet-environment.mjs'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
const invalidEntry = message => Object.assign(new Error(message), { code: 'verification-dotnet-sdk-entry-invalid' })

function selectedEntry(candidate, relativePath, type) {
  if (typeof relativePath !== 'string' || !relativePath.startsWith('dotnet-sdk/') || relativePath.split('/').some(segment => !segment || segment === '.' || segment === '..' || segment.includes('\\'))) {
    throw invalidEntry('SDK role requires an ordinary path inside dotnet-sdk')
  }
  const entry = candidate.entries.find(entry => entry.path === relativePath)
  if (entry?.type !== type) throw invalidEntry(`SDK role requires a real ${type}: ${relativePath}`)
  return entry
}

function capturedGlobalJson(sourceRoot) {
  const selectedRoot = path.resolve(sourceRoot)
  if (!fs.lstatSync(selectedRoot).isDirectory()) throw invalidEntry('Selected source root requires a real directory')
  const globalPath = path.join(selectedRoot, 'global.json')
  if (!fs.lstatSync(globalPath).isFile()) throw invalidEntry('Selected global.json requires a real file')
  const bytes = fs.readFileSync(globalPath)
  let global
  try {
    global = JSON.parse(bytes)
  } catch (cause) {
    throw Object.assign(invalidEntry('Selected global.json must be JSON'), { cause })
  }
  if (!global?.sdk || typeof global.sdk.version !== 'string' || !global.sdk.version || global.sdk.paths !== undefined || global['msbuild-sdks'] !== undefined) {
    throw invalidEntry('SDK preparation requires a declared SDK version without additional SDK search locations')
  }
  return bytes
}

function candidateDirectory(candidate, reportedPath) {
  if (!path.isAbsolute(reportedPath)) throw invalidEntry(`SDK probe requires an absolute selected path: ${reportedPath}`)
  const actual = fs.realpathSync(reportedPath)
  const sdkRoot = path.join(candidate.root, 'dotnet-sdk')
  if (actual !== sdkRoot && !actual.startsWith(`${sdkRoot}${path.sep}`)) throw invalidEntry(`SDK probe borrows a directory outside the selected bundle: ${reportedPath}`)
  const relative = path.relative(candidate.root, actual).split(path.sep).join('/')
  selectedEntry(candidate, relative, 'Directory')
  return relative
}

function installedSdks(candidate, output) {
  const rows = output.split(/\r?\n/).filter(Boolean).map(line => {
    const match = /^([0-9A-Za-z.+-]+) \[([^\]\r\n]+)\]$/.exec(line)
    if (!match) throw invalidEntry('Selected SDK must report ordinary installed SDK identities')
    const root = candidateDirectory(candidate, match[2])
    if (root !== 'dotnet-sdk/sdk') throw invalidEntry('Installed SDK directory differs from the selected bundle layout')
    const basePath = `${root}/${match[1]}`
    selectedEntry(candidate, basePath, 'Directory')
    selectedEntry(candidate, `${basePath}/dotnet.dll`, 'File')
    return { version: match[1], basePath }
  })
  if (!rows.length || new Set(rows.map(row => row.version)).size !== rows.length) throw invalidEntry('Selected bundle requires distinct installed SDKs')
  return rows
}

function installedRuntimes(candidate, output) {
  const rows = output.split(/\r?\n/).filter(Boolean).map(line => {
    const match = /^([A-Za-z0-9_.-]+) ([0-9A-Za-z.+-]+) \[([^\]\r\n]+)\]$/.exec(line)
    if (!match) throw invalidEntry('Selected SDK must report ordinary installed runtime identities')
    const root = candidateDirectory(candidate, match[3])
    if (root !== `dotnet-sdk/shared/${match[1]}`) throw invalidEntry('Installed runtime directory differs from the selected bundle layout')
    const runtimePath = `${root}/${match[2]}`
    selectedEntry(candidate, runtimePath, 'Directory')
    return { name: match[1], version: match[2], path: runtimePath }
  })
  if (!rows.some(row => row.name === 'Microsoft.NETCore.App') || new Set(rows.map(row => `${row.name}/${row.version}`)).size !== rows.length) throw invalidEntry('Selected bundle requires distinct runtimes including Microsoft.NETCore.App')
  return rows
}

function infoSection(info, heading) {
  const lines = info.split(/\r?\n/)
  const index = lines.indexOf(heading)
  if (index === -1) throw invalidEntry(`Selected SDK info requires ${heading}`)
  const section = []
  for (const line of lines.slice(index + 1)) {
    if (!line.trim()) break
    section.push(line.trim())
  }
  return section
}

function validateSdkInfo(candidate, info, sdk, sdks, runtimes, globalPath) {
  const sdkInfo = infoSection(info, '.NET SDK:')
  if (sdkInfo.find(line => line.startsWith('Version:'))?.slice('Version:'.length).trim() !== sdk.version) throw invalidEntry('Selected SDK info and version probe disagree')
  const basePath = infoSection(info, 'Runtime Environment:').find(line => line.startsWith('Base Path:'))?.slice('Base Path:'.length).trim()
  if (!basePath || candidateDirectory(candidate, basePath) !== sdk.basePath) throw invalidEntry('Selected SDK info reports a different SDK base path')
  const infoSdks = installedSdks(candidate, infoSection(info, '.NET SDKs installed:').join('\n'))
  const infoRuntimes = installedRuntimes(candidate, infoSection(info, '.NET runtimes installed:').join('\n'))
  if (JSON.stringify(infoSdks) !== JSON.stringify(sdks) || JSON.stringify(infoRuntimes) !== JSON.stringify(runtimes)) throw invalidEntry('Selected SDK installed identities disagree across probes')
  if (JSON.stringify(infoSection(info, 'Other architectures found:')) !== JSON.stringify(['None'])) throw invalidEntry('Selected SDK reports another architecture outside the bounded bundle')
  if (JSON.stringify(infoSection(info, 'global.json file:')) !== JSON.stringify([globalPath])) throw invalidEntry('SDK selection did not use the captured global.json')
}

export async function prepareVerificationDotnetSdk({ sourceRoot, archivePath, archiveSha256, parentDirectory, expectedSdkVersion, dotnetPath = 'dotnet-sdk/dotnet', signal }) {
  signal?.throwIfAborted()
  if (typeof expectedSdkVersion !== 'string' || !/^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?$/.test(expectedSdkVersion)) throw invalidEntry('SDK preparation requires an explicit expected SDK version')
  const globalBytes = capturedGlobalJson(sourceRoot)
  const archiveBytes = fs.readFileSync(archivePath)
  const candidate = await materializeVerificationArchive({ archiveBytes, archiveSha256, parentDirectory, rootDirectory: 'dotnet-sdk', errorPrefix: 'verification-dotnet-sdk' })
  let probeRoot
  let probeOwner
  try {
    signal?.throwIfAborted()
    candidate.revalidate()
    const dotnetEntry = selectedEntry(candidate, dotnetPath, 'File')
    if ((dotnetEntry.mode & 0o111) === 0) throw invalidEntry('Selected dotnet requires executable permission')
    probeOwner = allocateVerificationDirectory(parentDirectory, 'verification-dotnet-sdk-probe-', invalidEntry)
    probeRoot = probeOwner.root
    const globalPath = path.join(probeRoot, 'global.json')
    fs.writeFileSync(globalPath, globalBytes, { flag: 'wx' })
    const executable = path.join(candidate.root, dotnetPath)
    const options = { cwd: probeRoot, env: privateDotnetEnvironment(probeRoot, path.join(candidate.root, 'dotnet-sdk'), executable), signal }
    const version = await runVerificationToolProbe(executable, ['--version'], options)
    probeOwner.assertOwned()
    candidate.revalidate()
    if (version !== expectedSdkVersion) throw invalidEntry('Actual SDK version differs from the selected expected SDK version')
    const sdksOutput = await runVerificationToolProbe(executable, ['--list-sdks'], options)
    probeOwner.assertOwned()
    candidate.revalidate()
    const sdks = installedSdks(candidate, sdksOutput)
    const runtimesOutput = await runVerificationToolProbe(executable, ['--list-runtimes'], options)
    probeOwner.assertOwned()
    candidate.revalidate()
    const runtimes = installedRuntimes(candidate, runtimesOutput)
    const sdk = sdks.find(sdk => sdk.version === version)
    if (!sdk) throw invalidEntry('Actual SDK version is absent from the selected installed SDKs')
    const info = await runVerificationToolProbe(executable, ['--info'], options)
    probeOwner.assertOwned()
    candidate.revalidate()
    validateSdkInfo(candidate, info, sdk, sdks, runtimes, globalPath)
    for (const directory of ['dotnet-sdk/host', 'dotnet-sdk/host/fxr', 'dotnet-sdk/shared', 'dotnet-sdk/packs']) selectedEntry(candidate, directory, 'Directory')
    signal?.throwIfAborted()
    if (!fs.readFileSync(globalPath).equals(globalBytes)) throw invalidEntry('SDK probe changed the captured global.json')
    candidate.revalidate()
    probeOwner.dispose()
    const entriesDigest = sha256(JSON.stringify(candidate.entries))
    const globalJsonSha256 = sha256(globalBytes)
    const dotnet = { path: dotnetPath, sha256: dotnetEntry.sha256 }
    const identityScope = 'selected-dotnet-sdk-bundle'
    const sdkDigest = sha256(JSON.stringify({ archiveSha256, entriesDigest, globalJsonSha256, dotnet, sdk, runtimes, identityScope }))
    const selection = { toolRoot: candidate.root, entries: candidate.entries, entriesDigest, archiveSha256, globalJsonSha256, dotnet, sdk, runtimes, sdkDigest, identityScope }
    const capturedSelection = JSON.stringify(selection)
    const prepared = {
      ...selection,
      revalidate() {
        const actual = Object.fromEntries(Object.keys(selection).map(key => [key, prepared[key]]))
        if (JSON.stringify(actual) !== capturedSelection) throw invalidEntry('Selected SDK receipt differs from its owned bundle identity')
        candidate.revalidate()
      },
      dispose: candidate.dispose,
    }
    return prepared
  } catch (error) {
    const failures = [error]
    if (probeRoot) {
      try {
        probeOwner.dispose()
      } catch (cleanupError) {
        failures.push(cleanupError)
      }
    }
    try {
      candidate.dispose()
    } catch (cleanupError) {
      failures.push(cleanupError)
    }
    if (failures.length > 1) throw new AggregateError(failures, 'SDK preparation and cleanup failed', { cause: error })
    throw error
  }
}
