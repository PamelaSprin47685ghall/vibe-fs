import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { create } from 'tar'
import { createNpmInstallFixture } from './npm-install-fixture.mjs'
import { fixturePhase } from './fixture-phase.mjs'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
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

async function toolArchiveFixture({ observeVersions = false } = {}) {
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
    fs.rmSync(selected, { recursive: true })
    return {
      ...fixture,
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

async function captureToolArchive(t, options) {
  let fixture
  let failure
  try {
    await t.test('WHAT[verification-system-016] complete selected Node and npm archive capture preserves locked inputs before execution', async () => {
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
      } catch (error) {
        failure = { error }
        throw error
      }
    })
  } catch (error) {
    failure ??= { error }
  }
  if (failure) {
    try {
      await fixture?.dispose()
    } catch (cleanupError) {
      throw new AggregateError([failure.error, cleanupError], 'Tool archive capture verification and cleanup failed', { cause: failure.error })
    }
    throw failure.error
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

export function registerNpmToolArchiveTests() {
  test('WHAT[verification-system-016] selected npm tools replaced during the actual runtime probe cannot start the next version probe', async t => {
    let fixture
    let replacedRoot
    let parked
    let operationFailure
    const cleanupFailures = []
    try {
      fixture = await captureToolArchive(t, { observeVersions: true })
      let rejectionFailure
      await t.test('WHAT[verification-system-016] actual runtime probe refuses a complete copied tool owner before version or registry execution', async () => {
        try {
          const output = {
            write(chunk) {
              if (replacedRoot || !String(chunk).includes('"platform"')) return
              const names = fs.readdirSync(fixture.parentDirectory).filter(name => name.startsWith('verification-archive-'))
              assert.equal(names.length, 1)
              replacedRoot = path.join(fixture.parentDirectory, names[0])
              parked = path.join(fixture.root, 'parked-tools')
              fs.renameSync(replacedRoot, parked)
              fs.cpSync(parked, replacedRoot, { recursive: true, preserveTimestamps: true, verbatimSymlinks: true })
              fs.chmodSync(replacedRoot, fs.statSync(parked).mode & 0o777)
              for (const entry of fixture.capturedSelection.entries) {
                if (entry.type !== 'SymbolicLink') fs.chmodSync(path.join(replacedRoot, entry.path), entry.mode)
              }
              assert.deepEqual(selectedEntries(replacedRoot), fixture.capturedSelection.entries)
            },
          }
          const [outcome] = await Promise.allSettled([installFromArchive({ ...fixture.installOptions, output })])
          assert.ok(replacedRoot, 'the actual installation runtime probe was observed')
          assert.equal(outcome.status, 'rejected')
          assert.ok(outcome.reason instanceof AggregateError)
          assert.equal(fs.readFileSync(fixture.versionMarker, 'utf8'), 'version\n', 'only the initial tool preparation version probe ran')
          assert.equal(outcome.reason.cause.code, 'verification-tool-entry-invalid')
          assert.equal(fs.existsSync(path.join(replacedRoot, fixture.toolArchive.npmCliPath)), true)
          assert.equal(fs.existsSync(parked), true, 'production cleanup does not guess the parked original directory')
          assert.deepEqual(fixture.requests, [], 'no package download can start from a replaced tool owner')
        } catch (error) {
          rejectionFailure = { error }
          throw error
        }
      })
      if (rejectionFailure) throw rejectionFailure.error
    } catch (error) {
      operationFailure = { error }
    } finally {
      try {
        await fixture?.dispose()
      } catch (error) {
        cleanupFailures.push(error)
      }
    }
    if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Runtime replacement fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
    if (operationFailure) throw operationFailure.error
  })
  test('WHAT[verification-system-016] npm tool archive installation owns selected complete tools and locked dependencies through publication', async t => {
    await t.test('WHAT[verification-system-016] two actual locked registry packages preserve modes use the selected bundle layout and bind its complete identity after tools cleanup', async positive => {
      let fixture
      let candidate
      let packageBytes
      let lockBytes
      let operationFailure
      let settled
      let originalUmask
      const controller = new AbortController()
      const cleanupFailures = []
      try {
        fixture = await captureToolArchive(positive)
        packageBytes = fs.readFileSync(path.join(fixture.sourceRoot, 'package.json'))
        lockBytes = fs.readFileSync(path.join(fixture.sourceRoot, 'package-lock.json'))
        fixture.holdLeaf()
        originalUmask = process.umask(0o002)
        const installing = installFromArchive({ ...fixture.installOptions, signal: controller.signal })
        settled = Promise.allSettled([installing])
        let installationFailure
        await positive.test('WHAT[verification-system-016] actual selected complete tools consume the locked request before its held tarball is released', async () => {
          try {
            await waitForHeldInstall(fixture, installing)
          } catch (error) {
            installationFailure = { error }
            throw error
          }
        })
        if (installationFailure) throw installationFailure.error
        await positive.test('WHAT[verification-system-016] actual locked registry installation consumes the captured complete tool archive and reclaims selected tools', async () => {
          try {
            fixture.releaseLeaf()
            candidate = await installing
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
            installationFailure = { error }
            throw error
          }
        })
        if (installationFailure) throw installationFailure.error
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
          await fixture?.dispose()
        } catch (error) {
          cleanupFailures.push(error)
        }
      }
      if (cleanupFailures.length) throw new AggregateError(operationFailure ? [operationFailure.error, ...cleanupFailures] : cleanupFailures, 'Tool archive positive fixture cleanup failed', { cause: operationFailure ? operationFailure.error : cleanupFailures[0] })
      if (operationFailure) throw operationFailure.error
    })
    for (const mutation of ['non-CLI library bytes', 'new tool member']) {
      await t.test(`WHAT[verification-system-016] npm tool archive rejects ${mutation} changed during a real held install and reclaims unpublished dependencies`, async held => {
        const fixture = await captureToolArchive(held)
        const controller = new AbortController()
        let settled
        try {
          fixture.holdLeaf()
          const installing = installFromArchive({ ...fixture.installOptions, signal: controller.signal })
          settled = Promise.allSettled([installing])
          const toolRoot = await waitForHeldInstall(fixture, installing)
          if (mutation === 'non-CLI library bytes') fs.appendFileSync(path.join(toolRoot, 'toolchain/npm/lib/cli.js'), '\n// controlled library mutation\n')
          else fs.writeFileSync(path.join(toolRoot, 'toolchain/npm/bin/unselected-member.txt'), 'controlled extra member')
          fixture.releaseLeaf()
          await assert.rejects(installing, { code: 'verification-tool-entry-invalid' })
          await fixture.leafClosed
          assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
        } finally {
          controller.abort(new Error('tool mutation fixture cleanup'))
          await settled
          await fixture.dispose()
        }
      })
    }
    for (const reason of [new Error('controlled tool archive install cancellation'), null]) {
      await t.test(`WHAT[verification-system-016] npm tool archive held install cancellation preserves ${reason === null ? 'null' : 'Error'} and reclaims tools installation and dependencies`, async held => {
        const fixture = await captureToolArchive(held)
        const controller = new AbortController()
        let settled
        try {
          fixture.holdLeaf()
          const installing = installFromArchive({ ...fixture.installOptions, signal: controller.signal })
          settled = Promise.allSettled([installing])
          await waitForHeldInstall(fixture, installing)
          const execution = JSON.parse(fs.readFileSync(fixture.executionMarker, 'utf8'))
          assert.ok(Number.isInteger(execution.pid))
          controller.abort(reason)
          const [outcome] = await settled
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason, reason)
          await fixture.leafClosed
          assert.throws(() => process.kill(execution.pid, 0), error => error.code === 'ESRCH')
          assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
        } finally {
          controller.abort(reason)
          await settled
          await fixture.dispose()
        }
      })
    }
    for (const role of ['Node', 'npm']) {
      await t.test(`WHAT[verification-system-016] npm tool archive rejects a mismatched actual ${role} version before registry execution`, async mismatch => {
        const fixture = await captureToolArchive(mismatch)
        try {
          const options = { ...fixture.installOptions }
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
        } finally {
          await fixture.dispose()
        }
      })
    }
    for (const reason of [new Error('cancelled before tool archive installation'), null]) {
      await t.test(`WHAT[verification-system-016] npm tool archive already aborted preserves ${reason === null ? 'null' : 'Error'} before reading missing source or archive`, async () => {
        const fixture = await createNpmInstallFixture()
        try {
          const controller = new AbortController()
          controller.abort(reason)
          const [outcome] = await Promise.allSettled([installFromArchive({ sourceRoot: path.join(fixture.root, 'missing-source'), parentDirectory: fixture.parentDirectory, toolArchive: { archivePath: path.join(fixture.root, 'missing-tools.tar'), archiveSha256: '0'.repeat(64) }, expectedNpmVersion: fixture.options.expectedNpmVersion, signal: controller.signal })])
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason, reason)
          assert.deepEqual(fixture.requests, [])
          assert.deepEqual(fs.readdirSync(fixture.parentDirectory), [])
        } finally {
          await fixture.dispose()
        }
      })
    }
  })
}
