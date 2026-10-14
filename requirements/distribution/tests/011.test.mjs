import assert from 'node:assert/strict'
import crypto from 'node:crypto'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import * as tar from 'tar'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../../..', import.meta.url))

test('WHAT[distribution-011] source checkout consumes one tracked wanxiangli npm artifact with matching lock integrity', async () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
  const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
  assert.deepEqual(manifest.bundleDependencies, ['wanxiangli'])
  assert.equal(manifest.dependencies.wanxiangli, 'file:vendor/wanxiangli-0.5.2.tgz')

  const artifact = path.join(root, 'vendor/wanxiangli-0.5.2.tgz')
  assert.ok(fs.existsSync(artifact), 'tracked wanxiangli artifact is missing')
  const bytes = fs.readFileSync(artifact)
  const integrity = 'sha512-' + crypto.createHash('sha512').update(bytes).digest('base64')
  const locked = lock.packages['node_modules/wanxiangli']
  assert.equal(locked.version, '0.5.2')
  assert.equal(locked.resolved, 'file:vendor/wanxiangli-0.5.2.tgz')
  assert.equal(locked.integrity, integrity)
  assert.equal(locked.inBundle, true)

  let packageJson = ''
  await tar.t({
    file: artifact,
    onReadEntry(entry) {
      if (entry.path !== 'package/package.json') return
      const chunks = []
      entry.on('data', chunk => chunks.push(chunk))
      entry.on('end', () => { packageJson = Buffer.concat(chunks).toString('utf8') })
    },
  })
  assert.equal(JSON.parse(packageJson).version, '0.5.2')

  const trackedSource = execFileSync('git', ['ls-files', '--stage', 'wanxiangli'], {
    cwd: root,
    encoding: 'utf8',
  }).trim()
  assert.equal(trackedSource, '', 'wanxiangli source checkout must not be a superproject gitlink')
  const ignored = fs.readFileSync(path.join(root, '.gitignore'), 'utf8')
  assert.match(ignored, /^\/wanxiangli\/$/m)
})
