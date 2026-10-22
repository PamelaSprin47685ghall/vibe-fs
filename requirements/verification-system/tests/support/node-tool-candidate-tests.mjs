import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import { createRequire } from 'node:module'
import os from 'node:os'
import path from 'node:path'
import { Transform } from 'node:stream'
import { pipeline } from 'node:stream/promises'
import test from 'node:test'
import { create } from 'tar'
import { fixturePhase } from './fixture-phase.mjs'
import { withFinitePhysicalWait } from './finite-physical-wait.mjs'
import { firstMissingNpmProductionLibrary } from '../../../../scripts/lib/verification-node-tools.mjs'

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex')
const prepareTools = async options => {
  fixturePhase('node-tools:prepare-enter', { archivePath: options.archivePath })
  try {
    return await (await import('../../../../scripts/lib/verification-node-tools.mjs')).prepareVerificationNodeTools(options)
  } finally {
    fixturePhase('node-tools:prepare-settled', { archivePath: options.archivePath })
  }
}

function selectedNpmRoot() {
  const candidates = process.env.WXS_VERIFICATION_NPM_CLI
    ? [process.env.WXS_VERIFICATION_NPM_CLI]
    : [path.join(path.dirname(process.execPath), 'npm'), ...String(process.env.PATH ?? '').split(path.delimiter).map(directory => path.join(directory, 'npm'))]
  let lastFailure
  for (const candidate of candidates) {
    let cli
    try {
      cli = fs.realpathSync(candidate)
    } catch (error) {
      if (error.code === 'ENOENT') continue
      throw error
    }
    if (path.basename(cli) !== 'npm-cli.js') {
      lastFailure = `Candidate ${candidate} realpath basename is not npm-cli.js: ${cli}`
      continue
    }
    const root = path.resolve(cli, '../..')
    let manifest
    try {
      manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
    } catch (error) {
      lastFailure = `Candidate ${candidate} package manifest error: ${error.message}`
      continue
    }
    if (manifest?.name !== 'npm') {
      lastFailure = `Candidate ${candidate} package manifest name is not npm`
      continue
    }
    const missing = firstMissingNpmProductionLibrary(root)
    if (missing) {
      lastFailure = missing.message
      continue
    }
    return root
  }
  throw new Error('The selected bundle fixture requires a complete available npm package' + (lastFailure ? `: ${lastFailure}` : ''))
}

async function archiveTools(root) {
  const archivePath = path.join(root, 'tools.tar')
  fixturePhase('node-tools:archive-enter', { archivePath })
  const digest = createHash('sha256')
  let bytes = 0
  await pipeline(
    create({ cwd: path.join(root, 'selected'), portable: true, noMtime: true }, ['toolchain']),
    new Transform({
      transform(chunk, _encoding, callback) {
        digest.update(chunk)
        bytes += chunk.length
        callback(null, chunk)
      },
    }),
    fs.createWriteStream(archivePath),
  )
  fixturePhase('node-tools:archive-written', { archivePath, bytes })
  return { archivePath, archiveSha256: digest.digest('hex'), parentDirectory: path.join(root, 'candidates') }
}

function copyActualToolchain(root) {
  const selected = path.join(root, 'selected/toolchain')
  fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
  fs.mkdirSync(path.join(root, 'candidates'))
  fs.copyFileSync(process.execPath, path.join(selected, 'node/bin/node'))
  fs.chmodSync(path.join(selected, 'node/bin/node'), 0o755)
  fs.cpSync(selectedNpmRoot(), path.join(selected, 'npm'), { recursive: true, verbatimSymlinks: true })
  return selected
}

function physicalProbeScope(context) {
  return (operation, run) => {
    const deadline = Date.now() + 5000
    return withFinitePhysicalWait(context, { operation, limitMs: deadline - Date.now() }, () => run({ deadline }))
  }
}

export function registerNodeToolCandidateTests() {
  for (const ending of ['return', 'throw']) {
    test(`WHAT[verification-system-016] a physical probe scope that ends by ${ending} cannot supply results or start a delayed operation`, async () => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'node-probe-ended-scope-'))
      try {
        const selected = path.join(root, 'selected/toolchain')
        fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
        fs.mkdirSync(path.join(selected, 'npm/bin'), { recursive: true })
        fs.mkdirSync(path.join(root, 'candidates'))
        fs.writeFileSync(path.join(selected, 'npm/package.json'), JSON.stringify({ name: 'npm', version: '1.0.0' }))
        fs.writeFileSync(path.join(selected, 'npm/bin/npm-cli.js'), '')
        const calls = path.join(root, 'calls')
        fs.writeFileSync(path.join(selected, 'node/bin/node'), `#!${process.execPath}
require('node:fs').writeFileSync(${JSON.stringify(calls)}, 'executed')
`, { mode: 0o755 })
        let delayedRun
        const scopeFailure = new Error('controlled scope failure before dispatch')
        await assert.rejects(prepareTools({ ...await archiveTools(root), probeScope(_operation, run) {
          delayedRun = run
          if (ending === 'throw') throw scopeFailure
          return { version: process.version, npmVersion: '1.0.0' }
        } }), error => ending === 'throw' ? error === scopeFailure : error.code === 'verification-tool-entry-invalid' && /await its actual operation/.test(error.message))
        assert.equal(typeof delayedRun, 'function')
        assert.throws(() => delayedRun({ deadline: Date.now() + 5000 }), error => error.code === 'verification-tool-entry-invalid' && /scope must start its operation exactly once/.test(error.message))
        assert.equal(fs.existsSync(calls), false, 'Neither an invented result nor a retained run capability can start the selected executable')
        assert.deepEqual(fs.readdirSync(path.join(root, 'candidates')), [])
      } finally {
        fs.rmSync(root, { recursive: true, force: true })
      }
    })
  }
  for (const ending of ['return', 'throw', 'repeat']) {
    test(`WHAT[verification-system-016] a physical probe scope cannot ${ending} while leaving its actual operation undrained`, async () => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'node-probe-held-scope-'))
      let watcher
      let physical
      try {
        const selected = path.join(root, 'selected/toolchain')
        fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
        fs.mkdirSync(path.join(selected, 'npm/bin'), { recursive: true })
        fs.mkdirSync(path.join(root, 'candidates'))
        fs.writeFileSync(path.join(selected, 'npm/package.json'), JSON.stringify({ name: 'npm', version: '1.0.0' }))
        fs.writeFileSync(path.join(selected, 'npm/bin/npm-cli.js'), '')
        const marker = path.join(root, 'started.json')
        fs.writeFileSync(path.join(selected, 'node/bin/node'), `#!${process.execPath}
const fs = require('node:fs')
fs.watch(${JSON.stringify(root)}, () => {})
fs.writeFileSync(${JSON.stringify(`${marker}.tmp`)}, JSON.stringify({pid:process.pid}))
fs.renameSync(${JSON.stringify(`${marker}.tmp`)}, ${JSON.stringify(marker)})
`, { mode: 0o755 })
        const options = await archiveTools(root)
        const started = Promise.withResolvers()
        watcher = fs.watch(root, (_event, filename) => {
          if (filename !== path.basename(marker) || !fs.existsSync(marker)) return
          try { started.resolve(JSON.parse(fs.readFileSync(marker, 'utf8'))) } catch (error) { started.reject(error) }
        })
        const scopeFailure = new Error('controlled scope failure with a live tool')
        await assert.rejects(prepareTools({ ...options, async probeScope(_operation, run) {
          const deadline = Date.now() + 5000
          const running = run({ deadline })
          physical = await Promise.race([started.promise, running.then(() => { throw new Error('Held runtime probe unexpectedly completed') })])
          if (ending === 'throw') throw scopeFailure
          if (ending === 'repeat') await run({ deadline })
        } }), error => ending === 'throw' ? error === scopeFailure : error.code === 'verification-tool-entry-invalid')
        assert.ok(Number.isInteger(physical.pid))
        assert.throws(() => process.kill(physical.pid, 0), error => error.code === 'ESRCH')
        assert.deepEqual(fs.readdirSync(options.parentDirectory), [], 'Scope rejection waits for the original owner before reclaiming its roots')
      } finally {
        watcher?.close()
        try {
          if (physical) {
            try { process.kill(physical.pid, 'SIGKILL') } catch (error) { if (error.code !== 'ESRCH') throw error }
          }
        } finally {
          fs.rmSync(root, { recursive: true, force: true })
        }
      }
    })
  }
  for (const replaced of ['archive', 'probe']) {
    test(`WHAT[verification-system-016] replacing the ${replaced} root while physical probe admission waits cannot execute a replacement`, async () => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'node-probe-scope-namespace-'))
      try {
        const selected = path.join(root, 'selected/toolchain')
        fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
        fs.mkdirSync(path.join(selected, 'npm/bin'), { recursive: true })
        const parentDirectory = path.join(root, 'candidates')
        fs.mkdirSync(parentDirectory)
        fs.writeFileSync(path.join(selected, 'npm/package.json'), JSON.stringify({ name: 'npm', version: '1.0.0' }))
        fs.writeFileSync(path.join(selected, 'npm/bin/npm-cli.js'), '')
        const calls = path.join(root, 'calls')
        fs.writeFileSync(path.join(selected, 'node/bin/node'), `#!${process.execPath}
require('node:fs').writeFileSync(${JSON.stringify(calls)}, 'executed')
`, { mode: 0o755 })
        const options = await archiveTools(root)
        const [outcome] = await Promise.allSettled([prepareTools({ ...options, async probeScope(_operation, run) {
          const allocated = fs.readdirSync(parentDirectory).map(name => path.join(parentDirectory, name))
          const owned = allocated.find(directory => replaced === 'archive' ? fs.existsSync(path.join(directory, 'toolchain')) : fs.existsSync(path.join(directory, 'home')))
          assert.equal(typeof owned, 'string')
          await Promise.resolve()
          const parked = path.join(root, 'parked')
          fs.renameSync(owned, parked)
          fs.cpSync(parked, owned, { recursive: true })
          fs.writeFileSync(path.join(owned, 'foreign-marker'), 'replacement')
          await run({ deadline: Date.now() + 5000 })
        } })])
        assert.equal(outcome.status, 'rejected')
        assert.ok(outcome.reason instanceof AggregateError)
        assert.equal(outcome.reason.cause.code, 'verification-tool-entry-invalid')
        assert.equal(fs.existsSync(calls), false, 'Admission revalidates actual owners before the first selected executable')
        const remaining = fs.readdirSync(parentDirectory)
        assert.equal(remaining.length, 1)
        assert.equal(fs.readFileSync(path.join(parentDirectory, remaining[0], 'foreign-marker'), 'utf8'), 'replacement')
      } finally {
        fs.rmSync(root, { recursive: true, force: true })
      }
    })
  }
  test('WHAT[verification-system-016] actual bundled Node and npm probes ignore hostile ambient HOME launchers and npm configuration', async t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-node-tools-hostile-'))
    let candidate
    try {
      const selected = copyActualToolchain(root)
      const options = await archiveTools(root)
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
        candidate = await prepareTools({ ...options, signal: t.signal, probeScope: physicalProbeScope(t) })
      } finally {
        for (const [name, value] of before) {
          if (value === undefined) delete process.env[name]
          else process.env[name] = value
        }
      }
      assert.deepEqual(candidate.node, { path: 'toolchain/node/bin/node', sha256: sha256(fs.readFileSync(process.execPath)), version: process.version, platform: process.platform, arch: process.arch })
      const manifest = fs.readFileSync(path.join(selected, 'npm/package.json'))
      assert.deepEqual(candidate.npm, { cliPath: 'toolchain/npm/bin/npm-cli.js', cliSha256: sha256(fs.readFileSync(path.join(selected, 'npm/bin/npm-cli.js'))), manifestSha256: sha256(manifest), version: JSON.parse(manifest).version })
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
    } finally {
      candidate?.dispose()
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
  test('WHAT[verification-system-016] a valid selected archive missing an npm required module fails the actual npm probe and reclaims its private resources', async t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-node-tools-missing-module-'))
    try {
      const selected = copyActualToolchain(root)
      fs.rmSync(path.join(selected, 'npm/lib/cli.js'))
      const options = await archiveTools(root)
      await assert.rejects(prepareTools({ ...options, signal: t.signal, probeScope: physicalProbeScope(t) }), error => error.code === 'verification-tool-probe-failed' && error.stderr.includes('MODULE_NOT_FOUND'))
      assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
    } finally {
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
  test('WHAT[verification-system-016] an actual npm CLI that adds an unselected file before reporting its real version cannot publish the original bundle identity', async t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-node-tools-unselected-file-'))
    try {
      const selected = copyActualToolchain(root)
      const cli = path.join(selected, 'npm/bin/npm-cli.js')
      const original = fs.readFileSync(cli, 'utf8')
      const shebangEnd = original.startsWith('#!') ? original.indexOf('\n') + 1 : 0
      const write = "require('node:fs').writeFileSync(require('node:path').join(__dirname, 'probe-added.txt'), 'probe-effect')\n"
      fs.writeFileSync(cli, original.slice(0, shebangEnd) + write + original.slice(shebangEnd))
      const options = await archiveTools(root)
      await assert.rejects(prepareTools({ ...options, signal: t.signal, probeScope: physicalProbeScope(t) }), error => error.code === 'verification-tool-entry-invalid')
      assert.deepEqual(fs.readdirSync(options.parentDirectory), [])
    } finally {
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
  for (const reason of [new Error('controlled selected tool cancellation'), null]) {
    test(`WHAT[verification-system-016] a held actual npm probe preserves ${reason === null ? 'null' : 'Error'} cancellation and drains its process group`, { skip: process.platform === 'win32' }, async t => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'verification-node-tools-cancelled-'))
      let watcher
      let controller
      let settled
      let physical
      try {
        const selected = copyActualToolchain(root)
        const marker = path.join(root, `started-${reason === null ? 'null' : 'error'}.json`)
        const fallback = `${marker}.fallback`
        const cli = path.join(selected, 'npm/bin/npm-cli.js')
        const original = fs.readFileSync(cli, 'utf8')
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
        const options = await archiveTools(root)
        const started = Promise.withResolvers()
        watcher = fs.watch(root, (_event, filename) => {
          if (filename !== path.basename(marker) || !fs.existsSync(marker)) return
          try { started.resolve(JSON.parse(fs.readFileSync(marker, 'utf8'))) } catch (error) { started.reject(error) }
        })
        controller = new AbortController()
        const preparing = prepareTools({ ...options, signal: AbortSignal.any([t.signal, controller.signal]), probeScope: physicalProbeScope(t) })
        settled = Promise.allSettled([preparing])
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
        watcher?.close()
        controller?.abort(reason)
        try {
          if (physical) {
            try { process.kill(-physical.pid, 'SIGKILL') } catch (error) { if (error.code !== 'ESRCH') throw error }
          }
        } finally {
          await settled
          fs.rmSync(root, { recursive: true, force: true })
        }
      }
    })
  }
  for (const replaced of ['archive', 'probe']) {
    test(`WHAT[verification-system-016] successful Node probe replacing its ${replaced} root cannot start another selected executable`, async () => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'node-probe-success-namespace-'))
      try {
        const selected = path.join(root, 'selected/toolchain')
        fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
        fs.mkdirSync(path.join(selected, 'npm/bin'), { recursive: true })
        fs.mkdirSync(path.join(root, 'candidates'))
        fs.writeFileSync(path.join(selected, 'npm/package.json'), JSON.stringify({ name: 'npm', version: '1.0.0' }))
        fs.writeFileSync(path.join(selected, 'npm/bin/npm-cli.js'), '')
        const calls = path.join(root, 'calls')
        fs.writeFileSync(path.join(selected, 'node/bin/node'), `#!${process.execPath}
const fs = require('node:fs')
const path = require('node:path')
fs.appendFileSync(${JSON.stringify(calls)}, JSON.stringify(process.argv.slice(2)) + '\\n')
if (process.argv[2] === '--input-type=module') {
  const owned = ${replaced === 'archive' ? "path.resolve(__dirname, '../../..')" : 'path.dirname(process.env.HOME)'}
  fs.renameSync(owned, ${JSON.stringify(path.join(root, 'parked'))})
  fs.cpSync(${JSON.stringify(path.join(root, 'parked'))}, owned, { recursive: true })
  fs.writeFileSync(path.join(owned, 'foreign-marker'), 'replacement')
  console.log(JSON.stringify({version:'v22.0.0',platform:process.platform,arch:process.arch}))
} else { console.log('1.0.0') }
`, { mode: 0o755 })
        const [outcome] = await Promise.allSettled([prepareTools(await archiveTools(root))])
        assert.equal(outcome.status, 'rejected')
        const probes = fs.readFileSync(calls, 'utf8').trim().split('\n').map(line => JSON.parse(line))
        assert.equal(probes.length, 1, 'changed runtime ownership stops before the next physical npm probe')
        assert.deepEqual(probes[0].slice(0, 2), ['--input-type=module', '-e'])
        assert.ok(outcome.reason instanceof AggregateError)
        assert.equal(outcome.reason.cause.code, 'verification-tool-entry-invalid', JSON.stringify(outcome.reason.cause))
        const remaining = fs.readdirSync(path.join(root, 'candidates'))
        assert.equal(remaining.length, 1)
        assert.equal(fs.readFileSync(path.join(root, 'candidates', remaining[0], 'foreign-marker'), 'utf8'), 'replacement')
      } finally {
        fs.rmSync(root, { recursive: true, force: true })
      }
    })
  }
  for (const runtimeVersion of ['v22.0.0', '22.0.0']) {
    test(`WHAT[verification-system-016] controlled runtime probe protocol ${runtimeVersion.startsWith('v') ? 'alone supplies Node version platform and architecture before npm admission' : 'rejects an invalid Node version before npm admission'}`, async () => {
      const root = fs.mkdtempSync(path.join(os.tmpdir(), 'node-runtime-identity-'))
      let candidate
      try {
        const selected = path.join(root, 'selected/toolchain')
        fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
        fs.mkdirSync(path.join(selected, 'npm/bin'), { recursive: true })
        fs.mkdirSync(path.join(root, 'candidates'))
        fs.writeFileSync(path.join(selected, 'npm/package.json'), JSON.stringify({ name: 'npm', version: '1.0.0' }))
        fs.writeFileSync(path.join(selected, 'npm/bin/npm-cli.js'), '')
        const calls = path.join(root, 'calls')
        fs.writeFileSync(path.join(selected, 'node/bin/node'), `#!${process.execPath}
const fs = require('node:fs')
fs.appendFileSync(${JSON.stringify(calls)}, JSON.stringify(process.argv.slice(2)) + '\\n')
if (process.argv[2] === '--input-type=module') console.log(JSON.stringify({version:${JSON.stringify(runtimeVersion)},platform:process.platform,arch:process.arch}))
else if (process.argv[2].endsWith('/npm-cli.js')) console.log('1.0.0')
else process.exit(74)
`, { mode: 0o755 })
        const [outcome] = await Promise.allSettled([prepareTools(await archiveTools(root))])
        if (outcome.status === 'fulfilled') candidate = outcome.value
        const probes = fs.readFileSync(calls, 'utf8').trim().split('\n').map(line => JSON.parse(line))
        assert.deepEqual(probes[0].slice(0, 2), ['--input-type=module', '-e'])
        if (runtimeVersion.startsWith('v')) {
          assert.equal(outcome.status, 'fulfilled')
          assert.deepEqual({version:candidate.node.version,platform:candidate.node.platform,arch:candidate.node.arch}, {version:runtimeVersion,platform:process.platform,arch:process.arch})
          assert.equal(candidate.npm.version, '1.0.0')
          assert.deepEqual(probes.slice(1), [[path.join(candidate.toolRoot, 'toolchain/npm/bin/npm-cli.js'), '--version']])
          candidate.dispose()
        } else {
          assert.equal(outcome.status, 'rejected')
          assert.equal(outcome.reason.code, 'verification-tool-entry-invalid')
          assert.equal(probes.length, 1, 'invalid runtime identity cannot authorize the npm probe')
        }
        assert.deepEqual(fs.readdirSync(path.join(root, 'candidates')), [])
      } finally {
        candidate?.dispose()
        fs.rmSync(root, { recursive: true, force: true })
      }
    })
  }
  test('WHAT[verification-system-016] failed Node probe preserves a copied replacement of its private probe root', async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'node-probe-namespace-'))
    try {
      const selected = path.join(root, 'selected/toolchain')
      fs.mkdirSync(path.join(selected, 'node/bin'), { recursive: true })
      fs.mkdirSync(path.join(selected, 'npm/bin'), { recursive: true })
      fs.mkdirSync(path.join(root, 'candidates'))
      fs.writeFileSync(path.join(selected, 'npm/package.json'), JSON.stringify({ name: 'npm', version: '1.0.0' }))
      fs.writeFileSync(path.join(selected, 'npm/bin/npm-cli.js'), '')
      fs.writeFileSync(path.join(selected, 'node/bin/node'), `#!${process.execPath}
const fs = require('node:fs')
const path = require('node:path')
const root = path.dirname(process.env.HOME)
fs.renameSync(root, ${JSON.stringify(path.join(root, 'parked-probe'))})
fs.cpSync(${JSON.stringify(path.join(root, 'parked-probe'))}, root, {recursive:true})
fs.writeFileSync(path.join(root, 'foreign-marker'), 'replacement')
process.exit(73)
`, { mode: 0o755 })
      const [outcome] = await Promise.allSettled([prepareTools(await archiveTools(root))])
      assert.equal(outcome.status, 'rejected')
      assert.ok(outcome.reason instanceof AggregateError)
      assert.equal(outcome.reason.cause.exitCode, 73)
      assert.ok(outcome.reason.errors.some(error => error.code === 'verification-tool-entry-invalid'))
      const remaining = fs.readdirSync(path.join(root, 'candidates'))
      assert.equal(remaining.length, 1)
      assert.equal(fs.readFileSync(path.join(root, 'candidates', remaining[0], 'foreign-marker'), 'utf8'), 'replacement')
    } finally {
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
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
    try {
      await t.test('WHAT[verification-system-016] the formal fixture captures an explicit complete npm package and actual selected Node binary', async () => {
        const selected = copyActualToolchain(root)
        options = await archiveTools(root)
        assert.ok(fs.statSync(options.archivePath).size > 0)
        assert.equal(fs.statSync(path.join(selected, 'npm/lib/cli.js')).isFile(), true)
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
      fs.rmSync(root, { recursive: true, force: true })
    }
  })
}
