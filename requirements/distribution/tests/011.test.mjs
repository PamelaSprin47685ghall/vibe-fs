import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { tmpdir } from 'node:os'
import { createHash } from 'node:crypto'
import { checkSourceDependency, checkTrackedArchives } from '../../../scripts/wanxiangli-source.mjs'

const root = fileURLToPath(new URL('../../..', import.meta.url))

test('WHAT[distribution-011] source checkout consumes the exact gitlink through one installed source link', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'))
  const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
  assert.deepEqual(manifest.bundleDependencies, ['wanxiangli'])
  assert.equal(manifest.dependencies.wanxiangli, 'file:wanxiangli')

  const sourceDir = checkSourceDependency(root)
  assert.equal(fs.realpathSync(sourceDir), fs.realpathSync(path.join(root, 'wanxiangli')))
  checkTrackedArchives(root)
  checkTrackedArchives(sourceDir)

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
  const checkedOut = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: sourceDir, encoding: 'utf8' }).trim()
  assert.equal(checkedOut, trackedSource.split(' ')[1])
  const ignored = fs.readFileSync(path.join(root, '.gitignore'), 'utf8')
  assert.match(ignored, /^\*\.tgz$/m)
  assert.doesNotMatch(ignored, /^\/wanxiangli\/$/m)
})

test('WHAT[distribution-011] retained evidence is exact and cannot admit a dependency archive', () => {
  const directory = fs.mkdtempSync(path.join(tmpdir(), 'wxs-retained-archives-'))
  const retained = 'evidence/original.tgz'
  const bytes = Buffer.from([0, 255, 10, 128])
  const archives = { [retained]: createHash('sha256').update(bytes).digest('hex') }
  const manifest = path.join(directory, 'evidence/retained-archives.json')
  const git = (...args) => execFileSync('git', args, { cwd: directory, encoding: 'utf8' }).trim()
  try {
    git('init', '--quiet')
    fs.mkdirSync(path.join(directory, 'evidence'))
    fs.writeFileSync(path.join(directory, retained), bytes)
    git('add', retained)
    git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.com', 'commit', '--quiet', '-m', 'Retain original evidence')
    const baseline = git('rev-parse', 'HEAD')
    fs.writeFileSync(manifest, JSON.stringify({ baseline, archives }))
    checkTrackedArchives(directory)
    fs.writeFileSync(path.join(directory, retained), Buffer.from('changed evidence'))
    assert.throws(() => checkTrackedArchives(directory), /Retained evidence changed/)
    fs.writeFileSync(path.join(directory, retained), bytes)
    fs.writeFileSync(path.join(directory, 'dependency.tgz'), bytes)
    execFileSync('git', ['add', 'dependency.tgz'], { cwd: directory })
    assert.throws(() => checkTrackedArchives(directory), /Tracked dependency archives are forbidden/)
    fs.writeFileSync(manifest, JSON.stringify({ baseline, archives: { ...archives, 'dependency.tgz': archives[retained] } }))
    assert.throws(() => checkTrackedArchives(directory), /baseline inventory/)
    execFileSync('git', ['rm', '--cached', 'dependency.tgz'], { cwd: directory, stdio: 'pipe' })
    fs.writeFileSync(manifest, JSON.stringify({ baseline, archives: { 'evidence/missing.tgz': archives[retained] } }))
    assert.throws(() => checkTrackedArchives(directory), /Tracked dependency archives are forbidden/)
    fs.writeFileSync(manifest, JSON.stringify({ baseline, archives: { [retained]: createHash('sha256').update(Buffer.from('changed evidence')).digest('hex') } }))
    fs.writeFileSync(path.join(directory, retained), Buffer.from('changed evidence'))
    assert.throws(() => checkTrackedArchives(directory), /differs from its baseline/)
    fs.writeFileSync(path.join(directory, retained), bytes)
    fs.writeFileSync(manifest, JSON.stringify({ baseline, archives }))
    checkTrackedArchives(directory)
  } finally {
    fs.rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[distribution-011] a different checked-out dependency commit is rejected before build', () => {
  const directory = fs.mkdtempSync(path.join(tmpdir(), 'wxs-source-pin-'))
  const source = path.join(directory, 'wanxiangli')
  const git = (cwd, ...args) => execFileSync('git', args, { cwd, encoding: 'utf8' }).trim()
  const commit = message => git(source, '-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.com', 'commit', '--quiet', '--allow-empty', '-m', message)
  try {
    git(directory, 'init', '--quiet')
    fs.mkdirSync(source)
    git(source, 'init', '--quiet')
    fs.writeFileSync(path.join(source, 'package.json'), JSON.stringify({ name: 'wanxiangli', version: '0.8.1' }))
    git(source, 'add', 'package.json')
    commit('Pinned dependency')
    const pinned = git(source, 'rev-parse', 'HEAD')
    fs.writeFileSync(path.join(directory, 'package.json'), JSON.stringify({ dependencies: { wanxiangli: 'file:wanxiangli' } }))
    fs.writeFileSync(path.join(directory, '.gitmodules'), '[submodule "wanxiangli"]\npath = wanxiangli\nurl = ./unused\nbranch = main\n')
    git(directory, 'update-index', '--add', '--cacheinfo', `160000,${pinned},wanxiangli`)
    assert.equal(checkSourceDependency(directory), source)
    commit('Unpinned dependency')
    assert.throws(() => checkSourceDependency(directory), /checkout must match its pinned gitlink/)
    git(source, 'checkout', '--quiet', '--detach', pinned)
    assert.equal(checkSourceDependency(directory), source)
  } finally {
    fs.rmSync(directory, { recursive: true, force: true })
  }
})
