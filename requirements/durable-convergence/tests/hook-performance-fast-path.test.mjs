import assert from 'node:assert/strict'
import { chmodSync, existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { execFileSync, spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { ensure } from '../../../dist/Git/Hook/Dispatcher.js'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[DURABLE-CONVERGENCE-010] no-op sync reuses stat-fingerprint materialization instead of rereading durable bytes', async () => {
  const log = await read('src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(log, /physicalFingerprint/)
  assert.match(log, /statSync/)
  assert.match(sync, /tryCachedLocal/)
  assert.match(sync, /physicalFingerprint/)
  assert.match(sync, /materializationCache/i)
})

test('WHAT[DURABLE-CONVERGENCE-010] near-equal worst path reads and blobifies only changed files', async () => {
  const log = await read('src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(log, /writerPhysicalStats/)
  assert.match(log, /payloadPhysicalStats/)
  assert.match(log, /payloadExists[\s\S]*existsSync/)
  assert.match(sync, /CachedFile/)
  assert.match(sync, /cachedOid/)
  assert.match(sync, /remoteEntryNeeded/)
  assert.match(sync, /changedRemoteEntries/)
  assert.match(sync, /cached\.Oid = entry\.Oid/)
  assert.doesNotMatch(sync, /readRemoteTrees[\s\S]*readBlobList raw writerEntries[\s\S]*readBlobList raw payloadEntries/)
})

test('WHAT[DURABLE-CONVERGENCE-010] pre-push starts from tracking ref and only discovers remote after lease rejection', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')

  assert.match(gateway, /readTrackedRemote|trackingRef/)
  assert.match(gateway, /pushSnapshot/)
  assert.match(gateway, /Error _ when retriesLeft > 0[\s\S]*discoverRemote/s)
  assert.doesNotMatch(gateway, /\| None ->\s*let! snapshot, expected = discoverRemote run remote/)
})

test('WHAT[DURABLE-CONVERGENCE-010] clean tracked snapshot skips all Wanxiang transport', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(sync, /tryCachedLocalSnapshot/)
  assert.match(gateway, /tryCachedLocalSnapshot[\s\S]*sameSnapshot[\s\S]*return cached/s)
  assert.match(gateway, /readTrackedRemote/)
})

test('WHAT[DURABLE-CONVERGENCE-010] hook installer enables repo-local SSH multiplex without clobbering ssh identity options', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-ssh-mux-'))

  try {
    execFileSync('git', ['init', '--quiet', repo])
    const base = 'ssh -F /dev/null -i /tmp/wxs-test-key'
    execFileSync('git', ['-C', repo, 'config', '--local', 'core.sshCommand', base])

    const first = ensure(repo)
    assert.equal(first.tag, 0, `hook ensure failed: ${JSON.stringify(first)}`)
    const configured = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()

    assert.match(configured, /^ssh -F \/dev\/null -i \/tmp\/wxs-test-key\b/)
    assert.match(configured, /ControlMaster=auto/)
    assert.match(configured, /ControlPersist=15s/)
    assert.match(configured, /ControlPath=/)

    const second = ensure(repo)
    assert.equal(second.tag, 0, `second hook ensure failed: ${JSON.stringify(second)}`)
    const configuredAgain = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()
    assert.equal(configuredAgain, configured, 'repeated ensure must not stack SSH multiplex options')
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-CONVERGENCE-010] hook installer respects user-owned SSH multiplex configuration', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-ssh-user-owned-'))

  try {
    execFileSync('git', ['init', '--quiet', repo])
    const userOwned = 'ssh -o ControlMaster=yes -o ControlPath=/tmp/user-owned-%C'
    execFileSync('git', ['-C', repo, 'config', '--local', 'core.sshCommand', userOwned])
    const verdict = ensure(repo)
    assert.equal(verdict.tag, 0, `hook ensure failed: ${JSON.stringify(verdict)}`)
    const configured = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()
    assert.equal(configured, userOwned)
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-CONVERGENCE-010] confirmed same-root convergence does not publish an empty snapshot', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')

  assert.match(gateway, /remoteKnownCurrent|confirmedRemote/i)
  assert.match(gateway, /RootOid\.value merged\.RootOid/)
  assert.match(gateway, /expectedRemote/)
  assert.match(gateway, /return Ok\(\)/)
})

test('WHAT[DURABLE-CONVERGENCE-010] irrelevant reference transactions exit before starting Node', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-fast-path-'))

  try {
    execFileSync('git', ['init', '--quiet', repo])
    const verdict = ensure(repo)
    assert.equal(verdict.tag, 0, `hook ensure failed: ${JSON.stringify(verdict)}`)

    const marker = join(repo, 'node-started')
    const bin = join(repo, 'bin')
    execFileSync('mkdir', ['-p', bin])
    const fakeNode = join(bin, 'node')
    writeFileSync(fakeNode, `#!/bin/sh\nprintf started > ${JSON.stringify(marker)}\n`)
    chmodSync(fakeNode, 0o755)

    const hook = join(repo, '.git', 'hooks', 'reference-transaction')
    const localRef = `${'0'.repeat(40)} ${'1'.repeat(40)} refs/heads/main\n`
    const ignored = spawnSync(hook, ['committed'], {
      cwd: repo,
      encoding: 'utf8',
      input: localRef,
      env: { ...process.env, PATH: `${bin}:${process.env.PATH}` },
    })

    assert.equal(ignored.status, 0, ignored.stderr || ignored.stdout)
    assert.equal(existsSync(marker), false, 'ordinary refs must not pay Node/module startup cost')

    const trackedStore = `${'0'.repeat(40)} ${'1'.repeat(40)} refs/wanxiang/remotes/origin/store\n`
    const relevant = spawnSync(hook, ['committed'], {
      cwd: repo,
      encoding: 'utf8',
      input: trackedStore,
      env: { ...process.env, PATH: `${bin}:${process.env.PATH}` },
    })

    assert.equal(relevant.status, 0, relevant.stderr || relevant.stdout)
    assert.equal(readFileSync(marker, 'utf8'), 'started')
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})
