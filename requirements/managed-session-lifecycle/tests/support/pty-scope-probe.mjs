import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, existsSync, writeFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { setTimeout as delay } from 'node:timers/promises'
import { spawnSync } from 'node:child_process'
import { dlopen } from 'bun:ffi'
import {
  ptyScopeOpen, supervisorCreate, supervisorAttach, createPtyPort, portFork,
  ptyId, portCloseAll, portExists, portAddMailboxSender,
} from '../../../../dist/Process/Surface.js'

const native = dlopen(process.platform === 'darwin' ? '/usr/lib/libSystem.B.dylib' : 'libc.so.6', {
  getpgid: { args: ['i32'], returns: 'i32' },
  getsid: { args: ['i32'], returns: 'i32' },
  setenv: { args: ['cstring', 'cstring', 'i32'], returns: 'i32' },
  unsetenv: { args: ['cstring'], returns: 'i32' },
}).symbols
const quote = value => `'${value.replaceAll("'", "'\\''")}'`
const alive = pid => {
  try { process.kill(pid, 0); return true } catch (error) {
    if (error.code === 'ESRCH') return false
    throw error
  }
}
const waitFor = async (predicate, label) => {
  const end = Date.now() + 8000
  while (!predicate()) {
    assert.ok(Date.now() < end, `timed out waiting for ${label}`)
    await delay(5)
  }
}
const opened = []
const targetPids = []
const memberRows = owner => {
  const sample = spawnSync('/bin/ps', ['-axo', 'pid=,ppid=,pgid=,stat='], { encoding: 'utf8' })
  assert.equal(sample.status, 0)
  return sample.stdout.trim().split('\n').map(line => {
    const [pid, parent, group, state] = line.trim().split(/\s+/)
    return { pid: Number(pid), parent: Number(parent), group: Number(group), state }
  }).filter(row => row.pid === owner || row.parent === owner)
}
const anchorFor = scope => {
  const target = scope.read('started').target
  const anchors = memberRows(scope.term.pid).filter(row => row.parent === scope.term.pid && row.pid !== target)
  assert.equal(anchors.length, 1)
  assert.equal(anchors[0].group, target)
  assert.equal(native.getsid(anchors[0].pid), scope.term.pid)
  return anchors[0].pid
}
const open = async command => {
  const term = await ptyScopeOpen(command, process.cwd())
  const incarnation = term.inspect().incarnation
  let exit
  let text = ''
  term.onData(data => { text += data })
  term.onExit(event => { event.code = 999; throw new Error('consumer failure') })
  term.onExit(event => { exit = event })
  const read = name => {
    const state = term.inspect()
    if (name === 'started') return state.target ? { owner: state.owner, target: state.target } : undefined
    if (name === 'target-exit') return state.exit || undefined
    if (name === 'scope-ended') return state.phase === 'Closed' ? { incarnation, targetExit: state.exit } : undefined
    if (name === 'signal-applied') return state.forwardedSequence ? { sequence: state.forwardedSequence } : undefined
    if (name === 'fault') return state.failure || undefined
  }
  const scope = {
    term, read,
    exited: () => exit,
    text: () => text,
    signal(signal) {
      term.signal(signal)
    },
    async ended() {
      await waitFor(() => read('scope-ended') && exit, `scope exit (${text})`)
      assert.equal(read('scope-ended').incarnation, incarnation)
      return read('scope-ended')
    },
  }
  opened.push(scope)
  await waitFor(() => read('started') || read('fault'), `scope startup (${text})`)
  assert.equal(read('fault'), undefined)
  assert.equal(read('started').owner, term.pid)
  return scope
}

const mode = process.argv[2]
try {
  if (mode === 'stalled-owner' || mode === 'stalled-anchor') {
    const scope = await open('printf STALL_READY; exec sleep 60')
    await waitFor(() => scope.text().includes('STALL_READY'), 'stall target readiness')
    const target = scope.read('started').target
    const anchor = anchorFor(scope)
    const paused = mode === 'stalled-owner' ? scope.term.pid : anchor
    const id = ptyId(`pty-${mode}`)
    const port = createPtyPort({ handler: async (_id, command) => {
      if (command.kind === 'Signal') scope.term.signal(command.signal)
      return { ok: true }
    } })
    portFork(port, 'unused', 'devops', id, undefined)
    supervisorAttach(supervisorCreate(), port, id, scope.term)
    const completions = []
    portAddMailboxSender(port, value => completions.push(value))
    try {
      process.kill(paused, 'SIGSTOP')
      await waitFor(() => memberRows(scope.term.pid).some(row => row.pid === paused && row.state.startsWith('T')), 'stopped supervisor')
      let drain
      const closing = portCloseAll(port, 100).then(() => { drain = 'resolved' }, error => { drain = error.message })
      await waitFor(() => drain, 'bounded Unknown from an unresponsive supervisor')
      await closing
      assert.match(drain, /PTY .*timed out/)
      assert.equal(scope.term.inspect().phase, 'Unknown')
      assert.equal(portExists(port, id), true)
      assert.equal(scope.exited(), undefined)
      assert.deepEqual(completions, [])
      assert.equal(alive(target), true)
      if (mode === 'stalled-anchor') assert.ok(scope.term.inspect().forwardedSequence > 0)
      console.log(JSON.stringify({ mode, boundedUnknown: true, portRetained: true, completions: 0 }))
    } finally {
      if (alive(paused)) process.kill(paused, 'SIGCONT')
      await waitFor(() => !alive(scope.term.pid) && !alive(target) && !alive(anchor), 'stalled scope cleanup')
    }
  } else if (mode === 'direct-stop') {
    const scope = await open('printf DIRECT_READY; exec sleep 60')
    await waitFor(() => scope.text().includes('DIRECT_READY'), 'direct foreground readiness')
    const target = scope.read('started').target
    const anchor = anchorFor(scope)
    try {
      scope.term.write('\x1a')
      await waitFor(() => memberRows(scope.term.pid).some(row => row.pid === target && row.state.startsWith('T')), 'direct target Ctrl-Z')
      assert.equal(memberRows(scope.term.pid).find(row => row.pid === anchor).state.startsWith('T'), false, 'the group supervisor must remain runnable')
      scope.signal('SIGKILL')
      const ended = await scope.ended()
      assert.deepEqual(ended.targetExit, { code: null, signal: 'SIGKILL' })
      assert.equal(alive(target), false)
      assert.equal(alive(anchor), false)
      console.log(JSON.stringify({ mode, targetStopped: true, anchorRunnable: true, killed: true }))
    } finally {
      if (!scope.exited()) {
        if (alive(anchor)) process.kill(anchor, 'SIGCONT')
        scope.signal('SIGKILL')
        await scope.ended()
      }
    }
  } else if (mode === 'native-status') {
    for (const signal of ['SIGUSR1', 'SIGUSR2', 'SIGQUIT', 'SIGTERM', 'SIGHUP', 'SIGINT', 'SIGKILL']) {
      const scope = await open('ulimit -c 0; printf SIGNAL_READY; exec sleep 60')
      await waitFor(() => scope.text().includes('SIGNAL_READY'), `${signal} readiness`)
      scope.signal(signal)
      assert.deepEqual((await scope.ended()).targetExit, { code: null, signal })
    }
    for (const code of [0, 130, 158, 159, 255]) {
      const scope = await open(`exit ${code}`)
      assert.deepEqual((await scope.ended()).targetExit, { code, signal: null })
    }
    console.log(JSON.stringify({ mode, nativeSignals: 7, distinctExitCodes: 5 }))
  } else if (mode === 'compiled-host') {
    const expected = process.argv[3]
    if (expected === 'unset') delete process.env.BUN_BE_BUN
    else process.env.BUN_BE_BUN = expected
    const key = Buffer.from('BUN_BE_BUN\0')
    const restored = expected === 'unset'
      ? native.unsetenv(key)
      : native.setenv(key, Buffer.from(`${expected}\0`), 1)
    assert.equal(restored, 0)
    const before = process.env.BUN_BE_BUN
    process.env.WXS_PTY_ENV_PROBE = 'preserved-value'
    const scope = await open('test -t 0 && test -t 1 && test -t 2 && printf "MODE:%s\\nSENTINEL:%s\\n" "${BUN_BE_BUN-unset}" "$WXS_PTY_ENV_PROBE"; exit 37')
    const ended = await scope.ended()
    assert.deepEqual(ended.targetExit, { code: 37, signal: null })
    assert.match(scope.text(), new RegExp(`MODE:${expected}`))
    assert.match(scope.text(), /SENTINEL:preserved-value/)
    assert.equal(process.env.BUN_BE_BUN, before)
    console.log(JSON.stringify({ mode, compiledHost: true, environmentRestored: true, bun: process.versions.bun, exit: ended.targetExit }))
  } else if (mode === 'basic') {
    const scope = await open('test -t 0 && test -t 1 && test -t 2 && printf TTY_OK; read -r value; printf "INPUT:%s\\n" "$value"; stty size; exit 37')
    await waitFor(() => scope.text().includes('TTY_OK'), 'TTY check')
    scope.term.resize(91, 33)
    scope.term.write('plain input\n')
    const ended = await scope.ended()
    assert.match(scope.text(), /INPUT:plain input/)
    assert.match(scope.text(), /33 91/)
    assert.deepEqual(ended.targetExit, { code: 37, signal: null })
    console.log(JSON.stringify({ mode, tty: true, input: true, resize: true, exit: ended.targetExit }))
  } else if (mode === 'leader-kill' || mode === 'leader-natural') {
    const markerDirectory = mkdtempSync(join(tmpdir(), 'wxs-pty-descendant-'))
    const marker = join(markerDirectory, 'child')
    const release = join(markerDirectory, 'release')
    const child = `const fs = require('node:fs'); process.on('SIGTERM', () => {}); process.on('SIGHUP', () => {}); fs.writeFileSync(${JSON.stringify(marker)}, String(process.pid)); setInterval(() => {if(fs.existsSync(${JSON.stringify(release)})) process.exit(0)}, 5)`
    const parent = `const fs = require('node:fs'); require('node:child_process').spawn(process.execPath, ['-e', ${JSON.stringify(child)}], {stdio:'ignore'}); setInterval(() => {if(fs.existsSync(${JSON.stringify(marker)})) process.exit(23)}, 5)`
    const scope = await open(`exec ${quote(process.execPath)} -e ${quote(parent)}`)
    const sibling = await open('read -r value; exit 0')
    await waitFor(() => existsSync(marker) && readFileSync(marker, 'utf8').length > 0, 'descendant readiness')
    const pid = Number(readFileSync(marker, 'utf8'))
    targetPids.push(pid)
    await waitFor(() => scope.read('target-exit'), 'target exit receipt')
    assert.equal(alive(pid), true)
    assert.equal(scope.read('scope-ended'), undefined)
    assert.equal(scope.exited(), undefined)
    assert.equal(alive(sibling.term.pid), true)
    if (mode === 'leader-kill') {
      scope.signal('SIGTERM')
      await waitFor(() => scope.read('signal-applied')?.sequence === 1, 'TERM receipt')
      assert.equal(alive(pid), true)
      assert.equal(scope.read('scope-ended'), undefined)
      scope.signal('SIGKILL')
    } else writeFileSync(release, 'release')
    const ended = await scope.ended()
    await waitFor(() => !alive(pid), 'descendant exit')
    assert.deepEqual(ended.targetExit, { code: 23, signal: null })
    assert.equal(alive(sibling.term.pid), true)
    sibling.term.write('done\n')
    await sibling.ended()
    rmSync(markerDirectory, { recursive: true })
    console.log(JSON.stringify({ mode, retainedAfterTargetExit: true, descendantExited: true, siblingSurvived: true, exit: ended.targetExit }))
  } else if (mode === 'churn') {
    const markerDirectory = mkdtempSync(join(tmpdir(), 'wxs-pty-churn-'))
    const marker = join(markerDirectory, 'long-job')
    const shortMarker = join(markerDirectory, 'short-jobs')
    const script = `sleep 60 & long=$!; printf '%s' "$long" > ${quote(`${marker}.tmp`)}; mv ${quote(`${marker}.tmp`)} ${quote(marker)}; for value in 1 2 3 4 5 6 7 8 9 10 11 12; do sleep 0.02 & printf '%s\\n' "$!" >> ${quote(shortMarker)}; done; printf CHURN_READY; wait`
    const scope = await open(`exec sh -mc ${quote(script)}`)
    await waitFor(() => scope.text().includes('CHURN_READY'), 'short job startup')
    const pid = Number(readFileSync(marker, 'utf8'))
    targetPids.push(pid)
    const shortPids = readFileSync(shortMarker, 'utf8').trim().split('\n').map(Number)
    assert.equal(shortPids.length, 12)
    assert.ok(shortPids.every(pid => Number.isInteger(pid) && pid > 1))
    targetPids.push(...shortPids)
    scope.signal('SIGKILL')
    const ended = await scope.ended()
    await waitFor(() => !alive(pid), 'long job cleanup')
    assert.deepEqual(ended.targetExit, { code: null, signal: 'SIGKILL' })
    rmSync(markerDirectory, { recursive: true })
    console.log(JSON.stringify({ mode, shortGroupRace: true, allExited: true, exit: ended.targetExit }))
  } else if (mode === 'job-control') {
    const scope = await open('exec sh -i')
    scope.term.write('printf "%s\\n" "READY_$((4+3))"\n')
    await waitFor(() => scope.text().includes('READY_7'), 'interactive shell')
    scope.term.write('sleep 60\n')
    await delay(80)
    scope.term.write('\x1a')
    await waitFor(() => /[Ss]topped/.test(scope.text()), 'Ctrl-Z stopped job')
    scope.term.write('fg\n')
    await delay(80)
    scope.term.write('\x03')
    scope.term.write('printf "CTRL_STATUS:%s\\n" "$?"\n')
    await waitFor(() => /CTRL_STATUS:130/.test(scope.text()), `Ctrl-C (${scope.text()})`)
    const markerDirectory = mkdtempSync(join(tmpdir(), 'wxs-pty-job-'))
    const marker = join(markerDirectory, 'child')
    const child = `process.on('SIGHUP', () => {}); process.on('SIGTERM', () => {}); require('node:fs').writeFileSync(${JSON.stringify(marker)}, String(process.pid)); setInterval(() => {}, 1000)`
    scope.term.write(`${quote(process.execPath)} -e ${quote(child)} &\n`)
    await waitFor(() => existsSync(marker) && readFileSync(marker, 'utf8').length > 0, 'background job readiness')
    const pid = Number(readFileSync(marker, 'utf8'))
    targetPids.push(pid)
    const target = scope.read('started').target
    assert.notEqual(native.getpgid(pid), native.getpgid(target), 'shell must create a distinct job group')
    scope.term.write('exit 29\n')
    await waitFor(() => scope.read('target-exit'), 'interactive shell exit')
    assert.equal(alive(pid), true)
    assert.equal(scope.read('scope-ended'), undefined)
    scope.signal('SIGTERM')
    await waitFor(() => scope.read('signal-applied')?.sequence === 1, 'background TERM receipt')
    assert.equal(alive(pid), true)
    scope.signal('SIGKILL')
    const ended = await scope.ended()
    await waitFor(() => !alive(pid), 'background job exit')
    assert.deepEqual(ended.targetExit, { code: 29, signal: null })
    rmSync(markerDirectory, { recursive: true })
    console.log(JSON.stringify({ mode, ctrlZ: true, foregroundResume: true, ctrlC: true, backgroundGroupExited: true, exit: ended.targetExit }))
  } else throw new Error(`invalid probe mode ${mode}`)
} finally {
  for (const scope of opened) {
    if (scope.term.inspect().phase === 'Unknown' && !alive(scope.term.pid)) continue
    if (!scope.exited()) {
      scope.signal('SIGKILL')
      await scope.ended()
    }
    assert.equal(alive(scope.term.pid), false, 'the physical owner remains alive after its exit callback')
    const target = scope.read('started').target
    assert.equal(alive(target), false, 'the target remains alive after scope completion')
  }
  for (const pid of targetPids) assert.equal(alive(pid), false, `test process ${pid} remains alive`)
}
