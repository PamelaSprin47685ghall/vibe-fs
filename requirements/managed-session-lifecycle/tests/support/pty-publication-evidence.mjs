import assert from 'node:assert/strict'
import fs from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHmac } from 'node:crypto'
import { tmpdir } from 'node:os'
import { setTimeout as delay } from 'node:timers/promises'
import { withAdmissionWorkers } from './pty-admission.mjs'

const mode = process.argv[2]
const evidence = fs.mkdtempSync(join(tmpdir(), 'wxs-host-publication-evidence-'))
const trace = join(evidence, 'events.jsonl')
const preload = fileURLToPath(new URL('../../../process-execution/tests/support/pty-recovery-publication-preload.cjs', import.meta.url))
process.env.BUN_OPTIONS = `${process.env.BUN_OPTIONS ?? ''} --preload=${preload}`.trim()
process.env.WXS_PUBLICATION_MODE = mode
process.env.WXS_PUBLICATION_TRACE = trace
const records = () => fs.existsSync(trace) ? fs.readFileSync(trace, 'utf8').trim().split('\n').map(JSON.parse) : []
const alive = pid => {
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    if (error.code !== 'ESRCH') throw error
    return false
  }
}
const until = async (condition, message) => {
  const deadline = Date.now() + 5000
  while (!condition()) {
    assert.ok(Date.now() < deadline, message)
    await delay(10)
  }
}

await withAdmissionWorkers(`publication-${mode}`, async (producer, _unused, workspace, start) => {
  const marker = join(workspace, 'commands')
  let owner
  let endpoint
  try {
    const opened = await producer.call('open', `printf 'once\\n' >> '${marker}'; exit 23`)
    assert.equal(opened.ok, true, opened.error)
    const root = join(workspace, '.git', 'wanxiangshu', 'pty')
    await until(() => fs.existsSync(root) && fs.readdirSync(root).length === 1, 'the exact physical request must exist')
    const keys = fs.readdirSync(root)
    assert.equal(keys.length, 1)
    const directory = join(root, keys[0])
    const request = JSON.parse(fs.readFileSync(join(directory, 'request'), 'utf8'))
    endpoint = request.endpoint
    await until(() => fs.existsSync(join(directory, 'available')), 'the custodian identity must be available')
    owner = JSON.parse(fs.readFileSync(join(directory, 'available'), 'utf8')).owner
    await until(() => records().some(row => row.kind === 'inner-before-remove'), 'preserve the actual physical scope exit')
    const inner = records().find(row => row.kind === 'inner-before-remove').state
    assert.deepEqual(inner['scope-ended'].targetExit, { code: 23, signal: null })
    await delay(100)
    const original = await producer.call('stop')
    assert.equal(original.finished, false)
    assert.match(original.failure, /EIO/)
    assert.deepEqual(await producer.call('blockers'), [opened.id])
    if (mode === 'owner-lost') {
      const pending = fs.readdirSync(directory).filter(name => name.startsWith('terminal.') && name.endsWith('.pending'))
      assert.equal(pending.length, 1)
      const stored = JSON.parse(fs.readFileSync(join(directory, pending[0]), 'utf8'))
      assert.equal(stored.proof, createHmac('sha256', request.secret).update(JSON.stringify(stored.receipt)).digest('hex'))
      assert.equal(stored.receipt.key, request.key)
      assert.equal(stored.receipt.incarnation, request.incarnation)
      assert.deepEqual(stored.receipt.result.exit, { code: 23, signal: null })
      assert.equal(fs.existsSync(join(directory, 'terminal')), false)
      process.kill(owner, 'SIGKILL')
      await until(() => !alive(owner), 'the completed test custodian must exit')
      for (let attempt = 0; attempt < 2; attempt++) {
        const fresh = await start()
        const recovered = await fresh.call('stop')
        assert.equal(recovered.finished, false)
        assert.deepEqual(await fresh.call('blockers'), [opened.id])
      }
    } else {
      if (mode === 'rename') {
        const failed = await start()
        const recovered = await failed.call('stop')
        assert.equal(recovered.finished, false)
        assert.match(recovered.failure, /terminal rename EIO/)
        assert.deepEqual(await failed.call('blockers'), [opened.id])
      }
      const fresh = await start()
      const recovered = await fresh.call('stop')
      assert.equal(recovered.finished, true, recovered.failure)
      assert.deepEqual(await fresh.call('blockers'), [])
      const physical = JSON.parse(fs.readFileSync(join(directory, 'terminal'), 'utf8')).receipt.result
      assert.deepEqual(physical.exit, { code: 23, signal: null })
      assert.equal(physical.stopped, false)
      await until(() => !alive(owner), 'the physical custodian must retire after its exact receipt is available')
      const originalRetry = await producer.call('stop')
      assert.equal(originalRetry.finished, false, 'a fresh Host must not erase the original observation failure')
    }
    assert.equal(fs.readFileSync(marker, 'utf8'), 'once\n')
    const verified = { mode, nativeExit: 23, commands: 1, ownerExited: true, unknownRetained: mode === 'owner-lost' }
    fs.writeFileSync(join(evidence, 'verified.json'), JSON.stringify(verified))
    console.log(JSON.stringify({ ...verified, evidence }))
  } finally {
    if (owner && alive(owner)) {
      process.kill(owner, 'SIGKILL')
      await until(() => !alive(owner), 'clean only the test custodian after preserving native evidence')
    }
    if (endpoint) {
      if (fs.existsSync(endpoint)) fs.unlinkSync(endpoint)
      if (fs.existsSync(dirname(endpoint))) fs.rmdirSync(dirname(endpoint))
    }
  }
}, { realBackend: true, runtime: process.execPath })
