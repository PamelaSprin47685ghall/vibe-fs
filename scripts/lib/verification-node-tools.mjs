import { createHash } from 'node:crypto'
import fs from 'node:fs'
import { allocateVerificationDirectory } from './verification-directory-owner.mjs'
import path from 'node:path'
import { materializeVerificationArchive } from './verification-archive.mjs'
import { runVerificationToolProbe } from './verification-tool-probe.mjs'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
const invalidEntry = message => Object.assign(new Error(message), { code: 'verification-tool-entry-invalid' })
const deadlineFailure = deadline => Object.assign(new Error('The selected tool probe deadline expired'), { code: 'verification-tool-deadline-exceeded', deadline })

function requireRolePath(relativePath, prefix) {
  if (typeof relativePath !== 'string' || !relativePath.startsWith(`${prefix}/`) || relativePath.split('/').some(segment => !segment || segment === '.' || segment === '..' || segment.includes('\\'))) {
    throw invalidEntry(`Tool role requires an ordinary path inside ${prefix}`)
  }
}

function selectedFile(entries, relativePath, prefix) {
  requireRolePath(relativePath, prefix)
  const entry = entries.find(entry => entry.path === relativePath)
  if (entry?.type !== 'File') throw invalidEntry(`Tool role requires a real file: ${relativePath}`)
  return entry
}

function declaredProductionLibraries(manifest) {
  function declarations(field) {
    const declared = manifest[field] ?? {}
    if (typeof declared !== 'object' || Array.isArray(declared) || Object.values(declared).some(spec => typeof spec !== 'string')) throw invalidEntry(`Npm package requires ordinary ${field} declarations`)
    return declared
  }
  const optional = declarations('optionalDependencies')
  const libraries = new Map(Object.keys(declarations('dependencies')).map(name => [name, Object.hasOwn(optional, name)]))
  for (const name of Object.keys(optional)) libraries.set(name, true)
  for (const name of Object.keys(declarations('peerDependencies'))) {
    if (!libraries.has(name)) libraries.set(name, manifest.peerDependenciesMeta?.[name]?.optional === true)
  }
  return libraries
}

function walkDeclaredNpmLibraries({ npmRoot, readPackageManifest, onMissingLibrary }) {
  const inspected = new Set()

  function resolveLibrary(packageRoot, name) {
    if (!/^(?:@[A-Za-z0-9_.-]+\/)?[A-Za-z0-9_.-]+$/.test(name) || name.split('/').some(segment => segment === '.' || segment === '..')) throw invalidEntry(`Npm library requires an ordinary package name: ${name}`)
    let directory = packageRoot
    while (directory === npmRoot || directory.startsWith(`${npmRoot}${path.sep}`)) {
      if (path.basename(directory) !== 'node_modules') {
        const library = path.join(directory, 'node_modules', name)
        let actual
        try {
          actual = fs.realpathSync(library)
        } catch (error) {
          if (error.code !== 'ENOENT' && error.code !== 'ENOTDIR') throw error
        }
        if (actual) {
          if (!actual.startsWith(`${npmRoot}${path.sep}`)) throw invalidEntry(`Declared npm library escapes the selected npm package: ${name}`)
          return actual
        }
      }
      directory = path.dirname(directory)
    }
    return undefined
  }

  function inspect(packageRoot) {
    if (inspected.has(packageRoot)) return
    inspected.add(packageRoot)
    for (const [name, optional] of declaredProductionLibraries(readPackageManifest(packageRoot))) {
      const library = resolveLibrary(packageRoot, name)
      if (!library && !optional) {
        const requiredBy = path.relative(npmRoot, packageRoot) || 'npm'
        const stop = onMissingLibrary({ name, requiredBy, packageRoot })
        if (stop) return stop
      }
      if (library) {
        const stopped = inspect(library)
        if (stopped) return stopped
      }
    }
  }
  return inspect(npmRoot)
}

export function firstMissingNpmProductionLibrary(root) {
  const npmRoot = path.resolve(root)
  function readPackageManifest(packageRoot) {
    const manifestPath = path.join(packageRoot, 'package.json')
    let manifest
    try {
      manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'))
    } catch (cause) {
      throw Object.assign(invalidEntry(`Declared npm library manifest must be JSON: ${path.relative(npmRoot, manifestPath).split(path.sep).join('/')}`), { cause })
    }
    if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)) {
      throw invalidEntry(`Declared npm library manifest must be an object: ${path.relative(npmRoot, manifestPath).split(path.sep).join('/')}`)
    }
    return manifest
  }
  let missing
  walkDeclaredNpmLibraries({
    npmRoot,
    readPackageManifest,
    onMissingLibrary({ name, requiredBy }) {
      missing = { name, requiredBy, message: `Missing selected npm production library ${name} required by ${requiredBy}` }
      return true
    },
  })
  return missing
}

export function assertSelfContainedNpmRoot(root) {
  const missing = firstMissingNpmProductionLibrary(root)
  if (missing) {
    throw invalidEntry(missing.message)
  }
}

function validateDeclaredNpmLibraries(candidate) {
  const npmRoot = path.join(candidate.root, 'toolchain/npm')
  const entries = new Map(candidate.entries.map(entry => [entry.path, entry]))

  function packageManifest(packageRoot) {
    const manifestPath = path.join(packageRoot, 'package.json')
    const relative = path.relative(candidate.root, manifestPath).split(path.sep).join('/')
    if (entries.get(relative)?.type !== 'File') throw invalidEntry(`Declared npm library requires its selected package manifest: ${relative}`)
    let manifest
    try {
      manifest = JSON.parse(fs.readFileSync(manifestPath))
    } catch (cause) {
      throw Object.assign(invalidEntry(`Declared npm library manifest must be JSON: ${relative}`), { cause })
    }
    if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest)) throw invalidEntry(`Declared npm library manifest must be an object: ${relative}`)
    return manifest
  }

  walkDeclaredNpmLibraries({
    npmRoot,
    readPackageManifest: packageManifest,
    onMissingLibrary({ name, requiredBy }) {
      throw invalidEntry(`Missing selected npm production library ${name} required by ${requiredBy}`)
    },
  })
}

function privateEnvironment(root) {
  for (const name of ['home', 'config', 'cache', 'data', 'state', 'tmp']) {
    fs.mkdirSync(path.join(root, name))
  }
  for (const name of ['user.npmrc', 'global.npmrc']) {
    fs.writeFileSync(path.join(root, name), '')
  }
  return {
    CI: 'true',
    HOME: path.join(root, 'home'),
    XDG_CONFIG_HOME: path.join(root, 'config'),
    XDG_CACHE_HOME: path.join(root, 'cache'),
    XDG_DATA_HOME: path.join(root, 'data'),
    XDG_STATE_HOME: path.join(root, 'state'),
    TMPDIR: path.join(root, 'tmp'),
    TMP: path.join(root, 'tmp'),
    TEMP: path.join(root, 'tmp'),
    PATH: '',
    npm_config_userconfig: path.join(root, 'user.npmrc'),
    npm_config_globalconfig: path.join(root, 'global.npmrc'),
    npm_config_cache: path.join(root, 'cache'),
    npm_config_prefix: root,
    npm_config_update_notifier: 'false',
  }
}

export async function prepareVerificationNodeTools({ archivePath, archiveSha256, parentDirectory, nodePath = 'toolchain/node/bin/node', npmCliPath = 'toolchain/npm/bin/npm-cli.js', signal, probeScope }) {
  signal?.throwIfAborted()
  if (probeScope !== undefined && typeof probeScope !== 'function') throw new TypeError('The selected tool probe scope must be a function')
  requireRolePath(nodePath, 'toolchain')
  requireRolePath(npmCliPath, 'toolchain/npm')
  const archiveBytes = fs.readFileSync(archivePath)
  const candidate = await materializeVerificationArchive({ archiveBytes, archiveSha256, parentDirectory, rootDirectory: 'toolchain', errorPrefix: 'verification-tool' })
  let probeRoot
  let probeOwner
  try {
    signal?.throwIfAborted()
    candidate.revalidate()
    const nodeEntry = selectedFile(candidate.entries, nodePath, 'toolchain')
    if ((nodeEntry.mode & 0o111) === 0) throw invalidEntry('Selected Node binary requires executable permission')
    const cliEntry = selectedFile(candidate.entries, npmCliPath, 'toolchain/npm')
    const manifestEntry = selectedFile(candidate.entries, 'toolchain/npm/package.json', 'toolchain/npm')
    let manifest
    try {
      manifest = JSON.parse(fs.readFileSync(path.join(candidate.root, manifestEntry.path)))
    } catch (cause) {
      throw Object.assign(invalidEntry('Selected npm package manifest must be JSON'), { cause })
    }
    if (manifest?.name !== 'npm' || typeof manifest.version !== 'string' || !/^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$/.test(manifest.version)) throw invalidEntry('Selected npm package requires its declared npm version')
    validateDeclaredNpmLibraries(candidate)
    probeOwner = allocateVerificationDirectory(parentDirectory, 'verification-tool-probe-', invalidEntry)
    probeRoot = probeOwner.root
    const environment = privateEnvironment(probeRoot)
    const nodeExecutable = path.join(candidate.root, nodePath)
    let runtime
    let npmVersion
    const runProbes = async (deadline, probeSignal) => {
      const options = { cwd: candidate.root, env: environment, signal: probeSignal, deadline }
      const runtimeOutput = await runVerificationToolProbe(nodeExecutable, ['--input-type=module', '-e', 'console.log(JSON.stringify({ version: process.version, platform: process.platform, arch: process.arch }))'], options)
      probeOwner.assertOwned()
      candidate.revalidate()
      if (deadline !== undefined && Date.now() >= deadline) throw deadlineFailure(deadline)
      try {
        runtime = JSON.parse(runtimeOutput)
      } catch (cause) {
        throw Object.assign(invalidEntry('Selected Node must report its actual runtime identity'), { cause })
      }
      if (typeof runtime?.version !== 'string' || !/^v\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$/.test(runtime.version) || typeof runtime.platform !== 'string' || !runtime.platform || typeof runtime.arch !== 'string' || !runtime.arch) throw invalidEntry('Selected Node must report a valid runtime identity')
      npmVersion = await runVerificationToolProbe(nodeExecutable, [path.join(candidate.root, npmCliPath), '--version'], options)
      probeOwner.assertOwned()
      candidate.revalidate()
      if (deadline !== undefined && Date.now() >= deadline) throw deadlineFailure(deadline)
    }
    if (probeScope === undefined) {
      await runProbes(undefined, signal)
    } else {
      const controller = new AbortController()
      const probeSignal = signal ? AbortSignal.any([signal, controller.signal]) : controller.signal
      let invocation
      let attempted = false
      let settled = false
      let open = true
      let scopeFailure
      const run = options => {
        if (!open || attempted) {
          const error = invalidEntry('The selected tool probe scope must start its operation exactly once')
          scopeFailure ??= { error }
          controller.abort(scopeFailure.error)
          throw error
        }
        attempted = true
        try {
          const deadline = options?.deadline
          if (!Number.isSafeInteger(deadline)) throw new TypeError('The selected tool probe scope requires an absolute safe-integer deadline')
          if (Date.now() >= deadline) throw deadlineFailure(deadline)
          probeSignal.throwIfAborted()
          probeOwner.assertOwned()
          candidate.revalidate()
          if (Date.now() >= deadline) throw deadlineFailure(deadline)
          invocation = runProbes(deadline, probeSignal)
          invocation.then(() => { settled = true }, () => { settled = true })
          return invocation
        } catch (error) {
          scopeFailure ??= { error }
          throw error
        }
      }
      try {
        const scoped = probeScope('selected Node runtime and npm identity probes', run)
        if (scoped !== null && (typeof scoped === 'object' || typeof scoped === 'function') && typeof scoped.then === 'function') await scoped
      } catch (error) {
        scopeFailure ??= { error }
      } finally {
        open = false
      }
      if (!scopeFailure && (invocation === undefined || !settled)) scopeFailure = { error: invalidEntry('The selected tool probe scope must await its actual operation') }
      if (scopeFailure) controller.abort(scopeFailure.error)
      if (invocation !== undefined) {
        const [outcome] = await Promise.allSettled([invocation])
        if (outcome.status === 'rejected') {
          if (scopeFailure && scopeFailure.error !== outcome.reason) throw new AggregateError([scopeFailure.error, outcome.reason], 'Tool probe scope and owned operation failed', { cause: scopeFailure.error })
          throw outcome.reason
        }
      }
      if (scopeFailure) throw scopeFailure.error
    }
    if (npmVersion !== manifest.version) throw invalidEntry('Actual npm version differs from the selected package manifest')
    signal?.throwIfAborted()
    probeOwner.dispose()
    const entriesDigest = sha256(JSON.stringify(candidate.entries))
    const node = { path: nodePath, sha256: nodeEntry.sha256, version: runtime.version, platform: runtime.platform, arch: runtime.arch }
    const npm = { cliPath: npmCliPath, cliSha256: cliEntry.sha256, manifestSha256: manifestEntry.sha256, version: npmVersion }
    const identityScope = 'selected-node-npm-bundle'
    const toolDigest = sha256(JSON.stringify({ archiveSha256, entriesDigest, node, npm, identityScope }))
    return { toolRoot: candidate.root, entries: candidate.entries, entriesDigest, archiveSha256, node, npm, toolDigest, identityScope, revalidate: candidate.revalidate, dispose: candidate.dispose }
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
    if (failures.length > 1) throw new AggregateError(failures, 'Tool preparation and cleanup failed', { cause: error })
    throw error
  }
}
