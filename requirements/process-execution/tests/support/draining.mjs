import assert from 'node:assert/strict'
import { mkdtempSync, existsSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import * as runtime from '../../../../dist/Process/Surface.js'

const alive = pid => {
  try { process.kill(pid, 0) } catch (error) {
    if (error.code === 'ESRCH') return false
    throw error
  }
  const status = spawnSync('/bin/ps', ['-p', String(pid), '-o', 'stat='], { encoding: 'utf8' })
  if (status.error) throw status.error
  return status.status === 0 && !status.stdout.trim().startsWith('Z')
}

const until = async (observe, label) => {
  const deadline = performance.now() + 5000
  while (performance.now() < deadline) {
    if (observe()) return
    await delay(10)
  }
  assert.fail(label)
}

export async function verifyDrainingStop(t, reason) {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-draining-stop-'))
  const targetFile = join(directory, 'target.json')
  const readyFile = join(directory, 'grandchild.json')
  const token = runtime.createCancellationToken(false)
  let grandchild
  const grandchildCode = `const fs = require('node:fs'); process.on('SIGTERM', () => {}); fs.writeFileSync(${JSON.stringify(`${readyFile}.pending`)}, JSON.stringify({pid:process.pid})); fs.renameSync(${JSON.stringify(`${readyFile}.pending`)}, ${JSON.stringify(readyFile)}); setInterval(() => {}, 1000)`
  const targetCode = `const {spawn}=require('node:child_process'); const fs=require('node:fs'); const child=spawn(process.execPath,['-e',${JSON.stringify(grandchildCode)}],{stdio:['ignore',1,2]}); fs.writeFileSync(${JSON.stringify(`${targetFile}.pending`)},JSON.stringify({pid:process.pid})); fs.renameSync(${JSON.stringify(`${targetFile}.pending`)},${JSON.stringify(targetFile)}); child.unref(); process.exit(23)`
  const running = runtime.run(
    runtime.command(process.execPath, ['-e', targetCode], directory, undefined),
    runtime.estimate(30, 1048576, 'medium'),
    runtime.context(directory, 2000),
    token,
  )
  t.after(async () => {
    runtime.cancel(token)
    if (!grandchild && existsSync(readyFile)) grandchild = JSON.parse(readFileSync(readyFile, 'utf8')).pid
    if (grandchild) {
      try { process.kill(grandchild, 'SIGKILL') } catch (error) {
        if (error.code !== 'ESRCH') throw error
      }
    }
    await running.catch(() => {})
    rmSync(directory, { recursive: true, force: true })
  })
  await until(() => existsSync(targetFile) && existsSync(readyFile), 'both real processes must reach the barrier')
  const target = JSON.parse(readFileSync(targetFile, 'utf8')).pid
  grandchild = JSON.parse(readFileSync(readyFile, 'utf8')).pid
  await until(() => !alive(target), 'the target must exit before cancellation or deadline')
  assert.equal(alive(grandchild), true)
  if (reason === 'cancel') {
    runtime.cancel(token)
    const result = await running
    assert.equal(result.kind, 'Observed')
    assert.deepEqual(result.ending, { kind: 'Exited', exitCode: 23 })
    assert.equal(result.stopRequested.kind, 'CancellationRequested')
    assert.equal(result.output, null)
  } else {
    const result = await running
    assert.equal(result.kind, 'Observed')
    assert.deepEqual(result.ending, { kind: 'Exited', exitCode: 23 })
    assert.equal(result.stopRequested.kind, 'DeadlineStopRequested')
  }
  await until(() => !alive(grandchild), 'the same-group pipe holder must actually terminate')
}
