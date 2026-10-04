import { spawn } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { create } from 'tar'
import { prepareVerificationDependencies } from './verification-dependency-candidate.mjs'
import { prepareVerificationNodeTools } from './verification-node-tools.mjs'
import { rejectSymbolicVerificationInput } from './verification-input-path.mjs'

const digest = bytes => createHash('sha256').update(bytes).digest('hex')
const lockInvalid = message => Object.assign(new Error(message), { code: 'verification-npm-lock-invalid' })

function registryUrl(value) {
  let url
  try {
    url = new URL(value)
  } catch {
    throw lockInvalid('Npm preparation requires an explicit registry URL')
  }
  if (url.username || url.password || url.search || url.hash || (url.protocol !== 'https:' && !(url.protocol === 'http:' && ['127.0.0.1', '[::1]', 'localhost'].includes(url.hostname)))) {
    throw lockInvalid('Npm preparation requires HTTPS or a loopback fixture registry without credentials')
  }
  if (!url.pathname.endsWith('/')) url.pathname += '/'
  return url
}

function unsupportedDependencyAuthority(spec) {
  if (typeof spec !== 'string') return true
  if (spec.startsWith('npm:')) return false
  return /[:/\\]/.test(spec) || spec.startsWith('.') || /[.](?:tgz|tar[.]gz|tar)$/i.test(spec)
}

function validateDependencies(manifest) {
  for (const field of ['dependencies', 'devDependencies', 'optionalDependencies', 'peerDependencies']) {
    for (const spec of Object.values(manifest[field] ?? {})) {
      if (unsupportedDependencyAuthority(spec)) throw lockInvalid(`Unsupported npm dependency authority: ${spec}`)
    }
  }
  function validateOverrides(overrides) {
    for (const override of Object.values(overrides ?? {})) {
      if (typeof override === 'string') {
        if (!override.startsWith('$') && unsupportedDependencyAuthority(override)) throw lockInvalid(`Unsupported npm override authority: ${override}`)
      } else if (override && typeof override === 'object' && !Array.isArray(override)) validateOverrides(override)
      else throw lockInvalid('Invalid npm override declaration')
    }
  }
  validateOverrides(manifest.overrides)
}

function validateLock(manifest, lock, registry, expectedNpmVersion) {
  if (!manifest || typeof manifest !== 'object' || Array.isArray(manifest) || !lock || typeof lock !== 'object' || Array.isArray(lock)) throw lockInvalid('Npm package and lockfile must be objects')
  if (manifest.workspaces || lock.packages?.['']?.workspaces) throw lockInvalid('Workspace dependencies require a separate preparation contract')
  if (manifest.packageManager && manifest.packageManager !== `npm@${expectedNpmVersion}`) throw lockInvalid('Selected npm version differs from packageManager')
  if (lock.lockfileVersion !== 3 || !lock.packages || typeof lock.packages !== 'object' || !lock.packages['']) throw lockInvalid('Npm preparation requires a complete version 3 lockfile')
  validateDependencies(manifest)
  for (const [memberPath, member] of Object.entries(lock.packages)) {
    if (!member || typeof member !== 'object' || member.link) throw lockInvalid(`Unsupported npm lock member: ${memberPath}`)
    validateDependencies(member)
    if (memberPath === '') continue
    const segments = memberPath.split('/')
    if (segments[0] !== 'node_modules' || segments.some(segment => !segment || ['.', '..', '.git'].includes(segment.toLowerCase()) || segment.includes('\\'))) throw lockInvalid(`Invalid npm lock path: ${memberPath}`)
    let resolved
    try {
      resolved = new URL(member.resolved)
    } catch {
      throw lockInvalid(`Npm lock member requires a registry tarball: ${memberPath}`)
    }
    if (resolved.origin !== registry.origin || !resolved.pathname.startsWith(registry.pathname) || resolved.username || resolved.password || resolved.search || resolved.hash) throw lockInvalid(`Npm lock tarball lies outside the selected registry: ${memberPath}`)
    const integrity = /^(sha256|sha512)-([A-Za-z0-9+/]+={0,2})$/.exec(member.integrity ?? '')
    if (!integrity || Buffer.from(integrity[2], 'base64').length !== (integrity[1] === 'sha512' ? 64 : 32) || Buffer.from(integrity[2], 'base64').toString('base64') !== integrity[2]) throw lockInvalid(`Npm lock member requires a complete integrity identity: ${memberPath}`)
  }
}

function installedEntries(root) {
  const entries = []
  function visit(relativePath) {
    const absolute = path.join(root, relativePath)
    const stat = fs.lstatSync(absolute)
    if (stat.isSymbolicLink()) entries.push({ path: relativePath, type: 'SymbolicLink', target: fs.readlinkSync(absolute) })
    else if (stat.isDirectory()) {
      entries.push({ path: relativePath, type: 'Directory', mode: stat.mode & 0o777 })
      for (const child of fs.readdirSync(absolute).sort()) visit(`${relativePath}/${child}`)
    } else if (stat.isFile() && stat.nlink === 1) {
      const bytes = fs.readFileSync(absolute)
      entries.push({ path: relativePath, type: 'File', mode: stat.mode & 0o777, size: bytes.length, sha256: digest(bytes) })
    } else throw lockInvalid(`Unsupported installed npm entry: ${relativePath}`)
  }
  visit('node_modules')
  return entries.sort((left, right) => left.path < right.path ? -1 : left.path > right.path ? 1 : 0)
}

function runBootstrap(nodeExecutable, argv, { cwd, env, signal, output }) {
  signal?.throwIfAborted()
  return new Promise((resolve, reject) => {
    const child = spawn(nodeExecutable, argv, { cwd, env, detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] })
    let failure
    let stdout = ''
    let stderr = ''
    const stop = () => {
      try {
        if (process.platform === 'win32') child.kill('SIGKILL')
        else if (child.pid) process.kill(-child.pid, 'SIGKILL')
      } catch (error) {
        if (error.code !== 'ESRCH') failure ??= { error }
      }
    }
    const abort = () => {
      failure ??= { error: signal.reason }
      stop()
    }
    signal?.addEventListener('abort', abort, { once: true })
    if (signal?.aborted) abort()
    child.on('error', error => { failure ??= { error } })
    child.stdout.setEncoding('utf8').on('data', chunk => {
      stdout = (stdout + chunk).slice(-65536)
      try {
        output?.write(chunk)
      } catch (error) {
        failure ??= { error }
        stop()
      }
    })
    child.stdout.on('error', error => {
      failure ??= { error }
      stop()
    })
    child.stderr.setEncoding('utf8').on('data', chunk => {
      stderr = (stderr + chunk).slice(-65536)
      try {
        output?.write(chunk)
      } catch (error) {
        failure ??= { error }
        stop()
      }
    })
    child.stderr.on('error', error => {
      failure ??= { error }
      stop()
    })
    child.once('exit', stop)
    child.once('close', (exitCode, exitSignal) => {
      signal?.removeEventListener('abort', abort)
      if (failure) reject(failure.error)
      else if (exitCode !== 0) reject(Object.assign(new Error('Npm dependency preparation failed'), { code: 'verification-npm-install-failed', exitCode, signal: exitSignal, stdout, stderr }))
      else resolve(stdout)
    })
  })
}

function readNpmSource({ sourceRoot, expectedNpmVersion, registry, signal }) {
  signal?.throwIfAborted()
  rejectSymbolicVerificationInput(sourceRoot)
  const sourceBytes = Object.fromEntries(['package.json', 'package-lock.json'].map(name => {
    const file = path.join(sourceRoot, name)
    if (!rejectSymbolicVerificationInput(file).isFile()) throw lockInvalid(`Npm source must be a regular file: ${name}`)
    return [name, fs.readFileSync(file)]
  }))
  let manifest
  let lock
  try {
    manifest = JSON.parse(sourceBytes['package.json'])
    lock = JSON.parse(sourceBytes['package-lock.json'])
  } catch (cause) {
    throw Object.assign(lockInvalid('Unparseable npm source or lockfile'), { cause })
  }
  const selectedRegistry = registryUrl(registry)
  validateLock(manifest, lock, selectedRegistry, expectedNpmVersion)
  return { sourceBytes, selectedRegistry }
}

export async function installVerificationDependencies({ sourceRoot, parentDirectory, nodeExecutable, npmCli, nodeSha256, npmCliSha256, expectedNpmVersion, registry = 'https://registry.npmjs.org/', signal, output }) {
  const source = readNpmSource({ sourceRoot, expectedNpmVersion, registry, signal })
  const nodeBytes = fs.readFileSync(nodeExecutable)
  if (!/^[a-f0-9]{64}$/.test(nodeSha256) || !/^[a-f0-9]{64}$/.test(npmCliSha256) || digest(nodeBytes) !== nodeSha256 || digest(fs.readFileSync(npmCli)) !== npmCliSha256) {
    throw Object.assign(new Error('Npm bootstrap files differ from their explicit selected identities'), { code: 'verification-npm-tool-invalid' })
  }
  return installSelectedDependencies({ ...source, parentDirectory, execution: { kind: 'bootstrap', nodeBytes, npmCli: path.resolve(npmCli), nodeSha256, npmCliSha256, expectedNpmVersion }, signal, output })
}

export async function installVerificationDependenciesFromToolArchive({ sourceRoot, parentDirectory, toolArchive, expectedNpmVersion, expectedNodeVersion, registry = 'https://registry.npmjs.org/', signal, output }) {
  const source = readNpmSource({ sourceRoot, expectedNpmVersion, registry, signal })
  let tools
  let candidate
  try {
    tools = await prepareVerificationNodeTools({ archivePath: toolArchive.archivePath, archiveSha256: toolArchive.archiveSha256, nodePath: toolArchive.nodePath, npmCliPath: toolArchive.npmCliPath, parentDirectory, signal })
    if (tools.npm.version !== expectedNpmVersion || (expectedNodeVersion !== undefined && tools.node.version !== expectedNodeVersion)) {
      throw Object.assign(new Error('Actual selected tool versions differ from the installation contract'), { code: 'verification-npm-tool-invalid' })
    }
    candidate = await installSelectedDependencies({
      ...source,
      parentDirectory,
      execution: { kind: 'selected-bundle', nodeExecutable: path.join(tools.toolRoot, tools.node.path), npmCli: path.join(tools.toolRoot, tools.npm.cliPath), nodeSha256: tools.node.sha256, npmCliSha256: tools.npm.cliSha256, expectedNpmVersion, nodeVersion: tools.node.version, toolDigest: tools.toolDigest, revalidate: tools.revalidate },
      signal,
      output,
    })
    signal?.throwIfAborted()
    tools.revalidate()
    tools.dispose()
    return candidate
  } catch (error) {
    const errors = [error]
    try {
      candidate?.dispose()
    } catch (cleanupError) {
      errors.push(cleanupError)
    }
    try {
      tools?.dispose()
    } catch (cleanupError) {
      errors.push(cleanupError)
    }
    if (errors.length > 1) throw new AggregateError(errors, 'Tool archive installation and cleanup failed', { cause: error })
    throw error
  }
}

async function installSelectedDependencies({ sourceBytes, selectedRegistry, parentDirectory, execution, signal, output }) {
  signal?.throwIfAborted()
  const installationRoot = fs.mkdtempSync(path.join(path.resolve(parentDirectory), 'verification-npm-install-'))
  let candidate
  try {
    const workspace = path.join(installationRoot, 'workspace')
    const env = { CI: 'true' }
    for (const name of ['workspace', 'home', 'config', 'cache', 'data', 'state', 'tmp', 'tools']) fs.mkdirSync(path.join(installationRoot, name))
    for (const [name, bytes] of Object.entries(sourceBytes)) fs.writeFileSync(path.join(workspace, name), bytes, { flag: 'wx' })
    const privateNode = execution.kind === 'bootstrap' ? path.join(installationRoot, 'tools/node') : execution.nodeExecutable
    if (execution.kind === 'bootstrap') fs.writeFileSync(privateNode, execution.nodeBytes, { flag: 'wx', mode: 0o755 })
    for (const name of ['user.npmrc', 'global.npmrc']) fs.writeFileSync(path.join(installationRoot, name), '')
    Object.assign(env, {
      HOME: path.join(installationRoot, 'home'),
      XDG_CONFIG_HOME: path.join(installationRoot, 'config'),
      XDG_CACHE_HOME: path.join(installationRoot, 'cache'),
      XDG_DATA_HOME: path.join(installationRoot, 'data'),
      XDG_STATE_HOME: path.join(installationRoot, 'state'),
      TMPDIR: path.join(installationRoot, 'tmp'),
      TMP: path.join(installationRoot, 'tmp'),
      TEMP: path.join(installationRoot, 'tmp'),
      PATH: path.dirname(privateNode),
      npm_config_userconfig: path.join(installationRoot, 'user.npmrc'),
      npm_config_globalconfig: path.join(installationRoot, 'global.npmrc'),
      npm_config_cache: path.join(installationRoot, 'cache'),
      npm_config_prefix: workspace,
      npm_config_fetch_retries: '0',
      npm_config_update_notifier: 'false',
    })
    const options = { cwd: workspace, env, signal, output }
    const runtime = JSON.parse(await runBootstrap(privateNode, ['--input-type=module', '-e', 'console.log(JSON.stringify({ platform: process.platform, arch: process.arch }))'], options))
    if (typeof runtime.platform !== 'string' || typeof runtime.arch !== 'string') throw Object.assign(new Error('Npm bootstrap runtime did not report its platform identity'), { code: 'verification-npm-tool-invalid' })
    const npmVersion = (await runBootstrap(privateNode, [execution.npmCli, '--version'], options)).trim()
    if (npmVersion !== execution.expectedNpmVersion) throw Object.assign(new Error('Actual npm version differs from its selected version'), { code: 'verification-npm-tool-invalid' })
    if (execution.kind === 'selected-bundle') execution.revalidate()
    await runBootstrap(privateNode, [execution.npmCli, 'ci', '--ignore-scripts', '--include=dev', '--include=optional', '--no-audit', '--no-fund', '--workspaces=false', '--install-strategy=hoisted', `--registry=${selectedRegistry.href}`], options)
    signal?.throwIfAborted()
    for (const [name, bytes] of Object.entries(sourceBytes)) {
      const installed = path.join(workspace, name)
      if (!rejectSymbolicVerificationInput(installed).isFile() || !fs.readFileSync(installed).equals(bytes)) throw lockInvalid(`Npm changed selected source bytes: ${name}`)
    }
    const expectedEntries = installedEntries(workspace)
    const chunks = []
    for await (const chunk of create({ cwd: workspace, portable: false, noMtime: true }, ['node_modules'])) {
      signal?.throwIfAborted()
      chunks.push(chunk)
    }
    const archive = Buffer.concat(chunks)
    const archivePath = path.join(installationRoot, 'dependencies.tar')
    fs.writeFileSync(archivePath, archive, { flag: 'wx' })
    candidate = await prepareVerificationDependencies({ sourceRoot: workspace, parentDirectory, archivePath, archiveSha256: digest(archive) })
    signal?.throwIfAborted()
    if (JSON.stringify(candidate.entries) !== JSON.stringify(expectedEntries)) throw lockInvalid('Prepared dependency inventory differs from the complete npm installation')
    if (execution.kind === 'selected-bundle') execution.revalidate()
    const packageJsonSha256 = digest(sourceBytes['package.json'])
    const installation = { npmVersion, nodeSha256: execution.nodeSha256, npmCliSha256: execution.npmCliSha256, platform: runtime.platform, arch: runtime.arch, lifecycleScripts: 'disabled', identityScope: execution.kind === 'bootstrap' ? 'bootstrap-admission' : 'selected-node-npm-bundle' }
    if (execution.kind === 'selected-bundle') Object.assign(installation, { nodeVersion: execution.nodeVersion, toolDigest: execution.toolDigest })
    const dependencyDigest = digest(JSON.stringify({ preparedDependencyDigest: candidate.dependencyDigest, packageJsonSha256, installation }))
    fs.rmSync(installationRoot, { recursive: true, force: true })
    return { ...candidate, packageJsonSha256, installation, dependencyDigest }
  } catch (error) {
    const errors = [error]
    try {
      candidate?.dispose()
    } catch (cleanupError) {
      errors.push(cleanupError)
    }
    try {
      fs.rmSync(installationRoot, { recursive: true, force: true })
    } catch (cleanupError) {
      errors.push(cleanupError)
    }
    if (errors.length > 1) throw new AggregateError(errors, 'Npm preparation and cleanup failed', { cause: error })
    throw error
  }
}
