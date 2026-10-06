import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { allocateVerificationDirectory } from '../../../../scripts/lib/verification-directory-owner.mjs'
import { assertReadonlyVerificationInputs, consumeReadonlyVerificationInputs, withReadonlyVerificationInputs } from '../../../../scripts/lib/verification-readonly-inputs.mjs'
import { runVerificationToolProbe } from '../../../../scripts/lib/verification-tool-probe.mjs'
import { integrationTest } from './tier-gate.mjs'

async function withDirectories(action) {
  const root = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'verification-readonly-test-')))
  const inputs = path.join(root, 'inputs')
  const output = path.join(root, 'output')
  fs.mkdirSync(inputs)
  fs.mkdirSync(output)
  const directories = ['source', 'sdk', 'tools', 'project'].map(name => allocateVerificationDirectory(inputs, `${name}-`, message => new Error(message)))
  directories.forEach(directory => fs.writeFileSync(path.join(directory.root, 'member'), directory.root))
  const owners = directories.map(directory => ({ revalidate: directory.assertOwned }))
  const [source, sdk, tools, project] = owners
  source.sourceRoot = directories[0].root
  sdk.toolRoot = directories[1].root
  tools.toolRoot = directories[2].root
  project.projectRoot = directories[3].root
  const options = { source, sdk, tools, project, parentDirectory: root }
  let restoreProven = false
  try {
    await action({ root, inputs, output, directories, owners, options })
    restoreProven = true
  } finally {
    if (restoreProven) {
      directories.forEach(directory => directory.dispose())
      fs.rmSync(root, { recursive: true })
    }
  }
}

function snapshotReadonlyFixtureInputs(directories) {
  return directories.map(directory => {
    const root = fs.lstatSync(directory.root, { bigint: true })
    const member = fs.lstatSync(path.join(directory.root, 'member'), { bigint: true })
    assert.equal(root.isDirectory(), true)
    assert.equal(member.isFile(), true)
    assert.deepEqual(fs.readdirSync(directory.root).sort(), ['member'])
    return {
      root: { dev: root.dev, ino: root.ino, mode: root.mode },
      member: {
        dev: member.dev, ino: member.ino, mode: member.mode, size: member.size,
        bytes: fs.readFileSync(path.join(directory.root, 'member')),
      },
    }
  })
}

function runReadonlyFixtureCommand(file, args, signal, input) {
  signal?.throwIfAborted()
  return new Promise((resolve, reject) => {
    const child = execFile(file, args, { encoding: 'utf8', timeout: 30000, maxBuffer: 2097152, signal }, (error, stdout, stderr) => {
      if (signal?.aborted) reject(signal.reason)
      else if (error) reject(Object.assign(error, { stdout, stderr }))
      else resolve(stdout)
    })
    child.stdin.on('error', reject)
    child.stdin.end(input)
  })
}

async function readonlyFixtureImages() {
  const xml = await runReadonlyFixtureCommand('/usr/bin/hdiutil', ['info', '-plist'])
  const decoded = await runReadonlyFixtureCommand('/usr/bin/plutil', ['-convert', 'json', '-o', '-', '-'], undefined, xml)
  return JSON.parse(decoded).images ?? []
}

export function registerReadonlyInputTests() {
  const platform = { skip: process.platform !== 'darwin' }
  integrationTest('WHAT[verification-system-016] a real readonly mount protects original role paths and rejects foreign or expired view capabilities', platform, async () => {
    await withDirectories(async ({ root, inputs, output, directories, owners, options }) => {
      const original = directories.map(directory => fs.readFileSync(path.join(directory.root, 'member')))
      for (const directory of directories) fs.writeFileSync(path.join(directory.root, 'member'), directory.root)
      let capability
      await withReadonlyVerificationInputs(options, async view => {
        capability = view
        assertReadonlyVerificationInputs(view, owners, output)
        for (let index = 0; index < owners.length; index++) {
          assert.throws(() => assertReadonlyVerificationInputs(view, owners.map((owner, other) => index === other ? { ...owner } : owner), output), /exact prepared owners/)
        }
        assert.throws(() => assertReadonlyVerificationInputs(view, owners, inputs), /outside readonly inputs/)
        for (const [index, directory] of directories.entries()) {
          assert.deepEqual(fs.readFileSync(path.join(directory.root, 'member')), original[index])
          assert.throws(() => directory.assertOwned(), /owned directory identity/)
          assert.throws(() => fs.writeFileSync(path.join(directory.root, 'member'), 'changed'), { code: 'EROFS' })
          assert.throws(() => fs.writeFileSync(path.join(directory.root, 'added'), 'added'), { code: 'EROFS' })
          assert.throws(() => fs.unlinkSync(path.join(directory.root, 'member')), { code: 'EROFS' })
        }
      })
      assert.throws(() => assertReadonlyVerificationInputs(capability, owners, output), /not active/)
      directories.forEach((directory, index) => {
        directory.assertOwned()
        assert.deepEqual(fs.readFileSync(path.join(directory.root, 'member')), original[index])
      })
      assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
    })
  })
  integrationTest('WHAT[verification-system-016] a paused actual Node consumer keeps four readonly namespaces intact through valid write add delete rename and new symlink attempts', platform, async t => {
    await withDirectories(async ({ root, output, directories, options }) => {
      const original = snapshotReadonlyFixtureInputs(directories)
      const attempts = [
        {
          name: 'write',
          run: directory => fs.writeFileSync(path.join(directory, 'member'), 'replacement'),
          check: directory => assert.equal(fs.readFileSync(path.join(directory, 'member'), 'utf8'), 'replacement'),
        },
        {
          name: 'add',
          run: directory => fs.writeFileSync(path.join(directory, 'added'), 'new', { flag: 'wx' }),
          check: directory => assert.equal(fs.readFileSync(path.join(directory, 'added'), 'utf8'), 'new'),
        },
        {
          name: 'delete',
          run: directory => fs.unlinkSync(path.join(directory, 'member')),
          check: directory => assert.equal(fs.existsSync(path.join(directory, 'member')), false),
        },
        {
          name: 'rename',
          run: directory => fs.renameSync(path.join(directory, 'member'), path.join(directory, 'moved')),
          check: directory => {
            assert.equal(fs.existsSync(path.join(directory, 'member')), false)
            assert.equal(fs.readFileSync(path.join(directory, 'moved'), 'utf8'), 'control member')
          },
        },
        {
          name: 'new-symlink',
          run: directory => fs.symlinkSync('member', path.join(directory, 'added-link')),
          check: directory => {
            assert.equal(fs.readlinkSync(path.join(directory, 'added-link')), 'member')
            assert.equal(fs.readFileSync(path.join(directory, 'added-link'), 'utf8'), 'control member')
          },
        },
      ]
      for (const attempt of attempts) {
        const control = path.join(output, `writable-${attempt.name}`)
        fs.mkdirSync(control)
        fs.writeFileSync(path.join(control, 'member'), 'control member')
        attempt.run(control)
        attempt.check(control)
      }
      const ready = path.join(output, 'consumer-ready.json')
      const release = path.join(output, 'consumer-release')
      const result = path.join(output, 'consumer-result.json')
      const expectedReads = original.map(entry => entry.member.bytes.toString('hex'))
      const program = `
import fs from 'node:fs'
import path from 'node:path'
const roles = ${JSON.stringify(directories.map(directory => directory.root))}
const readRoles = () => roles.map(role => fs.readFileSync(path.join(role, 'member')).toString('hex'))
const released = new Promise((resolve, reject) => {
  const check = () => {
    if (fs.existsSync(${JSON.stringify(release)})) {
      watcher.close()
      resolve()
    }
  }
  const watcher = fs.watch(${JSON.stringify(output)}, check)
  watcher.once('error', error => { watcher.close(); reject(error) })
  check()
})
fs.writeFileSync(${JSON.stringify(path.join(output, 'consumer-ready.staging'))}, JSON.stringify({ pid: process.pid, reads: readRoles() }))
fs.renameSync(${JSON.stringify(path.join(output, 'consumer-ready.staging'))}, ${JSON.stringify(ready)})
await released
const observed = { pid: process.pid, reads: readRoles(), legalOutput: 'actual consumer output' }
fs.writeFileSync(${JSON.stringify(result)}, JSON.stringify(observed))
fs.writeSync(1, JSON.stringify(observed))
`
      const refused = []
      let actualReady
      let actualResult
      let ownerRefusedDuringScope = false
      await withReadonlyVerificationInputs({ ...options, signal: t.signal }, async view => {
        let closeReadyWatcher
        const readyFact = new Promise((resolve, reject) => {
          const check = () => { if (fs.existsSync(ready)) resolve() }
          const abort = () => reject(t.signal.reason)
          const watcher = fs.watch(output, check)
          watcher.once('error', reject)
          t.signal.addEventListener('abort', abort, { once: true })
          closeReadyWatcher = () => {
            watcher.close()
            t.signal.removeEventListener('abort', abort)
          }
          check()
          if (t.signal.aborted) abort()
        })
        let consumer
        try {
          consumer = consumeReadonlyVerificationInputs(view, signal => runVerificationToolProbe(
            process.execPath, ['--input-type=module', '-e', program],
            { cwd: root, env: { PATH: path.dirname(process.execPath) }, signal },
          ))
          await Promise.race([readyFact, consumer.then(() => { throw new Error('Actual consumer exited before its ready fact') })])
          actualReady = JSON.parse(fs.readFileSync(ready, 'utf8'))
          for (const [role, directory] of directories.entries()) {
            for (const attempt of attempts) {
              try {
                attempt.run(directory.root)
                refused.push({ role, operation: attempt.name, code: null })
              } catch (error) {
                refused.push({ role, operation: attempt.name, code: error.code })
              }
            }
          }
        } finally {
          closeReadyWatcher()
          fs.writeFileSync(release, 'release')
        }
        actualResult = JSON.parse(await consumer)
        assert.throws(() => directories[0].assertOwned(), /owned directory identity/)
        ownerRefusedDuringScope = true
      })
      directories.forEach(directory => directory.assertOwned())
      assert.deepEqual(snapshotReadonlyFixtureInputs(directories), original)
      assert.equal(ownerRefusedDuringScope, true)
      assert.ok(Number.isSafeInteger(actualReady.pid) && actualReady.pid > 0)
      assert.deepEqual(actualReady.reads, expectedReads)
      assert.deepEqual(actualResult, { pid: actualReady.pid, reads: expectedReads, legalOutput: 'actual consumer output' })
      assert.deepEqual(JSON.parse(fs.readFileSync(result, 'utf8')), actualResult)
      assert.throws(() => process.kill(actualReady.pid, 0), { code: 'ESRCH' })
      assert.deepEqual(refused, directories.flatMap((_, role) => attempts.map(attempt => ({ role, operation: attempt.name, code: 'EROFS' }))))
      assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
      t.diagnostic(JSON.stringify({ actualPid: actualReady.pid, restoredOwners: directories.length, protectedOperations: refused.length, legalOutput: actualResult.legalOutput }))
    })
  })
  integrationTest('WHAT[verification-system-016] an actual same UID nonforce detach makes a successful readonly consumer insufficient for scope success and preserves an independent foreign device', platform, async t => {
    await withDirectories(async ({ root, inputs, output, directories, options }) => {
      const original = snapshotReadonlyFixtureInputs(directories)
      const directoryIdentity = directory => {
        const stat = fs.lstatSync(directory, { bigint: true })
        assert.equal(stat.isDirectory(), true)
        return { dev: String(stat.dev), ino: String(stat.ino), mode: String(stat.mode) }
      }
      const fileIdentity = file => {
        const stat = fs.lstatSync(file, { bigint: true })
        assert.equal(stat.isFile(), true)
        return { dev: String(stat.dev), ino: String(stat.ino), mode: String(stat.mode), size: String(stat.size), sha256: createHash('sha256').update(fs.readFileSync(file)).digest('hex') }
      }
      const originalInputIdentity = directoryIdentity(inputs)
      const foreignOwner = allocateVerificationDirectory(output, 'foreign-readonly-image-', message => new Error(message))
      const foreignSource = path.join(foreignOwner.root, 'source')
      const foreignMount = path.join(foreignOwner.root, 'mounted')
      const foreignCapture = path.join(foreignOwner.root, 'capture.dmg')
      const foreignImage = path.join(foreignOwner.root, 'inputs.dmg')
      fs.mkdirSync(foreignSource)
      fs.mkdirSync(foreignMount)
      fs.writeFileSync(path.join(foreignSource, 'member'), 'independent foreign image bytes')
      fs.writeFileSync(path.join(foreignMount, 'member'), 'foreign hidden original bytes')
      const originalForeignMount = directoryIdentity(foreignMount)
      let foreignImageIdentity
      let foreignDevice
      let foreignAttachAttempted = false
      const readForeign = async () => {
        foreignOwner.assertOwned()
        assert.deepEqual(fileIdentity(foreignImage), foreignImageIdentity)
        const matches = (await readonlyFixtureImages()).filter(entry => entry['image-path'] === foreignImage)
        assert.equal(matches.length, 1)
        assert.equal(matches[0].writeable, false)
        assert.equal(matches[0]['image-type'], 'UDIF read-only')
        const entities = matches[0]['system-entities'].filter(entry => entry['mount-point'] === foreignMount)
        assert.equal(entities.length, 1)
        assert.equal(entities[0]['dev-entry'], foreignDevice)
        return { image: fileIdentity(foreignImage), mount: directoryIdentity(foreignMount), device: foreignDevice, bytes: fs.readFileSync(path.join(foreignMount, 'member'), 'utf8') }
      }
      const ready = path.join(output, 'detach-consumer-ready.json')
      const release = path.join(output, 'detach-consumer-release')
      const result = path.join(output, 'detach-consumer-result.json')
      let target
      let scopeOutcome
      let actualReady
      let actualConsumer
      let actualActor
      let foreignBefore
      let foreignAfter
      let consumer
      let producerArtifactsRetained = false
      try {
        await runReadonlyFixtureCommand('/usr/bin/hdiutil', ['create', '-srcfolder', foreignSource, '-format', 'UDRW', '-fs', 'Case-sensitive HFS+', '-volname', 'ForeignReadonlyInputs', foreignCapture], t.signal)
        await runReadonlyFixtureCommand('/usr/bin/hdiutil', ['convert', foreignCapture, '-format', 'UDRO', '-o', foreignImage], t.signal)
        foreignImageIdentity = fileIdentity(foreignImage)
        foreignAttachAttempted = true
        const attached = await runReadonlyFixtureCommand('/usr/bin/hdiutil', ['attach', foreignImage, '-readonly', '-nobrowse', '-noautoopen', '-mountpoint', foreignMount, '-plist'], t.signal)
        const receipt = JSON.parse(await runReadonlyFixtureCommand('/usr/bin/plutil', ['-convert', 'json', '-o', '-', '-'], t.signal, attached))
        const entities = receipt['system-entities'].filter(entry => entry['mount-point'] === foreignMount)
        assert.equal(entities.length, 1)
        foreignDevice = entities[0]['dev-entry']
        assert.match(foreignDevice, /^\/dev\/disk\d+(s\d+)?$/)
        foreignBefore = await readForeign()
        const outcomes = await Promise.allSettled([withReadonlyVerificationInputs({ ...options, signal: t.signal }, async view => {
          const matches = (await readonlyFixtureImages()).filter(entry => entry['system-entities'].some(entity => entity['mount-point'] === inputs))
          assert.equal(matches.length, 1)
          assert.equal(matches[0].writeable, false)
          assert.equal(matches[0]['image-type'], 'UDIF read-only')
          const image = matches[0]['image-path']
          const imageRoot = path.dirname(image)
          assert.equal(path.dirname(imageRoot), root)
          assert.deepEqual(fs.readdirSync(imageRoot).sort(), ['capture.dmg', 'inputs.dmg'])
          const targetEntities = matches[0]['system-entities'].filter(entity => entity['mount-point'] === inputs)
          assert.equal(targetEntities.length, 1)
          target = { image, imageRoot, rootIdentity: directoryIdentity(imageRoot), imageIdentity: fileIdentity(image), captureIdentity: fileIdentity(path.join(imageRoot, 'capture.dmg')), device: targetEntities[0]['dev-entry'] }
          assert.match(target.device, /^\/dev\/disk\d+(s\d+)?$/)
          assert.notEqual(target.device, foreignDevice)
          const program = `
import fs from 'node:fs'
const roles = ${JSON.stringify(directories.map(directory => directory.root))}
const read = () => {
  const stat = fs.lstatSync(${JSON.stringify(inputs)}, {bigint:true})
  return {root:{dev:String(stat.dev),ino:String(stat.ino),mode:String(stat.mode)},reads:roles.map(role=>fs.readFileSync(role+'/member').toString('hex'))}
}
const released = new Promise((resolve,reject)=>{
  const check=()=>{
    if(fs.existsSync(${JSON.stringify(release)})){
      watcher.close()
      resolve()
    }
  }
  const watcher=fs.watch(${JSON.stringify(output)},check)
  watcher.once('error',error=>{
    watcher.close()
    reject(error)
  })
  check()
})
const before={pid:process.pid,uid:process.getuid(),...read()}
fs.writeFileSync(${JSON.stringify(path.join(output, 'detach-consumer-ready.staging'))},JSON.stringify(before))
fs.renameSync(${JSON.stringify(path.join(output, 'detach-consumer-ready.staging'))},${JSON.stringify(ready)})
await released
const observed={before,after:read(),legalOutput:'actual detached consumer output'}
fs.writeFileSync(${JSON.stringify(result)},JSON.stringify(observed))
fs.writeSync(1,JSON.stringify(observed))
`
          let closeReadyWatcher
          const readyFact = new Promise((resolve, reject) => {
            const watcher = fs.watch(output, () => { if (fs.existsSync(ready)) resolve() })
            const abort = () => reject(t.signal.reason)
            watcher.once('error', reject)
            t.signal.addEventListener('abort', abort, { once: true })
            closeReadyWatcher = () => {
              watcher.close()
              t.signal.removeEventListener('abort', abort)
            }
            if (fs.existsSync(ready)) resolve()
            if (t.signal.aborted) abort()
          })
          try {
            consumer = consumeReadonlyVerificationInputs(view, signal => runVerificationToolProbe(process.execPath, ['--input-type=module', '-e', program], { cwd: output, env: { PATH: path.dirname(process.execPath) }, signal }))
            await Promise.race([readyFact, consumer.then(() => { throw new Error('Actual detach consumer exited before ready') })])
            actualReady = JSON.parse(fs.readFileSync(ready, 'utf8'))
            assert.ok(Number.isSafeInteger(actualReady.pid) && actualReady.pid > 0)
            process.kill(actualReady.pid, 0)
            const actorProgram = `
import assert from 'node:assert/strict'
import {spawn} from 'node:child_process'
import fs from 'node:fs'
const stat=fs.lstatSync(${JSON.stringify(target.image)},{bigint:true})
assert.equal(String(stat.dev),${JSON.stringify(target.imageIdentity.dev)})
assert.equal(String(stat.ino),${JSON.stringify(target.imageIdentity.ino)})
const child=spawn('/usr/bin/hdiutil',['detach',${JSON.stringify(target.device)}],{stdio:['ignore','pipe','pipe'],timeout:30000})
let stdout='',stderr=''
child.stdout.setEncoding('utf8').on('data',value=>{stdout+=value})
child.stderr.setEncoding('utf8').on('data',value=>{stderr+=value})
child.once('error',error=>{throw error})
child.once('close',(exitCode,signal)=>fs.writeSync(1,JSON.stringify({pid:process.pid,uid:process.getuid(),commandPid:child.pid,device:${JSON.stringify(target.device)},force:false,exitCode,signal,stdout,stderr})))
`
            actualActor = JSON.parse(await runVerificationToolProbe(process.execPath, ['--input-type=module', '-e', actorProgram], { cwd: output, env: { PATH: path.dirname(process.execPath) }, signal: t.signal }))
            assert.equal(actualActor.exitCode, 0)
            assert.equal(actualActor.signal, null)
            assert.equal((await readonlyFixtureImages()).some(entry => entry['image-path'] === target.image), false)
          } finally {
            closeReadyWatcher()
            fs.writeFileSync(release, 'release')
          }
          actualConsumer = JSON.parse(await consumer)
          return 'a successful consumer must not publish this detached scope result'
        })])
        scopeOutcome = outcomes[0]
        if (!actualConsumer) {
          if (scopeOutcome.status === 'rejected') throw scopeOutcome.reason
          throw new Error('Readonly scope finished without the actual detached consumer result')
        }
        foreignAfter = await readForeign()
        producerArtifactsRetained = fs.existsSync(target.imageRoot)
      } finally {
        if (consumer) {
          if (!fs.existsSync(release)) fs.writeFileSync(release, 'release')
          await Promise.allSettled([consumer])
        }
        if (foreignDevice) {
          await readForeign()
          await runReadonlyFixtureCommand('/usr/bin/hdiutil', ['detach', foreignDevice])
          assert.equal((await readonlyFixtureImages()).some(entry => entry['image-path'] === foreignImage), false)
        } else if (foreignAttachAttempted) throw new Error('Unconfirmed foreign attach retains its private fixture artifacts')
        assert.deepEqual(directoryIdentity(foreignMount), originalForeignMount)
        assert.equal(fs.readFileSync(path.join(foreignMount, 'member'), 'utf8'), 'foreign hidden original bytes')
        foreignOwner.dispose()
        if (target && fs.existsSync(target.imageRoot)) {
          assert.deepEqual(directoryIdentity(target.imageRoot), target.rootIdentity)
          assert.deepEqual(fs.readdirSync(target.imageRoot).sort(), ['capture.dmg', 'inputs.dmg'])
          assert.deepEqual(fileIdentity(target.image), target.imageIdentity)
          assert.deepEqual(fileIdentity(path.join(target.imageRoot, 'capture.dmg')), target.captureIdentity)
          assert.equal((await readonlyFixtureImages()).some(entry => [target.image, path.join(target.imageRoot, 'capture.dmg')].includes(entry['image-path'])), false)
          fs.rmSync(target.imageRoot, { recursive: true })
        }
      }
      directories.forEach(directory => directory.assertOwned())
      assert.deepEqual(snapshotReadonlyFixtureInputs(directories), original)
      assert.deepEqual(directoryIdentity(inputs), originalInputIdentity)
      assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
      assert.equal(actualReady.uid, process.getuid())
      assert.equal(actualActor.uid, process.getuid())
      assert.notEqual(actualActor.pid, process.pid)
      assert.notEqual(actualActor.pid, actualReady.pid)
      assert.equal(actualActor.force, false)
      assert.equal(actualActor.device, target.device)
      assert.deepEqual(actualReady.reads, original.map(entry => entry.member.bytes.toString('hex')))
      assert.notDeepEqual(actualReady.root, originalInputIdentity)
      assert.deepEqual(actualConsumer, { before: actualReady, after: { root: originalInputIdentity, reads: actualReady.reads }, legalOutput: 'actual detached consumer output' })
      assert.deepEqual(JSON.parse(fs.readFileSync(result, 'utf8')), actualConsumer)
      for (const pid of [actualReady.pid, actualActor.pid, actualActor.commandPid]) assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
      assert.deepEqual(foreignAfter, foreignBefore)
      assert.equal(foreignAfter.bytes, 'independent foreign image bytes')
      assert.equal(producerArtifactsRetained, true)
      assert.equal(scopeOutcome.status, 'rejected')
      assert.ok(scopeOutcome.reason instanceof AggregateError)
      assert.equal(scopeOutcome.reason.cause.code, 'verification-readonly-inputs-invalid')
      assert.equal(scopeOutcome.reason.cause.message, 'Readonly mounted root identity changed')
      assert.equal(scopeOutcome.reason.errors.length, 2)
      assert.equal(scopeOutcome.reason.errors[0], scopeOutcome.reason.cause)
      assert.equal(scopeOutcome.reason.errors[1].message, 'Owned readonly mount disappeared before cleanup')
      t.diagnostic(JSON.stringify({ actualConsumerPid: actualReady.pid, actorPid: actualActor.pid, detachPid: actualActor.commandPid, uid: actualActor.uid, targetDevice: target.device, preservedForeignDevice: foreignDevice, scopeOutcome: scopeOutcome.status, producerArtifactsRetainedUntilFixtureRecovery: producerArtifactsRetained }))
    })
  })
  for (const reason of [new Error('original readonly operation failed'), null, undefined]) {
    const drainTitle = reason === undefined ? 'a successful action omitting await drains and rejects its actual Node consumer before readonly device recovery' : `an early ${reason === null ? 'null' : 'Error'} action failure drains its actual Node consumer before readonly device recovery`
    integrationTest(`WHAT[verification-system-016] ${drainTitle}`, platform, async t => {
      await withDirectories(async ({ root, directories, options }) => {
        const ready = path.join(root, 'consumer-ready')
        let consumer
        let drainedBeforeRestore = false
        let pid
        const expectedFailure = error => reason === undefined ? error?.message === 'Readonly scope ended before its consumers settled' : error === reason
        await assert.rejects(() => withReadonlyVerificationInputs({ ...options, signal: t.signal }, async view => {
          const program = `import fs from 'node:fs'; fs.readFileSync(${JSON.stringify(path.join(directories[0].root, 'member'))}); fs.writeFileSync(${JSON.stringify(ready)}, String(process.pid)); setInterval(() => {}, 1000)`
          consumer = consumeReadonlyVerificationInputs(view, async signal => {
            try {
              await runVerificationToolProbe(process.execPath, ['--input-type=module', '-e', program], { cwd: root, env: { PATH: path.dirname(process.execPath) }, signal })
            } finally {
              if (pid !== undefined) assert.throws(() => consumeReadonlyVerificationInputs(view, () => {}), /no longer admits consumers/)
              assert.throws(() => directories[0].assertOwned(), /owned directory identity/)
              drainedBeforeRestore = true
            }
          })
          let waiting = true
          const readyFact = (async () => {
            while (waiting && !fs.existsSync(ready)) {
              t.signal.throwIfAborted()
              await new Promise(resolve => setTimeout(resolve, 10))
            }
          })()
          try {
            await Promise.race([readyFact, consumer.then(() => { throw new Error('Actual consumer exited before its ready fact') })])
          } finally { waiting = false }
          pid = Number(fs.readFileSync(ready, 'utf8'))
          if (reason !== undefined) throw reason
        }), expectedFailure)
        await assert.rejects(consumer, expectedFailure)
        assert.equal(drainedBeforeRestore, true)
        assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
        directories.forEach(directory => directory.assertOwned())
        fs.unlinkSync(ready)
        assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
      })
    })
    if (reason === undefined) continue
    integrationTest(`WHAT[verification-system-016] actual readonly mount cleanup preserves ${reason === null ? 'null' : 'Error'} operation failure and restores every original directory`, platform, async () => {
      await withDirectories(async ({ root, directories, options }) => {
        await assert.rejects(() => withReadonlyVerificationInputs(options, async () => { throw reason }), error => error === reason)
        directories.forEach(directory => directory.assertOwned())
        assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
      })
    })
    integrationTest(`WHAT[verification-system-016] cancellation of an attached readonly scope preserves ${reason === null ? 'null' : 'Error'} and removes only its owned image`, platform, async () => {
      await withDirectories(async ({ root, directories, options }) => {
        const controller = new AbortController()
        await assert.rejects(() => withReadonlyVerificationInputs({ ...options, signal: controller.signal }, async () => {
          controller.abort(reason)
        }), error => error === reason)
        directories.forEach(directory => directory.assertOwned())
        assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
      })
    })
  }
  for (const reason of [undefined, new Error('primary readonly action failed'), null]) {
    const title = reason === undefined
      ? 'a normal action return cannot conceal an independently failed actual readonly Node consumer'
      : `the original ${reason === null ? 'null' : 'Error'} action failure remains the cause alongside an independently failed actual readonly Node consumer`
    integrationTest(`WHAT[verification-system-016] ${title}`, platform, async t => {
      let outcome
      let consumerFailure
      let consumerPid
      const expectedStderr = 'actual readonly consumer exit 73\n'
      await withDirectories(async ({ root, directories, options }) => {
        const original = snapshotReadonlyFixtureInputs(directories)
        const program = `
import fs from 'node:fs'
const bytes = fs.readFileSync(${JSON.stringify(path.join(directories[0].root, 'member'))})
fs.writeSync(1, JSON.stringify({ pid: process.pid, readHex: bytes.toString('hex') }))
fs.writeSync(2, ${JSON.stringify(expectedStderr)})
process.exit(73)
`
        const scopeResults = await Promise.allSettled([
          withReadonlyVerificationInputs({ ...options, signal: t.signal }, async view => {
            const consumer = consumeReadonlyVerificationInputs(view, signal => runVerificationToolProbe(
              process.execPath, ['--input-type=module', '-e', program],
              { cwd: root, env: { PATH: path.dirname(process.execPath) }, signal },
            ))
            consumerFailure = await consumer.then(
              () => { throw new Error('Actual readonly consumer unexpectedly exited successfully') },
              error => error,
            )
            assert.equal(consumerFailure.code, 'verification-tool-probe-failed')
            assert.equal(consumerFailure.exitCode, 73)
            assert.equal(consumerFailure.signal, null)
            assert.equal(consumerFailure.stderr, expectedStderr)
            assert.equal(consumerFailure.cause, undefined, 'The actual nonzero terminal must not hide a cleanup failure')
            const observed = JSON.parse(consumerFailure.stdout)
            consumerPid = observed.pid
            assert.ok(Number.isSafeInteger(consumerPid) && consumerPid > 0)
            assert.equal(observed.readHex, original[0].member.bytes.toString('hex'))
            assert.throws(() => process.kill(consumerPid, 0), { code: 'ESRCH' })
            assert.throws(() => directories[0].assertOwned(), /owned directory identity/, 'The actual process has drained before restoring the mounted input namespace')
            view.revalidate()
            if (reason !== undefined) throw reason
            return 'this normal action result must never be published'
          }),
        ])
        outcome = scopeResults[0]
        directories.forEach(directory => directory.assertOwned())
        assert.deepEqual(snapshotReadonlyFixtureInputs(directories), original)
        assert.throws(() => process.kill(consumerPid, 0), { code: 'ESRCH' })
        assert.deepEqual(fs.readdirSync(root).sort(), ['inputs', 'output'])
        t.diagnostic(JSON.stringify({
          actualPid: consumerPid, actualExitCode: consumerFailure.exitCode,
          actualSignal: consumerFailure.signal, scopeOutcome: outcome.status,
          restoredOwners: directories.length, remainingRootEntries: fs.readdirSync(root).sort(),
        }))
      })
      // Finish physical cleanup before checking the refusal outcome.
      assert.equal(outcome.status, 'rejected')
      if (reason === undefined) {
        assert.equal(outcome.reason, consumerFailure)
      } else {
        assert.ok(outcome.reason instanceof AggregateError)
        assert.equal(outcome.reason.cause, reason)
        assert.equal(outcome.reason.errors.length, 2)
        assert.equal(outcome.reason.errors[0], reason)
        assert.equal(outcome.reason.errors[1], consumerFailure)
      }
    })
  }
}
