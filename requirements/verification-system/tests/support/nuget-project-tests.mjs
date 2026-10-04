import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { prepareGitSourceCandidate } from '../../../../scripts/lib/verification-source-candidate.mjs'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
const sha512 = bytes => createHash('sha512').update(bytes).digest('base64')
const prepareProject = async options => (await import('../../../../scripts/lib/verification-nuget-project.mjs')).prepareVerificationNugetProject(options)
const projectPath = 'src/Fixture.fsproj'
const entryInvalid = error => error.code === 'verification-nuget-project-entry-invalid'

function inventory(root) {
  const entries = []
  function visit(relative) {
    const file = path.join(root, relative)
    const stat = fs.lstatSync(file)
    if (stat.isDirectory()) {
      entries.push({ path: relative, type: 'Directory', mode: stat.mode & 0o777 })
      for (const name of fs.readdirSync(file).sort()) visit(relative === '.' ? name : `${relative}/${name}`)
    } else {
      assert.equal(stat.isFile(), true, 'The Git candidate fixture contains ordinary files and directories')
      const bytes = fs.readFileSync(file)
      entries.push({ path: relative, type: 'File', mode: stat.mode & 0o777, size: bytes.length, sha256: sha256(bytes) })
    }
  }
  visit('.')
  return entries
}

function executor({ root, evaluation = {} }) {
  return `
const fs = require('node:fs')
const path = require('node:path')
fs.writeFileSync(${JSON.stringify(path.join(root, 'executor-entered'))}, 'selected Node protocol executor ran')
if (process.argv[2] === 'msbuild') {
  const fullPath = path.resolve(process.argv[3])
  const output = {Properties:{TargetFramework:'net10.0',TargetFrameworks:'',MSBuildProjectFullPath:fullPath},Items:{ProjectReference:[]}}
  Object.assign(output.Properties, ${JSON.stringify(evaluation.properties ?? {})})
  output.Items.ProjectReference = ${JSON.stringify(evaluation.references ?? [])}
  process.stdout.write(JSON.stringify(output))
} else {
  process.stderr.write('controlled project restore failure\\n')
  process.exit(73)
}
`
}

// The explicit JSON and files below are adapter protocol inputs. Their validity
// as a real NuGet resolution is proved separately by repositoryNugetProjectTest.
function graphExecutor({ root, mutation = '', phase = 'first' }) {
  return `
const fs = require('node:fs')
const path = require('node:path')
const assert = require('node:assert/strict')
const args = process.argv.slice(2)
const projectFile = path.resolve(args[1])
if (args[0] === 'msbuild') {
  process.stdout.write(JSON.stringify({Properties:{TargetFramework:'net10.0',TargetFrameworks:'',MSBuildProjectFullPath:projectFile},Items:{ProjectReference:[]}}))
} else {
  const packagesRoot = process.env.NUGET_PACKAGES
  const privateRoot = path.dirname(packagesRoot)
  const feed = path.join(privateRoot, 'feed')
  const configPath = path.join(privateRoot, 'NuGet.Config')
  const assetsPath = path.join(privateRoot, 'artifacts', 'obj', 'Fixture', 'project.assets.json')
  const lockPath = path.join(privateRoot, 'artifacts', 'packages.lock.json')
  const locked = args.includes('--locked-mode')
  const callsFile = ${JSON.stringify(path.join(root, 'restore-calls.json'))}
  const calls = fs.existsSync(callsFile) ? JSON.parse(fs.readFileSync(callsFile, 'utf8')) : []
  calls.push(locked ? 'locked' : 'first')
  fs.writeFileSync(callsFile, JSON.stringify(calls))
  assert.deepEqual(fs.readdirSync(packagesRoot), [], 'Each restore must start from an empty private package cache')
  assert.equal(args.includes('--force-evaluate'), false)
  assert.equal(args.includes(locked ? '--force' : '--use-lock-file'), true)
  for (const property of ['EnableTargetingPackDownload=false','EnableRuntimePackDownload=false','RestoreAdditionalProjectSources=','RestoreFallbackFolders=','RestoreAdditionalProjectFallbackFolders=']) assert.equal(args.includes('-p:' + property), true)
  const packageDirectory = path.join(packagesRoot, 'fixture.package', '1.0.0')
  fs.mkdirSync(path.join(packageDirectory, 'lib', 'net10.0'), {recursive:true})
  const archiveBytes = fs.readFileSync(path.join(feed, 'fixture.package.1.0.0.nupkg'))
  const rawSha512 = require('node:crypto').createHash('sha512').update(archiveBytes).digest('base64')
  const contentHash = Buffer.alloc(64, 5).toString('base64')
  fs.writeFileSync(path.join(packageDirectory, 'fixture.package.1.0.0.nupkg'), archiveBytes)
  fs.writeFileSync(path.join(packageDirectory, 'fixture.package.1.0.0.nupkg.sha512'), rawSha512)
  fs.writeFileSync(path.join(packageDirectory, '.nupkg.metadata'), JSON.stringify({contentHash,source:feed}))
  fs.writeFileSync(path.join(packageDirectory, 'fixture.package.nuspec'), '<package><metadata><id>fixture.package</id><version>1.0.0</version></metadata></package>')
  fs.writeFileSync(path.join(packageDirectory, 'lib', 'net10.0', 'fixture.dll'), 'ordinary Node protocol fixture bytes')
  if (!locked) fs.writeFileSync(path.join(packageDirectory, 'first-only-member'), 'must not survive cache reset')
  const target = {type:'package',dependencies:{'fixture.dependency':'2.0.0'},compile:{'lib/net10.0/fixture.dll':{}},runtime:{'lib/net10.0/fixture.dll':{}}}
  const library = {type:'package',path:'fixture.package/1.0.0',sha512:contentHash,files:['fixture.package.nuspec','lib/net10.0/fixture.dll']}
  const lockedPackage = {type:'Direct',requested:'[1.0.0, )',resolved:'1.0.0',contentHash,dependencies:{'fixture.dependency':'2.0.0'}}
  const assets = {version:3,targets:{'net10.0':{'fixture.package/1.0.0':target}},libraries:{'fixture.package/1.0.0':library},packageFolders:{[packagesRoot]:{}},project:{restore:{projectPath:projectFile,outputPath:path.dirname(assetsPath) + path.sep,configFilePaths:[configPath],sources:{[feed]:{}},fallbackFolders:[],frameworks:{'net10.0':{projectReferences:{}}}}}}
  const lock = {version:1,dependencies:{'net10.0':{'fixture.package':lockedPackage}}}
  const dependencyDirectory = path.join(packagesRoot, 'fixture.dependency', '2.0.0')
  fs.mkdirSync(path.join(dependencyDirectory, 'lib', 'net10.0'), {recursive:true})
  const dependencyBytes = fs.readFileSync(path.join(feed, 'fixture.dependency.2.0.0.nupkg'))
  fs.writeFileSync(path.join(dependencyDirectory, 'fixture.dependency.2.0.0.nupkg'), dependencyBytes)
  fs.writeFileSync(path.join(dependencyDirectory, 'fixture.dependency.2.0.0.nupkg.sha512'), require('node:crypto').createHash('sha512').update(dependencyBytes).digest('base64'))
  fs.writeFileSync(path.join(dependencyDirectory, '.nupkg.metadata'), JSON.stringify({contentHash,source:feed}))
  fs.writeFileSync(path.join(dependencyDirectory, 'lib', 'net10.0', 'dependency.dll'), 'ordinary transitive adapter fixture bytes')
  assets.targets['net10.0']['fixture.dependency/2.0.0'] = {type:'package',compile:{'lib/net10.0/dependency.dll':{}},runtime:{'lib/net10.0/dependency.dll':{}}}
  assets.libraries['fixture.dependency/2.0.0'] = {type:'package',path:'fixture.dependency/2.0.0',sha512:contentHash,files:['lib/net10.0/dependency.dll']}
  lock.dependencies['net10.0']['fixture.dependency'] = {type:'Transitive',resolved:'2.0.0',contentHash}
  fs.mkdirSync(path.dirname(assetsPath), {recursive:true})
  fs.writeFileSync(assetsPath, JSON.stringify(assets))
  if (!locked) fs.writeFileSync(lockPath, JSON.stringify(lock))
  if (${phase === 'first' ? '!locked' : 'locked'}) {
    ${mutation}
  }
}
`
}

async function withProjectFixture(action, { projectReferences = false, evaluation, executableBody, includeDependency = false } = {}) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'verification-nuget-project-test-')))
  const repositoryRoot = path.join(root, 'repository')
  const sourceParent = path.join(root, 'sources')
  const parentDirectory = path.join(root, 'projects')
  const toolRoot = path.join(root, 'sdk')
  const globalBytes = Buffer.from('{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}\n')
  let source
  let baselineSource
  let baselineSdk
  let actionFailure
  try {
    fs.mkdirSync(path.join(repositoryRoot, 'src'), { recursive: true })
    fs.mkdirSync(sourceParent)
    fs.mkdirSync(parentDirectory)
    fs.mkdirSync(path.join(toolRoot, 'dotnet-sdk'), { recursive: true })
    fs.writeFileSync(path.join(repositoryRoot, 'global.json'), globalBytes)
    fs.writeFileSync(path.join(repositoryRoot, 'Directory.Build.props'), '<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><DisableImplicitFSharpCoreReference>true</DisableImplicitFSharpCoreReference></PropertyGroup></Project>\n')
    fs.writeFileSync(path.join(repositoryRoot, projectPath), `<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="fixture.package" Version="1.0.0"/>${projectReferences ? '<ProjectReference Include="Other.fsproj"/>' : ''}</ItemGroup></Project>\n`)
    if (projectReferences) fs.writeFileSync(path.join(repositoryRoot, 'src/Other.fsproj'), '<Project Sdk="Microsoft.NET.Sdk"/>\n')
    execFileSync('git', ['init', '--quiet', '--object-format=sha1', '--template=', repositoryRoot])
    execFileSync('git', ['-C', repositoryRoot, 'add', '.'])
    const treeId = execFileSync('git', ['-C', repositoryRoot, 'write-tree'], { encoding: 'utf8' }).trim()
    source = prepareGitSourceCandidate({ repositoryRoot, treeId, parentDirectory: sourceParent })
    baselineSource = inventory(source.sourceRoot)
    // This executable is a Node protocol/failure adapter, not an actual SDK or
    // evidence that NuGet restored the declared fixture package.
    const executable = path.join(toolRoot, 'dotnet-sdk/dotnet')
    const body = executableBody ? executableBody({ root, source, executable }) : executor({ root, evaluation })
    fs.writeFileSync(executable, `#!${process.execPath}\n${body}\n`, { mode: 0o755 })
    const executableBytes = fs.readFileSync(executable)
    baselineSdk = inventory(toolRoot)
    const entries = baselineSdk.filter(entry => entry.path !== '.')
    const sdk = {
      toolRoot,
      entries,
      entriesDigest: sha256(JSON.stringify(entries)),
      archiveSha256: sha256(executableBytes),
      globalJsonSha256: sha256(globalBytes),
      dotnet: { path: 'dotnet-sdk/dotnet', sha256: sha256(executableBytes) },
      sdk: { version: '10.0.302', basePath: 'dotnet-sdk/sdk/10.0.302' },
      runtimes: [],
      identityScope: 'selected-dotnet-sdk-bundle',
      revalidate() {
        assert.deepEqual(inventory(toolRoot), baselineSdk)
      },
    }
    sdk.sdkDigest = sha256(JSON.stringify({ archiveSha256: sdk.archiveSha256, entriesDigest: sdk.entriesDigest, globalJsonSha256: sdk.globalJsonSha256, dotnet: sdk.dotnet, sdk: sdk.sdk, runtimes: sdk.runtimes, identityScope: sdk.identityScope }))
    const archivePath = path.join(root, 'fixture.package.1.0.0.nupkg')
    const archiveBytes = Buffer.from('explicit fixture bytes admitted only for the failing Node executor')
    fs.writeFileSync(archivePath, archiveBytes)
    const packageArchives = [{ id: 'fixture.package', version: '1.0.0', archivePath, sha512: sha512(archiveBytes) }]
    const dependencyArchive = path.join(root, 'fixture.dependency.2.0.0.nupkg')
    const dependencyBytes = Buffer.from('explicit transitive archive bytes for the Node protocol adapter')
    if (includeDependency) {
      fs.writeFileSync(dependencyArchive, dependencyBytes)
      packageArchives.push({ id: 'fixture.dependency', version: '2.0.0', archivePath: dependencyArchive, sha512: sha512(dependencyBytes) })
    }
    const result = await action({ root, source, sdk, parentDirectory, archivePath, options: { source, sdk, projectPath, packageArchives, parentDirectory } })
    assert.deepEqual(fs.readFileSync(archivePath), archiveBytes)
    if (includeDependency) assert.deepEqual(fs.readFileSync(dependencyArchive), dependencyBytes)
    return result
  } catch (error) {
    actionFailure = { error }
    throw error
  } finally {
    const failures = []
    try {
      assert.deepEqual(fs.readdirSync(parentDirectory), [], 'Failed project preparation reclaims all owned project roots')
      if (baselineSource) assert.deepEqual(inventory(source.sourceRoot), baselineSource, 'Project preparation preserves the complete caller-owned Git candidate, including .git')
      if (baselineSdk) assert.deepEqual(inventory(toolRoot), baselineSdk, 'Project preparation preserves its caller-owned selected SDK')
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
    if (failures.length) throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Project fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
  }
}

async function rejectsProject(options, predicate) {
  const [outcome] = await Promise.allSettled([prepareProject(options)])
  try {
    assert.equal(outcome.status, 'rejected', 'Invalid project preparation cannot publish a receipt')
    assert.ok(predicate(outcome.reason), `Unexpected project failure: ${String(outcome.reason)}`)
  } finally {
    if (outcome.status === 'fulfilled') outcome.value.dispose()
  }
}

async function withPreparedProjectFixture(action) {
  await withProjectFixture(async fixture => {
    const candidate = await prepareProject(fixture.options)
    const originalRoot = candidate.projectRoot
    let actionFailure
    try {
      await action({ ...fixture, candidate, originalRoot })
    } catch (error) {
      actionFailure = { error }
      throw error
    } finally {
      try {
        candidate.dispose()
        assert.equal(fs.existsSync(originalRoot), false, 'Disposal reclaims the original owned project root')
      } catch (error) {
        if (actionFailure) throw new AggregateError([actionFailure.error, error], 'Project receipt action and disposal failed', { cause: actionFailure.error })
        throw error
      }
    }
  }, { includeDependency: true, executableBody: ({ root }) => graphExecutor({ root }) })
}

async function withSourceFixture(action, { aliasedParent = false } = {}) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'verification-git-input-test-')))
  let source
  let actionFailure
  try {
    const repositoryRoot = path.join(root, 'repository')
    const parentDirectory = path.join(root, 'sources')
    fs.mkdirSync(repositoryRoot)
    fs.mkdirSync(parentDirectory)
    fs.writeFileSync(path.join(repositoryRoot, 'input.txt'), 'captured tracked source bytes\n')
    execFileSync('git', ['init', '--quiet', '--object-format=sha1', '--template=', repositoryRoot])
    execFileSync('git', ['-C', repositoryRoot, 'add', '.'])
    const treeId = execFileSync('git', ['-C', repositoryRoot, 'write-tree'], { encoding: 'utf8' }).trim()
    const parentAlias = path.join(root, 'parent-alias')
    if (aliasedParent) fs.symlinkSync(parentDirectory, parentAlias)
    source = prepareGitSourceCandidate({ repositoryRoot, treeId, parentDirectory: aliasedParent ? parentAlias : parentDirectory })
    await action({ root, source, parentDirectory, parentAlias })
  } catch (error) {
    actionFailure = { error }
    throw error
  } finally {
    const failures = []
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
    if (failures.length) throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Source input fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
  }
}

function registerNugetGraphTests() {
  const writeAssets = "fs.writeFileSync(assetsPath, JSON.stringify(assets))"
  const writeLock = "fs.writeFileSync(lockPath, JSON.stringify(lock))"
  const invalidGraphs = [
    ['foreign output directory', "assets.project.restore.outputPath = '/foreign/artifacts/'\n" + writeAssets, 'entry-invalid'],
    ['unselected dependency edge', "target.dependencies = {'unselected.package':'9.0.0'}\nlockedPackage.dependencies = {'unselected.package':'9.0.0'}\n" + writeAssets + '\n' + writeLock, 'entry-invalid'],
    ['target and lock dependency version disagreement', "target.dependencies['fixture.dependency'] = '9.0.0'\n" + writeAssets, 'entry-invalid'],
    ['null library', "assets.libraries['fixture.package/1.0.0'] = null\n" + writeAssets, 'entry-invalid'],
    ['null compile asset map', "target.compile = null\n" + writeAssets, 'entry-invalid'],
    ['array runtime asset map', "target.runtime = []\n" + writeAssets, 'entry-invalid'],
    ['null lock framework dependency map', "lock.dependencies['net10.0'] = null\n" + writeLock, 'entry-invalid'],
    ['array lock framework dependency map', "lock.dependencies['net10.0'] = []\n" + writeLock, 'entry-invalid'],
    ['malformed lock JSON', "fs.writeFileSync(lockPath, '{broken lock')", 'entry-invalid'],
    ['malformed assets JSON', "fs.writeFileSync(assetsPath, '{broken assets')", 'entry-invalid'],
    ['lock contentHash disagreement', "lockedPackage.contentHash = Buffer.alloc(64, 6).toString('base64')\n" + writeLock, 'entry-invalid'],
    ['raw archive sidecar disagreement', "fs.writeFileSync(path.join(packageDirectory, 'fixture.package.1.0.0.nupkg.sha512'), Buffer.alloc(64).toString('base64'))", 'integrity-invalid'],
    ['changed restored archive bytes', "fs.appendFileSync(path.join(packageDirectory, 'fixture.package.1.0.0.nupkg'), 'changed package bytes')", 'integrity-invalid'],
    ['missing runtime file', "fs.unlinkSync(path.join(packageDirectory, 'lib', 'net10.0', 'fixture.dll'))", 'entry-invalid'],
    ['foreign package folder', "assets.packageFolders = {'/foreign/packages':{}}\n" + writeAssets, 'entry-invalid'],
  ]
  for (const [description, mutation, code] of invalidGraphs) {
    test(`WHAT[verification-system-016] project graph rejects ${description} without publishing a restore receipt`, async () => {
      await withProjectFixture(async ({ root, options }) => {
        await rejectsProject(options, error => error.code === `verification-nuget-project-${code}`)
        assert.deepEqual(JSON.parse(fs.readFileSync(path.join(root, 'restore-calls.json'), 'utf8')), ['first'], 'A corrupt graph cannot authorize a subsequent locked restore')
      }, { includeDependency: true, executableBody: ({ root }) => graphExecutor({ root, mutation }) })
    })
  }
  const changedInputs = [
    ['an added feed archive', "fs.writeFileSync(path.join(feed, 'unselected.9.9.9.nupkg'), 'unselected bytes')", 'entry-invalid'],
    ['selected feed bytes', "fs.appendFileSync(path.join(feed, 'fixture.package.1.0.0.nupkg'), 'changed bytes')", 'integrity-invalid'],
    ['the selected NuGet config', "fs.appendFileSync(configPath, '<unexpected/>')", 'entry-invalid'],
  ]
  for (const [description, mutation, code] of changedInputs) {
    test(`WHAT[verification-system-016] project restore rejects ${description} changed by the first consumer before starting locked restore`, async () => {
      await withProjectFixture(async ({ root, options }) => {
        await rejectsProject(options, error => error.code === `verification-nuget-project-${code}`)
        assert.deepEqual(JSON.parse(fs.readFileSync(path.join(root, 'restore-calls.json'), 'utf8')), ['first'], 'A corrupt captured input cannot be consumed by a subsequent restore stage')
      }, { includeDependency: true, executableBody: ({ root }) => graphExecutor({ root, mutation }) })
    })
  }
  test('WHAT[verification-system-016] project restore rejects a Git journal file changed by the first consumer before starting locked restore', async () => {
    await withProjectFixture(async ({ root, source, options }) => {
      const head = path.join(source.sourceRoot, '.git/HEAD')
      const bytes = fs.readFileSync(head)
      try {
        await rejectsProject(options, error => error.code === 'source-candidate-entry-invalid')
        assert.deepEqual(JSON.parse(fs.readFileSync(path.join(root, 'restore-calls.json'), 'utf8')), ['first'])
      } finally {
        fs.writeFileSync(head, bytes)
      }
    }, { includeDependency: true, executableBody: ({ root, source }) => graphExecutor({ root, mutation: `fs.appendFileSync(${JSON.stringify(path.join(source.sourceRoot, '.git/HEAD'))}, 'changed source metadata')` }) })
  })
  const changedSecondRestore = [
    ['graph change', "target.compile['lib/net10.0/second.dll'] = {}\nfs.writeFileSync(path.join(packageDirectory, 'lib', 'net10.0', 'second.dll'), 'new compile asset')\n" + writeAssets, 'entry-invalid'],
    ['lock byte change', "fs.appendFileSync(lockPath, '\\n')", 'entry-invalid'],
    ['physical locked restore failure', "process.stderr.write('controlled locked restore failure\\n')\nprocess.exit(74)", 'probe-failed'],
  ]
  for (const [description, mutation, code] of changedSecondRestore) {
    test(`WHAT[verification-system-016] fresh-cache locked project restore rejects ${description}`, async () => {
      await withProjectFixture(async ({ root, options }) => {
        await rejectsProject(options, error => code === 'probe-failed' ? error.code === 'verification-tool-probe-failed' && error.exitCode === 74 && error.stderr.includes('controlled locked restore failure') : error.code === `verification-nuget-project-${code}`)
        assert.deepEqual(JSON.parse(fs.readFileSync(path.join(root, 'restore-calls.json'), 'utf8')), ['first', 'locked'])
      }, { includeDependency: true, executableBody: ({ root }) => graphExecutor({ root, mutation, phase: 'locked' }) })
    })
  }
  test('WHAT[verification-system-016] a changed public project root cannot borrow the prepared identity or redirect disposal', async () => {
    await withPreparedProjectFixture(async ({ root, candidate, originalRoot }) => {
      const foreignRoot = path.join(root, 'foreign-project')
      const marker = path.join(foreignRoot, 'caller-owned.txt')
      fs.mkdirSync(foreignRoot)
      fs.writeFileSync(marker, 'foreign project bytes must survive')
      candidate.projectRoot = foreignRoot
      assert.throws(() => candidate.revalidate(), entryInvalid)
      candidate.dispose()
      assert.equal(fs.existsSync(originalRoot), false)
      assert.equal(fs.readFileSync(marker, 'utf8'), 'foreign project bytes must survive')
    })
  })
  test('WHAT[verification-system-016] a changed public project digest cannot claim the original prepared identity', async () => {
    await withPreparedProjectFixture(async ({ candidate }) => {
      const originalDigest = candidate.projectDigest
      try {
        candidate.projectDigest = '0'.repeat(64)
        assert.throws(() => candidate.revalidate(), entryInvalid)
      } finally {
        candidate.projectDigest = originalDigest
      }
      candidate.revalidate()
    })
  })
  for (const mutation of ['bytes', 'mode', 'added', 'missing']) {
    test(`WHAT[verification-system-016] a prepared project rejects ${mutation} output identity before further consumption`, async () => {
      await withPreparedProjectFixture(async ({ candidate, originalRoot }) => {
        const member = path.join(originalRoot, 'packages/fixture.package/1.0.0/lib/net10.0/fixture.dll')
        const bytes = fs.readFileSync(member)
        const mode = fs.statSync(member).mode & 0o777
        const added = path.join(originalRoot, 'artifacts/unselected-output')
        try {
          if (mutation === 'bytes') fs.writeFileSync(member, Buffer.alloc(bytes.length, 0))
          else if (mutation === 'mode') fs.chmodSync(member, mode ^ 0o100)
          else if (mutation === 'added') fs.writeFileSync(added, 'unselected output bytes')
          else fs.unlinkSync(member)
          assert.throws(() => candidate.revalidate(), entryInvalid)
        } finally {
          if (mutation === 'added') fs.unlinkSync(added)
          else {
            fs.writeFileSync(member, bytes, { mode })
            fs.chmodSync(member, mode)
          }
        }
        candidate.revalidate()
      })
    })
  }
  test('WHAT[verification-system-016] a changed project parent alias cannot redirect disposal into caller-owned data', async () => {
    await withProjectFixture(async ({ root, parentDirectory, options }) => {
      const alias = path.join(root, 'project-parent-alias')
      fs.symlinkSync(parentDirectory, alias)
      const candidate = await prepareProject({ ...options, parentDirectory: alias })
      const originalRoot = candidate.projectRoot
      const foreignParent = path.join(root, 'foreign-project-parent')
      const foreignRoot = path.join(foreignParent, path.basename(originalRoot))
      const marker = path.join(foreignRoot, 'caller-owned.txt')
      let actionFailure
      try {
        fs.mkdirSync(foreignRoot, { recursive: true })
        fs.writeFileSync(marker, 'foreign bytes must survive alias replacement')
        fs.unlinkSync(alias)
        fs.symlinkSync(foreignParent, alias)
        candidate.revalidate()
        candidate.dispose()
        assert.equal(fs.existsSync(originalRoot), false)
        assert.equal(fs.readFileSync(marker, 'utf8'), 'foreign bytes must survive alias replacement')
      } catch (error) {
        actionFailure = { error }
        throw error
      } finally {
        try {
          candidate.dispose()
          assert.equal(fs.existsSync(originalRoot), false)
        } catch (error) {
          if (actionFailure) throw new AggregateError([actionFailure.error, error], 'Project alias action and disposal failed', { cause: actionFailure.error })
          throw error
        }
      }
    }, { includeDependency: true, executableBody: ({ root }) => graphExecutor({ root }) })
  })
  test('WHAT[verification-system-016] project receipt binds a consistent two-package graph after both actual adapter processes start from empty private caches', async () => {
    await withProjectFixture(async ({ root, source, sdk, options }) => {
      const candidate = await prepareProject(options)
      let actionFailure
      try {
        assert.deepEqual(JSON.parse(fs.readFileSync(path.join(root, 'restore-calls.json'), 'utf8')), ['first', 'locked'])
        assert.equal(candidate.identityScope, 'selected-nuget-project-restore')
        assert.equal(candidate.projectPath, projectPath)
        assert.equal(candidate.targetFramework, 'net10.0')
        assert.equal(candidate.assetsPath, 'artifacts/obj/Fixture/project.assets.json')
        assert.equal(candidate.lockPath, 'artifacts/packages.lock.json')
        assert.equal(candidate.sourceDigest, source.sourceDigest)
        assert.equal(candidate.treeId, source.treeId)
        assert.equal(candidate.sdkDigest, sdk.sdkDigest)
        assert.deepEqual(candidate.packages, options.packageArchives.map(({ id, version, sha512 }) => ({ id, version, rawSha512: sha512 })).sort((left, right) => left.id.localeCompare(right.id, 'en')))
        const assets = JSON.parse(fs.readFileSync(path.join(candidate.projectRoot, candidate.assetsPath), 'utf8'))
        const lock = JSON.parse(fs.readFileSync(path.join(candidate.projectRoot, candidate.lockPath), 'utf8'))
        assert.deepEqual(candidate.graph, { targetFramework: 'net10.0', targets: assets.targets, libraries: assets.libraries, dependencies: lock.dependencies })
        assert.deepEqual(assets.targets['net10.0']['fixture.package/1.0.0'].dependencies, { 'fixture.dependency': '2.0.0' })
        assert.deepEqual(lock.dependencies['net10.0']['fixture.package'].dependencies, { 'fixture.dependency': '2.0.0' })
        for (const pkg of candidate.packages) assert.notEqual(pkg.rawSha512, assets.libraries[`${pkg.id}/${pkg.version}`].sha512)
        assert.equal(candidate.graphDigest, sha256(JSON.stringify(candidate.graph)))
        assert.deepEqual(candidate.entries, inventory(candidate.projectRoot))
        assert.equal(candidate.entries.some(entry => entry.path.endsWith('first-only-member')), false)
        assert.equal(candidate.entriesDigest, sha256(JSON.stringify(candidate.entries)))
        assert.equal(candidate.projectDigest, sha256(JSON.stringify({ sourceDigest: candidate.sourceDigest, treeId: candidate.treeId, sdkDigest: candidate.sdkDigest, projectPath: candidate.projectPath, packages: candidate.packages, graphDigest: candidate.graphDigest, entriesDigest: candidate.entriesDigest, identityScope: candidate.identityScope })))
        candidate.revalidate()
      } catch (error) {
        actionFailure = { error }
        throw error
      } finally {
        try {
          candidate.dispose()
          assert.equal(fs.existsSync(candidate.projectRoot), false)
        } catch (error) {
          if (actionFailure) throw new AggregateError([actionFailure.error, error], 'Project receipt action and disposal failed', { cause: actionFailure.error })
          throw error
        }
      }
    }, { includeDependency: true, executableBody: ({ root }) => graphExecutor({ root }) })
  })
}

export function registerNugetProjectTests() {
  registerNugetGraphTests()
  for (const relative of ['input.txt', '.git/HEAD']) {
    for (const mutation of ['bytes', 'mode', 'missing']) {
      test(`WHAT[verification-system-016] the prepared Git input revalidates ${mutation} identity of ${relative}`, async () => {
        await withSourceFixture(async ({ source }) => {
          const file = path.join(source.sourceRoot, relative)
          const bytes = fs.readFileSync(file)
          const mode = fs.statSync(file).mode & 0o777
          try {
            if (mutation === 'bytes') fs.writeFileSync(file, Buffer.alloc(bytes.length, 0))
            else if (mutation === 'mode') fs.chmodSync(file, mode ^ 0o100)
            else fs.unlinkSync(file)
            assert.throws(() => source.revalidate(), error => error.code === 'source-candidate-entry-invalid')
          } finally {
            fs.writeFileSync(file, bytes, { mode })
            fs.chmodSync(file, mode)
          }
          source.revalidate()
        })
      })
    }
  }
  for (const relative of ['unselected.txt', '.git/unselected']) {
    test(`WHAT[verification-system-016] the prepared Git input rejects an added ordinary member ${relative}`, async () => {
      await withSourceFixture(async ({ source }) => {
        const added = path.join(source.sourceRoot, relative)
        fs.writeFileSync(added, 'unselected bytes')
        try {
          assert.throws(() => source.revalidate(), error => error.code === 'source-candidate-entry-invalid')
        } finally {
          fs.unlinkSync(added)
        }
        source.revalidate()
      })
    })
  }
  test('WHAT[verification-system-016] a changed public source root cannot borrow the captured Git identity or redirect disposal', async () => {
    await withSourceFixture(async ({ root, source }) => {
      const originalRoot = source.sourceRoot
      const foreignRoot = path.join(root, 'foreign-input')
      const marker = path.join(foreignRoot, 'caller-owned.txt')
      fs.mkdirSync(foreignRoot)
      fs.writeFileSync(marker, 'foreign bytes must survive')
      try {
        source.sourceRoot = foreignRoot
        assert.throws(() => source.revalidate(), error => error.code === 'source-candidate-entry-invalid')
      } finally {
        source.sourceRoot = originalRoot
      }
      source.revalidate()
      try {
        source.sourceRoot = foreignRoot
        source.dispose()
        assert.equal(fs.existsSync(originalRoot), false)
        assert.equal(fs.readFileSync(marker, 'utf8'), 'foreign bytes must survive')
      } finally {
        source.sourceRoot = originalRoot
      }
    })
  })
  test('WHAT[verification-system-016] prepared Git input disposal reclaims its canonical owned root and preserves the foreign owner after parent link replacement', async t => {
    await withSourceFixture(async ({ root, source, parentAlias }) => {
      const originalRoot = fs.realpathSync(source.sourceRoot)
      const foreignParent = path.join(root, 'foreign-parent')
      const foreignRoot = path.join(foreignParent, path.basename(originalRoot))
      const marker = path.join(foreignRoot, 'caller-owned.txt')
      fs.mkdirSync(foreignRoot, { recursive: true })
      fs.writeFileSync(marker, 'foreign bytes must survive')
      fs.unlinkSync(parentAlias)
      fs.symlinkSync(foreignParent, parentAlias)
      source.dispose()
      const ownership = { originalRootExists: fs.existsSync(originalRoot), foreignMarkerExists: fs.existsSync(marker) }
      t.diagnostic(JSON.stringify(ownership))
      assert.deepEqual(ownership, { originalRootExists: false, foreignMarkerExists: true })
      assert.equal(fs.readFileSync(marker, 'utf8'), 'foreign bytes must survive')
    }, { aliasedParent: true })
  })
  for (const selectedPath of ['missing.fsproj', '../outside.fsproj', '/absolute.fsproj', 'src/../src/Fixture.fsproj']) {
    test(`WHAT[verification-system-016] project preparation rejects missing or nonordinary selected path ${selectedPath}`, async () => {
      await withProjectFixture(async ({ root, options }) => {
        await rejectsProject({ ...options, projectPath: selectedPath }, entryInvalid)
        assert.equal(fs.existsSync(path.join(root, 'executor-entered')), false)
      })
    })
  }
  test('WHAT[verification-system-016] project preparation requires the captured Git source digest rather than a changed public claim', async () => {
    await withProjectFixture(async ({ root, source, options }) => {
      const digest = source.sourceDigest
      try {
        source.sourceDigest = '0'.repeat(64)
        await rejectsProject(options, error => error.code === 'source-candidate-entry-invalid')
        assert.equal(fs.existsSync(path.join(root, 'executor-entered')), false)
      } finally {
        source.sourceDigest = digest
      }
    })
  })
  test('WHAT[verification-system-016] project preparation rejects a source containing a symbolic selected project without following its foreign target', async () => {
    await withProjectFixture(async ({ root, source, options }) => {
      const foreign = path.join(root, 'foreign.fsproj')
      const alias = path.join(source.sourceRoot, 'src/Alias.fsproj')
      const bytes = '<Project Sdk="Microsoft.NET.Sdk"/>\n'
      fs.writeFileSync(foreign, bytes)
      fs.symlinkSync(foreign, alias)
      try {
        await rejectsProject({ ...options, projectPath: 'src/Alias.fsproj' }, error => error.code === 'source-candidate-entry-invalid')
        assert.equal(fs.existsSync(path.join(root, 'executor-entered')), false)
        assert.equal(fs.readFileSync(foreign, 'utf8'), bytes)
      } finally {
        fs.unlinkSync(alias)
      }
    })
  })
  test('WHAT[verification-system-016] project preparation rejects selected archive byte identity mismatch before invoking MSBuild', async () => {
    await withProjectFixture(async ({ root, options }) => {
      const packageArchives = options.packageArchives.map(archive => ({ ...archive, sha512: Buffer.alloc(64).toString('base64') }))
      await rejectsProject({ ...options, packageArchives }, error => error.code === 'verification-nuget-project-integrity-invalid')
      assert.equal(fs.existsSync(path.join(root, 'executor-entered')), false)
    })
  })
  for (const mutation of ['bytes', 'mode', 'missing']) {
    test(`WHAT[verification-system-016] project preparation rejects ${mutation} corruption in the actual tracked source before evaluation`, async () => {
      await withProjectFixture(async ({ root, source, options }) => {
        const file = path.join(source.sourceRoot, projectPath)
        const bytes = fs.readFileSync(file)
        const mode = fs.statSync(file).mode & 0o777
        try {
          if (mutation === 'bytes') fs.writeFileSync(file, Buffer.alloc(bytes.length, 0))
          else if (mutation === 'mode') fs.chmodSync(file, mode ^ 0o100)
          else fs.unlinkSync(file)
          await rejectsProject(options, error => error.code === 'source-candidate-entry-invalid')
          assert.equal(fs.existsSync(path.join(root, 'executor-entered')), false)
        } finally {
          fs.writeFileSync(file, bytes, { mode })
          fs.chmodSync(file, mode)
        }
      })
    })
  }
  test('WHAT[verification-system-016] project preparation rejects evaluated project references instead of silently restoring a larger graph', async () => {
    await withProjectFixture(async ({ options }) => {
      await rejectsProject(options, error => error.code === 'verification-nuget-project-unsupported')
    }, { projectReferences: true, evaluation: { references: [{ Identity: 'Other.fsproj' }] } })
  })
  test('WHAT[verification-system-016] project preparation rejects evaluated multiple target frameworks', async () => {
    await withProjectFixture(async ({ options }) => {
      await rejectsProject(options, error => error.code === 'verification-nuget-project-unsupported')
    }, { evaluation: { properties: { TargetFramework: '', TargetFrameworks: 'net10.0;net9.0' } } })
  })
  test('WHAT[verification-system-016] project preparation rejects an evaluated project path belonging to a different source entry', async () => {
    await withProjectFixture(async ({ options }) => {
      await rejectsProject(options, entryInvalid)
    }, { evaluation: { properties: { MSBuildProjectFullPath: '/foreign/Other.fsproj' } } })
  })
  test('WHAT[verification-system-016] actual selected project executor failure retains the physical cause and reclaims only owned output', async () => {
    await withProjectFixture(async ({ options }) => {
      await rejectsProject(options, error => error.code === 'verification-tool-probe-failed' && error.exitCode === 73 && error.stderr.includes('controlled project restore failure'))
    })
  })
  for (const reason of [new Error('project preparation cancelled before admission'), null]) {
    test(`WHAT[verification-system-016] early project cancellation preserves ${reason === null ? 'null' : 'Error'} before reading absent inputs`, async () => {
      await withProjectFixture(async ({ root, options }) => {
        const controller = new AbortController()
        controller.abort(reason)
        await rejectsProject({ ...options, source: { ...options.source, sourceRoot: path.join(root, 'missing-source') }, packageArchives: [], signal: controller.signal }, error => error === reason)
        assert.equal(fs.existsSync(path.join(root, 'executor-entered')), false)
      })
    })
  }
  for (const reason of [new Error('cancel held project restore'), null]) {
    test(`WHAT[verification-system-016] held project restore preserves ${reason === null ? 'null' : 'Error'} cancellation and drains its actual process group`, { skip: process.platform === 'win32' }, async () => {
      await withProjectFixture(async ({ root, options }) => {
        const marker = path.join(root, 'started.json')
        const fallback = path.join(root, 'fallback-cleanup.txt')
        const controller = new AbortController()
        const started = Promise.withResolvers()
        let physical
        let settled
        let actionFailure
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
          const preparing = prepareProject({ ...options, signal: controller.signal })
          settled = Promise.allSettled([preparing])
          physical = await Promise.race([started.promise, preparing.then(() => { throw new Error('Held project restore unexpectedly published') })])
          assert.ok(Number.isInteger(physical.pid) && Number.isInteger(physical.childPid))
          assert.equal(fs.readdirSync(options.parentDirectory).length, 1)
          controller.abort(reason)
          // EOF cancellation drains asynchronously; a second signal is only valid after settlement.
          const [outcome] = await settled
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason, reason)
          assert.throws(() => process.kill(physical.pid, 'SIGTERM'), error => error.code === 'ESRCH')
          assert.equal(fs.existsSync(fallback), false)
          for (const pid of [physical.pid, physical.childPid]) {
            assert.throws(() => process.kill(pid, 0), error => error.code === 'ESRCH')
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
          if (failures.length) throw new AggregateError(actionFailure ? [actionFailure.error, ...failures] : failures, 'Held project fixture cleanup failed', { cause: actionFailure ? actionFailure.error : failures[0] })
        }
      }, { executableBody: ({ root }) => {
        const marker = path.join(root, 'started.json')
        const fallback = path.join(root, 'fallback-cleanup.txt')
        const childProgram = `require('node:fs').watch(${JSON.stringify(root)}, () => {})\nprocess.stdout.write('ready\\n')`
        return `
const fs = require('node:fs')
const path = require('node:path')
if (process.argv[2] === 'msbuild') {
  process.stdout.write(JSON.stringify({Properties:{TargetFramework:'net10.0',TargetFrameworks:'',MSBuildProjectFullPath:path.resolve(process.argv[3])},Items:{ProjectReference:[]}}))
} else {
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
}
`
      } })
    })
  }
}
