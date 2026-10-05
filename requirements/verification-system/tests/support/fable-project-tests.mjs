import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { prepareGitSourceCandidate } from '../../../../scripts/lib/verification-source-candidate.mjs'

const compileProject = async options => (await import('../../../../scripts/lib/verification-fable-project.mjs')).compileVerificationFableProject(options)
const hash = bytes => createHash('sha256').update(bytes).digest('hex')
const projectPath = 'src/Fixture.fsproj'
const entryInvalid = error => error?.code === 'verification-fable-project-entry-invalid'

function inventory(root) {
  const entries = []
  function visit(relative) {
    const file = path.join(root, relative)
    const stat = fs.lstatSync(file)
    if (stat.isDirectory()) {
      entries.push({ path: relative, type: 'Directory', mode: stat.mode & 0o777 })
      for (const name of fs.readdirSync(file).sort()) visit(relative === '.' ? name : `${relative}/${name}`)
    } else {
      assert.equal(stat.isFile(), true, 'Protocol fixture inventories contain only ordinary members')
      const bytes = fs.readFileSync(file)
      entries.push({ path: relative, type: 'File', mode: stat.mode & 0o777, size: bytes.length, sha256: hash(bytes) })
    }
  }
  visit('.')
  return entries
}

// These three ports declare captured protocol inputs, not actual SDK, NuGet or
// Fable preparation. The repository integration uses all four real owners.
function capturedPort(root, selection, code) {
  const expected = JSON.stringify(selection)
  const expectedEntries = JSON.stringify(inventory(root))
  const port = {
    ...selection,
    revalidate() {
      const current = Object.fromEntries(Object.keys(selection).map(key => [key, port[key]]))
      if (JSON.stringify(current) !== expected || JSON.stringify(inventory(root)) !== expectedEntries) throw Object.assign(new Error('Captured protocol input changed'), { code })
    },
  }
  return port
}

function executor({ root, sourceRoot, mutation = '', mutatedInput, empty = false, linked = false, failure = false, held = false }) {
  const inputFiles = {
    source: path.join(sourceRoot, '.git/HEAD'),
    sdk: path.join(root, 'sdk/dotnet-sdk/dotnet'),
    tools: path.join(root, 'tools/packages/fable/5.13.0/tools/net10.0/any/fable.dll'),
    project: path.join(root, 'project/packages/fixture/1.0.0/runtime.dll'),
  }
  return `
const fs = require('node:fs')
const path = require('node:path')
const args = process.argv.slice(2)
const outDir = args[args.indexOf('--outDir') + 1]
const compileRoot = path.dirname(outDir)
fs.writeFileSync(${JSON.stringify(path.join(root, 'entered.json'))}, JSON.stringify({args,env:process.env,cwd:process.cwd(),compileRoot}))
${held ? `
const {spawn} = require('node:child_process')
const child = spawn(process.execPath, ['-e', ${JSON.stringify(`require('node:fs').watch(${JSON.stringify(root)}, () => {})\nprocess.stdout.write('ready\\n')`)}], {stdio:['ignore','pipe','inherit']})
child.stdout.once('data', () => {
  fs.writeFileSync(${JSON.stringify(path.join(root, 'started.tmp'))}, JSON.stringify({parent:process.pid,child:child.pid,compileRoot}))
  fs.renameSync(${JSON.stringify(path.join(root, 'started.tmp'))}, ${JSON.stringify(path.join(root, 'started.json'))})
})
process.on('SIGTERM', () => {})
` : `
${failure ? "process.stderr.write('controlled Fable process failure\\n')\nprocess.exit(73)" : ''}
fs.mkdirSync(outDir, {recursive:true})
${empty ? '' : linked ? `fs.symlinkSync(${JSON.stringify(path.join(sourceRoot, 'src/Fixture.fs'))}, path.join(outDir, 'Fixture.js'))` : "fs.writeFileSync(path.join(outDir, 'Fixture.js'), 'export const value = 42\\n')"}
${mutation}
${mutatedInput ? `fs.appendFileSync(${JSON.stringify(inputFiles[mutatedInput])}, ' consumer changed captured input')` : ''}
`}
`
}

async function withCompileFixture(action, behavior = {}) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'verification-fable-project-test-')))
  const repositoryRoot = path.join(root, 'repository')
  const parentDirectory = path.join(root, 'compiles')
  let source
  let snapshots
  let actionFailure
  try {
    fs.mkdirSync(path.join(repositoryRoot, 'src'), { recursive: true })
    fs.mkdirSync(path.join(repositoryRoot, '.config'))
    fs.mkdirSync(parentDirectory)
    fs.mkdirSync(path.join(root, 'sources'))
    const globalBytes = Buffer.from('{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}\n')
    const manifestBytes = Buffer.from('{"version":1,"isRoot":true,"tools":{"fable":{"version":"5.13.0","commands":["fable"]}}}\n')
    fs.writeFileSync(path.join(repositoryRoot, 'global.json'), globalBytes)
    fs.writeFileSync(path.join(repositoryRoot, '.config/dotnet-tools.json'), manifestBytes)
    fs.writeFileSync(path.join(repositoryRoot, projectPath), '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><Compile Include="Fixture.fs"/></ItemGroup></Project>\n')
    fs.writeFileSync(path.join(repositoryRoot, 'src/Fixture.fs'), 'module Fixture\nlet value = 42\n')
    execFileSync('git', ['init', '--quiet', '--object-format=sha1', '--template=', repositoryRoot])
    execFileSync('git', ['-C', repositoryRoot, 'add', '.'])
    const treeId = execFileSync('git', ['-C', repositoryRoot, 'write-tree'], { encoding: 'utf8' }).trim()
    source = prepareGitSourceCandidate({ repositoryRoot, treeId, parentDirectory: path.join(root, 'sources') })
    const sdkRoot = path.join(root, 'sdk')
    fs.mkdirSync(path.join(sdkRoot, 'dotnet-sdk'), { recursive: true })
    const executable = path.join(sdkRoot, 'dotnet-sdk/dotnet')
    fs.writeFileSync(executable, `#!${process.execPath}\n${executor({ root, sourceRoot: source.sourceRoot, ...behavior })}`, { mode: 0o755 })
    const sdkEntries = inventory(sdkRoot)
    const sdkSelection = {
      toolRoot: sdkRoot, entries: sdkEntries, entriesDigest: hash(JSON.stringify(sdkEntries)),
      archiveSha256: hash(fs.readFileSync(executable)), globalJsonSha256: hash(globalBytes),
      dotnet: { path: 'dotnet-sdk/dotnet', sha256: hash(fs.readFileSync(executable)) },
      sdk: { version: '10.0.302', basePath: 'dotnet-sdk/sdk/10.0.302' }, runtimes: [], identityScope: 'selected-dotnet-sdk-bundle',
    }
    sdkSelection.sdkDigest = hash(JSON.stringify({ archiveSha256: sdkSelection.archiveSha256, entriesDigest: sdkSelection.entriesDigest, globalJsonSha256: sdkSelection.globalJsonSha256, dotnet: sdkSelection.dotnet, sdk: sdkSelection.sdk, runtimes: sdkSelection.runtimes, identityScope: sdkSelection.identityScope }))
    const sdk = capturedPort(sdkRoot, sdkSelection, 'verification-dotnet-sdk-integrity-invalid')
    const toolsRoot = path.join(root, 'tools')
    const toolPath = 'packages/fable/5.13.0/tools/net10.0/any/fable.dll'
    fs.mkdirSync(path.dirname(path.join(toolsRoot, toolPath)), { recursive: true })
    fs.writeFileSync(path.join(toolsRoot, toolPath), 'ordinary selected DLL protocol bytes')
    const toolsEntries = inventory(toolsRoot)
    const toolsSelection = {
      toolRoot: toolsRoot, sdkDigest: sdk.sdkDigest, globalJsonSha256: hash(globalBytes), toolManifestSha256: hash(manifestBytes),
      packages: [{ id: 'fable', version: '5.13.0', sha512: Buffer.alloc(64, 1).toString('base64') }],
      tools: [{ id: 'fable', version: '5.13.0', command: 'fable', executable: toolPath }],
      entries: toolsEntries, entriesDigest: hash(JSON.stringify(toolsEntries)), identityScope: 'selected-dotnet-tool-restore',
    }
    toolsSelection.toolsDigest = hash(JSON.stringify({ sdkDigest: toolsSelection.sdkDigest, globalJsonSha256: toolsSelection.globalJsonSha256, toolManifestSha256: toolsSelection.toolManifestSha256, packages: toolsSelection.packages, entriesDigest: toolsSelection.entriesDigest, identityScope: toolsSelection.identityScope }))
    const tools = capturedPort(toolsRoot, toolsSelection, 'verification-dotnet-tools-entry-invalid')
    const projectRoot = path.join(root, 'project')
    const assetsPath = 'artifacts/obj/Fixture/project.assets.json'
    fs.mkdirSync(path.dirname(path.join(projectRoot, assetsPath)), { recursive: true })
    fs.mkdirSync(path.join(projectRoot, 'artifacts/.hidden/nested'), { recursive: true, mode: 0o700 })
    fs.writeFileSync(path.join(projectRoot, assetsPath), '{"version":3,"targets":{"net10.0":{}}}\n')
    fs.writeFileSync(path.join(projectRoot, 'artifacts/.hidden/nested/seed'), 'captured hidden seed bytes', { mode: 0o640 })
    fs.mkdirSync(path.join(projectRoot, 'packages/fixture/1.0.0'), { recursive: true })
    fs.writeFileSync(path.join(projectRoot, 'packages/fixture/1.0.0/runtime.dll'), 'captured project package runtime bytes')
    fs.writeFileSync(path.join(projectRoot, 'artifacts/packages.lock.json'), '{"version":1,"dependencies":{"net10.0":{}}}\n')
    const graph = { targetFramework: 'net10.0', targets: { 'net10.0': {} }, libraries: {}, dependencies: { 'net10.0': {} } }
    const projectEntries = inventory(projectRoot)
    const projectSelection = {
      projectRoot, projectPath, targetFramework: 'net10.0', assetsPath, lockPath: 'artifacts/packages.lock.json',
      sourceDigest: source.sourceDigest, treeId: source.treeId, sdkDigest: sdk.sdkDigest,
      packages: [], graph, graphDigest: hash(JSON.stringify(graph)), entries: projectEntries,
      entriesDigest: hash(JSON.stringify(projectEntries)), identityScope: 'selected-nuget-project-restore',
    }
    projectSelection.projectDigest = hash(JSON.stringify({ sourceDigest: projectSelection.sourceDigest, treeId: projectSelection.treeId, sdkDigest: projectSelection.sdkDigest, projectPath, packages: projectSelection.packages, graphDigest: projectSelection.graphDigest, entriesDigest: projectSelection.entriesDigest, identityScope: projectSelection.identityScope }))
    const project = capturedPort(projectRoot, projectSelection, 'verification-nuget-project-entry-invalid')
    snapshots = [source.sourceRoot, sdkRoot, toolsRoot, projectRoot].map(ownerRoot => ({ root: ownerRoot, entries: inventory(ownerRoot) }))
    const parentAlias = path.join(root, 'compile-parent-alias')
    if (behavior.parentAlias) fs.symlinkSync(parentDirectory, parentAlias)
    await action({ root, source, sdk, tools, project, parentDirectory, parentAlias, options: { source, sdk, tools, project, parentDirectory: behavior.parentAlias ? parentAlias : parentDirectory } })
  } catch (error) {
    actionFailure = { error }
    throw error
  } finally {
    const failures = []
    try {
      assert.deepEqual(fs.readdirSync(parentDirectory), [], 'Compilation reclaims its own output root')
      for (const snapshot of snapshots ?? []) assert.deepEqual(inventory(snapshot.root), snapshot.entries, 'Compilation preserves every caller-owned input byte, member and mode')
    } catch (error) {
      failures.push(error)
    }
    try {
      source?.dispose()
    } catch (error) {
      failures.push(error)
    }
    try {
      fs.rmSync(root, { recursive: true, force: true })
    } catch (error) {
      failures.push(error)
    }
    if (failures.length) throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Compile fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
  }
}

async function rejectsCompile(options, predicate) {
  const [outcome] = await Promise.allSettled([compileProject(options)])
  try {
    assert.equal(outcome.status, 'rejected', 'Invalid compilation cannot publish a receipt')
    assert.ok(predicate(outcome.reason), `Unexpected compile failure: ${String(outcome.reason)}`)
  } finally {
    if (outcome.status === 'fulfilled') outcome.value.dispose()
  }
}

async function withCompiledFixture(action, behavior) {
  await withCompileFixture(async fixture => {
    const candidate = await compileProject(fixture.options)
    const originalRoot = candidate.compileRoot
    let actionFailure
    try {
      await action({ ...fixture, candidate, originalRoot })
    } catch (error) {
      actionFailure = { error }
      throw error
    } finally {
      try {
        candidate.dispose()
        assert.equal(fs.existsSync(originalRoot), false)
      } catch (error) {
        if (actionFailure) throw new AggregateError([actionFailure.error, error], 'Compiled output action and disposal failed', { cause: actionFailure.error })
        throw error
      }
    }
  }, behavior)
}

export function registerFableProjectTests() {
  test('WHAT[verification-system-016] malformed Fable admission preserves the public rejected Promise contract', async () => {
    const { compileVerificationFableProject } = await import('../../../../scripts/lib/verification-fable-project.mjs')
    for (const options of [undefined, null]) {
      let pending
      assert.doesNotThrow(() => { pending = compileVerificationFableProject(options) })
      assert.ok(pending instanceof Promise)
      await assert.rejects(pending, TypeError)
    }
  })
  test('WHAT[verification-system-016] Fable compilation requires four prepared input owners before publishing an output', async () => {
    await assert.rejects(compileProject({}), error => error.code === 'verification-fable-project-entry-invalid')
  })
  const mismatches = [
    ['source tree', 'project', { treeId: '0'.repeat(40) }],
    ['source digest', 'project', { sourceDigest: '0'.repeat(64) }],
    ['project SDK', 'project', { sdkDigest: '0'.repeat(64) }],
    ['tools SDK', 'tools', { sdkDigest: '0'.repeat(64) }],
    ['original global', 'tools', { globalJsonSha256: '0'.repeat(64) }],
    ['original tool manifest', 'tools', { toolManifestSha256: '0'.repeat(64) }],
  ]
  for (const [description, owner, replacement] of mismatches) {
    test(`WHAT[verification-system-016] Fable compilation rejects a mismatched ${description} before executing`, async () => {
      await withCompileFixture(async ({ root, options }) => {
        await rejectsCompile({ ...options, [owner]: { ...options[owner], ...replacement } }, entryInvalid)
        assert.equal(fs.existsSync(path.join(root, 'entered.json')), false)
      })
    })
  }
  for (const role of ['missing', 'duplicate', 'foreign']) {
    test(`WHAT[verification-system-016] Fable compilation rejects a ${role} selected Fable role before executing`, async () => {
      await withCompileFixture(async ({ root, options, tools }) => {
        const selected = tools.tools[0]
        const roles = role === 'missing' ? [] : role === 'duplicate' ? [selected, selected] : [{ ...selected, executable: '/foreign/fable.dll' }]
        await rejectsCompile({ ...options, tools: { ...tools, tools: roles } }, entryInvalid)
        assert.equal(fs.existsSync(path.join(root, 'entered.json')), false)
      })
    })
  }
  test('WHAT[verification-system-016] selected Fable compilation preserves caller inputs and binds the seeded private output inventory', async () => {
    await withCompiledFixture(async ({ root, source, sdk, tools, project, candidate, originalRoot }) => {
      const observed = JSON.parse(fs.readFileSync(path.join(root, 'entered.json'), 'utf8'))
      assert.deepEqual(observed.args, [path.join(tools.toolRoot, tools.tools[0].executable), path.join(source.sourceRoot, projectPath), '--noRestore', '--noCache', '--noGitignore', '--outDir', path.join(originalRoot, 'js')])
      assert.equal(observed.cwd, source.sourceRoot)
      assert.equal(observed.env.ArtifactsDir, path.join(originalRoot, 'artifacts') + path.sep)
      assert.equal(observed.env.NUGET_PACKAGES, path.join(project.projectRoot, 'packages'))
      assert.equal(observed.env.DOTNET_ROOT, path.join(sdk.toolRoot, 'dotnet-sdk'))
      assert.equal(observed.env.DOTNET_HOST_PATH, path.join(sdk.toolRoot, sdk.dotnet.path))
      assert.equal(observed.env.HOME, path.join(originalRoot, 'home'))
      assert.equal(observed.env.TMPDIR, path.join(originalRoot, 'tmp'))
      assert.equal(observed.env.NUGET_HTTP_CACHE_PATH, path.join(originalRoot, 'cache'))
      assert.deepEqual(inventory(path.join(originalRoot, 'artifacts')), inventory(path.join(project.projectRoot, 'artifacts')))
      fs.appendFileSync(path.join(originalRoot, 'artifacts/.hidden/nested/seed'), ' private compile changes')
      assert.equal(fs.readFileSync(path.join(project.projectRoot, 'artifacts/.hidden/nested/seed'), 'utf8'), 'captured hidden seed bytes')
      assert.throws(() => candidate.revalidate(), entryInvalid)
      fs.writeFileSync(path.join(originalRoot, 'artifacts/.hidden/nested/seed'), 'captured hidden seed bytes')
      assert.equal(candidate.identityScope, 'selected-fable-project-compile')
      assert.equal(candidate.projectPath, projectPath)
      assert.equal(candidate.outputPath, 'js')
      assert.equal(candidate.treeId, source.treeId)
      assert.equal(candidate.sourceDigest, source.sourceDigest)
      assert.equal(candidate.sdkDigest, sdk.sdkDigest)
      assert.equal(candidate.toolsDigest, tools.toolsDigest)
      assert.equal(candidate.projectDigest, project.projectDigest)
      assert.deepEqual(candidate.fable, tools.tools[0])
      assert.deepEqual(candidate.entries, inventory(originalRoot))
      assert.equal(candidate.entriesDigest, hash(JSON.stringify(candidate.entries)))
      assert.equal(candidate.compileDigest, hash(JSON.stringify({ sourceDigest: candidate.sourceDigest, treeId: candidate.treeId, sdkDigest: candidate.sdkDigest, toolsDigest: candidate.toolsDigest, projectDigest: candidate.projectDigest, projectPath: candidate.projectPath, fable: candidate.fable, entriesDigest: candidate.entriesDigest, identityScope: candidate.identityScope })))
      assert.equal(fs.readFileSync(path.join(originalRoot, 'js/Fixture.js'), 'utf8'), 'export const value = 42\n')
      candidate.revalidate()
    })
  })
  for (const output of ['empty', 'linked']) {
    test(`WHAT[verification-system-016] Fable compilation rejects ${output} JavaScript output without publication`, async () => {
      await withCompileFixture(async ({ options }) => rejectsCompile(options, entryInvalid), { [output]: true })
    })
  }
  test('WHAT[verification-system-016] Fable process failure retains its physical cause and reclaims only compile output', async () => {
    await withCompileFixture(async ({ options }) => {
      await rejectsCompile(options, error => error.code === 'verification-tool-probe-failed' && error.exitCode === 73 && error.stderr.includes('controlled Fable process failure'))
    }, { failure: true })
  })
  for (const reason of [new Error('compile cancelled before admission'), null]) {
    test(`WHAT[verification-system-016] early Fable cancellation preserves ${reason === null ? 'null' : 'Error'} without reading absent owners`, async () => {
      const controller = new AbortController()
      controller.abort(reason)
      await rejectsCompile({ signal: controller.signal }, error => error === reason)
    })
  }
  const changedOwners = [
    ['source', '.git/HEAD', 'source-candidate-entry-invalid'],
    ['sdk', 'dotnet-sdk/dotnet', 'verification-dotnet-sdk-integrity-invalid'],
    ['tools', 'packages/fable/5.13.0/tools/net10.0/any/fable.dll', 'verification-dotnet-tools-entry-invalid'],
    ['project', 'packages/fixture/1.0.0/runtime.dll', 'verification-nuget-project-entry-invalid'],
  ]
  for (const [owner, relative, code] of changedOwners) {
    test(`WHAT[verification-system-016] compilation rejects ${owner} input changed by the actual selected consumer without publication`, async () => {
      await withCompileFixture(async fixture => {
        const input = fixture[owner]
        const file = path.join(owner === 'source' ? input.sourceRoot : owner === 'project' ? input.projectRoot : input.toolRoot, relative)
        const bytes = fs.readFileSync(file)
        try {
          await rejectsCompile(fixture.options, error => error.code === code)
          assert.equal(fs.existsSync(path.join(fixture.root, 'entered.json')), true)
        } finally {
          fs.writeFileSync(file, bytes)
        }
        input.revalidate()
      }, { mutatedInput: owner })
    })
  }
  for (const { reason, replaceParent } of [
    { reason: new Error('cancel actual held Fable process'), replaceParent: false },
    { reason: null, replaceParent: false },
    { reason: new Error('cancel after actual compilation parent replacement'), replaceParent: true },
    { reason: null, replaceParent: true },
  ]) {
    const title = replaceParent ? `Fable namespace cancellation preserves ${reason === null ? 'null' : 'Error'} and reports refused foreign cleanup` : `held Fable compilation preserves ${reason === null ? 'null' : 'Error'} cancellation and drains the actual process group`
    test(`WHAT[verification-system-016] ${title}`, { skip: process.platform === 'win32' }, async () => {
      await withCompileFixture(async ({ root, options, parentDirectory }) => {
        const marker = path.join(root, 'started.json')
        const parked = path.join(root, 'parked-held-compile-parent')
        const foreignParent = path.join(root, 'foreign-held-compile-parent')
        const controller = new AbortController()
        const started = Promise.withResolvers()
        let physical
        let settled
        let actionFailure
        let namespaceChanged = false
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
          const compiling = compileProject({ ...options, signal: controller.signal })
          settled = Promise.allSettled([compiling])
          physical = await Promise.race([started.promise, compiling.then(() => { throw new Error('Held Fable process unexpectedly published') })])
          assert.ok(Number.isInteger(physical.parent) && Number.isInteger(physical.child))
          assert.equal(fs.readdirSync(options.parentDirectory).length, 1)
          let foreignRoot
          if (replaceParent) {
            foreignRoot = path.join(foreignParent, path.basename(physical.compileRoot))
            fs.mkdirSync(foreignRoot, { recursive: true })
            fs.writeFileSync(path.join(foreignRoot, 'caller-owned'), 'foreign held bytes must survive')
            fs.renameSync(parentDirectory, parked)
            fs.symlinkSync(foreignParent, parentDirectory)
            namespaceChanged = true
          }
          controller.abort(reason)
          const [outcome] = await settled
          assert.equal(outcome.status, 'rejected')
          if (replaceParent) {
            assert.ok(outcome.reason instanceof AggregateError)
            assert.equal(outcome.reason.cause, reason)
            assert.equal(outcome.reason.errors[0], reason)
            assert.equal(outcome.reason.errors.length, 2)
            assert.ok(entryInvalid(outcome.reason.errors[1]))
            assert.match(outcome.reason.errors[1].message, /cannot verify owned identity/i)
            assert.equal(fs.readFileSync(path.join(foreignRoot, 'caller-owned'), 'utf8'), 'foreign held bytes must survive')
            assert.equal(fs.existsSync(path.join(parked, path.basename(physical.compileRoot))), true, 'Unknown namespace leaves the original output intact rather than guessing its location')
          } else {
            assert.equal(outcome.reason, reason)
            assert.equal(fs.existsSync(physical.compileRoot), false)
          }
          for (const pid of [physical.parent, physical.child]) assert.throws(() => process.kill(pid, 0), error => error.code === 'ESRCH')
        } catch (error) {
          actionFailure = { error }
          throw error
        } finally {
          const failures = []
          watcher.close()
          controller.abort(reason)
          if (physical) {
            try {
              process.kill(-physical.parent, 'SIGKILL')
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
          if (namespaceChanged) {
            try {
              fs.unlinkSync(parentDirectory)
              fs.renameSync(parked, parentDirectory)
              fs.rmSync(physical.compileRoot, { recursive: true })
              assert.deepEqual(fs.readdirSync(parentDirectory), [], 'Fixture reclaims the original output only after restoring its owned namespace')
            } catch (error) {
              failures.push(error)
            }
          }
          if (failures.length) throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Held Fable fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
        }
      }, { held: true })
    })
  }
  for (const field of ['compileRoot', 'compileDigest']) {
    test(`WHAT[verification-system-016] changed public ${field} cannot borrow the captured compile identity or redirect disposal`, async () => {
      await withCompiledFixture(async ({ root, candidate, originalRoot, parentDirectory, parentAlias }) => {
        const foreignParent = path.join(root, 'foreign-compile-parent')
        const foreignRoot = path.join(foreignParent, path.basename(originalRoot))
        const marker = path.join(foreignRoot, 'caller-owned')
        fs.mkdirSync(foreignRoot, { recursive: true })
        fs.writeFileSync(marker, 'foreign bytes must survive')
        if (field === 'compileRoot') {
          assert.equal(path.dirname(originalRoot), parentDirectory)
          fs.unlinkSync(parentAlias)
          fs.symlinkSync(foreignParent, parentAlias)
          candidate.revalidate()
        }
        candidate[field] = field === 'compileRoot' ? foreignRoot : '0'.repeat(64)
        assert.throws(() => candidate.revalidate(), entryInvalid)
        candidate.dispose()
        assert.equal(fs.existsSync(originalRoot), false)
        assert.equal(fs.readFileSync(marker, 'utf8'), 'foreign bytes must survive')
      }, { parentAlias: field === 'compileRoot' })
    })
  }
  for (const replacement of ['canonical parent', 'ordinary root']) {
    test(`WHAT[verification-system-016] Fable namespace ${replacement} replacement cannot authorize revalidation or foreign disposal`, async () => {
      await withCompiledFixture(async ({ root, candidate, originalRoot, parentDirectory }) => {
        const parked = path.join(root, 'parked-compile-owner')
        const foreignParent = path.join(root, 'foreign-namespace')
        const foreignRoot = replacement === 'canonical parent' ? path.join(foreignParent, path.basename(originalRoot)) : originalRoot
        const expected = inventory(originalRoot)
        const marker = path.join(foreignRoot, 'js/Fixture.js')
        const markerBytes = fs.readFileSync(path.join(originalRoot, 'js/Fixture.js'))
        if (replacement === 'canonical parent') {
          fs.renameSync(parentDirectory, parked)
          fs.mkdirSync(foreignParent)
          fs.cpSync(path.join(parked, path.basename(originalRoot)), foreignRoot, { recursive: true, preserveTimestamps: true })
          fs.symlinkSync(foreignParent, parentDirectory)
        } else {
          fs.renameSync(originalRoot, parked)
          fs.cpSync(parked, foreignRoot, { recursive: true, preserveTimestamps: true })
        }
        for (const entry of expected) fs.chmodSync(path.join(foreignRoot, entry.path), entry.mode)
        try {
          assert.deepEqual(inventory(foreignRoot), expected, 'Foreign replacement has the same complete bytes, modes and members')
          let revalidationFailure
          let disposalFailure
          try {
            candidate.revalidate()
          } catch (error) {
            revalidationFailure = error
          }
          try {
            candidate.dispose()
          } catch (error) {
            disposalFailure = error
          }
          assert.deepEqual(fs.readFileSync(marker), markerBytes, 'Captured path cannot authorize deleting a foreign directory')
          assert.deepEqual(inventory(foreignRoot), expected, 'Foreign namespace remains entirely unchanged')
          assert.ok(entryInvalid(revalidationFailure), 'Revalidation must reject the replaced directory identity')
          assert.ok(entryInvalid(disposalFailure), 'Disposal must reject the replaced directory identity')
          assert.match(disposalFailure.message, /cannot verify owned identity/i)
        } finally {
          if (replacement === 'canonical parent') {
            fs.unlinkSync(parentDirectory)
            fs.renameSync(parked, parentDirectory)
          } else {
            if (fs.existsSync(originalRoot)) fs.renameSync(originalRoot, foreignParent)
            fs.renameSync(parked, originalRoot)
          }
          candidate.dispose()
          assert.doesNotThrow(() => candidate.dispose(), 'Absent output is idempotent only within the restored original parent')
          assert.deepEqual(fs.readdirSync(parentDirectory), [], 'Restored namespace permits actual owned output reclamation')
        }
      })
    })
  }
  test('WHAT[verification-system-016] Fable namespace missing root under a replaced parent cannot claim completed disposal', async () => {
    await withCompiledFixture(async ({ root, candidate, originalRoot, parentDirectory }) => {
      const parked = path.join(root, 'parked-absent-compile-parent')
      const foreignParent = path.join(root, 'foreign-absent-compile-parent')
      fs.mkdirSync(foreignParent)
      fs.writeFileSync(path.join(foreignParent, 'caller-owned'), 'foreign parent bytes must survive')
      fs.renameSync(parentDirectory, parked)
      fs.symlinkSync(foreignParent, parentDirectory)
      try {
        assert.equal(fs.existsSync(originalRoot), false)
        assert.throws(() => candidate.dispose(), entryInvalid)
        assert.equal(fs.readFileSync(path.join(foreignParent, 'caller-owned'), 'utf8'), 'foreign parent bytes must survive')
        assert.equal(fs.existsSync(path.join(parked, path.basename(originalRoot))), true)
      } finally {
        fs.unlinkSync(parentDirectory)
        fs.renameSync(parked, parentDirectory)
      }
    })
  })
  test('WHAT[verification-system-016] compiled JavaScript revalidation rejects bytes, mode, added and missing output members', async t => {
    await withCompiledFixture(async ({ candidate, originalRoot }) => {
      const file = path.join(originalRoot, 'js/Fixture.js')
      const bytes = fs.readFileSync(file)
      const mode = fs.statSync(file).mode & 0o777
      for (const mutation of ['bytes', 'mode', 'added', 'missing']) {
        await t.test(mutation, () => {
          const added = path.join(originalRoot, 'js/unselected.js')
          try {
            if (mutation === 'bytes') fs.writeFileSync(file, Buffer.alloc(bytes.length, 0))
            else if (mutation === 'mode') fs.chmodSync(file, mode ^ 0o100)
            else if (mutation === 'added') fs.writeFileSync(added, 'unselected bytes')
            else fs.unlinkSync(file)
            assert.throws(() => candidate.revalidate(), entryInvalid)
          } finally {
            if (mutation === 'added') fs.unlinkSync(added)
            else {
              fs.writeFileSync(file, bytes, { mode })
              fs.chmodSync(file, mode)
            }
          }
          candidate.revalidate()
        })
      }
    })
  })
}
