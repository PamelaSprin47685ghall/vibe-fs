import { execFileSync, spawn } from 'node:child_process'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'
import { PROCESS_TREE_TIMEOUT_MS } from '../../e2e/support/time-budget.js'

const directory = process.env.WXS_DETACHED_RUNNER_DIRECTORY

if (process.argv[2] === 'detached-owner') {
  const child = spawn(process.execPath, ['-e', `require('node:fs').watch(${JSON.stringify(directory)}, () => {}); process.stdout.write('ready')`], {
    stdio: ['ignore', 'pipe', 'inherit'],
  })
  child.stdout.resume()
  await new Promise((accept, reject) => {
    child.once('error', reject)
    child.stdout.once('data', accept)
  })
  const output = execFileSync('/bin/ps', ['-o', 'pid=,pgid=', '-p', [process.pid, child.pid].join(',')], {
    encoding: 'utf8', timeout: PROCESS_TREE_TIMEOUT_MS,
  })
  const groups = new Map(output.trim().split('\n').map(line => line.trim().split(/\s+/).map(Number)))
  fs.writeFileSync(process.env.WXS_DETACHED_RUNNER_OWNERSHIP, JSON.stringify({
    pid: process.pid, group: groups.get(process.pid), childPid: child.pid, childGroup: groups.get(child.pid),
  }))
} else {
  const { default: test } = await import('node:test')
  test('an actual detached runner has no parent lifeline', async () => {
    const child = spawn(process.execPath, [fileURLToPath(import.meta.url), 'detached-owner'], {
      detached: true, stdio: ['ignore', 'ignore', 'inherit'],
    })
    await new Promise((_, reject) => child.once('error', reject))
  })
}
