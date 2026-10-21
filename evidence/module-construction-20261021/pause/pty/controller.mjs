import { spawn } from 'node:child_process'
import { readFileSync, writeFileSync } from 'node:fs'

const folder = '/private/tmp/vibe-fs-pty-soak-gen100-20261021'
const child = spawn('/opt/homebrew/bin/bun', [folder + '/consumer/pty-soak.mjs'], { cwd: folder + '/consumer', env: { PATH: '/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin', TMPDIR: folder + '/tmp' }, stdio: 'inherit' })
writeFileSync(folder + '/controller.json', JSON.stringify({ pid: process.pid, harnessPid: child.pid, startedAt: new Date().toISOString() }))
const cleanup = () => {
  try {
    const status = JSON.parse(readFileSync(folder + '/status.json', 'utf8'))
    for (const signal of ['SIGCONT', 'SIGKILL']) {
      for (const pid of (status.ownedPids ?? []).reverse()) { try { process.kill(pid, signal) } catch {} }
    }
  } catch {}
}
const limit = setTimeout(() => { cleanup(); child.kill('SIGTERM') }, (120 * 60 + 120) * 1000)
const force = setTimeout(() => { cleanup(); child.kill('SIGKILL') }, (120 * 60 + 150) * 1000)
process.on('SIGTERM', () => { cleanup(); child.kill('SIGTERM') })
process.on('SIGINT', () => { cleanup(); child.kill('SIGTERM') })
const result = await new Promise(resolve => {
  child.on('error', error => resolve({ code: 1, error: error.message }))
  child.on('exit', (code, signal) => resolve({ code, signal }))
})
clearTimeout(limit)
clearTimeout(force)
if (result.code !== 0) cleanup()
writeFileSync(folder + '/controller-exit.json', JSON.stringify({ ...result, at: new Date().toISOString() }))
process.exit(result.code === 0 ? 0 : 1)
