import assert from 'node:assert/strict'
import test from 'node:test'
import { spawn } from 'node:child_process'
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { NativeTestSupervision, validatePhysicalWait } from './native-test-supervision.mjs'
import { createFiniteWaitChannel } from './finite-wait-channel.mjs'

const token = 'a'.repeat(32)
const file = resolve('finite-a.test.mjs')
const other = resolve('finite-b.test.mjs')
const native = (type, sequence, entryFile = file, name = 'probe', extra = {}) => ({ type,
  data: { entryFile, file: entryFile, name, line: 1, column: 1, nesting: 0, nativeSequence: sequence, ...extra } })
const begin = (extra = {}) => ({ entryFile: file, runId: 'run-a', requestId: 1,
  phase: 'begin', contextToken: token, operation: 'bounded process drain', limitMs: 1000,
  name: 'probe', fullName: 'probe', ...extra })

function model(files = [file]) {
  let time = 0
  const owner = new NativeTestSupervision({ files, silenceMs: 100, now: () => time })
  const start = (entryFile = file) => owner.observe({ type: 'runner:file-start',
    data: { entryFile, runId: entryFile === file ? 'run-a' : 'run-b' } })
  start()
  return { owner, start, at(value) { time = value } }
}

async function actual(bodies, { silenceMs = 600, expected = 0, concurrency = 1, diagnosticMarkers = [], entryPrelude = true } = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'finite-wait-proof-'))
  try {
    const helper = new URL('./finite-physical-wait.mjs', import.meta.url).href
    const supervisor = new URL('../e2e/support/supervise-node-test.mjs', import.meta.url).href
    const files = bodies.map((body, index) => {
      const entry = join(directory, `probe-${index}.mjs`)
      const prelude = entryPrelude ? `import test from 'node:test'
import assert from 'node:assert/strict'
import { setTimeout as delay } from 'node:timers/promises'
import { withFinitePhysicalWait } from ${JSON.stringify(helper)}
` : ''
      writeFileSync(entry, prelude + body + '\n')
      return entry
    })
    const launcher = join(directory, 'launch.mjs')
    writeFileSync(launcher, `import { superviseNodeTest } from ${JSON.stringify(supervisor)}
await superviseNodeTest({files:${JSON.stringify(files)},label:'finite-proof',silenceMs:${silenceMs}})\n`)
    const env = { ...process.env, NODE_TEST_CONCURRENCY: String(concurrency) }
    delete env.NODE_TEST_CONTEXT
    const child = spawn(process.execPath, [launcher], { env, stdio: ['ignore', 'pipe', 'pipe'] })
    let output = ''
    const channels = { stdout: '', stderr: '' }
    for (const [name, stream] of [['stdout', child.stdout], ['stderr', child.stderr]]) {
      stream.setEncoding('utf8')
      stream.on('data', text => {
        output += text
        channels[name] += text
      })
    }
    const timer = setTimeout(() => child.kill('SIGKILL'), 12000)
    const exit = await new Promise((accept, reject) => {
      child.once('error', reject)
      child.once('exit', (code, signal) => accept({ code, signal }))
    }).finally(() => clearTimeout(timer))
    assert.equal(exit.signal, null, output)
    assert.equal(exit.code, expected, output)
    assert.match(output, /accepted=true/, output)
    for (const marker of diagnosticMarkers) {
      assert.ok(channels.stderr.includes(marker), output)
      assert.equal(channels.stdout.includes(marker), false, output)
    }
    return output
  } finally { rmSync(directory, { recursive: true, force: true }) }
}

async function ackBoundary(mode) {
  const directory = mkdtempSync(join(tmpdir(), 'finite-ack-proof-'))
  const entryFile = fileURLToPath(new URL('./fixtures/finite-wait-ack.fixture.mjs', import.meta.url))
  const marker = join(directory, 'begin')
  const evidence = join(directory, 'evidence')
  const requests = []
  const errors = []
  const late = []
  let channel
  try {
    channel = await createFiniteWaitChannel({ entryFile, onError: error => errors.push(error),
      onRequest: request => {
        requests.push(request.phase)
        if (request.phase === 'cancel') return
        if (request.phase === 'end') {
          if (mode !== 'end-lost') channel.reply({ ...request, ok: true })
        } else {
          writeFileSync(marker, '')
          if (mode === 'refused') channel.reply({ ...request, ok: false, reason: 'controlled refusal' })
          else if (mode === 'wrong-instance') channel.reply({ ...request, runId: 'wrong', ok: true })
          else if (mode === 'malformed') channel.reply({ ...request, ok: 'true' })
          else if (mode === 'deadline-late' || mode === 'context-closed-late') {
            late.push(new Promise(accept => setTimeout(() => {
              channel.reply({ ...request, ok: true })
              accept()
            }, mode === 'deadline-late' ? 1200 : 250)))
          } else if (mode !== 'lost') channel.reply({ ...request, ok: true })
        }
      } })
    const preload = new URL('./verdict-transport.mjs', import.meta.url)
    for (const [key, value] of Object.entries({ ...channel.descriptor, runId: channel.runId, workerParentPid: process.pid })) {
      preload.searchParams.set(key, value)
    }
    const env = { ...process.env, WXS_FINITE_ACK_MODE: mode,
      WXS_FINITE_ACK_MARKER: marker, WXS_FINITE_ACK_EVIDENCE: evidence }
    delete env.NODE_TEST_CONTEXT
    const child = spawn(process.execPath, [`--import=${preload.href}`, entryFile], {
      env, stdio: ['ignore', 'pipe', 'pipe'],
    })
    let output = ''
    for (const stream of [child.stdout, child.stderr]) {
      stream.setEncoding('utf8')
      stream.on('data', value => { output += value })
    }
    const timer = setTimeout(() => child.kill('SIGKILL'), 5000)
    const outcome = await new Promise((accept, reject) => {
      child.once('error', reject)
      child.once('close', (code, signal) => accept({ code, signal }))
    }).finally(() => clearTimeout(timer))
    await Promise.all(late)
    assert.deepEqual(outcome, { code: 0, signal: null }, output)
    assert.deepEqual(errors, [])
    assert.equal(requests.filter(phase => phase === 'begin').length, 1)
    assert.equal(requests.filter(phase => phase === 'end').length, ['positive', 'end-lost'].includes(mode) ? 1 : 0)
    assert.equal(JSON.parse(readFileSync(evidence, 'utf8')).callbacks, ['positive', 'end-lost'].includes(mode) ? 1 : 0)
  } finally {
    if (channel) await channel.close()
    rmSync(directory, { recursive: true, force: true })
  }
}

export function registerFiniteWaitTests() {
  for (const mode of ['positive', 'refused', 'lost', 'deadline-late', 'context-closed-late', 'end-lost', 'wrong-instance', 'malformed']) {
    test(`WHAT[verification-system-006] the actual physical wait ACK boundary preserves execution and single send (${mode})`,
      () => ackBoundary(mode))
  }
  test('WHAT[verification-system-006] finite physical waits preserve every other file deadline', () => {
    const m = model([file, other])
    m.start(other)
    m.owner.observe(native('test:dequeue', 1))
    assert.equal(m.owner.control(begin())[0].ok, true)
    assert.deepEqual(m.owner.nextDeadline(), { at: 100, file: other, kind: 'silence' })
    m.at(99)
    m.owner.observe(native('test:complete', 1, other, 'other progress'))
    assert.equal(m.owner.nextDeadline().at, 199)
    m.at(199)
    assert.throws(() => m.owner.control(begin({ phase: 'end' })), /deadline expired/)
    assert.throws(() => m.owner.observe(native('test:complete', 2)), /deadline expired/)
  })

  test('WHAT[verification-system-006] finite end does not renew or release its absolute deadline', () => {
    const m = model()
    m.owner.observe(native('test:dequeue', 1))
    assert.equal(m.owner.control(begin())[0].ok, true)
    m.at(900)
    assert.equal(m.owner.control(begin({ phase: 'end' }))[0].ok, true)
    assert.equal(m.owner.nextDeadline().at, 1000)
    assert.equal(m.owner.control(begin({ phase: 'end' }))[0].ok, false)
    m.at(999)
    m.owner.observe(native('test:complete', 2))
    assert.equal(m.owner.nextDeadline().at, 1099)
    m.owner.observe(native('test:dequeue', 3))
    assert.equal(m.owner.control(begin())[0].ok, false)
    assert.equal(m.owner.nextDeadline().at, 1099)
  })

  test('WHAT[verification-system-006] every late transition loses before delayed timer dispatch', () => {
    for (const event of ['begin', 'end', 'complete', 'drain', 'summary', 'exit']) {
      const m = model()
      m.owner.observe(native('test:dequeue', 1))
      if (event !== 'begin') m.owner.control(begin())
      m.at(event === 'begin' ? 100 : 1000)
      const transition = event === 'begin' || event === 'end'
        ? () => m.owner.control(begin({ phase: event }))
        : () => m.owner.observe(event === 'complete' ? native('test:complete', 2)
          : { type: event === 'drain' ? 'runner:file-drained' : `runner:${event}`, data: { entryFile: file } })
      assert.throws(transition, /deadline expired/, event)
      m.at(0)
      assert.throws(() => m.owner.check(), /deadline expired/, 'expiry must latch')
    }
  })

  test('WHAT[verification-system-006] native completion requires end and a single real occurrence', () => {
    const m = model()
    m.owner.observe(native('test:dequeue', 1))
    m.owner.control(begin())
    assert.throws(() => m.owner.observe(native('test:complete', 2)), /without its end/)
    const concurrent = model()
    concurrent.owner.observe(native('test:dequeue', 1))
    concurrent.owner.observe(native('test:dequeue', 2))
    assert.equal(concurrent.owner.control(begin())[0].ok, false)
    const nested = model()
    nested.owner.observe(native('test:dequeue', 1, file, 'probe', { nesting: 1 }))
    assert.equal(nested.owner.control(begin())[0].ok, false)
    const granted = model()
    granted.owner.observe(native('test:dequeue', 1))
    granted.owner.control(begin())
    assert.throws(() => granted.owner.observe(native('test:dequeue', 2, file, 'other')), /Another native/)
  })

  test('WHAT[verification-system-006] delayed dequeue binding cannot give pending requests provisional time', () => {
    const m = model()
    assert.deepEqual(m.owner.control(begin()), [])
    assert.equal(m.owner.nextDeadline().at, 100)
    assert.deepEqual(m.owner.control(begin({ phase: 'cancel' })), [])
    assert.deepEqual(m.owner.observe(native('test:dequeue', 1)), [])
    assert.equal(m.owner.control(begin())[0].ok, false)
    const ready = model()
    assert.deepEqual(ready.owner.control(begin()), [])
    ready.at(20)
    assert.equal(ready.owner.observe(native('test:dequeue', 1))[0].ok, true)
    assert.equal(ready.owner.nextDeadline().at, 1020)
  })

  test('WHAT[verification-system-006] only native complete advances once despite nested testNumber reuse', () => {
    const m = model()
    m.owner.observe(native('test:dequeue', 1))
    m.at(90)
    m.owner.observe(native('test:pass', undefined))
    assert.equal(m.owner.nextDeadline().at, 100)
    m.owner.observe(native('test:complete', 2, file, 'probe', { testNumber: 1 }))
    assert.equal(m.owner.nextDeadline().at, 190)
    m.at(100)
    m.owner.observe(native('test:dequeue', 3, file, 'child', { nesting: 1 }))
    m.owner.observe(native('test:complete', 4, file, 'child', { nesting: 1, testNumber: 1 }))
    assert.equal(m.owner.nextDeadline().at, 200)
    assert.throws(() => m.owner.observe(native('test:complete', 4)), /repeated or out of order/)
    for (const limit of [0, -1, 30001, 1.5, Infinity]) assert.throws(() => validatePhysicalWait('probe', limit))
    assert.throws(() => validatePhysicalWait('', 100))
  })

  test('WHAT[verification-system-006] a real finite operation may outlast ordinary silence without permitting reuse', async () => {
    await actual([`test('physical 界', async t => {
  await withFinitePhysicalWait(t, {operation:'physical 界',limitMs:2000}, () => delay(900))
  let calls = 0
  await assert.rejects(withFinitePhysicalWait(t, {operation:'again',limitMs:2000}, () => {calls++}), /already declared/)
  assert.equal(calls, 0)
})`])
  })

  test('WHAT[verification-system-006] a real pending physical operation expires at its own hard deadline', async () => {
    const output = await actual([`test('never ends', async t => {
  await withFinitePhysicalWait(t, {operation:'bounded physical never',limitMs:1000}, async () => {
    setInterval(() => console.error('ordinary noise'), 50)
    await new Promise(() => {})
  })
})`], { expected: 1 })
    assert.match(output, /"kind":"physical"/)
  })

  test('WHAT[verification-system-006] ending a real operation cannot exempt a later hung body', async () => {
    const output = await actual([`test('end then hang', async t => {
  await withFinitePhysicalWait(t, {operation:'finite first',limitMs:1000}, () => delay(50))
  setInterval(() => {}, 50)
  await new Promise(() => {})
})`], { expected: 1 })
    assert.match(output, /"kind":"physical"/)
  })

  test('WHAT[verification-system-006] one real file allowance cannot mask another silent file', async () => {
    const output = await actual([
      `test('allowed', t => withFinitePhysicalWait(t, {operation:'physical wait',limitMs:2000}, () => delay(1500)))`,
      `test('unallowed', async () => {
  setInterval(() => {}, 50)
  await new Promise(() => {})
})`,
    ], { expected: 1, concurrency: 2 })
    assert.match(output, /probe-1\.mjs/)
    assert.match(output, /"kind":"silence"/)
  })

  test('WHAT[verification-system-006] a closed unused context cannot claim a later same-name native invocation', async () => {
    await actual([`let old
test('same', t => {old = t})
test('same', async t => {
  let calls = 0
  await assert.rejects(withFinitePhysicalWait(old, {operation:'stale',limitMs:1000}, () => {calls++}))
  const fake = {name:t.name,fullName:t.fullName,signal:t.signal}
  await assert.rejects(withFinitePhysicalWait(fake, {operation:'forged',limitMs:1000}, () => {calls++}), /registered TestContext/)
  assert.equal(calls, 0)
  await withFinitePhysicalWait(t, {operation:'current',limitMs:2000}, () => delay(900))
})`])
  })

  test('WHAT[verification-system-006] nested tests remain ordinary and cannot obtain a finite exception', async () => {
    await actual([`test('parent', async t => {
  await t.test('nested', async context => {
    let calls = 0
    await assert.rejects(withFinitePhysicalWait(context, {operation:'nested',limitMs:1000}, () => {calls++}), /unique ordinary top-level/)
    assert.equal(calls, 0)
  })
})
test('later ordinary leaf', () => assert.equal(1, 1))`])
  })

  test('WHAT[verification-system-006] a synchronous verdict burst cannot block the next real native admission', async () => {
    await actual([`for (let index = 0; index < 12; index += 1) {
  test('synchronous ordinary ' + index, () => assert.equal(1, 1))
}
for (let index = 0; index < 4; index += 1) {
  test('physical after burst ' + index, async t => {
    let calls = 0
    await withFinitePhysicalWait(t, {operation:'physical after burst',limitMs:2000}, () => {calls++})
    assert.equal(calls, 1)
  })
}`])
  })

  test('WHAT[verification-system-006] a drained file releases its real control socket before the next entry', async () => {
    const imports = `import { writeFileSync, readFileSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
const marker = join(dirname(process.argv[1]), 'previous-socket')
`
    await actual([
      imports + `test('first file socket', () => {
  const argument = process.execArgv.find(value => value.startsWith('--import=') && value.includes('verdict-transport.mjs?'))
  const socket = new URL(argument.slice(9)).searchParams.get('path')
  assert.equal(existsSync(socket), true)
  writeFileSync(marker, socket)
})`,
      imports + `test('second file checks drain', () => {
  assert.equal(existsSync(readFileSync(marker, 'utf8')), false)
})`,
    ])
  })

  test('WHAT[verification-system-006] real body diagnostics remain observable without entering the native protocol stream', async () => {
    await actual([`import { stdout } from 'node:process'
import canonicalConsole, { log } from 'node:console'
test('ordinary diagnostic body', () => {
  process.stdout.write('body-string-diagnostic\\n')
  process.stdout.write(Buffer.from('body-buffer-diagnostic\\n'))
  stdout.write('body-named-stream-diagnostic\\n')
  console.log('body-global-console-diagnostic')
  canonicalConsole.log('body-default-console-diagnostic')
  log('body-named-console-diagnostic')
})
test('physical after ordinary diagnostics', async t => {
  await withFinitePhysicalWait(t, {operation:'physical after body diagnostics',limitMs:2000}, () => delay(900))
})`], { diagnosticMarkers: ['body-string-diagnostic', 'body-buffer-diagnostic', 'body-named-stream-diagnostic',
      'body-global-console-diagnostic', 'body-default-console-diagnostic', 'body-named-console-diagnostic'] })
  })

  test('WHAT[verification-system-006] inherited entry transport leaves another physical process stdout and stderr intact', async () => {
    await actual([`import assert from 'node:assert/strict'
import { fork } from 'node:child_process'
import { writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
if (process.argv[2] === 'ordinary-child') {
  assert.equal(process.stdout.fd, 1)
  assert.equal(process.stderr.fd, 2)
  process.stdout.write('ordinary-stdout\\n')
  process.stderr.write('ordinary-stderr\\n')
} else {
  const { default: test } = await import('node:test')
  test('same entry fork keeps ordinary streams', async () => {
    const other = join(dirname(process.argv[1]), 'ordinary.mjs')
    writeFileSync(other, "process.stdout.write('ordinary-stdout\\\\n')\\nprocess.stderr.write('ordinary-stderr\\\\n')\\n")
    for (const file of [process.argv[1], other]) {
      const child = fork(file, ['ordinary-child'], { silent: true })
      let stdout = ''
      let stderr = ''
      child.stdout.setEncoding('utf8')
      child.stderr.setEncoding('utf8')
      child.stdout.on('data', value => {stdout += value})
      child.stderr.on('data', value => {stderr += value})
      const outcome = await new Promise((accept, reject) => {
        child.once('error', reject)
        child.once('close', (code, signal) => accept({code, signal}))
      })
      assert.deepEqual(outcome, {code:0,signal:null})
      assert.equal(stdout, 'ordinary-stdout\\n')
      assert.equal(stderr, 'ordinary-stderr\\n')
    }
  })
}`], { entryPrelude: false })
  })
}
