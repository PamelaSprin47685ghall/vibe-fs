import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync, chmodSync, mkdirSync, readFileSync, readdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, relative } from 'node:path'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { create, createWithRunner, run } from '../../../dist/Persistence/EventStore/GitRawSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const ok = result => {
  assert.equal(result.ok, true, result.error?.description ?? 'expected a successful storage receipt')
  return result.value
}
const failed = result => {
  assert.equal(result.ok, false, 'expected a storage failure')
  return result.error
}
const observation = (ending, stdout = Buffer.alloc(0), changes = {}) => ({
  kind: 'Observed', ending, stopRequested: changes.stop ?? null,
  output: { stdout, stderr: Buffer.from('diagnostic'), complete: changes.complete ?? true },
  fault: changes.fault ?? null,
})
const exit = exitCode => ({ kind: 'Exited', exitCode })
const repository = t => {
  const path = mkdtempSync(join(tmpdir(), 'wxs-binary-git-'))
  execFileSync('git', ['init', '--bare', '--quiet', path])
  t.after(() => rmSync(path, { recursive: true, force: true }))
  return path
}
const oid = 'a'.repeat(40)
const inputBytes = size => {
  const bytes = Buffer.alloc(size)
  let state = 0x6d2b79f5
  for (let index = 0; index < bytes.length; index++) {
    state ^= state << 13
    state ^= state >>> 17
    state ^= state << 5
    bytes[index] = state & 255
  }
  return bytes
}

for (const [name, receipt] of [
  ['unknown', observation({ kind: 'Unconfirmed' }, Buffer.from([0, 255, 10]), { fault: 'lost receipt' })],
  ['signal', observation({ kind: 'Signaled', signal: 'SIGTERM' })],
  ['deadline', observation(exit(0), Buffer.from('written'), { stop: { kind: 'DeadlineStopRequested', durationMs: 100 } })],
  ['partial', observation(exit(0), Buffer.from('partial'), { complete: false })],
  ['normal failure', observation(exit(128), Buffer.from('failed bytes'))],
]) {
  test(`WHAT[process-execution-017] ${name} reads stay errors and unknown writes are sent once`, async () => {
    const calls = []
    const store = createWithRunner('/unused', async request => {
      calls.push(request)
      return receipt
    })
    for (const read of [() => store.readObject(oid), () => store.readTree(oid), () => store.readRef('refs/wanxiang/store')]) {
      const before = calls.length
      const result = failed(await read()).result
      assert.deepEqual(result.ending, receipt.ending)
      assert.deepEqual(result.stopRequested, receipt.stopRequested)
      assert.equal(result.fault, receipt.fault)
      assert.deepEqual(Buffer.from(result.output.stdout), receipt.output.stdout)
      assert.equal(calls.length, before + 1)
    }
    const before = calls.length
    const failure = failed(await store.writeBlob(Buffer.from([0, 255, 128])))
    assert.deepEqual(failure.result.ending, receipt.ending)
    assert.match(failure.description, /diagnostic/)
    assert.equal(calls.length, before + 1)
    assert.deepEqual(Buffer.from(calls.at(-1).input), Buffer.from([0, 255, 128]))
  })
}

test('WHAT[process-execution-017] a thrown runner is unconfirmed and cannot fall back to another command', async () => {
  let calls = 0
  const store = createWithRunner('/unused', async () => {
    calls++
    throw new Error('transport callback missing')
  })
  const failure = failed(await store.writeBlob(Buffer.from('one write')))
  assert.equal(failure.result.kind, 'Observed')
  assert.equal(failure.result.ending.kind, 'Unconfirmed')
  assert.match(failure.result.fault, /transport callback missing/)
  assert.equal(calls, 1)
})

test('WHAT[process-execution-017] native Git nonzero preserves its status and both binary channels', async t => {
  const repo = repository(t)
  const command = "!printf '\\377\\000A\\376\\200\\n'; printf 'stderr-data' >&2; exit 7"
  const observed = await run(repo, 5000, ['-c', `alias.d2-fail=${command}`, 'd2-fail'], undefined, 1024)
  assert.equal(observed.kind, 'Observed')
  assert.deepEqual(observed.ending, exit(7))
  assert.equal(observed.output.complete, true)
  assert.equal(observed.fault, null)
  assert.deepEqual(Buffer.from(observed.output.stdout), Buffer.from([255, 0, 65, 254, 128, 10]))
  assert.equal(Buffer.from(observed.output.stderr).toString(), 'stderr-data')
  const noWorktree = await run(repo, 5000, ['rev-parse', '--show-toplevel'], undefined, 4096)
  assert.deepEqual(noWorktree.ending, exit(128))
  assert.ok(noWorktree.output.stderr.length > 0)
})

test('WHAT[process-execution-017] draining stdin while echoing cannot send EOF before the final target write', async t => {
  const repo = repository(t)
  const witness = join(repo, 'target-input')
  const bytes = inputBytes(2097169)
  bytes.set([239, 187, 191], 0)
  const command = '!f() { tee "$1"; printf "diagnostic\\n" >&2; exit 7; }; f'
  const result = await run(repo, 5000, ['-c', `alias.d2-echo=${command}`, 'd2-echo', witness], bytes, bytes.length + 64)
  assert.deepEqual(result.ending, exit(7))
  assert.equal(result.stopRequested, null)
  assert.equal(result.fault, null)
  assert.equal(result.output.complete, true)
  const actualInput = readFileSync(witness)
  assert.equal(actualInput.length, bytes.length)
  assert.equal(createHash('sha256').update(actualInput).digest('hex'), createHash('sha256').update(bytes).digest('hex'))
  assert.deepEqual(Buffer.from(result.output.stdout), bytes)
  assert.equal(Buffer.from(result.output.stderr).toString(), 'diagnostic\n')
})

test('WHAT[process-execution-017] missing objects remain errors while exact missing refs return None', async t => {
  const repo = repository(t)
  const store = create(repo)
  for (const result of [await store.readObject(oid), await store.readTree(oid)]) {
    const error = failed(result)
    assert.deepEqual(error.result.ending, exit(0))
    assert.equal(Buffer.from(error.result.output.stdout).toString(), `${oid} missing\n`)
    assert.match(error.reason, /absence was not confirmed/)
  }
  assert.equal(ok(await store.readRef('refs/wanxiang/store')), null)
  writeFileSync(join(repo, 'refs', 'heads', 'broken'), 'not-an-object-id\n')
  failed(await store.readRef('refs/heads/broken'))
  failed(await store.readRef('--all'))
  let calls = 0
  const forbidden = createWithRunner(repo, async () => {
    calls++
    throw new Error('invalid input must not reach Git')
  })
  for (const name of ['', '.', '..', 'a/b', 'a\0b', '\ud800']) {
    failed(await forbidden.writeTree([{ mode: '100644', name, oid }]))
  }
  failed(await forbidden.writeTree([{ mode: '100644', name: 'same', oid }, { mode: '100755', name: 'same', oid }]))
  failed(await forbidden.readObject('bad\nHEAD'))
  assert.equal(calls, 0)
  const emptyBlob = ok(await store.writeBlob(Buffer.alloc(0)))
  assert.equal(ok(await create(repo).readObject(emptyBlob)).length, 0)
  const emptyTree = ok(await store.writeTree([]))
  assert.deepEqual(ok(await create(repo).readTree(emptyTree)), [])
})

test('WHAT[process-execution-017] malformed metadata and truncated bodies never enter the object cache', async () => {
  for (const metadata of [`${oid} blob 5`, `${oid} blob -1\n`, `${oid} blob 5\nextra\n`, `${oid} blob 2147483648\n`, `b${oid.slice(1)} blob 5\n`]) {
    let calls = 0
    const store = createWithRunner('/unused', async () => {
      calls++
      return observation(exit(0), Buffer.from(metadata))
    })
    failed(await store.readObject(oid))
    failed(await store.readObject(oid))
    assert.equal(calls, 2)
  }
  let calls = 0
  const store = createWithRunner('/unused', async request => {
    calls++
    return observation(exit(0), Buffer.from(request.arguments.includes('--batch-check') ? `${oid} blob 99\n` : 'short'))
  })
  for (let attempt = 0; attempt < 2; attempt++) {
    const error = failed(await store.readObject(oid))
    assert.match(error.reason, /truncated or excess/)
    assert.deepEqual(error.result.ending, exit(0))
    assert.equal(Buffer.from(error.result.output.stdout).toString(), 'short')
  }
  assert.equal(calls, 4)
})

test('WHAT[process-execution-017] malformed tree records are rejected whole and never cached', async () => {
  const valid = `100644 blob ${oid}\tleaf\0`
  const malformed = [
    Buffer.from(valid.slice(0, -1)),
    Buffer.from(`100644 tree ${oid}\tleaf\0`),
    Buffer.from(`100644 blob ${oid}\ta/b\0`),
    Buffer.from(valid + valid),
    Buffer.concat([Buffer.from(`100644 blob ${oid}\t`), Buffer.from([255, 0])]),
    Buffer.alloc(0),
  ]
  for (const bytes of malformed) {
    let calls = 0
    const store = createWithRunner('/unused', async request => {
      calls++
      return observation(exit(0), request.arguments.includes('--batch-check') ? Buffer.from(`${oid} tree 32\n`) : bytes)
    })
    failed(await store.readTree(oid))
    failed(await store.readTree(oid))
    assert.equal(calls, 4)
  }
})

for (const size of [33554449, 67108864, 67108881]) {
  integrationTest(`WHAT[process-execution-017] ${size} arbitrary bytes survive real writes and an independent cold OS reader`, { timeout: 120000 }, async t => {
    const repo = repository(t)
    const store = create(repo)
    const bytes = Buffer.alloc(size)
    for (let index = 0; index < bytes.length; index++) bytes[index] = index % 251
    const written = ok(await store.writeBlob(bytes))
    const nativeOid = execFileSync('git', ['-C', repo, 'hash-object', '--stdin'], { input: bytes, maxBuffer: 1024 }).toString().trim()
    assert.equal(written, nativeOid)
    const expected = createHash('sha256').update(bytes).digest('hex')
    bytes.fill(0)
    const cold = execFileSync(process.execPath, ['--input-type=module', '-e', `
      import { createHash } from 'node:crypto';
      import { create } from ${JSON.stringify(new URL('../../../dist/Persistence/EventStore/GitRawSurface.js', import.meta.url).href)};
      const result = await create(process.argv[1]).readObject(process.argv[2]);
      if (!result.ok || result.value === null) throw new Error('cold read failed');
      console.log(JSON.stringify({size: result.value.length, hash: createHash('sha256').update(result.value).digest('hex')}));
    `, repo, nativeOid], { timeout: 60000, encoding: 'utf8' })
    assert.deepEqual(JSON.parse(cold), { size, hash: expected })
    const first = ok(await store.readObject(written))
    first.fill(0)
    const second = ok(await store.readObject(written))
    assert.equal(createHash('sha256').update(second).digest('hex'), expected)
  })
}

test('WHAT[process-execution-017] NUL-separated Git trees preserve Unicode tab LF and Git byte ordering', async t => {
  const repo = repository(t)
  const store = create(repo)
  const blob = ok(await store.writeBlob(Buffer.from([0, 255, 10])))
  const names = ['汉字-é.bin', 'tab\tname', 'line\nname', '\ue000', '\u{10000}']
  const tree = ok(await store.writeTree(names.map(name => ({ mode: '100644', name, oid: blob }))))
  const expected = [...names].sort((left, right) => Buffer.compare(Buffer.from(left), Buffer.from(right)))
  assert.deepEqual(ok(await store.readTree(tree)).map(entry => entry.name), expected)
  assert.deepEqual(ok(await create(repo).readTree(tree)).map(entry => entry.name), expected)
  const native = execFileSync('git', ['-C', repo, 'ls-tree', '-z', tree])
  assert.deepEqual(native.toString().split('\0').filter(Boolean).map(row => row.slice(row.indexOf('\t') + 1)), expected)
})

test('WHAT[process-execution-017] real object and ref permission failures never become missing data', async t => {
  const repo = repository(t)
  const store = create(repo)
  const blob = ok(await store.writeBlob(Buffer.from('unreadable object')))
  const objectPath = join(repo, 'objects', blob.slice(0, 2), blob.slice(2))
  chmodSync(objectPath, 0)
  try {
    assert.throws(() => readFileSync(objectPath), { code: 'EACCES' })
    const error = failed(await create(repo).readObject(blob))
    assert.equal(error.result.ending.kind, 'Exited')
    assert.match(error.description, /Permission denied/i)
  } finally {
    chmodSync(objectPath, 0o644)
  }
  const directory = join(repo, 'refs', 'wanxiang')
  const refPath = join(directory, 'store')
  mkdirSync(directory)
  writeFileSync(refPath, `${blob}\n`)
  assert.equal(ok(await store.readRef('refs/wanxiang/store')), blob)
  for (const path of [refPath, directory]) {
    chmodSync(path, 0)
    try {
      assert.throws(() => path === refPath ? readFileSync(path) : readdirSync(path), { code: 'EACCES' })
      const error = failed(await create(repo).readRef('refs/wanxiang/store'))
      assert.deepEqual(error.result.ending, exit(1))
      assert.match(error.description, /Permission denied/i)
    } finally {
      chmodSync(path, path === directory ? 0o755 : 0o644)
    }
  }
  writeFileSync(refPath, 'ref: refs/wanxiang/absent\n')
  failed(await store.readRef('refs/wanxiang/store'))
})

test('WHAT[process-execution-017] unreadable pack and index cannot masquerade as absent objects', async t => {
  const repo = repository(t)
  const blob = ok(await create(repo).writeBlob(Buffer.from('packed bytes')))
  execFileSync('git', ['-C', repo, 'update-ref', 'refs/wanxiang/packed', blob])
  execFileSync('git', ['-C', repo, 'repack', '-a', '-d'])
  const directory = join(repo, 'objects', 'pack')
  const files = readdirSync(directory).filter(name => /\.(pack|idx)$/.test(name))
  assert.equal(files.length, 2)
  for (const name of files) {
    const path = join(directory, name)
    chmodSync(path, 0)
    try {
      assert.throws(() => readFileSync(path), { code: 'EACCES' })
      const error = failed(await create(repo).readObject(blob))
      assert.deepEqual(error.result.ending, exit(0))
      assert.equal(Buffer.from(error.result.output.stdout).toString(), `${blob} missing\n`)
      assert.equal(error.result.output.stderr.length, 0)
    } finally {
      chmodSync(path, 0o444)
    }
    assert.equal(Buffer.from(ok(await create(repo).readObject(blob))).toString(), 'packed bytes')
  }
})

test('WHAT[process-execution-017] relative repository binding survives a later unrelated cwd', async t => {
  const repo = repository(t)
  const previous = process.cwd()
  const store = create(relative(previous, repo))
  try {
    process.chdir(tmpdir())
    const written = ok(await store.writeBlob(Buffer.from('bound repository')))
    assert.equal(execFileSync('git', ['-C', repo, 'cat-file', 'blob', written]).toString(), 'bound repository')
  } finally {
    process.chdir(previous)
  }
})

test('WHAT[process-execution-017] native signal deadline and output overflow retain separate facts', async t => {
  const repo = repository(t)
  const bin = join(repo, 'bin')
  mkdirSync(bin)
  const executable = join(bin, 'git')
  const previousPath = process.env.PATH
  process.env.PATH = `${bin}:${previousPath}`
  try {
    writeFileSync(executable, '#!/bin/sh\nprintf "before-signal"\nprintf "signal-diagnostic" >&2\nkill -TERM $$\n')
    chmodSync(executable, 0o755)
    const signaled = await run(repo, 5000, ['ignored'], undefined, 1024)
    assert.deepEqual(signaled.ending, { kind: 'Signaled', signal: 'SIGTERM' })
    assert.equal(Buffer.from(signaled.output.stdout).toString(), 'before-signal')
    writeFileSync(executable, '#!/bin/sh\ntrap "" TERM\nprintf "started"\nexec sleep 30\n')
    const started = performance.now()
    const timed = await run(repo, 100, ['ignored'], undefined, 1024)
    assert.ok(performance.now() - started < 3000)
    assert.deepEqual(timed.stopRequested, { kind: 'DeadlineStopRequested', durationMs: 100 })
    assert.equal(timed.output.complete, false)
    writeFileSync(executable, '#!/bin/sh\nprintf "0123456789"\nexit 23\n')
    const overflow = await run(repo, 5000, ['ignored'], undefined, 4)
    assert.equal(overflow.output.complete, false)
    assert.equal(overflow.output.stdout.length + overflow.output.stderr.length, 4)
    assert.match(overflow.fault, /byte limit/)
  } finally {
    process.env.PATH = previousPath
  }
})
