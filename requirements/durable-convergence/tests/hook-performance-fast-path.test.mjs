import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { chmodSync, existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { execFileSync, spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { ensure } from '../../../dist/Git/Hook/Surface.js'
import { remotePayloadNeedsRead } from '../../../dist/Persistence/EventStore/RetentionSurface.js'

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
  const remoteTrees = sync.slice(sync.indexOf('let private readRemoteTrees'), sync.indexOf('let private readRemote\n'))

  assert.match(log, /writerPhysicalStats/)
  assert.match(log, /payloadPhysicalStats/)
  assert.match(log, /payloadExists[\s\S]*existsSync/)
  assert.match(sync, /CachedFile/)
  assert.match(sync, /cachedOid/)
  assert.match(sync, /remoteEntryNeeded/)
  assert.match(sync, /changedRemoteEntries/)
  assert.match(sync, /changedRemotePayloadEntries/)
  assert.match(sync, /cached\.Oid = entry\.Oid/)
  assert.doesNotMatch(remoteTrees, /changedRemoteEntries[\s\S]*Map\.empty[\s\S]*payloadEntries/)
  assert.doesNotMatch(sync, /readRemoteTrees[\s\S]*readBlobList raw writerEntries[\s\S]*readBlobList raw payloadEntries/)
})

test('WHAT[DURABLE-CONVERGENCE-010] unchanged remote payload is not reread merely because payloads have no writer manifest', () => {
  assert.equal(remotePayloadNeedsRead('stat-a', 'a'.repeat(40), 'stat-a', 'a'.repeat(40), true), false)
  assert.equal(remotePayloadNeedsRead('stat-a', 'a'.repeat(40), 'stat-b', 'a'.repeat(40), true), true)
  assert.equal(remotePayloadNeedsRead('stat-a', 'a'.repeat(40), 'stat-a', 'b'.repeat(40), true), true)
  assert.equal(remotePayloadNeedsRead('stat-a', 'a'.repeat(40), 'stat-a', 'a'.repeat(40), false), true)
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
    const commonDir = execFileSync('git', ['-C', repo, 'rev-parse', '--path-format=absolute', '--git-common-dir'], { encoding: 'utf8' }).trim()
    const wrapper = join(commonDir, 'wanxiang', 'ssh-command')
    const base = 'ssh -F /dev/null -i /tmp/wxs-test-key'
    execFileSync('git', ['-C', repo, 'config', '--local', 'core.sshCommand', base])

    assert.equal(ensure(repo), true, 'hook ensure failed')
    const configured = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()
    const wrapperBody = readFileSync(wrapper, 'utf8')

    assert.match(configured, /wanxiang\/ssh-command/)
    assert.doesNotMatch(configured, /ControlMaster|ControlPath/)
    assert.match(wrapperBody, /ssh -F \/dev\/null -i \/tmp\/wxs-test-key\b/)
    assert.match(wrapperBody, /ControlMaster=auto/)
    assert.match(wrapperBody, /ControlPersist=15s/)
    assert.match(wrapperBody, /ControlPath=.*wanxiang-ssh-[0-9a-f]{12}\/ssh-%C/)
    assert.match(wrapperBody, /mkdir -p/)

    assert.equal(ensure(repo), true, 'second hook ensure failed')
    const configuredAgain = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()
    assert.equal(configuredAgain, configured, 'repeated ensure must not stack SSH multiplex options')
    assert.equal(readFileSync(wrapper, 'utf8'), wrapperBody, 'repeated ensure must keep the owned SSH wrapper stable')
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-CONVERGENCE-010] hook installer migrates the obsolete long repo-local control socket path', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-ssh-migrate-'))

  try {
    execFileSync('git', ['init', '--quiet', repo])
    const commonDir = execFileSync('git', ['-C', repo, 'rev-parse', '--path-format=absolute', '--git-common-dir'], { encoding: 'utf8' }).trim()
    const base = 'ssh -F /dev/null -i /tmp/wxs-test-key'
    const legacy = `${base} -o ControlMaster=auto -o ControlPersist=15s -o 'ControlPath=${join(commonDir, 'wanxiang', 'ssh-%C')}'`
    execFileSync('git', ['-C', repo, 'config', '--local', 'core.sshCommand', legacy])

    assert.equal(ensure(repo), true, 'hook ensure failed')
    const configured = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()
    const wrapper = join(commonDir, 'wanxiang', 'ssh-command')
    const wrapperBody = readFileSync(wrapper, 'utf8')
    assert.match(configured, /wanxiang\/ssh-command/)
    assert.match(wrapperBody, /^#!\/bin\/sh/m)
    assert.match(wrapperBody, /ssh -F \/dev\/null -i \/tmp\/wxs-test-key\b/)
    assert.match(wrapperBody, /ControlPath=.*wanxiang-ssh-[0-9a-f]{12}\/ssh-%C/)
    assert.doesNotMatch(configured, new RegExp(`${commonDir.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/wanxiang/ssh-%C`))
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-CONVERGENCE-010] hook installer migrates the ephemeral tmp-directory path and recreates it at SSH invocation', () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-hook-ssh-ephemeral-migrate-'))

  try {
    execFileSync('git', ['init', '--quiet', repo])
    const commonDir = execFileSync('git', ['-C', repo, 'rev-parse', '--path-format=absolute', '--git-common-dir'], { encoding: 'utf8' }).trim()
    const repoKey = createHash('sha256').update(commonDir).digest('hex').slice(0, 12)
    const socketDir = join(tmpdir(), `wanxiang-ssh-${repoKey}`)
    const observedArgs = join(repo, 'ssh-args')
    const fakeSsh = join(repo, 'fake-ssh')
    writeFileSync(fakeSsh, `#!/bin/sh\nprintf '%s\\n' "$@" > ${JSON.stringify(observedArgs)}\n`)
    chmodSync(fakeSsh, 0o755)
    const base = fakeSsh
    const ephemeral = `${base} -o ControlMaster=auto -o ControlPersist=15s -o 'ControlPath=${join(tmpdir(), `wanxiang-ssh-${repoKey}`, 'ssh-%C')}'`
    execFileSync('git', ['-C', repo, 'config', '--local', 'core.sshCommand', ephemeral])

    assert.equal(ensure(repo), true, 'hook ensure failed')
    const configured = execFileSync('git', ['-C', repo, 'config', '--local', '--get', 'core.sshCommand'], { encoding: 'utf8' }).trim()
    const wrapper = join(commonDir, 'wanxiang', 'ssh-command')
    assert.match(configured, /wanxiang\/ssh-command/)
    assert.doesNotMatch(configured, /wanxiang-ssh-[0-9a-f]{12}\/ssh-%C/)

    rmSync(socketDir, { recursive: true, force: true })
    assert.equal(existsSync(socketDir), false)
    const invoked = spawnSync(wrapper, ['example.test', 'git-receive-pack repo.git'], { encoding: 'utf8' })
    assert.equal(invoked.status, 0, invoked.stderr || invoked.stdout)
    assert.equal(existsSync(socketDir), true, 'SSH wrapper must recreate its private multiplex directory at invocation time')
    const args = readFileSync(observedArgs, 'utf8')
    assert.match(args, /ControlMaster=auto/)
    assert.match(args, /ControlPersist=15s/)
    assert.match(args, new RegExp(`ControlPath=${socketDir.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/ssh-%C`))
    assert.match(args, /example\.test/)
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
    assert.equal(ensure(repo), true, 'hook ensure failed')
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
    assert.equal(ensure(repo), true, 'hook ensure failed')

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
