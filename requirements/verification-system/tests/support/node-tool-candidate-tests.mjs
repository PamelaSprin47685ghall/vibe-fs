import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import { createRequire } from 'node:module'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { create } from 'tar'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
const prepareTools = async options => (await import('../../../../scripts/lib/verification-node-tools.mjs')).prepareVerificationNodeTools(options)

function selectedNpmRoot() {
  const candidates = process.env.WXS_VERIFICATION_NPM_CLI
    ? [process.env.WXS_VERIFICATION_NPM_CLI]
    : [path.join(path.dirname(process.execPath), 'npm'), ...String(process.env.PATH ?? '').split(path.delimiter).map(directory => path.join(directory, 'npm'))]
  for (const candidate of candidates) {
    let cli
    try {
      cli = fs.realpathSync(candidate)
    } catch (error) {
      if (error.code === 'ENOENT') continue
      throw error
    }
    if (path.basename(cli) !== 'npm-cli.js') continue
    const root = path.resolve(cli, '../..')
    assert.equal(JSON.parse(fs.readFileSync(path.join(root, 'package.json'))).name, 'npm')
    return root
  }
  throw new Error('The selected bundle fixture requires a complete available npm package')
}

async function archiveTools(root) {
  const archivePath = path.join(root, 'tools.tar')
  const chunks = []
  for await (const chunk of create({ cwd: path.join(root, 'selected'), portable: true, noMtime: true }, ['toolchain'])) {
    chunks.push(chunk)
  }
  const bytes = Buffer.concat(chunks)
  fs.writeFileSync(archivePath, bytes)
  return { archivePath, archiveSha256: sha256(bytes), parentDirectory: path.join(root, 'candidates') }
}

export function registerNodeToolCandidateTests() {
  test('WHAT[verification-system-016] Node and npm role admission rejects invalid structure before archive reads or root allocation', async t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-tool-role-admission-'))
    const parentDirectory = path.join(root, 'unallocated-parent')
    const options = { archivePath: path.join(root, 'missing-tools.tar'), archiveSha256: '0'.repeat(64), parentDirectory }
    try {
      for (const [name, roles] of [
        ['outside Node path', { nodePath: '../node' }],
        ['Node traversal alias', { nodePath: 'toolchain/node/bin/../bin/node' }],
        ['non-string Node role', { nodePath: null }],
        ['absolute npm path', { npmCliPath: '/foreign/npm-cli.js' }],
        ['npm role outside its package', { npmCliPath: 'toolchain/node/bin/node' }],
        ['npm traversal alias', { npmCliPath: 'toolchain/npm/bin/../../foreign/npm-cli.js' }],
        ['empty npm role', { npmCliPath: '' }],
      ]) {
        await t.test(`WHAT[verification-system-016] ${name} fails with the typed role error before reading a missing archive`, async () => {
          await assert.rejects(prepareTools({ ...options, ...roles }), error => error.code === 'verification-tool-entry-invalid')
          assert.equal(fs.existsSync(parentDirectory), false, 'Structural role rejection cannot allocate the supplied parent or owned roots')
          assert.deepEqual(fs.readdirSync(root), [])
        })
      }
      for (const reason of [new Error('controlled cancelled role admission'), null]) {
        await t.test(`WHAT[verification-system-016] already cancelled role admission preserves ${reason === null ? 'null' : 'Error'} before role validation`, async () => {
          const controller = new AbortController()
          controller.abort(reason)
          const [outcome] = await Promise.allSettled([prepareTools({ ...options, nodePath: '../node', npmCliPath: '/foreign/npm-cli.js', signal: controller.signal })])
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason, reason)
          assert.equal(fs.existsSync(parentDirectory), false)
          assert.deepEqual(fs.readdirSync(root), [])
        })
      }
    } finally {
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
  test('WHAT[verification-system-016] selected Node and complete npm bundle preparation binds actual probes without borrowing ambient configuration', async t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-node-tools-fixture-'))
    let options
    let candidate
    let requiredModuleBytes
    let originalCliBytes
    try {
      await t.test('WHAT[verification-system-016] the formal fixture captures an explicit complete npm package and actual selected Node binary', async () => {
        const selected = path.join(root, 'selected/toolchain')
        fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
        fs.mkdirSync(path.join(root, 'candidates'))
        fs.copyFileSync(process.execPath, path.join(selected, 'node/bin/node'))
        fs.chmodSync(path.join(selected, 'node/bin/node'), 0o755)
        fs.cpSync(selectedNpmRoot(), path.join(selected, 'npm'), { recursive: true, verbatimSymlinks: true })
        options = await archiveTools(root)
        assert.ok(fs.statSync(options.archivePath).size > 0)
        assert.equal(fs.statSync(path.join(selected, 'npm/lib/cli.js')).isFile(), true)
        requiredModuleBytes = fs.readFileSync(path.join(selected, 'npm/lib/cli.js'))
        originalCliBytes = fs.readFileSync(path.join(selected, 'npm/bin/npm-cli.js'))
      })
      await t.test('WHAT[verification-system-016] wrong selected tool archive digest publishes no candidate', async () => {
        await assert.rejects(prepareTools({ ...options, archiveSha256: '0'.repeat(64) }), error => error.code === 'verification-tool-integrity-invalid')
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
      })
      await t.test('WHAT[verification-system-016] tool role paths reject aliases and npm entrypoints outside the selected npm package', async () => {
        for (const roles of [{ nodePath: 'toolchain/node/bin/../bin/node' }, { npmCliPath: 'toolchain/node/bin/node' }, { npmCliPath: 'toolchain/npm/bin/missing.js' }]) {
          await assert.rejects(prepareTools({ ...options, ...roles }), error => error.code === 'verification-tool-entry-invalid')
          assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
        }
      })
      await t.test('WHAT[verification-system-016] actual bundled Node and npm probes ignore hostile ambient HOME launchers and npm configuration', async () => {
        const home = path.join(root, 'foreign-home')
        fs.mkdirSync(home)
        const preload = path.join(home, 'poison.cjs')
        const npmrc = path.join(home, '.npmrc')
        fs.writeFileSync(preload, 'throw new Error("ambient launcher inherited")\n')
        fs.writeFileSync(npmrc, 'registry=http://127.0.0.1:1/poison\n')
        const poison = { HOME: home, NODE_OPTIONS: `--require=${preload}`, NODE_PATH: home, PATH: home, npm_config_userconfig: npmrc, NPM_CONFIG_GLOBALCONFIG: npmrc }
        const before = new Map(Object.keys(poison).map(name => [name, process.env[name]]))
        try {
          Object.assign(process.env, poison)
          candidate = await prepareTools(options)
        } finally {
          for (const [name, value] of before) {
            if (value === undefined) delete process.env[name]
            else process.env[name] = value
          }
        }
        assert.deepEqual(candidate.node, { path: 'toolchain/node/bin/node', sha256: sha256(fs.readFileSync(process.execPath)), version: process.version, platform: process.platform, arch: process.arch })
        const manifest = fs.readFileSync(path.join(root, 'selected/toolchain/npm/package.json'))
        assert.deepEqual(candidate.npm, { cliPath: 'toolchain/npm/bin/npm-cli.js', cliSha256: sha256(fs.readFileSync(path.join(root, 'selected/toolchain/npm/bin/npm-cli.js'))), manifestSha256: sha256(manifest), version: JSON.parse(manifest).version })
        assert.equal(candidate.archiveSha256, options.archiveSha256)
        assert.equal(candidate.entriesDigest, sha256(JSON.stringify(candidate.entries)))
        assert.equal(candidate.identityScope, 'selected-node-npm-bundle')
        assert.equal(candidate.toolDigest, sha256(JSON.stringify({ archiveSha256: candidate.archiveSha256, entriesDigest: candidate.entriesDigest, node: candidate.node, npm: candidate.npm, identityScope: candidate.identityScope })))
        assert.ok(candidate.entries.some(entry => entry.path === 'toolchain/npm/lib/cli.js' && entry.type === 'File'))
        assert.ok(candidate.entries.some(entry => entry.path.startsWith('toolchain/npm/node_modules/')))
        assert.deepEqual(fs.readdirSync(home).sort(), ['.npmrc', 'poison.cjs'])
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [path.basename(candidate.toolRoot)])
        fs.rmSync(options.archivePath)
        const answer = execFileSync(path.join(candidate.toolRoot, candidate.node.path), [path.join(candidate.toolRoot, candidate.npm.cliPath), '--version'], { cwd: candidate.toolRoot, env: { HOME: home }, encoding: 'utf8' }).trim()
        assert.equal(answer, candidate.npm.version)
        candidate.dispose()
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
      })
      await t.test('WHAT[verification-system-016] a missing declared npm library cannot be borrowed from the candidate parent directory by the actual npm CLI', async () => {
        const selectedLibrary = path.join(root, 'selected/toolchain/npm/node_modules/graceful-fs')
        const foreignModules = path.join(options.parentDirectory, 'node_modules')
        const foreignLibrary = path.join(foreignModules, 'graceful-fs')
        fs.mkdirSync(foreignModules)
        fs.renameSync(selectedLibrary, foreignLibrary)
        let borrowed
        try {
          options = await archiveTools(root)
          const [outcome] = await Promise.allSettled([prepareTools(options)])
          if (outcome.status === 'fulfilled') {
            borrowed = outcome.value
            assert.equal(borrowed.npm.version, JSON.parse(fs.readFileSync(path.join(root, 'selected/toolchain/npm/package.json'))).version)
            assert.fail(`Actual npm ${borrowed.npm.version} published while its declared graceful-fs library was available only in the candidate parent`)
          }
          assert.equal(outcome.reason.code, 'verification-tool-entry-invalid')
          assert.deepEqual(fs.readdirSync(options.parentDirectory), ['node_modules'])
          assert.equal(fs.existsSync(path.join(foreignLibrary, 'package.json')), true)
        } finally {
          borrowed?.dispose()
          fs.renameSync(foreignLibrary, selectedLibrary)
          fs.rmSync(foreignModules, { recursive: true, force: true })
        }
      })
      await t.test('WHAT[verification-system-016] complete root npm dependencies cannot conceal a missing declared transitive production library', async () => {
        const npmRoot = fs.realpathSync(path.join(root, 'selected/toolchain/npm'))
        const importerPath = path.join(npmRoot, 'node_modules/make-fetch-happen/package.json')
        const importer = JSON.parse(fs.readFileSync(importerPath))
        const manifest = JSON.parse(fs.readFileSync(path.join(npmRoot, 'package.json')))
        const dependency = Object.keys(importer.dependencies).find(name => !Object.hasOwn(manifest.dependencies, name) && !Object.hasOwn(importer.optionalDependencies ?? {}, name))
        assert.equal(typeof dependency, 'string', 'The actual npm library must declare a required transitive dependency outside root npm dependencies')
        let library = path.dirname(createRequire(importerPath).resolve(dependency))
        while (!fs.existsSync(path.join(library, 'package.json'))) {
          assert.ok(library.startsWith(`${npmRoot}${path.sep}`), 'The actual transitive library must resolve inside the selected npm package')
          library = path.dirname(library)
        }
        assert.ok(library.startsWith(`${npmRoot}${path.sep}`))
        assert.equal(JSON.parse(fs.readFileSync(path.join(library, 'package.json'))).name, dependency)
        const held = path.join(root, 'held-transitive-library')
        assert.equal(Object.hasOwn(manifest.dependencies, dependency), false)
        fs.renameSync(library, held)
        let incomplete
        try {
          options = await archiveTools(root)
          const [outcome] = await Promise.allSettled([prepareTools(options)])
          if (outcome.status === 'fulfilled') incomplete = outcome.value
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason.code, 'verification-tool-entry-invalid')
          assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
        } finally {
          incomplete?.dispose()
          fs.renameSync(held, library)
        }
      })
      await t.test('WHAT[verification-system-016] a valid selected archive missing an npm required module fails the actual npm probe and reclaims its private resources', async () => {
        fs.rmSync(path.join(root, 'selected/toolchain/npm/lib/cli.js'))
        options = await archiveTools(root)
        await assert.rejects(prepareTools(options), error => error.code === 'verification-tool-probe-failed' && error.stderr.includes('MODULE_NOT_FOUND'))
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
      })
      await t.test('WHAT[verification-system-016] an internal symbolic Node entrypoint cannot stand in for the selected executable file', async () => {
        fs.symlinkSync('node', path.join(root, 'selected/toolchain/node/bin/node-alias'))
        options = await archiveTools(root)
        await assert.rejects(prepareTools({ ...options, nodePath: 'toolchain/node/bin/node-alias' }), error => error.code === 'verification-tool-entry-invalid')
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
      })
      await t.test('WHAT[verification-system-016] a non-executable selected Node file cannot authorize a tool probe', async () => {
        fs.chmodSync(path.join(root, 'selected/toolchain/node/bin/node'), 0o644)
        options = await archiveTools(root)
        await assert.rejects(prepareTools(options), error => error.code === 'verification-tool-entry-invalid')
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
      })
      await t.test('WHAT[verification-system-016] an actual npm CLI that adds an unselected file before reporting its real version cannot publish the original bundle identity', async () => {
        fs.chmodSync(path.join(root, 'selected/toolchain/node/bin/node'), 0o755)
        fs.writeFileSync(path.join(root, 'selected/toolchain/npm/lib/cli.js'), requiredModuleBytes)
        const cli = path.join(root, 'selected/toolchain/npm/bin/npm-cli.js')
        const original = fs.readFileSync(cli, 'utf8')
        const shebangEnd = original.startsWith('#!') ? original.indexOf('\n') + 1 : 0
        const write = "require('node:fs').writeFileSync(require('node:path').join(__dirname, 'probe-added.txt'), 'probe-effect')\n"
        fs.writeFileSync(cli, original.slice(0, shebangEnd) + write + original.slice(shebangEnd))
        options = await archiveTools(root)
        await assert.rejects(prepareTools(options), error => error.code === 'verification-tool-entry-invalid')
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
      })
      for (const reason of [new Error('controlled selected tool cancellation'), null]) {
        await t.test(`WHAT[verification-system-016] a held actual npm probe preserves ${reason === null ? 'null' : 'Error'} cancellation and drains its process group`, { skip: process.platform === 'win32' }, async () => {
          const marker = path.join(root, `started-${reason === null ? 'null' : 'error'}.json`)
          const fallback = `${marker}.fallback`
          const cli = path.join(root, 'selected/toolchain/npm/bin/npm-cli.js')
          const original = originalCliBytes.toString()
          const shebangEnd = original.startsWith('#!') ? original.indexOf('\n') + 1 : 0
          const hold = `
const fixtureFs = require('node:fs')
const fixtureChild = require('node:child_process').spawn(process.execPath, ['-e', ${JSON.stringify(`require('node:fs').watch(${JSON.stringify(root)}, () => {}); process.stdout.write('started\\n')`)}], { stdio: ['ignore', 'pipe', 'inherit'] })
fixtureChild.stdout.once('data', () => {
  fixtureFs.writeFileSync(${JSON.stringify(`${marker}.tmp`)}, JSON.stringify({ pid: process.pid, childPid: fixtureChild.pid }))
  fixtureFs.renameSync(${JSON.stringify(`${marker}.tmp`)}, ${JSON.stringify(marker)})
})
process.on('SIGTERM', () => {
  fixtureFs.writeFileSync(${JSON.stringify(fallback)}, 'fixture cleanup was needed')
  fixtureChild.once('close', () => process.exit(89))
  fixtureChild.kill('SIGKILL')
})
return
`
          fs.writeFileSync(cli, original.slice(0, shebangEnd) + hold + original.slice(shebangEnd))
          options = await archiveTools(root)
          const started = Promise.withResolvers()
          const watcher = fs.watch(root, (event, filename) => {
            if (filename !== path.basename(marker) || !fs.existsSync(marker)) return
            try { started.resolve(JSON.parse(fs.readFileSync(marker, 'utf8'))) } catch (error) { started.reject(error) }
          })
          const controller = new AbortController()
          const preparing = prepareTools({ ...options, signal: controller.signal })
          const settled = Promise.allSettled([preparing])
          let physical
          try {
            physical = await Promise.race([started.promise, preparing.then(() => { throw new Error('Held npm probe unexpectedly published') })])
            assert.ok(Number.isInteger(physical.pid) && Number.isInteger(physical.childPid))
            assert.ok(fs.readdirSync(options.parentDirectory).length > 0)
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
            assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
          } finally {
            watcher.close()
            controller.abort(reason)
            if (physical) {
              try { process.kill(-physical.pid, 'SIGKILL') } catch (error) { if (error.code !== 'ESRCH') throw error }
            }
            await settled
          }
        })
      }
      for (const reason of [new Error('cancelled before selected tool admission'), null]) {
        await t.test(`WHAT[verification-system-016] already aborted tool preparation preserves ${reason === null ? 'null' : 'Error'} before reading a missing archive`, async () => {
          const controller = new AbortController()
          controller.abort(reason)
          const [outcome] = await Promise.allSettled([prepareTools({ ...options, archivePath: path.join(root, 'missing-tools.tar'), signal: controller.signal })])
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason, reason)
          assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
        })
      }
    } finally {
      candidate?.dispose()
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
}
