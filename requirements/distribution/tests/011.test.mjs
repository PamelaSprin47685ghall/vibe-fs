import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { checkSourceDependency, assertNoTrackedArchives } from '../../../scripts/wanxiangli-source.mjs'

const root = fileURLToPath(new URL('../../..', import.meta.url))

test('WHAT[distribution-011] source checkout consumes one tracked wanxiangli npm artifact with matching lock integrity', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
  const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
  assert.deepEqual(manifest.bundleDependencies, ['wanxiangli'])
  assert.equal(manifest.dependencies.wanxiangli, 'file:wanxiangli')

  const sourceDir = checkSourceDependency(root)
  assert.equal(fs.realpathSync(sourceDir), fs.realpathSync(path.join(root, 'wanxiangli')))
  assertNoTrackedArchives(root)
  assertNoTrackedArchives(sourceDir)

  const sourcePkg = JSON.parse(fs.readFileSync(path.join(sourceDir, 'package.json'), 'utf8'))
  const locked = lock.packages['node_modules/wanxiangli']
  assert.deepEqual(locked, {
    resolved: 'wanxiangli',
    link: true,
  })
  const lockedWorkspace = lock.packages['wanxiangli']
  assert.equal(lockedWorkspace.version, sourcePkg.version)

  const installed = path.join(root, 'node_modules/wanxiangli')
  assert.equal(fs.realpathSync(installed), fs.realpathSync(sourceDir))

  const trackedSource = execFileSync('git', ['ls-files', '--stage', 'wanxiangli'], {
    cwd: root,
    encoding: 'utf8',
  }).trim()
  assert.match(trackedSource, /^160000 [0-9a-f]{40} 0\twanxiangli$/)
  const ignored = fs.readFileSync(path.join(root, '.gitignore'), 'utf8')
  assert.match(ignored, /^\*\.tgz$/m)
  assert.doesNotMatch(ignored, /^\/wanxiangli\/$/m)
})
