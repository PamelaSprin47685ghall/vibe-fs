import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[durable-convergence-009] dumb remote fixture has no Wanxiang domain or server-side logic', async () => {
  const remote = await read('requirements/verification-system/tests/support/dumb-remote.mjs')
  assert.doesNotMatch(remote, /dist\/Domain|CanonicalIntegrator|Projection|WriterStreamSync|HookSync/,
    'remote fixture must stay a dumb Git remote: no Event/Projection/Wanxiang domain')
  assert.match(remote, /git/, 'remote fixture speaks plain Git')
  assert.doesNotMatch(remote, /pre-receive|post-receive|serverSide|receive\.hook/i,
    'no server-side merge, pre-receive reducer or post-receive projection')

  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.match(gateway, /WriterStreamSync\.syncWriterStreams/,
    'all sync intelligence lives in the client gateway')
  assert.doesNotMatch(gateway, /pre-receive|post-receive/i,
    'client gateway must not install server-side receive hooks')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { retainedWriterIdsAt, syncAt, writerSyncAdapterScenario } = await import("../../../dist/Persistence/EventStore/RetentionSurface.js");

const canonicalLine = (id, stream) => JSON.stringify({
  event_id: id,
  event_type: 'JobRequested',
  parents: [],
  payload: {},
  payload_refs: [],
  stream_id: stream,
}) + '\n'
const operations = (protocol) => protocol.map((call) => call.split(' ', 1)[0])
const writtenRoot = (protocol) => protocol.findLast((call) => call.startsWith('WriteTree ')).slice('WriteTree '.length)

test('WHAT[durable-convergence-009] Adapter: writer sync preserves exact identities and fails closed at the Git gateway', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-writer-sync-adapter-'))
  const commonDir = join(root, '.git')
  const localWriterId = 'writer-local'
  const remoteWriterId = 'writer-remote'
  const nowMs = Date.now()

  try {
    const events = join(commonDir, 'wanxiang', 'events')
    mkdirSync(events, { recursive: true })
    writeFileSync(join(events, `${localWriterId}.ndjson`), canonicalLine('a'.repeat(40), '1'.repeat(40)))

    const result = await writerSyncAdapterScenario({
      commonDir,
      nowMs,
      remoteWriterId,
      remoteWriterText: canonicalLine('b'.repeat(40), '2'.repeat(40)),
      remoteActivityMs: nowMs,
    })

    assert.equal(result.first.ok, true, JSON.stringify(result.first))
    assert.equal(result.repeat.ok, true, JSON.stringify(result.repeat))
    assert.deepEqual(result.localWriterIds, [localWriterId, remoteWriterId])
    assert.deepEqual(result.writerIdsAfterInvalid, [localWriterId, remoteWriterId])

    assert.equal(result.first.protocol.includes(`ReadTree ${result.validRemoteRoot}`), true,
      'the adapter must receipt the exact supplied remote root')
    assert.equal(result.first.root, writtenRoot(result.first.protocol),
      'the returned snapshot identity must be the root written at the gateway')
    assert.equal(result.repeat.root, result.first.root,
      'repeating the same remote import must preserve snapshot identity')
    assert.equal(result.repeat.root, writtenRoot(result.repeat.protocol))

    assert.deepEqual(operations(result.first.protocol), [
      'WriteBlob', 'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
      'ReadTree', 'ReadTree', 'ReadObject', 'ReadObject', 'ReadTree',
      'WriteBlob', 'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
    ])
    assert.deepEqual(operations(result.repeat.protocol), [
      'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
      'ReadTree', 'ReadTree', 'ReadObject', 'ReadTree',
      'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
    ])

    assert.equal(result.invalid.ok, false)
    assert.match(result.invalid.error, /sync root must contain writers\/ and payloads\//)
    assert.deepEqual(operations(result.invalid.protocol), [
      'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree', 'ReadTree',
    ])
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
test('WHAT[durable-convergence-009] Adapter: writer sync with absent remote creates the local-only snapshot without error', async () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-writer-sync-remote-absent-'))
  execFileSync('git', ['init', '-q', repo])
  const commonDir = join(repo, '.git')
  const nowMs = Date.now()

  try {
    const events = join(commonDir, 'wanxiang', 'events')
    mkdirSync(events, { recursive: true })
    writeFileSync(join(events, 'writer-local.ndjson'), canonicalLine('a'.repeat(40), '1'.repeat(40)))

    // remote-absent ambiguity state: a null remote root must materialize the
    // local-only candidate through production syncAt, never fail closed.
    const result = await syncAt(repo, commonDir, null, nowMs)
    assert.equal(result.ok, true, JSON.stringify(result))

    const rootEntries = execFileSync('git', ['-C', repo, 'ls-tree', result.root], { encoding: 'utf8' })
    assert.match(rootEntries, /\twriters$/m)
    assert.match(rootEntries, /\tpayloads$/m)
    assert.match(rootEntries, /\twriter-manifest$/m)

    const writers = execFileSync('git', ['-C', repo, 'ls-tree', `${result.root}:writers`], { encoding: 'utf8' })
    assert.match(writers, /writer-local\.ndjson$/m)
    assert.deepEqual(retainedWriterIdsAt(commonDir, nowMs), ['writer-local'])
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { spawnSync } = await import("node:child_process");
const { existsSync, readFileSync } = await import("node:fs");
const { fileURLToPath } = await import("node:url");
const { join } = await import("node:path");
const { createBareWorkspace, readRemoteStoreOid, remoteHasObject } = await import("../../verification-system/tests/support/dumb-remote.mjs");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const { integrationTest } = await import("../../verification-system/tests/support/tier-gate.mjs");

const runner = fileURLToPath(new URL('../../../resources/git/wanxiang-hook.mjs', import.meta.url));

const event = (id, writer) => ({
  id,
  stream: 'dumb/remote',
  type: 'JobRequested',
  parents: [],
  payload: { writer },
  payloadRefs: [],
});

const open = (repo, writerId) => eventStore.create(join(repo, '.git'), writerId);

const append = async (handle, value) => {
  const result = await eventStore.append(handle, [value]);
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error));
};

const hook = (repo, kind = 'pre-push', arg = 'origin', input = '') => spawnSync(
  process.execPath,
  [runner, kind, arg],
  { cwd: repo, input, encoding: 'utf8', env: { ...process.env, WANXIANG_GIT_SYNC_ACTIVE: '' } },
);

const assertHookOk = (result) => {
  assert.equal(result.status, 0, "hook failed: " + (result.stderr || result.stdout));
};

integrationTest('WHAT[durable-convergence-009] dumb_remote_helper_has_no_Wanxiang_domain_or_projection_logic', () => {
  const source = readFileSync(new URL('../../verification-system/tests/support/dumb-remote.mjs', import.meta.url), 'utf8');
  assert.doesNotMatch(source, /dist\/Domain|CanonicalIntegrator|Projection|WriterStreamSync|HookSync/);
  assert.match(source, /git/);
});

integrationTest('WHAT[durable-convergence-009] pre_push_hook_process_uploads_one_local_writer_file_to_bare_remote_store_ref', async () => {
  const ws = createBareWorkspace(['a']);
  try {
    const repo = ws.client('a');
    const local = open(repo, 'writer-a');
    try {
      await append(local, event('a'.repeat(40), 'a'));
    } finally {
      eventStore.dispose(local);
    }
    assertHookOk(hook(repo));
    const oid = readRemoteStoreOid(ws.bare);
    assert.match(oid, /^[0-9a-f]{40}$/);
    assert.equal(remoteHasObject(ws.bare, oid), true);
  } finally {
    ws.cleanup();
  }
});

integrationTest('WHAT[durable-convergence-009] second_machine_hook_imports_remote_writer_truth_without_any_running_Wanxiang_process', async () => {
  const ws = createBareWorkspace(['a', 'b']);
  try {
    const a = ws.client('a');
    const b = ws.client('b');
    const localA = open(a, 'writer-a');
    try {
      await append(localA, event('a'.repeat(40), 'a'));
    } finally {
      eventStore.dispose(localA);
    }
    assertHookOk(hook(a));

    assertHookOk(hook(b));
    assert.equal(existsSync(join(b, '.git', 'wanxiang', 'events', 'writer-a.ndjson')), true);
    const reopenedB = open(b, 'writer-b-after-sync');
    try {
      assert.deepEqual(eventStore.read(reopenedB, 'a'.repeat(40)), event('a'.repeat(40), 'a'));
    } finally {
      eventStore.dispose(reopenedB);
    }
  } finally {
    ws.cleanup();
  }
});

integrationTest('WHAT[durable-convergence-009] two_offline_clients_converge_by_whole_writer_files_and_repeat_is_idempotent', async () => {
  const ws = createBareWorkspace(['a', 'b']);
  try {
    const a = ws.client('a');
    const b = ws.client('b');
    const localA = open(a, 'writer-a');
    const localB = open(b, 'writer-b');
    try {
      await append(localA, event('a'.repeat(40), 'a'));
      await append(localB, event('b'.repeat(40), 'b'));
    } finally {
      eventStore.dispose(localA);
      eventStore.dispose(localB);
    }

    assertHookOk(hook(a));
    assertHookOk(hook(b));
    assertHookOk(hook(a));
    const firstTip = readRemoteStoreOid(ws.bare);
    assertHookOk(hook(a));
    assert.equal(readRemoteStoreOid(ws.bare), firstTip, 'same complete writer bytes materialize the same Git root');

    for (const repo of [a, b]) {
      assert.equal(existsSync(join(repo, '.git', 'wanxiang', 'events', 'writer-a.ndjson')), true);
      assert.equal(existsSync(join(repo, '.git', 'wanxiang', 'events', 'writer-b.ndjson')), true);
    }
  } finally {
    ws.cleanup();
  }
});
}
