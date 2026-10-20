import assert from 'node:assert/strict'
import { existsSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { sandbox, runProgram, nodeCommand } from './executor.mjs'
import { oneCommand } from './runnable.mjs'

export async function runWithSpoolFault(options = {}) {
  const { dir, cleanup } = sandbox('wxs-run-spool-fault-')
  const previous = process.env.TMPDIR
  let pending
  try {
    const ready = join(dir, 'ready')
    const emit = join(dir, 'emit')
    const command = nodeCommand(`const fs = require('node:fs'); fs.writeFileSync(${JSON.stringify(ready)}, 'ready'); const timer = setInterval(() => { if (fs.existsSync(${JSON.stringify(emit)})) { clearInterval(timer); process.stdout.write('x'.repeat(600000)); process.exitCode = 7; } }, 10)`)
    pending = runProgram(dir, `return await js.call(${oneCommand(command, options)});`)
    const deadline = performance.now() + 4000
    while (!existsSync(ready) && performance.now() < deadline) await delay(10)
    assert.equal(existsSync(ready), true, 'the target must start before the capture fault is introduced')
    process.env.TMPDIR = `${dir}/missing-parent`
    writeFileSync(emit, 'emit')
    return await pending
  } finally {
    if (previous === undefined) delete process.env.TMPDIR
    else process.env.TMPDIR = previous
    await pending?.catch(() => {})
    cleanup()
  }
}
