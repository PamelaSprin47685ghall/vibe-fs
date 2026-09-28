import assert from 'node:assert/strict'
import { execFileSync, spawn } from 'node:child_process'
import { once } from 'node:events'
import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const fixturePath = fileURLToPath(import.meta.url)
const evidencePath = process.env.LATE_ORPHAN_EVIDENCE
const processGroup = (pid) => Number(execFileSync('ps', ['-o', 'pgid=', '-p', String(pid)], { encoding: 'utf8' }).trim())

if (process.argv[2] === '--orphan') {
  setInterval(() => {}, 1000)
  process.send({ type: 'ready' }, () => process.disconnect())
} else if (process.argv[2] === '--worker') {
  setInterval(() => {}, 1000)
  process.once('SIGTERM', async () => {
    const orphan = spawn(process.execPath, [fixturePath, '--orphan'], {
      detached: false,
      stdio: ['ignore', 'ignore', 'ignore', 'ipc'],
    })
    const [ready] = await once(orphan, 'message')
    assert.equal(ready.type, 'ready')
    writeFileSync(evidencePath, JSON.stringify({
      workerPid: process.pid,
      orphanPid: orphan.pid,
      pgid: processGroup(process.pid),
      orphanPgid: processGroup(orphan.pid),
    }))
    process.exit(0)
  })
  process.send({ type: 'ready' })
} else {
  const { default: test } = await import('node:test')
  const { terminateChild } = await import('../../e2e/support/process-host-utils.js')

  test('a child creates a same-group orphan only while handling its stop request', async () => {
    assert.ok(evidencePath, 'the harness must own the evidence file and final cleanup')
    const worker = spawn(process.execPath, [fixturePath, '--worker'], {
      detached: false,
      stdio: ['ignore', 'ignore', 'inherit', 'ipc'],
    })
    const exited = once(worker, 'exit')
    const [ready] = await once(worker, 'message')
    assert.equal(ready.type, 'ready')
    await terminateChild(worker, undefined, undefined, { detached: false })
    assert.deepEqual(await exited, [0, null])
    const evidence = JSON.parse(readFileSync(evidencePath, 'utf8'))
    assert.equal(evidence.workerPid, worker.pid)
    assert.equal(evidence.pgid, processGroup(process.pid))
    assert.equal(evidence.orphanPgid, evidence.pgid)
    assert.equal(processGroup(evidence.orphanPid), evidence.pgid)
    writeFileSync(evidencePath, JSON.stringify({ ...evidence, stopReturned: true }))
  })
}
