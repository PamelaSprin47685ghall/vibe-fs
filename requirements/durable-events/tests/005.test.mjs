import test from 'node:test'

// WHAT[durable-events-005] — writer 段序列合同（红阶段）。
// 本文件承载无需注入段上限的用例：
//   1) 既有断言按新合同改写为 <WriterId>-<seq>.ndjson 段名形态，
//      在旧实现（无后缀单文件）上因「文件名形态」而红；
//   2) append 续写最高 seq 段：旧实现把 append 写到无后缀文件而红；
//   3) 旧形态兼容锚：在旧实现上绿，新实现必须保持绿（旧形态不得 leave-unread）。
// 需要注入 64KiB 同构小上限的滚动 / 重组 / 故障注入用例见 026.test.mjs。

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

test('WHAT[durable-events-005] small_history_stays_one_segment_named_with_seq_suffix', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'one-file-law')
  try {
    let parent = null
    for (let n = 1; n <= 128; n += 1) {
      const next = id(n)
      await eventStore.append(store, [event(next, parent ? [parent] : [])])
      parent = next
    }
    const files = await readdir(join(dir, 'wanxiangshu', 'events'))
    assert.deepEqual(files, ['one-file-law-0000.ndjson'],
      'history below the segment cap stays one segment named <WriterId>-0000.ndjson')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
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

test('WHAT[durable-events-005] DURABLE_EVENTS_005_one_process_is_one_segment_series_and_appends_preserve_prior_bytes', async () => {
  const gitCommonDir = await commonDir()
  const store = eventStore.create(gitCommonDir, 'writer-proof-a')
  try {
    const first = Array.from({ length: 4 }, (_, i) => event(hexId(i + 1), i + 1))
    assert.equal((await eventStore.append(store, first)).ok, true)
    const file = path.join(gitCommonDir, 'wanxiangshu', 'events', 'writer-proof-a-0000.ndjson')
    const prefix = await readFile(file)

    const many = Array.from({ length: 160 }, (_, i) => event(hexId(i + 100), i + 100))
    assert.equal((await eventStore.append(store, many)).ok, true)
    const after = await readFile(file)

    assert.equal(after.subarray(0, prefix.length).equals(prefix), true, 'append must preserve every prior byte')
    assert.equal(path.basename(file), 'writer-proof-a-0000.ndjson')

    const files = await readdir(path.join(gitCommonDir, 'wanxiangshu', 'events'))
    assert.deepEqual(files, ['writer-proof-a-0000.ndjson'],
      'history below the segment cap must stay one segment named <WriterId>-0000.ndjson')
  } finally {
    eventStore.dispose(store)
    await remove(path.dirname(gitCommonDir))
  }
})
test('WHAT[durable-events-005] DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_segment_series', async () => {
  const gitCommonDir = await commonDir()
  const a = eventStore.create(gitCommonDir, 'writer-a')
  const b = eventStore.create(gitCommonDir, 'writer-b')
  try {
    assert.equal((await eventStore.append(a, [event(hexId(0xa1), 1)])).ok, true)
    assert.equal((await eventStore.append(b, [event(hexId(0xb1), 2)])).ok, true)

    const files = (await readdir(path.join(gitCommonDir, 'wanxiangshu', 'events'))).sort()
    assert.deepEqual(files, ['writer-a-0000.ndjson', 'writer-b-0000.ndjson'])
  } finally {
    eventStore.dispose(a)
    eventStore.dispose(b)
    await remove(path.dirname(gitCommonDir))
  }
})
}

{
// 手写段 fixture：证明 append 续写最高 seq 段（红），与旧形态兼容读取（锚）。
const { default: assert } = await import("node:assert/strict");
const { readdir, readFile } = await import("node:fs/promises");
const { mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { dirname, join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");

const hexId = (n) => n.toString(16).padStart(40, '0')
const event = (id, n, parents = []) => ({
  id, stream: 'proof/segment', type: 'JobRequested', parents, payload: { n }, payloadRefs: [],
})
const canonicalLine = (event) => JSON.stringify({
  event_id: event.id, event_type: event.type, parents: [...event.parents].sort(),
  payload: event.payload, payload_refs: [...event.payloadRefs].sort(), stream_id: event.stream,
}) + '\n'
const commonDir = () => join(mkdtempSync(join(tmpdir(), 'wxs-segment-law-')), '.git')
const eventsDir = (dir) => join(dir, 'wanxiangshu', 'events')

test('WHAT[durable-events-005] append_continues_the_highest_segment_instead_of_opening_a_suffixless_file', async () => {
  const dir = commonDir()
  const first = event(hexId(1), 1)
  mkdirSync(eventsDir(dir), { recursive: true })
  // fixture 先落盘再 create：装载重放必须看到段 0 的事件。
  writeFileSync(join(eventsDir(dir), 'cont-law-0000.ndjson'), canonicalLine(first))
  const store = eventStore.create(dir, 'cont-law')
  try {

    const second = event(hexId(2), 2, [hexId(1)])
    const result = await eventStore.append(store, [second])
    assert.equal(result.ok, true, JSON.stringify(result.error))

    const names = (await readdir(eventsDir(dir))).sort()
    assert.deepEqual(names, ['cont-law-0000.ndjson'],
      'append must extend the highest segment, never open <WriterId>.ndjson')
    assert.equal(await readFile(join(eventsDir(dir), 'cont-law-0000.ndjson'), 'utf8'),
      canonicalLine(first) + canonicalLine(second))
    assert.equal(eventStore.read(store, hexId(2)) != null, true)
  } finally {
    eventStore.dispose(store)
    rmSync(dirname(dir), { recursive: true, force: true })
  }
})

test('WHAT[durable-events-005] legacy_suffixless_file_is_still_read_as_one_writer_stream', async () => {
  // 兼容锚：本用例在旧实现上绿；新实现必须保持绿——
  // 无段后缀的旧形态文件按其完整内容读取为单条逻辑流，不得 leave-unread。
  const dir = commonDir()
  const e1 = event(hexId(1), 1)
  const e2 = event(hexId(2), 2, [hexId(1)])
  mkdirSync(eventsDir(dir), { recursive: true })
  // fixture 先落盘再 create：装载重放必须看到旧形态文件。
  writeFileSync(join(eventsDir(dir), 'legacy-law.ndjson'), canonicalLine(e1) + canonicalLine(e2))
  const store = eventStore.create(dir, 'legacy-law')
  try {

    assert.deepEqual(eventStore.heads(store, 'proof/segment'), [hexId(2)])
    assert.equal(eventStore.read(store, hexId(1)) != null, true)
  } finally {
    eventStore.dispose(store)
    rmSync(dirname(dir), { recursive: true, force: true })
  }
})
}
