import { spawn } from 'node:child_process'
import { readFileSync, writeFileSync, renameSync, openSync, closeSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const root = '/private/tmp/vibe-fs-physical-soak-20261021-a'
const script = fileURLToPath(new URL('./lane.mjs', import.meta.url))
const runtimes = { node22: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node', bun: '/opt/homebrew/bin/bun' }
const children = []
const result = { state: 'Running', startedAt: new Date().toISOString(), coordinatorPid: process.pid, source: '3db7b439283779f6037244d57cf05e6ec3e7ab31', targetElapsedHours: 4, lanes: {} }
const save = () => {
  for (const lane of Object.keys(runtimes)) {
    try { result.lanes[lane] = JSON.parse(readFileSync(root + '/' + lane + '/status.json', 'utf8')) } catch {}
  }
  result.updatedAt = new Date().toISOString()
  writeFileSync(root + '/status.next', JSON.stringify(result))
  renameSync(root + '/status.next', root + '/status.json')
}
let stopped = false
const stop = reason => {
  if (stopped) return
  stopped = true
  result.failure = reason
  for (const child of children) if (child.exitCode === null) child.kill('SIGTERM')
  save()
}
process.on('SIGTERM', () => stop('Operator requested cancellation'))
process.on('SIGINT', () => stop('Operator requested cancellation'))
const promises = []
for (const [lane, executable] of Object.entries(runtimes)) {
  const descriptor = openSync(root + '/' + lane + '/runner.log', 'a')
  const child = spawn(executable, [script, lane, root + '/' + lane], {
    cwd: root + '/consumer', env: { PATH: '/usr/bin:/bin:/usr/sbin:/sbin', TMPDIR: root + '/' + lane + '/tmp' }, stdio: ['ignore', descriptor, descriptor],
  })
  closeSync(descriptor)
  children.push(child)
  promises.push(new Promise(resolve => {
    child.on('error', error => { stop(lane + ': ' + error.message); resolve({ lane, error: error.message }) })
    child.on('exit', (code, signal) => {
      if (code !== 0) stop(lane + ': exit ' + code + ', signal ' + signal)
      resolve({ lane, code, signal })
    })
  }))
}
save()
const ticker = setInterval(save, 15000)
const watchdog = setTimeout(() => stop('Four-hour envelope plus three-minute cleanup exceeded'), (4 * 60 * 60 + 180) * 1000)
result.results = await Promise.all(promises)
clearInterval(ticker)
clearTimeout(watchdog)
result.state = result.results.every(item => item.code === 0) ? 'Passed' : 'Failed'
result.endedAt = new Date().toISOString()
save()
writeFileSync(root + '/exit', result.state === 'Passed' ? '0' : '1')
process.exit(result.state === 'Passed' ? 0 : 1)
