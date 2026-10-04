import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
const sha512 = bytes => createHash('sha512').update(bytes).digest('base64')
const prepareTools = async options => (await import('../../../../scripts/lib/verification-dotnet-tools.mjs')).prepareVerificationDotnetTools(options)
const manifest = version => JSON.stringify({ version: 1, isRoot: true, tools: { 'fixture.tool': { version, commands: ['fixture-tool'], rollForward: false } } })

async function withFixture(action, { toolManifest = manifest('1.0.0'), executableBody = 'process.stderr.write("controlled restore failure\\n"); process.exit(73)' } = {}) {
  const allocatedRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-dotnet-restore-'))
  const root = fs.realpathSync(allocatedRoot)
  const sourceRoot = path.join(root, 'source')
  const parentDirectory = path.join(root, 'candidates')
  const toolRoot = path.join(root, 'sdk')
  const globalBytes = Buffer.from('{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}\n')
  const manifestPath = path.join(sourceRoot, '.config/dotnet-tools.json')
  fs.mkdirSync(path.dirname(manifestPath), { recursive: true })
  fs.mkdirSync(parentDirectory)
  fs.mkdirSync(path.join(toolRoot, 'dotnet-sdk'), { recursive: true })
  fs.writeFileSync(path.join(sourceRoot, 'global.json'), globalBytes)
  if (toolManifest !== null) fs.writeFileSync(manifestPath, toolManifest)
  // This is an actual Node executor for failure and cancellation boundaries,
  // not a .NET tool restore or a successful SDK preparation fixture.
  const executable = path.join(toolRoot, 'dotnet-sdk/dotnet')
  const body = typeof executableBody === 'function' ? executableBody({ root, executable }) : executableBody
  fs.writeFileSync(executable, `#!${process.execPath}\n${body}\n`, { mode: 0o755 })
  const executableBytes = fs.readFileSync(executable)
  const entries = [
    { path: 'dotnet-sdk', type: 'Directory', mode: fs.statSync(path.join(toolRoot, 'dotnet-sdk')).mode & 0o777 },
    { path: 'dotnet-sdk/dotnet', type: 'File', mode: fs.statSync(executable).mode & 0o777, size: executableBytes.length, sha256: sha256(executableBytes) },
  ]
  const sdk = {
    toolRoot,
    identityScope: 'selected-dotnet-sdk-bundle',
    entries,
    entriesDigest: sha256(JSON.stringify(entries)),
    archiveSha256: sha256(executableBytes),
    dotnet: { path: 'dotnet-sdk/dotnet', sha256: sha256(executableBytes) },
    sdk: { version: '10.0.302', basePath: 'dotnet-sdk/sdk/10.0.302' },
    runtimes: [],
    globalJsonSha256: sha256(globalBytes),
    revalidate() {
      if (!fs.readFileSync(executable).equals(executableBytes)) {
        throw Object.assign(new Error('Selected executor fixture changed'), { code: 'verification-dotnet-sdk-entry-invalid' })
      }
    },
  }
  sdk.sdkDigest = sha256(JSON.stringify({ archiveSha256: sdk.archiveSha256, entriesDigest: sdk.entriesDigest, globalJsonSha256: sdk.globalJsonSha256, dotnet: sdk.dotnet, sdk: sdk.sdk, runtimes: sdk.runtimes, identityScope: sdk.identityScope }))
  const archivePath = path.join(root, 'fixture.tool.1.0.0.nupkg')
  const archiveBytes = Buffer.from('selected archive bytes for the failing executor fixture')
  fs.writeFileSync(archivePath, archiveBytes)
  const packageArchives = [{ id: 'fixture.tool', version: '1.0.0', archivePath, sha512: sha512(archiveBytes) }]
  let actionFailure
  try {
    return await action({ root, sourceRoot, parentDirectory, sdk, executable, manifestPath, archivePath, options: { sdk, sourceRoot, parentDirectory, packageArchives } })
  } catch (error) {
    actionFailure = { error }
    throw error
  } finally {
    const failures = []
    try {
      assert.deepEqual(fs.readdirSync(parentDirectory), [], 'Rejected tool preparation must reclaim every owned root')
      assert.deepEqual(fs.readFileSync(path.join(sourceRoot, 'global.json')), globalBytes)
      if (toolManifest === null) assert.equal(fs.existsSync(manifestPath), false)
      else assert.equal(fs.readFileSync(manifestPath, 'utf8'), toolManifest)
      assert.deepEqual(fs.readFileSync(archivePath), archiveBytes)
      assert.equal(fs.statSync(toolRoot).isDirectory(), true, 'Tool restore does not own or dispose its supplied SDK')
    } catch (error) {
      failures.push(error)
    }
    try {
      fs.rmSync(root, { recursive: true, force: true })
    } catch (error) {
      failures.push(error)
    }
    if (failures.length) {
      throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Tool restore fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
    }
  }
}

async function rejectsPreparation(options, predicate) {
  let published
  try {
    const [outcome] = await Promise.allSettled([prepareTools(options)])
    if (outcome.status === 'fulfilled') published = outcome.value
    assert.equal(outcome.status, 'rejected', 'Invalid or cancelled tool preparation cannot publish a candidate')
    assert.ok(predicate(outcome.reason), `Unexpected preparation failure: ${String(outcome.reason)}`)
  } finally {
    published?.dispose()
  }
}

// These cache files exercise the restore adapter's public receipt contract.
// The Node executor never claims to have restored or executed a .NET package.
function receiptExecutor(mutation = '') {
  return `
const fs = require('node:fs')
const path = require('node:path')
const root = process.cwd()
const expectedArgs = ['tool', 'restore', '--tool-manifest', path.join(root, '.config/dotnet-tools.json'), '--configfile', path.join(root, 'NuGet.Config'), '--disable-parallel', '--verbosity', 'minimal']
require('node:assert/strict').deepEqual(process.argv.slice(2), expectedArgs)
require('node:assert/strict').equal(process.env.NUGET_PACKAGES, path.join(root, 'packages'))
require('node:assert/strict').equal(process.env.DOTNET_CLI_HOME, path.join(root, 'cli'))
const packageRoot = path.join(root, 'packages', 'fixture.tool', '1.0.0')
fs.mkdirSync(path.join(packageRoot, 'tools', 'net10.0', 'any'), {recursive:true})
fs.copyFileSync(path.join(root, 'feed', 'fixture.tool.1.0.0.nupkg'), path.join(packageRoot, 'fixture.tool.1.0.0.nupkg'))
const dll = path.join(packageRoot, 'tools', 'net10.0', 'any', 'fixture.dll')
fs.writeFileSync(dll, 'ordinary receipt fixture bytes')
fs.writeFileSync(path.join(path.dirname(dll), 'dependency.dll'), 'ordinary runtime asset fixture bytes')
const deps = {runtimeTarget:{name:'.NETCoreApp,Version=v10.0'},targets:{'.NETCoreApp,Version=v10.0':{'fixture.tool/1.0.0':{runtime:{'fixture.dll':{},'dependency.dll':{}}}}}}
fs.writeFileSync(path.join(path.dirname(dll), 'fixture.deps.json'), JSON.stringify(deps))
const cache = path.join(root, 'cli', '.dotnet', 'toolResolverCache', '1')
fs.mkdirSync(cache, {recursive:true})
const row = {Version:'1.0.0',TargetFramework:'net10.0',RuntimeIdentifier:'any',Name:'fixture-tool',Runner:'dotnet',PathToExecutable:dll}
fs.writeFileSync(path.join(cache, 'fixture-tool'), JSON.stringify([row]))
${mutation}
`
}

function readInventory(root) {
  const entries = []
  function visit(relative) {
    const file = path.join(root, relative)
    const stat = fs.lstatSync(file)
    if (stat.isDirectory()) {
      entries.push({ path: relative, type: 'Directory', mode: stat.mode & 0o777 })
      for (const name of fs.readdirSync(file).sort()) visit(relative === '.' ? name : `${relative}/${name}`)
    } else {
      assert.equal(stat.isFile(), true)
      const bytes = fs.readFileSync(file)
      entries.push({ path: relative, type: 'File', mode: stat.mode & 0o777, size: bytes.length, sha256: sha256(bytes) })
    }
  }
  visit('.')
  return entries
}

async function withReceipt(options, action) {
  const candidate = await prepareTools(options)
  let actionFailure
  try {
    return await action(candidate)
  } catch (error) {
    actionFailure = { error }
    throw error
  } finally {
    try {
      candidate.dispose()
      assert.equal(fs.existsSync(candidate.toolRoot), false)
    } catch (error) {
      if (actionFailure) throw new AggregateError([actionFailure.error, error], 'Receipt action and disposal failed', { cause: actionFailure.error })
      throw error
    }
  }
}

export function registerDotnetToolRestoreTests() {
  const invalidManifests = [
    ['missing', null],
    ['malformed JSON', '{broken manifest}'],
    ['floating tool version', manifest('1.*')],
    ['tool roll-forward', JSON.stringify({ version: 1, isRoot: true, tools: { 'fixture.tool': { version: '1.0.0', commands: ['fixture-tool'], rollForward: true } } })],
    ['duplicate command', JSON.stringify({ version: 1, isRoot: true, tools: { 'fixture.tool': { version: '1.0.0', commands: ['fixture-tool', 'fixture-tool'] } } })],
  ]
  for (const [description, toolManifest] of invalidManifests) {
    test(`WHAT[verification-system-016] tool preparation rejects a ${description} manifest before admitting selected archives`, async () => {
      await withFixture(async ({ options }) => {
        await rejectsPreparation(options, error => error.code === 'verification-dotnet-tools-entry-invalid')
      }, { toolManifest })
    })
  }
  test('WHAT[verification-system-016] tool archive SHA-512 and selected SDK global identity must match before restore', async () => {
    await withFixture(async ({ options }) => {
      await rejectsPreparation({ ...options, packageArchives: options.packageArchives.map(archive => ({ ...archive, sha512: Buffer.alloc(64).toString('base64') })) }, error => error.code === 'verification-dotnet-tools-integrity-invalid')
      await rejectsPreparation({ ...options, sdk: { ...options.sdk, globalJsonSha256: '0'.repeat(64) } }, error => error.code === 'verification-dotnet-tools-entry-invalid')
    })
  })
  test('WHAT[verification-system-016] a direct tool missing from the selected archive set cannot borrow ambient packages', async () => {
    await withFixture(async ({ options }) => {
      await rejectsPreparation({ ...options, packageArchives: [] }, error => error.code === 'verification-dotnet-tools-entry-invalid')
    })
  })
  test('WHAT[verification-system-016] actual selected executor failure retains its cause and reclaims the private restore roots', async () => {
    await withFixture(async ({ options }) => {
      await rejectsPreparation(options, error => error.code === 'verification-tool-probe-failed' && error.exitCode === 73 && error.stderr.includes('controlled restore failure'))
    })
  })
  for (const reason of [new Error('tool restore cancelled before admission'), null]) {
    test(`WHAT[verification-system-016] early tool cancellation preserves ${reason === null ? 'null' : 'Error'} without reading missing source or archives`, async () => {
      await withFixture(async ({ root, options }) => {
        const controller = new AbortController()
        controller.abort(reason)
        await rejectsPreparation({ ...options, sourceRoot: path.join(root, 'missing-source'), packageArchives: options.packageArchives.map(archive => ({ ...archive, archivePath: path.join(root, 'missing.nupkg') })), signal: controller.signal }, error => error === reason)
      })
    })
  }
  const heldScenarios = [
    { reason: new Error('cancel held tool restore executor'), redirectParent: false },
    { reason: null, redirectParent: false },
    { reason: new Error('cancel held restore after parent alias replacement'), redirectParent: true },
  ]
  for (const { reason, redirectParent } of heldScenarios) {
    const title = redirectParent ? 'held tool restore reclaims its original canonical root after parent link replacement and preserves the foreign owner' : `held tool restore preserves ${reason === null ? 'null' : 'Error'} cancellation and drains its actual process group`
    test(`WHAT[verification-system-016] ${title}`, { skip: process.platform === 'win32' }, async t => {
      await withFixture(async ({ root, options }) => {
        const marker = path.join(root, 'started.json')
        const fallback = path.join(root, 'fallback-cleanup.txt')
        const controller = new AbortController()
        const started = Promise.withResolvers()
        let physical
        let settled
        let actionFailure
        let originalOwnedRoot
        let foreignMarker
        let selectedParent = options.parentDirectory
        if (redirectParent) {
          selectedParent = path.join(root, 'parent-alias')
          fs.symlinkSync(options.parentDirectory, selectedParent)
        }
        const watcher = fs.watch(root, (_event, filename) => {
          if (filename !== path.basename(marker) || !fs.existsSync(marker)) return
          try {
            started.resolve(JSON.parse(fs.readFileSync(marker, 'utf8')))
          } catch (error) {
            started.reject(error)
          }
        })
        watcher.on('error', error => started.reject(error))
        try {
          const preparing = prepareTools({ ...options, parentDirectory: selectedParent, signal: controller.signal })
          settled = Promise.allSettled([preparing])
          physical = await Promise.race([started.promise, preparing.then(() => { throw new Error('Held tool restore unexpectedly published') })])
          assert.ok(Number.isInteger(physical.pid) && Number.isInteger(physical.childPid))
          assert.equal(fs.readdirSync(options.parentDirectory).length, 1)
          if (redirectParent) {
            const allocatedName = fs.readdirSync(options.parentDirectory)[0]
            originalOwnedRoot = path.join(options.parentDirectory, allocatedName)
            const foreignParent = path.join(root, 'foreign-parent')
            const foreignRoot = path.join(foreignParent, allocatedName)
            fs.mkdirSync(foreignRoot, { recursive: true })
            foreignMarker = path.join(foreignRoot, 'foreign-owner.txt')
            fs.writeFileSync(foreignMarker, 'foreign bytes must survive')
            fs.unlinkSync(selectedParent)
            fs.symlinkSync(foreignParent, selectedParent)
          }
          controller.abort(reason)
          // EOF cancellation drains asynchronously; a second signal is only valid after settlement.
          const [outcome] = await settled
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason, reason)
          assert.throws(() => process.kill(physical.pid, 'SIGTERM'), error => error.code === 'ESRCH')
          assert.equal(fs.existsSync(fallback), false, 'The restore owner must drain the process group before returning')
          for (const pid of [physical.pid, physical.childPid]) {
            assert.throws(() => process.kill(pid, 0), error => error.code === 'ESRCH')
          }
          if (redirectParent) {
            const ownership = { ownedRootExists: fs.existsSync(originalOwnedRoot), foreignMarkerExists: fs.existsSync(foreignMarker) }
            t.diagnostic(JSON.stringify(ownership))
            assert.deepEqual(ownership, { ownedRootExists: false, foreignMarkerExists: true })
            assert.equal(fs.readFileSync(foreignMarker, 'utf8'), 'foreign bytes must survive')
          }
        } catch (error) {
          actionFailure = { error }
          throw error
        } finally {
          const failures = []
          watcher.close()
          controller.abort(reason)
          if (physical) {
            try {
              process.kill(-physical.pid, 'SIGKILL')
            } catch (error) {
              if (error.code !== 'ESRCH') failures.push(error)
            }
          }
          if (settled) {
            const [outcome] = await settled
            if (outcome.status === 'fulfilled') {
              try {
                outcome.value.dispose()
              } catch (error) {
                failures.push(error)
              }
            }
          }
          if (failures.length) throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Held restore fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
        }
      }, { executableBody: ({ root }) => {
        const marker = path.join(root, 'started.json')
        const fallback = path.join(root, 'fallback-cleanup.txt')
        const childProgram = `require('node:fs').watch(${JSON.stringify(root)}, () => {})\nprocess.stdout.write('ready\\n')`
        return `
const fs = require('node:fs')
const child = require('node:child_process').spawn(process.execPath, ['-e', ${JSON.stringify(childProgram)}], {stdio:['ignore','pipe','inherit']})
child.stdout.once('data', () => {
  fs.writeFileSync(${JSON.stringify(`${marker}.tmp`)}, JSON.stringify({pid:process.pid,childPid:child.pid}))
  fs.renameSync(${JSON.stringify(`${marker}.tmp`)}, ${JSON.stringify(marker)})
})
process.on('SIGTERM', () => {
  fs.writeFileSync(${JSON.stringify(fallback)}, 'caller fallback used')
  child.once('close', () => process.exit(89))
  child.kill('SIGKILL')
})
`
      } })
    })
  }
  const invalidReceipts = [
    ['malformed resolver JSON', "fs.writeFileSync(path.join(cache, 'fixture-tool'), '{broken')", 'entry-invalid'],
    ['wrong resolver version', "row.Version = '2.0.0'\nfs.writeFileSync(path.join(cache, 'fixture-tool'), JSON.stringify([row]))", 'entry-invalid'],
    ['foreign executable', "row.PathToExecutable = process.execPath\nfs.writeFileSync(path.join(cache, 'fixture-tool'), JSON.stringify([row]))", 'entry-invalid'],
    ['unexpected command', "fs.writeFileSync(path.join(cache, 'unselected-command'), JSON.stringify([row]))", 'entry-invalid'],
    ['unselected package', "fs.mkdirSync(path.join(root, 'packages', 'unselected', '1.0.0'), {recursive:true})", 'entry-invalid'],
    ['unselected feed member', "fs.writeFileSync(path.join(root, 'feed', 'unselected.9.9.9.nupkg'), 'unselected archive')", 'entry-invalid'],
    ['missing selected package', "fs.rmSync(packageRoot, {recursive:true})", 'entry-invalid'],
    ['changed package archive', "fs.appendFileSync(path.join(packageRoot, 'fixture.tool.1.0.0.nupkg'), 'changed')", 'integrity-invalid'],
    ['missing runtime DLL', "fs.unlinkSync(path.join(path.dirname(dll), 'dependency.dll'))", 'entry-invalid'],
    ['malformed runtime deps JSON', "fs.writeFileSync(path.join(path.dirname(dll), 'fixture.deps.json'), '{broken')", 'entry-invalid'],
    ['escaping runtime asset', "deps.targets[deps.runtimeTarget.name]['fixture.tool/1.0.0'].runtime = {'../outside.dll':{}}\nfs.writeFileSync(path.join(path.dirname(dll), 'fixture.deps.json'), JSON.stringify(deps))", 'entry-invalid'],
    ['changed selected SDK during execution', "fs.appendFileSync(process.argv[1], '\\n// changed during restore\\n')", 'sdk-entry-invalid'],
  ]
  for (const [description, mutation, failure] of invalidReceipts) {
    test(`WHAT[verification-system-016] tool receipt rejects ${description} and reclaims all owned restore output`, async () => {
      await withFixture(async ({ options }) => {
        const code = failure === 'sdk-entry-invalid' ? 'verification-dotnet-sdk-entry-invalid' : `verification-dotnet-tools-${failure}`
        await rejectsPreparation(options, error => error.code === code)
      }, { executableBody: receiptExecutor(mutation) })
    })
  }
  test('WHAT[verification-system-016] mutated selected SDK entry metadata cannot execute a foreign launcher under the original SDK digest', async () => {
    await withFixture(async ({ root, options }) => {
      const marker = path.join(root, 'foreign-launcher-executed')
      const foreign = path.join(root, 'foreign-dotnet')
      const program = `require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'unselected executor ran')\n${receiptExecutor()}`
      fs.writeFileSync(foreign, `#!${process.execPath}\n${program}\n`, { mode: 0o755 })
      const changedSdk = { ...options.sdk, dotnet: { ...options.sdk.dotnet, path: '../foreign-dotnet' } }
      const [outcome] = await Promise.allSettled([prepareTools({ ...options, sdk: changedSdk })])
      try {
        assert.deepEqual({ status: outcome.status, foreignExecuted: fs.existsSync(marker) }, { status: 'rejected', foreignExecuted: false }, 'Invalid selected SDK metadata must be rejected before executing the foreign launcher')
        assert.equal(outcome.reason.code, 'verification-dotnet-tools-entry-invalid')
        assert.equal(fs.statSync(foreign).isFile(), true, 'The foreign launcher is not owned by restore cleanup')
      } finally {
        if (outcome.status === 'fulfilled') outcome.value.dispose()
      }
    })
  })
  for (const field of ['toolRoot', 'executable']) {
    test(`WHAT[verification-system-016] tool receipt revalidation rejects a changed public ${field} and disposal preserves the foreign owner`, async () => {
      await withFixture(async ({ root, options }) => {
        const foreignRoot = path.join(root, 'foreign-tools')
        const foreignFile = path.join(foreignRoot, 'foreign.dll')
        fs.mkdirSync(foreignRoot)
        fs.writeFileSync(foreignFile, 'foreign caller-owned bytes')
        let ownedRoot
        await withReceipt(options, async candidate => {
          ownedRoot = candidate.toolRoot
          const selectedExecutable = candidate.tools[0].executable
          candidate.revalidate()
          try {
            if (field === 'toolRoot') candidate.toolRoot = foreignRoot
            else candidate.tools[0].executable = path.relative(ownedRoot, foreignFile)
            assert.throws(() => candidate.revalidate(), error => error.code === 'verification-dotnet-tools-entry-invalid', 'Changed public selection metadata cannot reuse the original inventory identity')
          } finally {
            candidate.toolRoot = ownedRoot
            candidate.tools[0].executable = selectedExecutable
          }
          candidate.revalidate()
          assert.equal(fs.readFileSync(foreignFile, 'utf8'), 'foreign caller-owned bytes')
          assert.equal(fs.existsSync(ownedRoot), true)
          try {
            if (field === 'toolRoot') candidate.toolRoot = foreignRoot
            else candidate.tools[0].executable = path.relative(ownedRoot, foreignFile)
            candidate.dispose()
            assert.equal(fs.existsSync(ownedRoot), false)
            assert.equal(fs.readFileSync(foreignFile, 'utf8'), 'foreign caller-owned bytes')
          } finally {
            candidate.toolRoot = ownedRoot
            candidate.tools[0].executable = selectedExecutable
          }
        })
        assert.equal(fs.existsSync(ownedRoot), false, 'Disposal reclaims the originally allocated tool root')
        assert.equal(fs.readFileSync(foreignFile, 'utf8'), 'foreign caller-owned bytes')
      }, { executableBody: receiptExecutor() })
    })
  }
  test('WHAT[verification-system-016] selected tool receipt binds the complete ordinary output inventory and rejects byte, mode, added and deleted members', async t => {
    await withFixture(async ({ options }) => {
      await withReceipt(options, async candidate => {
        assert.equal(candidate.identityScope, 'selected-dotnet-tool-restore')
        assert.equal(candidate.sdkDigest, options.sdk.sdkDigest)
        assert.equal(candidate.globalJsonSha256, options.sdk.globalJsonSha256)
        assert.equal(candidate.toolManifestSha256, sha256(manifest('1.0.0')))
        assert.deepEqual(candidate.packages, options.packageArchives.map(({ id, version, sha512 }) => ({ id, version, sha512 })))
        assert.deepEqual(candidate.tools, [{ id: 'fixture.tool', version: '1.0.0', command: 'fixture-tool', executable: 'packages/fixture.tool/1.0.0/tools/net10.0/any/fixture.dll' }])
        assert.deepEqual(candidate.entries, readInventory(candidate.toolRoot))
        assert.equal(candidate.entriesDigest, sha256(JSON.stringify(candidate.entries)))
        assert.equal(candidate.toolsDigest, sha256(JSON.stringify({ sdkDigest: candidate.sdkDigest, globalJsonSha256: candidate.globalJsonSha256, toolManifestSha256: candidate.toolManifestSha256, packages: candidate.packages, entriesDigest: candidate.entriesDigest, identityScope: candidate.identityScope })))
        candidate.revalidate()
        const member = path.join(candidate.toolRoot, candidate.tools[0].executable)
        const bytes = fs.readFileSync(member)
        const mode = fs.statSync(member).mode & 0o777
        const added = path.join(candidate.toolRoot, 'unselected-output')
        const changes = [
          ['byte mutation', () => fs.writeFileSync(member, Buffer.alloc(bytes.length, 0)), () => fs.writeFileSync(member, bytes)],
          ['mode mutation', () => fs.chmodSync(member, mode ^ 0o100), () => fs.chmodSync(member, mode)],
          ['added member', () => fs.writeFileSync(added, 'new bytes'), () => fs.unlinkSync(added)],
          ['deleted member', () => fs.unlinkSync(member), () => fs.writeFileSync(member, bytes, { mode })],
        ]
        for (const [description, change, restore] of changes) {
          await t.test(`WHAT[verification-system-016] captured tool output rejects ${description}`, () => {
            change()
            try {
              assert.throws(() => candidate.revalidate(), error => error.code === 'verification-dotnet-tools-entry-invalid')
            } finally {
              restore()
            }
            candidate.revalidate()
          })
        }
      })
    }, { executableBody: receiptExecutor() })
  })
}
