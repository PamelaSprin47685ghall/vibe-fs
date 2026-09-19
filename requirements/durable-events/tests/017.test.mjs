import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFile, readdir } = await import("node:fs/promises");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");

const id = (n) => n.toString(16).padStart(40, '0')
const event = (id, parents = []) => ({
  id,
  stream: 'append/law',
  type: 'JobRequested',
  parents,
  payload: { id },
  payloadRefs: [],
})
const withTemp = (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-append-law-'))
  return fn(base)
}

test('WHAT[durable-events-017] append_path_has_no_Git_object_or_ref_capability', async () => {
  const source = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/Store.fs', import.meta.url), 'utf8')
  const log = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url), 'utf8')
  for (const token of ['WriteBlob', 'WriteTree', 'ReadRef', 'CompareAndSwapRef', 'RootOid', 'ProcessGitRawStore']) {
    assert.equal(source.includes(token), false)
    assert.equal(log.includes(token), false)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtemp, readFile, readdir, rm } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");

const hexId = (n) => n.toString(16).padStart(40, '0')
const event = (id, n, parents = []) => ({
  id,
  stream: 'proof/local',
  type: 'JobRequested',
  parents,
  payload: { n },
  payloadRefs: [],
})
const commonDir = async () => path.join(await mkdtemp(path.join(tmpdir(), 'wanxiang-local-log-')), '.git')
const remove = async (dir) => rm(dir, { recursive: true, force: true })

test('WHAT[durable-events-017] DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies', async () => {
  const gitCommonDir = await commonDir()
  const store1 = eventStore.create(gitCommonDir, 'writer-bytes-1')
  try {
    const e1 = event(hexId(1), 1)
    const e2 = event(hexId(2), 2, [hexId(1)])

    // 1. Two appends preserve all previous bytes exactly
    assert.equal((await eventStore.append(store1, [e1])).ok, true)
    const file = path.join(gitCommonDir, 'wanxiang', 'events', 'writer-bytes-1.ndjson')
    const bytesAfterFirst = await readFile(file)

    assert.equal((await eventStore.append(store1, [e2])).ok, true)
    const bytesAfterSecond = await readFile(file)
    assert.equal(bytesAfterSecond.subarray(0, bytesAfterFirst.length).equals(bytesAfterFirst), true)

    eventStore.dispose(store1)

    // 2. Restart and readback confirms both events are durable and intact
    const store2 = eventStore.create(gitCommonDir, 'writer-bytes-2')
    try {
      const read1 = eventStore.read(store2, hexId(1))
      const read2 = eventStore.read(store2, hexId(2))
      assert.deepEqual(read1?.payload, { n: 1 })
      assert.deepEqual(read2?.payload, { n: 2 })
    } finally {
      eventStore.dispose(store2)
    }

    // 3. Tail truncation / incomplete trailing line fail-closed on restart
    const { appendFile } = await import('node:fs/promises')
    await appendFile(file, '{"event_id":"corrupt-half-line')
    assert.throws(() => {
      const store3 = eventStore.create(gitCommonDir, 'writer-bytes-3')
      eventStore.dispose(store3)
    }, /incomplete trailing line|NonCanonical/i, 'boot must fail-closed on corrupted tail')
  } finally {
    await remove(path.dirname(gitCommonDir))
  }
})
}
