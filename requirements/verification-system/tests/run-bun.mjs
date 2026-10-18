import { spawnSync } from 'node:child_process'
import { mkdtempSync, realpathSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { checkBuildFreshness } from './support/build-freshness.mjs'
import { writeRoutingHome } from './support/routing-home.mjs'

const freshness = checkBuildFreshness()
if (!freshness.ok) throw new Error(freshness.reason)
if (process.argv.length < 3) throw new Error('Select the requirement test files to run with Bun')
const home = realpathSync(mkdtempSync(join(tmpdir(), 'wxs-bun-home-')))
try {
  writeRoutingHome(home)
  const result = spawnSync('bun', ['test', ...process.argv.slice(2)], {
    stdio: 'inherit',
    env: { ...process.env, HOME: home, USERPROFILE: home, WANXIANGSHU_PROVIDER_LANGUAGE: 'en' },
  })
  if (result.error) throw result.error
  if (result.signal) throw new Error(`Bun test process ended with ${result.signal}`)
  process.exitCode = result.status ?? 1
} finally {
  rmSync(home, { recursive: true, force: true })
}
