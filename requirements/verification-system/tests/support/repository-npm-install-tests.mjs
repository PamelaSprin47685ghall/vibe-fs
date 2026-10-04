import assert from 'node:assert/strict'
import { execFile, execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { create } from 'tar'
import { prepareGitSourceCandidate } from '../../../../scripts/lib/verification-source-candidate.mjs'
import { prepareVerificationNodeTools } from '../../../../scripts/lib/verification-node-tools.mjs'
import { installVerificationDependenciesFromToolArchive } from '../../../../scripts/lib/verification-npm-candidate.mjs'

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')

function consume(nodeExecutable, dependencyRoot, home, signal) {
  const program = `
    import assert from 'node:assert/strict'
    import fs from 'node:fs'
    import path from 'node:path'
    import { ofArray, toArray } from '@fable-org/fable-library-js/List.js'
    import { parse } from 'acorn'
    import { create, Parser } from 'tar'
    assert.deepEqual(toArray(ofArray([3, 1, 4])), [3, 1, 4])
    const parsed = parse('export const answer = 42', { ecmaVersion: 'latest', sourceType: 'module' })
    assert.equal(parsed.body[0].declaration.declarations[0].init.value, 42)
    const tarRoot = path.join(process.env.HOME, 'tar-roundtrip')
    fs.mkdirSync(tarRoot)
    fs.writeFileSync(path.join(tarRoot, 'payload.txt'), 'actual installed tar consumer')
    const chunks = []
    for await (const chunk of create({ cwd: tarRoot, portable: true, noMtime: true }, ['payload.txt'])) chunks.push(chunk)
    const entries = []
    const parser = new Parser({ onReadEntry: entry => {
      const bytes = []
      entry.on('data', chunk => bytes.push(chunk))
      entry.on('end', () => entries.push({ path: entry.path, content: Buffer.concat(bytes).toString() }))
    } })
    await new Promise((resolve, reject) => {
      parser.once('error', reject)
      parser.once('end', resolve)
      parser.end(Buffer.concat(chunks))
    })
    assert.deepEqual(entries, [{ path: 'payload.txt', content: 'actual installed tar consumer' }])
    console.log(JSON.stringify({ fable: true, acorn: true, tar: true, nodeVersion: process.version }))
  `
  return new Promise((resolve, reject) => {
    execFile(nodeExecutable, ['--input-type=module', '-e', program], {
      cwd: dependencyRoot,
      env: { HOME: home, TMPDIR: home, PATH: path.dirname(nodeExecutable) },
      signal,
      timeout: 30000,
      encoding: 'utf8',
    }, (error, stdout, stderr) => {
      if (error) {
        error.stdout = stdout
        error.stderr = stderr
        reject(error)
      } else resolve(stdout)
    })
  })
}

function assertDependencyIdentity(candidate, packageBytes, lockBytes, tools) {
  assert.equal(candidate.packageJsonSha256, sha256(packageBytes))
  assert.equal(candidate.lockfileSha256, sha256(lockBytes))
  assert.equal(candidate.installation.identityScope, 'selected-node-npm-bundle')
  assert.equal(candidate.installation.toolDigest, tools.toolDigest)
  assert.equal(candidate.installation.nodeSha256, tools.node.sha256)
  assert.equal(candidate.installation.npmCliSha256, tools.npm.cliSha256)
  assert.equal(candidate.installation.nodeVersion, tools.node.version)
  assert.equal(candidate.installation.npmVersion, '11.12.1')
  assert.equal(candidate.installation.platform, tools.node.platform)
  assert.equal(candidate.installation.arch, tools.node.arch)
  assert.equal(candidate.installation.lifecycleScripts, 'disabled')
  const preparedDependencyDigest = sha256(JSON.stringify({ archiveSha256: candidate.archiveSha256, lockfileSha256: candidate.lockfileSha256, entries: candidate.entries }))
  assert.equal(candidate.dependencyDigest, sha256(JSON.stringify({ preparedDependencyDigest, packageJsonSha256: candidate.packageJsonSha256, installation: candidate.installation })))
}

export async function repositoryNpmInstallTest(t) {
    const allocatedRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-repository-npm-'))
    let root
    let parentDirectory
    let source
    let tools
    let dependencies
    let toolArchive
    let packageBytes
    let lockBytes
    let prepared = false
    let installed = false
    try {
      root = fs.realpathSync(allocatedRoot)
      parentDirectory = path.join(root, 'candidates')
      fs.mkdirSync(parentDirectory)
      await t.test('WHAT[verification-system-016] selected committed source tree and complete Node/npm archive have explicit identities before installation', async () => {
        assert.ok(process.env.WXS_VERIFICATION_NPM_CLI, 'WXS_VERIFICATION_NPM_CLI must explicitly select a complete npm 11.12.1 package')
        const npmCli = fs.realpathSync(process.env.WXS_VERIFICATION_NPM_CLI)
        assert.equal(path.basename(npmCli), 'npm-cli.js')
        const npmRoot = path.resolve(npmCli, '../..')
        const npmManifest = fs.readFileSync(path.join(npmRoot, 'package.json'))
        assert.equal(JSON.parse(npmManifest).name, 'npm')
        assert.equal(JSON.parse(npmManifest).version, '11.12.1')
        const treeId = execFileSync('git', ['--no-replace-objects', '-C', repositoryRoot, 'rev-parse', 'HEAD^{tree}'], { encoding: 'utf8' }).trim()
        source = prepareGitSourceCandidate({ repositoryRoot, treeId, parentDirectory })
        assert.equal(source.treeId, treeId)
        packageBytes = fs.readFileSync(path.join(source.sourceRoot, 'package.json'))
        lockBytes = fs.readFileSync(path.join(source.sourceRoot, 'package-lock.json'))
        assert.equal(JSON.parse(packageBytes).packageManager, 'npm@11.12.1')
        assert.equal(JSON.parse(lockBytes).lockfileVersion, 3)
        const selected = path.join(root, 'selected')
        const nodePath = 'toolchain/node/bin/node'
        const npmCliPath = 'toolchain/npm/bin/npm-cli.js'
        fs.mkdirSync(path.join(selected, 'toolchain/node/bin'), { recursive: true })
        fs.copyFileSync(process.execPath, path.join(selected, nodePath))
        fs.chmodSync(path.join(selected, nodePath), 0o755)
        fs.cpSync(npmRoot, path.join(selected, 'toolchain/npm'), { recursive: true, verbatimSymlinks: true })
        const chunks = []
        for await (const chunk of create({ cwd: selected, portable: true, noMtime: true }, ['toolchain'])) chunks.push(chunk)
        const archiveBytes = Buffer.concat(chunks)
        const archivePath = path.join(root, 'tools.tar')
        fs.writeFileSync(archivePath, archiveBytes, { flag: 'wx' })
        toolArchive = { archivePath, archiveSha256: sha256(archiveBytes), nodePath, npmCliPath }
        fs.rmSync(selected, { recursive: true, force: true })
        tools = await prepareVerificationNodeTools({ ...toolArchive, parentDirectory, signal: t.signal })
        assert.equal(tools.node.sha256, sha256(fs.readFileSync(process.execPath)))
        assert.equal(tools.node.version, process.version)
        assert.equal(tools.npm.cliSha256, sha256(fs.readFileSync(npmCli)))
        assert.equal(tools.npm.manifestSha256, sha256(npmManifest))
        assert.equal(tools.npm.version, '11.12.1')
        assert.equal(tools.archiveSha256, toolArchive.archiveSha256)
        assert.equal(tools.entriesDigest, sha256(JSON.stringify(tools.entries)))
        assert.equal(tools.toolDigest, sha256(JSON.stringify({ archiveSha256: tools.archiveSha256, entriesDigest: tools.entriesDigest, node: tools.node, npm: tools.npm, identityScope: tools.identityScope })))
        t.diagnostic(JSON.stringify({ treeId, sourceDigest: source.sourceDigest, toolArchiveSha256: toolArchive.archiveSha256, toolDigest: tools.toolDigest, node: tools.node, npm: tools.npm }))
        prepared = true
      })
      if (!prepared) return
      await t.test('WHAT[verification-system-016] actual repository registry installation binds complete dependencies and retains no installer tools', async () => {
        dependencies = await installVerificationDependenciesFromToolArchive({
          sourceRoot: source.sourceRoot,
          parentDirectory,
          toolArchive,
          expectedNpmVersion: '11.12.1',
          expectedNodeVersion: tools.node.version,
          signal: t.signal,
          output: { write: chunk => t.diagnostic(chunk.toString()) },
        })
        assertDependencyIdentity(dependencies, packageBytes, lockBytes, tools)
        assert.deepEqual(fs.readdirSync(parentDirectory).sort(), [source.sourceRoot, tools.toolRoot, dependencies.dependencyRoot].map(directory => path.basename(directory)).sort())
        const inventory = new Map(dependencies.entries.map(entry => [entry.path, entry]))
        const presentOptional = []
        const absentOptional = []
        for (const [memberPath, member] of Object.entries(JSON.parse(lockBytes).packages)) {
          if (!memberPath) continue
          const manifestPath = `${memberPath}/package.json`
          const entry = inventory.get(manifestPath)
          if (!entry) {
            assert.equal(member.optional, true, `Required locked package missing: ${memberPath}`)
            absentOptional.push(memberPath)
            continue
          }
          assert.equal(entry.type, 'File')
          const bytes = fs.readFileSync(path.join(dependencies.dependencyRoot, manifestPath))
          assert.equal(sha256(bytes), entry.sha256)
          assert.equal(JSON.parse(bytes).version, member.version, `Installed package version differs from its exact lock member: ${memberPath}`)
          if (member.optional) presentOptional.push(memberPath)
        }
        assert.ok(presentOptional.length > 0, 'Actual platform optional packages must be inspected')
        assert.deepEqual(fs.readFileSync(path.join(source.sourceRoot, 'package.json')), packageBytes)
        assert.deepEqual(fs.readFileSync(path.join(source.sourceRoot, 'package-lock.json')), lockBytes)
        t.diagnostic(JSON.stringify({ dependencyDigest: dependencies.dependencyDigest, inventoryEntries: dependencies.entries.length, presentOptional, absentOptional, lifecycleScripts: 'disabled', nativeHostProven: false }))
        installed = true
      })
      if (!installed) return
      await t.test('WHAT[verification-system-016] isolated installed Fable Acorn and Tar consumers complete before all candidate resources are released', async () => {
        const home = path.join(root, 'consumer-home')
        fs.mkdirSync(home)
        const result = JSON.parse(await consume(path.join(tools.toolRoot, tools.node.path), dependencies.dependencyRoot, home, t.signal))
        assert.deepEqual(result, { fable: true, acorn: true, tar: true, nodeVersion: tools.node.version })
        tools.revalidate()
        dependencies.dispose()
        tools.dispose()
        source.dispose()
        assert.deepEqual(fs.readdirSync(parentDirectory), [])
        t.diagnostic(JSON.stringify({ consumers: result, candidateRootsRemaining: 0, nativeHostProven: false }))
      })
    } finally {
      const errors = []
      for (const candidate of [dependencies, tools, source]) {
        try {
          candidate?.dispose()
        } catch (error) {
          errors.push(error)
        }
      }
      if (parentDirectory && fs.existsSync(parentDirectory)) {
        try {
          assert.deepEqual(fs.readdirSync(parentDirectory), [])
        } catch (error) {
          errors.push(error)
        }
      }
      try {
        fs.rmSync(allocatedRoot, { recursive: true, force: true })
      } catch (error) {
        errors.push(error)
      }
      if (errors.length) throw new AggregateError(errors, 'Repository npm installation fixture cleanup failed')
    }
}
