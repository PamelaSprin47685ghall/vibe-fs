import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import http from 'node:http'
import os from 'node:os'
import path from 'node:path'
import test, { after } from 'node:test'
import { create } from 'tar'
import { createNpmInstallFixture } from './npm-install-fixture.mjs'
import { fixturePhase } from './fixture-phase.mjs'
import { withFinitePhysicalWait } from './finite-physical-wait.mjs'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')

function physicalProbeScope(context) {
  return (operation, run) => {
    const deadline = Date.now() + 5000
    return withFinitePhysicalWait(context, { operation, limitMs: deadline - Date.now() }, () => run({ deadline }))
  }
}

export async function createAbortedNpmAdmissionFixture() {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'verification-aborted-npm-')))
  const requests = []
  const sockets = new Set()
  const server = http.createServer((request, response) => {
    requests.push({ method: request.method, path: request.url })
    response.writeHead(500).end('An already aborted operation cannot request the registry')
  })
  server.on('connection', socket => {
    sockets.add(socket)
    socket.once('close', () => sockets.delete(socket))
  })
  const inventory = () => {
    const entries = []
    const visit = relative => {
      const file = path.join(root, relative)
      const stat = fs.lstatSync(file)
      const entry = { path: relative, device: stat.dev, inode: stat.ino, mode: stat.mode & 0o777 }
      if (stat.isDirectory()) {
        entries.push({ ...entry, type: 'Directory' })
        for (const name of fs.readdirSync(file).sort()) visit(relative === '.' ? name : `${relative}/${name}`)
      } else if (stat.isFile()) {
        entries.push({ ...entry, type: 'File', bytes: fs.readFileSync(file).toString('hex') })
      } else if (stat.isSymbolicLink()) {
        entries.push({ ...entry, type: 'SymbolicLink', target: fs.readlinkSync(file) })
      } else {
        throw new Error(`Unexpected admission fixture member: ${relative}`)
      }
    }
    visit('.')
    return entries
  }
  const parentDirectory = path.join(root, 'unallocated-candidates')
  try {
    fs.writeFileSync(path.join(root, 'caller-owned-input.txt'), 'caller bytes\r\n\0中文 😀  \n', { mode: 0o640 })
    const before = inventory()
    await new Promise((resolve, reject) => {
      server.once('error', reject)
      server.listen(0, '127.0.0.1', resolve)
    })
    return {
      root,
      options: {
        sourceRoot: path.join(root, 'missing-source'),
        parentDirectory,
        nodeExecutable: path.join(root, 'missing-node'),
        npmCli: path.join(root, 'missing-npm-cli.js'),
        nodeSha256: '0'.repeat(64),
        npmCliSha256: '0'.repeat(64),
        toolArchive: { archivePath: path.join(root, 'missing-tools.tar'), archiveSha256: '0'.repeat(64) },
        expectedNpmVersion: '0.0.0',
        registry: `http://127.0.0.1:${server.address().port}`,
      },
      assertUnchanged() {
        assert.deepEqual(requests, [], 'Already aborted admission cannot request the actual registry')
        assert.equal(fs.existsSync(parentDirectory), false, 'Already aborted admission cannot allocate its candidate parent')
        assert.deepEqual(inventory(), before, 'Already aborted admission preserves the exact private root identity and every member')
      },
      async dispose() {
        for (const socket of sockets) socket.destroy()
        try {
          await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()))
        } finally {
          fs.rmSync(root, { recursive: true, force: true })
        }
      },
    }
  } catch (error) {
    for (const socket of sockets) socket.destroy()
    if (server.listening) await new Promise(resolve => server.close(resolve))
    fs.rmSync(root, { recursive: true, force: true })
    throw error
  }
}

const installFromArchive = async options => {
  fixturePhase('npm:archive-install-enter', { archivePath: options.toolArchive.archivePath })
  try {
    return await (await import('../../../../scripts/lib/verification-npm-candidate.mjs')).installVerificationDependenciesFromToolArchive(options)
  } finally {
    fixturePhase('npm:archive-install-settled', { archivePath: options.toolArchive.archivePath })
  }
}

function selectedEntries(root) {
  const entries = []
  function read(relative) {
    const absolute = path.join(root, relative)
    const stat = fs.lstatSync(absolute)
    if (stat.isSymbolicLink()) entries.push({ path: relative, type: 'SymbolicLink', target: fs.readlinkSync(absolute) })
    else if (stat.isDirectory()) {
      entries.push({ path: relative, type: 'Directory', mode: stat.mode & 0o777 })
      for (const name of fs.readdirSync(absolute)) read(`${relative}/${name}`)
    } else {
      assert.ok(stat.isFile())
      entries.push({ path: relative, type: 'File', mode: stat.mode & 0o777, size: stat.size, sha256: sha256(fs.readFileSync(absolute)) })
    }
  }
  read('toolchain')
  return entries.sort((left, right) => left.path < right.path ? -1 : left.path > right.path ? 1 : 0)
}

async function toolArchiveFixture({ observeVersions = false, prepareReplacement = false } = {}) {
  const fixture = await createNpmInstallFixture()
  try {
    fixturePhase('npm:tool-capture-enter', { root: fixture.root })
    const selected = path.join(fixture.root, 'selected')
    const nodePath = 'toolchain/node/bin/node'
    const npmCliPath = 'toolchain/npm/bin/npm-cli.js'
    fs.mkdirSync(path.join(selected, 'toolchain/node/bin'), { recursive: true })
    fs.chmodSync(path.join(selected, 'toolchain/node'), 0o775)
    fs.copyFileSync(fixture.options.nodeExecutable, path.join(selected, nodePath))
    fs.chmodSync(path.join(selected, nodePath), 0o755)
    fs.cpSync(path.resolve(fixture.options.npmCli, '../..'), path.join(selected, 'toolchain/npm'), { recursive: true, verbatimSymlinks: true })
    fs.writeFileSync(path.join(selected, 'toolchain/node/sibling.txt'), 'selected Node layout')
    const executionMarker = path.join(fixture.root, 'execution.json')
    const versionMarker = path.join(fixture.root, 'version-calls.txt')
    const cli = path.join(selected, npmCliPath)
    const original = fs.readFileSync(cli, 'utf8')
    const shebangEnd = original.startsWith('#!') ? original.indexOf('\n') + 1 : 0
    const observe = `
${observeVersions ? `if (process.argv.includes('--version')) require('node:fs').appendFileSync(${JSON.stringify(versionMarker)}, 'version\\n')` : ''}
if (process.argv.includes('ci')) {
  const fixtureFs = require('node:fs')
  const fixturePath = require('node:path')
  if (fixtureFs.readFileSync(fixturePath.resolve(process.execPath, '../../sibling.txt'), 'utf8') !== 'selected Node layout') throw new Error('Selected Node directory layout was lost')
  fixtureFs.writeFileSync(${JSON.stringify(executionMarker)}, JSON.stringify({ nodeExecutable: process.execPath, npmCli: __filename, pid: process.pid }))
}
`
    fs.writeFileSync(cli, original.slice(0, shebangEnd) + observe + original.slice(shebangEnd))
    const archivePath = path.join(fixture.root, 'tools.tar')
    const chunks = []
    for await (const chunk of create({ cwd: selected, portable: false, noMtime: true }, ['toolchain'])) chunks.push(chunk)
    const archive = Buffer.concat(chunks)
    fs.writeFileSync(archivePath, archive)
    fixturePhase('npm:tool-archive-written', { archivePath, bytes: archive.length })
    const entries = selectedEntries(selected)
    const archiveSha256 = sha256(archive)
    const node = { path: nodePath, sha256: sha256(fs.readFileSync(path.join(selected, nodePath))), version: process.version, platform: process.platform, arch: process.arch }
    const manifest = fs.readFileSync(path.join(selected, 'toolchain/npm/package.json'))
    const npm = { cliPath: npmCliPath, cliSha256: sha256(fs.readFileSync(cli)), manifestSha256: sha256(manifest), version: JSON.parse(manifest).version }
    const identityScope = 'selected-node-npm-bundle'
    const toolDigest = sha256(JSON.stringify({ archiveSha256, entriesDigest: sha256(JSON.stringify(entries)), node, npm, identityScope }))
    if (!prepareReplacement) fs.rmSync(selected, { recursive: true })
    return {
      ...fixture,
      preparedReplacement: prepareReplacement ? selected : undefined,
      executionMarker,
      versionMarker,
      toolDigest,
      capturedSelection: { entries, node, npm, identityScope },
      toolArchive: { archivePath, archiveSha256, nodePath, npmCliPath },
      installOptions: { sourceRoot: fixture.sourceRoot, parentDirectory: fixture.parentDirectory, toolArchive: { archivePath, archiveSha256, nodePath, npmCliPath }, expectedNpmVersion: npm.version, expectedNodeVersion: node.version, registry: fixture.registry },
    }
  } catch (error) {
    try {
      await fixture.dispose()
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Complete tool archive capture and cleanup failed', { cause: error })
    }
    throw error
  }
}

async function captureToolArchive(options) {
  let fixture
  try {
    fixture = await toolArchiveFixture(options)
    const packageBytes = fs.readFileSync(path.join(fixture.sourceRoot, 'package.json'))
    const lockBytes = fs.readFileSync(path.join(fixture.sourceRoot, 'package-lock.json'))
    const { entries, node, npm, identityScope } = fixture.capturedSelection
    assert.equal(sha256(fs.readFileSync(fixture.toolArchive.archivePath)), fixture.toolArchive.archiveSha256)
    assert.equal(node.path, fixture.toolArchive.nodePath)
    assert.equal(npm.cliPath, fixture.toolArchive.npmCliPath)
    assert.equal(entries.find(entry => entry.path === node.path)?.sha256, node.sha256)
    assert.equal(node.sha256, sha256(fs.readFileSync(fixture.options.nodeExecutable)))
    assert.equal(entries.find(entry => entry.path === npm.cliPath)?.sha256, npm.cliSha256)
    assert.equal(entries.find(entry => entry.path === 'toolchain/npm/package.json')?.sha256, npm.manifestSha256)
    assert.ok(entries.some(entry => entry.path === 'toolchain/npm/lib/cli.js' && entry.type === 'File'))
    assert.equal(node.version, process.version)
    assert.equal(npm.version, fixture.installOptions.expectedNpmVersion)
    assert.equal(JSON.parse(packageBytes).packageManager, `npm@${npm.version}`)
    assert.equal(JSON.parse(lockBytes).lockfileVersion, 3)
    assert.equal(fixture.toolDigest, sha256(JSON.stringify({ archiveSha256: fixture.toolArchive.archiveSha256, entriesDigest: sha256(JSON.stringify(entries)), node, npm, identityScope })))
    assert.deepEqual(fixture.requests, [])
    assert.equal(fs.existsSync(fixture.executionMarker), false)
    assert.equal(fs.existsSync(fixture.versionMarker), false)
    assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
    if (fixture.preparedReplacement) {
      assert.deepEqual(fs.readdirSync(fixture.preparedReplacement), ['toolchain'])
      assert.deepEqual(selectedEntries(fixture.preparedReplacement), entries)
    }
  } catch (error) {
    try {
      await fixture?.dispose()
    } catch (cleanupError) {
      throw new AggregateError([error, cleanupError], 'Tool archive capture verification and cleanup failed', { cause: error })
    }
    throw error
  }
  return fixture
}

async function waitForHeldInstall(fixture, installing) {
  await Promise.race([fixture.leafRequest, installing.then(() => { throw new Error('Installation completed before the real held tarball') })])
  const execution = JSON.parse(fs.readFileSync(fixture.executionMarker, 'utf8'))
  const toolRoot = path.resolve(execution.nodeExecutable, '../../../..')
  assert.equal(execution.nodeExecutable, path.join(toolRoot, fixture.toolArchive.nodePath))
  assert.equal(execution.npmCli, path.join(toolRoot, fixture.toolArchive.npmCliPath))
  assert.equal(path.dirname(toolRoot), fs.realpathSync(fixture.parentDirectory))
  assert.ok(Number.isInteger(execution.pid))
  process.kill(execution.pid, 0)
  assert.deepEqual(selectedEntries(toolRoot), fixture.capturedSelection.entries)
  assert.ok(fixture.requests.some(request => request.method === 'GET' && request.path === `/${fixture.leafName}/-/${fixture.leafName}-1.0.0.tgz`))
  return toolRoot
}

function preparedToolArchiveTest(label) {
  const state = { controller: new AbortController(), fixture: null, installer: null }
  state.dispose = async () => {
    state.controller.abort(new Error('prepared tool archive fixture cleanup'))
    const failures = []
    try {
      state.installer?.dispose()
    } catch (error) {
      failures.push(error)
    } finally {
      state.installer = null
    }
    try {
      await state.fixture?.dispose()
    } catch (error) {
      failures.push(error)
    } finally {
      state.fixture = null
    }
    if (failures.length) throw new AggregateError(failures, 'Prepared tool archive fixture cleanup failed', { cause: failures[0] })
  }
  after(state.dispose)
  test(`WHAT[verification-system-016] actual selected tool preparation binds the complete bundle before ${label}`, async context => {
    try {
      state.fixture = await captureToolArchive()
      const { prepareVerificationNpmInstallationFromToolArchive } = await import('../../../../scripts/lib/verification-npm-candidate.mjs')
      state.installer = await prepareVerificationNpmInstallationFromToolArchive({
        ...state.fixture.installOptions,
        signal: state.controller.signal,
        probeScope: physicalProbeScope(context),
      })
      assert.deepEqual(state.installer.installation, {
        identityScope: 'selected-node-npm-bundle',
        toolDigest: state.fixture.toolDigest,
        nodeVersion: process.version,
        npmVersion: state.fixture.installOptions.expectedNpmVersion,
      })
      assert.equal(Object.isFrozen(state.installer), true)
      assert.equal(Object.isFrozen(state.installer.installation), true)
      assert.deepEqual(state.fixture.requests, [], 'Preparation cannot execute npm ci or contact the registry')
      assert.equal(fs.existsSync(state.fixture.executionMarker), false)
      const names = fs.readdirSync(state.fixture.parentDirectory)
      assert.equal(names.length, 1)
      assert.ok(names[0].startsWith('verification-archive-'))
      assert.deepEqual(selectedEntries(path.join(state.fixture.parentDirectory, names[0])), state.fixture.capturedSelection.entries)
    } catch (error) {
      try {
        await state.dispose()
      } catch (cleanupError) {
        throw new AggregateError([error, cleanupError], 'Selected tool preparation proof and cleanup failed', { cause: error })
      }
      throw error
    }
  })
  return state
}

export function registerNpmToolArchiveTests() {
  test('WHAT[verification-system-016] selected npm tools replaced during the actual runtime probe cannot start the next version probe', async t => {
    let fixture
    let replacedRoot
    let parked
    let operationFailure
    const cleanupFailures = []
    try {
      fixture = await captureToolArchive({ observeVersions: true, prepareReplacement: true })
      const output = {
        write(chunk) {
          if (replacedRoot || !String(chunk).includes('"platform"')) return
          const names = fs.readdirSync(fixture.parentDirectory).filter(name => name.startsWith('verification-archive-'))
          assert.equal(names.length, 1)
          replacedRoot = path.join(fixture.parentDirectory, names[0])
          parked = path.join(fixture.root, 'parked-tools')
          const mode = fs.statSync(replacedRoot).mode & 0o777
          fs.chmodSync(fixture.preparedReplacement, mode)
          fs.renameSync(replacedRoot, parked)
          fs.renameSync(fixture.preparedReplacement, replacedRoot)
        },
      }
      const [outcome] = await Promise.allSettled([installFromArchive({ ...fixture.installOptions, output, probeScope: physicalProbeScope(t) })])
      assert.ok(replacedRoot, new Error('the actual installation runtime probe was observed', { cause: outcome.reason }))
      assert.equal(outcome.status, 'rejected')
      assert.ok(outcome.reason instanceof AggregateError)
      assert.equal(fs.readFileSync(fixture.versionMarker, 'utf8'), 'version\n', 'only the initial tool preparation version probe ran')
      assert.equal(outcome.reason.cause.code, 'verification-tool-entry-invalid')
      assert.equal(fs.existsSync(path.join(replacedRoot, fixture.toolArchive.npmCliPath)), true)
      assert.equal(fs.existsSync(parked), true, 'production cleanup does not guess the parked original directory')
      assert.deepEqual(fs.readdirSync(replacedRoot), ['toolchain'])
      assert.deepEqual(selectedEntries(replacedRoot), fixture.capturedSelection.entries)
      assert.deepEqual(selectedEntries(parked), fixture.capturedSelection.entries)
      const replacement = fs.statSync(replacedRoot)
      const original = fs.statSync(parked)
      assert.notDeepEqual([replacement.dev, replacement.ino], [original.dev, original.ino],
        'identical tool bytes and modes do not make the replacement the actual owned directory')
      assert.equal(replacement.mode & 0o777, original.mode & 0o777)
      assert.deepEqual(fixture.requests, [], 'no package download can start from a replaced tool owner')
    } catch (error) {
      operationFailure = { error }
    } finally {
      try {
        await fixture?.dispose()
      } catch (error) {
        cleanupFailures.push(error)
        try {
          fs.rmSync(fixture.root, { recursive: true, force: true })
        } catch (cleanupError) {
          cleanupFailures.push(cleanupError)
        }
      }
    }
    if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Runtime replacement fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
    if (operationFailure) throw operationFailure.error
  })
  const positive = preparedToolArchiveTest('the locked registry installation')
  test('WHAT[verification-system-016] two actual locked registry packages preserve modes use the selected bundle layout and bind its complete identity after tools cleanup', async () => {
    const fixture = positive.fixture
    let candidate
    let packageBytes
    let lockBytes
    let operationFailure
    let settled
    let originalUmask
    const controller = positive.controller
    const cleanupFailures = []
    try {
      assert.ok(positive.installer, 'The actual selected bundle must have passed preparation')
      packageBytes = fs.readFileSync(path.join(fixture.sourceRoot, 'package.json'))
      lockBytes = fs.readFileSync(path.join(fixture.sourceRoot, 'package-lock.json'))
      fixture.holdLeaf()
      originalUmask = process.umask(0o002)
      const installing = positive.installer.install()
      settled = Promise.allSettled([installing])
      await assert.rejects(positive.installer.install(), { code: 'verification-npm-install-state-invalid' })
      assert.throws(() => positive.installer.dispose(), { code: 'verification-npm-install-state-invalid' })
      await waitForHeldInstall(fixture, installing)
      fixture.releaseLeaf()
      candidate = await installing
      await assert.rejects(positive.installer.install(), { code: 'verification-npm-install-state-invalid' })
      const execution = JSON.parse(fs.readFileSync(fixture.executionMarker, 'utf8'))
      assert.ok(execution.nodeExecutable.endsWith(`/${fixture.toolArchive.nodePath}`))
      assert.ok(execution.npmCli.endsWith(`/${fixture.toolArchive.npmCliPath}`))
      assert.equal(fs.existsSync(execution.nodeExecutable), false)
      assert.equal(fs.existsSync(execution.npmCli), false)
      assert.equal(candidate.installation.identityScope, 'selected-node-npm-bundle')
      assert.equal(candidate.installation.toolDigest, fixture.toolDigest)
      assert.equal(candidate.installation.npmVersion, fixture.installOptions.expectedNpmVersion)
      assert.equal(candidate.installation.nodeVersion, process.version)
      assert.ok(candidate.entries.some(entry => entry.type === 'Directory' && entry.mode === 0o775), 'The complete dependency inventory must preserve the group-writable directories produced by this install')
      assert.equal(candidate.packageJsonSha256, sha256(packageBytes))
      assert.equal(candidate.lockfileSha256, sha256(lockBytes))
      const preparedDependencyDigest = sha256(JSON.stringify({ archiveSha256: candidate.archiveSha256, lockfileSha256: candidate.lockfileSha256, entries: candidate.entries }))
      assert.equal(candidate.dependencyDigest, sha256(JSON.stringify({ preparedDependencyDigest, packageJsonSha256: candidate.packageJsonSha256, installation: candidate.installation })))
      assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [path.basename(candidate.dependencyRoot)])
      assert.equal(execFileSync(process.execPath, ['--input-type=module', '-e', `import value from '${fixture.parentName}'; console.log(value)`], { cwd: candidate.dependencyRoot, encoding: 'utf8' }).trim(), '42')
      assert.ok(fixture.requests.some(request => request.path.includes(fixture.parentName) && request.path.endsWith('.tgz')))
      assert.ok(fixture.requests.some(request => request.path.includes(fixture.leafName) && request.path.endsWith('.tgz')))
      assert.deepEqual(fs.readFileSync(path.join(fixture.sourceRoot, 'package.json')), packageBytes)
      assert.deepEqual(fs.readFileSync(path.join(fixture.sourceRoot, 'package-lock.json')), lockBytes)
      candidate.dispose()
      assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
    } catch (error) {
      operationFailure = { error }
    } finally {
      controller.abort(new Error('tool archive positive fixture cleanup'))
      await settled
      if (originalUmask !== undefined) process.umask(originalUmask)
      try {
        candidate?.dispose()
      } catch (error) {
        cleanupFailures.push(error)
      }
      try {
        await positive.dispose()
      } catch (error) {
        cleanupFailures.push(error)
      }
    }
    if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Tool archive positive fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
    if (operationFailure) throw operationFailure.error
  })
  for (const mutation of ['non-CLI library bytes', 'new tool member']) {
    const prepared = preparedToolArchiveTest(`the held installation with ${mutation}`)
    test(`WHAT[verification-system-016] npm tool archive rejects ${mutation} changed during a real held install and reclaims unpublished dependencies`, async () => {
      const fixture = prepared.fixture
      const controller = prepared.controller
      let settled
      let operationFailure
      const cleanupFailures = []
      try {
        assert.ok(prepared.installer, 'The actual selected bundle must have passed preparation')
        fixture.holdLeaf()
        const installing = prepared.installer.install()
        settled = Promise.allSettled([installing])
        const toolRoot = await waitForHeldInstall(fixture, installing)
        if (mutation === 'non-CLI library bytes') fs.appendFileSync(path.join(toolRoot, 'toolchain/npm/lib/cli.js'), '\n// controlled library mutation\n')
        else fs.writeFileSync(path.join(toolRoot, 'toolchain/npm/bin/unselected-member.txt'), 'controlled extra member')
        fixture.releaseLeaf()
        await assert.rejects(installing, { code: 'verification-tool-entry-invalid' })
        await assert.rejects(prepared.installer.install(), { code: 'verification-npm-install-state-invalid' })
        await fixture.leafClosed
        assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
      } catch (error) {
        operationFailure = { error }
      } finally {
        controller.abort(new Error('tool mutation fixture cleanup'))
        await settled
        try {
          await prepared.dispose()
        } catch (error) {
          cleanupFailures.push(error)
        }
      }
      if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Tool mutation fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
      if (operationFailure) throw operationFailure.error
    })
  }
  for (const reason of [new Error('controlled tool archive install cancellation'), null]) {
    const prepared = preparedToolArchiveTest(`the held installation cancelled with ${reason === null ? 'null' : 'Error'}`)
    test(`WHAT[verification-system-016] npm tool archive held install cancellation preserves ${reason === null ? 'null' : 'Error'} and reclaims tools installation and dependencies`, async () => {
      const fixture = prepared.fixture
      const controller = prepared.controller
      let settled
      let operationFailure
      const cleanupFailures = []
      try {
        assert.ok(prepared.installer, 'The actual selected bundle must have passed preparation')
        fixture.holdLeaf()
        const installing = prepared.installer.install()
        settled = Promise.allSettled([installing])
        await waitForHeldInstall(fixture, installing)
        const execution = JSON.parse(fs.readFileSync(fixture.executionMarker, 'utf8'))
        assert.ok(Number.isInteger(execution.pid))
        controller.abort(reason)
        const [outcome] = await settled
        assert.equal(outcome.status, 'rejected')
        assert.equal(outcome.reason, reason)
        await assert.rejects(prepared.installer.install(), { code: 'verification-npm-install-state-invalid' })
        await fixture.leafClosed
        assert.throws(() => process.kill(execution.pid, 0), error => error.code === 'ESRCH')
        assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
      } catch (error) {
        operationFailure = { error }
      } finally {
        controller.abort(reason)
        await settled
        try {
          await prepared.dispose()
        } catch (error) {
          cleanupFailures.push(error)
        }
      }
      if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Tool archive cancellation fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
      if (operationFailure) throw operationFailure.error
    })
  }
  const unused = preparedToolArchiveTest('disposal without an installation')
  test('WHAT[verification-system-016] disposing a prepared tool owner prevents an installation before registry execution', async () => {
    let operationFailure
    try {
      assert.ok(unused.installer, 'The actual selected bundle must have passed preparation')
      unused.installer.dispose()
      assert.deepEqual(fs.readdirSync(unused.fixture.parentDirectory), [])
      await assert.rejects(unused.installer.install(), { code: 'verification-npm-install-state-invalid' })
      assert.deepEqual(unused.fixture.requests, [])
      assert.equal(fs.existsSync(unused.fixture.executionMarker), false)
    } catch (error) {
      operationFailure = { error }
    }
    try {
      await unused.dispose()
    } catch (cleanupError) {
      if (operationFailure) throw new AggregateError([operationFailure.error, cleanupError], 'Unused tool owner proof and cleanup failed', { cause: operationFailure.error })
      throw cleanupError
    }
    if (operationFailure) throw operationFailure.error
  })
  for (const role of ['Node', 'npm']) {
    test(`WHAT[verification-system-016] npm tool archive rejects a mismatched actual ${role} version before registry execution`, async mismatch => {
      const fixture = await captureToolArchive()
      let operationFailure
      const cleanupFailures = []
      try {
        const options = { ...fixture.installOptions, probeScope: physicalProbeScope(mismatch) }
        if (role === 'Node') options.expectedNodeVersion = 'v0.0.0'
        else {
          options.expectedNpmVersion = '0.0.0'
          const packagePath = path.join(fixture.sourceRoot, 'package.json')
          const manifest = JSON.parse(fs.readFileSync(packagePath))
          manifest.packageManager = 'npm@0.0.0'
          fs.writeFileSync(packagePath, JSON.stringify(manifest))
        }
        await assert.rejects(installFromArchive(options), { code: 'verification-npm-tool-invalid' })
        assert.deepEqual(fixture.requests, [])
        assert.equal(fs.existsSync(fixture.executionMarker), false)
        assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
      } catch (error) {
        operationFailure = { error }
      } finally {
        try {
          await fixture.dispose()
        } catch (error) {
          cleanupFailures.push(error)
        }
      }
      if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Tool archive version fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
      if (operationFailure) throw operationFailure.error
    })
  }
  for (const reason of [new Error('cancelled before tool archive installation'), null]) {
    test(`WHAT[verification-system-016] npm tool archive already aborted preserves ${reason === null ? 'null' : 'Error'} before reading missing source or archive`, async () => {
      const fixture = await createAbortedNpmAdmissionFixture()
      let operationFailure
      const cleanupFailures = []
      try {
        fixture.assertUnchanged()
        const controller = new AbortController()
        controller.abort(reason)
        const [outcome] = await Promise.allSettled([installFromArchive({ ...fixture.options, signal: controller.signal })])
        assert.equal(outcome.status, 'rejected')
        assert.equal(outcome.reason, reason)
        fixture.assertUnchanged()
      } catch (error) {
        operationFailure = { error }
      } finally {
        try {
          await fixture.dispose()
        } catch (error) {
          cleanupFailures.push(error)
        }
      }
      if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Aborted tool archive fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
      if (operationFailure) throw operationFailure.error
    })
  }
}
