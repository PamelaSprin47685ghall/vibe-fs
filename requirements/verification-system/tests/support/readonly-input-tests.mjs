import assert from 'node:assert/strict'
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
}
