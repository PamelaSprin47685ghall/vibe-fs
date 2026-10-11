import test from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as retention from '../../../dist/Persistence/EventStore/RetentionSurface.js'

// WHAT[durable-events-005] — 段上限注入面（红阶段）。
// 本文件全部用例依赖两个新 Surface 承诺：
//   eventStore.createWithSegmentCap(commonDir, writerId, capBytes)
//   retention.syncAtWithSegmentCap(repo, commonDir, remoteRoot, nowMs, capBytes)
// 旧实现没有这两个入口：属性访问得到 undefined，每个用例以
// "createWithSegmentCap is not a function" / "syncAtWithSegmentCap is not a function"
// 失败——红因归因于合同变化（新 Surface 承诺不存在），而非语法错误。
// 实现阶段入口存在后，本文件用 64 KiB 同构缩小（64 KiB : 64 MiB = 1 : 1024）
// 验证 64 MiB 段上限合同。

const CAP = 64 * 1024

const hexId = (n) => n.toString(16).padStart(40, '0')
const canonicalLine = (value) => JSON.stringify({
  event_id: value.id, event_type: value.type, parents: [...value.parents].sort(),
  payload: value.payload, payload_refs: [...value.payloadRefs].sort(), stream_id: value.stream,
}) + '\n'

// 生成一条 canonical 编码后恰好 totalBytes 字节的事件行（payload 填充）。
const lineOf = (n, totalBytes, parents = []) => {
  const probe = { id: hexId(n), stream: 'proof/segment-cap', type: 'JobRequested', parents, payload: { fill: '' }, payloadRefs: [] }
  const skeleton = canonicalLine(probe).length
  assert.ok(totalBytes >= skeleton, 'requested line is smaller than its canonical skeleton')
  return { ...probe, payload: { fill: 'x'.repeat(totalBytes - skeleton) } }
}

const withRepo = async (fn) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-segcap-'))
  execFileSync('git', ['init', '-q', root])
  const commonDir = join(root, '.git')
  try { await fn(root, commonDir) } finally { rmSync(root, { recursive: true, force: true }) }
}
const eventsDir = (commonDir) => join(commonDir, 'wanxiangshu', 'events')
const writeLegacy = (commonDir, writerId, events) => {
  mkdirSync(eventsDir(commonDir), { recursive: true })
  const path = join(eventsDir(commonDir), writerId + '.ndjson')
  writeFileSync(path, events.map(canonicalLine).join(''))
  return path
}
const chain = (count, lineBytes) => {
  const events = []
  let parent = null
  for (let i = 1; i <= count; i += 1) {
    const e = lineOf(i, lineBytes, parent ? [parent] : [])
    parent = e.id
    events.push(e)
  }
  return events
}

test('WHAT[durable-events-005] appending_past_the_cap_rolls_to_the_next_segment', async () => {
  await withRepo(async (repo, commonDir) => {
    const store = eventStore.createWithSegmentCap(commonDir, 'roll-law', CAP)
    try {
      // 8 行 × CAP/8 恰好填满段 0；第 9 次 append 必须滚动到 0001。
      const events = chain(10, CAP / 8)
      for (const e of events) {
        const result = await eventStore.append(store, [e])
        assert.equal(result.ok, true, JSON.stringify(result.error))
      }
      const names = readdirSync(eventsDir(commonDir)).sort()
      assert.deepEqual(names, ['roll-law-0000.ndjson', 'roll-law-0001.ndjson'])
      assert.ok(statSync(join(eventsDir(commonDir), 'roll-law-0000.ndjson')).size <= CAP,
        'no segment may exceed the cap')
      const reassembled = Buffer.concat(names.map(name => readFileSync(join(eventsDir(commonDir), name))))
      assert.equal(reassembled.equals(Buffer.concat(events.map(e => Buffer.from(canonicalLine(e))))), true,
        'the logical stream (segments concatenated by seq) is byte-for-byte the append order')
      const observer = eventStore.create(commonDir, 'roll-observer')
      try {
        assert.deepEqual(eventStore.heads(observer, 'proof/segment-cap'), [events.at(-1).id])
      } finally {
        eventStore.dispose(observer)
      }
    } finally {
      eventStore.dispose(store)
    }
  })
})

test('WHAT[durable-events-005] an_append_landing_exactly_on_the_cap_stays_in_the_current_segment', async () => {
  await withRepo(async (repo, commonDir) => {
    const store = eventStore.createWithSegmentCap(commonDir, 'exact-law', CAP)
    try {
      const half = CAP / 2
      const first = lineOf(1, half)
      const second = lineOf(2, half, [first.id])
      for (const e of [first, second]) {
        const result = await eventStore.append(store, [e])
        assert.equal(result.ok, true, JSON.stringify(result.error))
      }
      const names = readdirSync(eventsDir(commonDir)).sort()
      assert.deepEqual(names, ['exact-law-0000.ndjson'],
        'size + text landing exactly on the cap does not roll; only strictly exceeding rolls')
      assert.equal(statSync(join(eventsDir(commonDir), 'exact-law-0000.ndjson')).size, CAP)
    } finally {
      eventStore.dispose(store)
    }
  })
})

test('WHAT[durable-events-005] a_single_line_larger_than_the_cap_occupies_its_own_segment', async () => {
  await withRepo(async (repo, commonDir) => {
    const store = eventStore.createWithSegmentCap(commonDir, 'oversize-law', CAP)
    try {
      // durable-events-012：单行体积无固定上限；超限单行允许独占一个段。
      const big = lineOf(1, CAP * 2)
      const small = lineOf(2, 256, [big.id])
      assert.equal((await eventStore.append(store, [big])).ok, true)
      assert.equal((await eventStore.append(store, [small])).ok, true)

      const names = readdirSync(eventsDir(commonDir)).sort()
      assert.deepEqual(names, ['oversize-law-0000.ndjson', 'oversize-law-0001.ndjson'],
        'an oversized line takes its own segment; the next append must roll')
      assert.equal(statSync(join(eventsDir(commonDir), 'oversize-law-0000.ndjson')).size, CAP * 2)
      assert.equal(statSync(join(eventsDir(commonDir), 'oversize-law-0001.ndjson')).size, canonicalLine(small).length)
      assert.equal(eventStore.read(store, small.id) != null, true)
    } finally {
      eventStore.dispose(store)
    }
  })
})

test('WHAT[durable-events-005] a_failed_roll_keeps_sealed_segment_bytes_and_leaves_the_event_uncommitted', async () => {
  await withRepo(async (repo, commonDir) => {
    const base = eventStore.createWithSegmentCap(commonDir, 'roll-fail-law', CAP)
    try {
      for (const e of chain(8, CAP / 8)) {
        assert.equal((await eventStore.append(base, [e])).ok, true)
      }
      const sealed = join(eventsDir(commonDir), 'roll-fail-law-0000.ndjson')
      const before = readFileSync(sealed)

      // 第 9 次 append 必须滚动；注入物理追加阶段的 typed 失败。
      const observed = []
      const failing = eventStore.createAppendFailureStore(base, {
        code: 'CommitUnknown',
        phase: 'PhysicalAppend',
        cause: new Error('segment roll physical failure'),
      }, (append) => observed.push(append))
      const ninth = chain(9, CAP / 8).at(-1)
      const result = await eventStore.append(failing, [ninth])
      assert.equal(result.ok, false, 'the rolled append must surface the injected typed failure')
      assert.equal(observed.length, 1, 'a failed append is never retried with a fresh event identity')

      assert.equal(readFileSync(sealed).equals(before), true,
        'the sealed segment bytes are never rewritten by a failed roll')
      assert.equal(eventStore.read(base, ninth.id) != null, false,
        'the uncommitted event stays unreadable after the failed roll')
    } finally {
      eventStore.dispose(base)
    }
  })
})

test('WHAT[durable-events-005] materialization_reorganizes_an_oversized_legacy_file_into_equal_segments', async () => {
  await withRepo(async (repo, commonDir) => {
    const events = chain(6, CAP / 4) // 1.5 × CAP：段 0 四行，段 1 两行。
    const legacy = writeLegacy(commonDir, 'reorg-law', events)
    const original = readFileSync(legacy)

    const snap = await retention.syncAtWithSegmentCap(repo, commonDir, null, Date.now(), CAP)
    assert.equal(snap.ok, true, JSON.stringify(snap.error))
    assert.equal(existsSync(legacy), false,
      'the legacy file is removed only after every segment has landed')
    const names = readdirSync(eventsDir(commonDir)).sort()
    assert.deepEqual(names, ['reorg-law-0000.ndjson', 'reorg-law-0001.ndjson'])
    for (const name of names) {
      assert.ok(statSync(join(eventsDir(commonDir), name)).size <= CAP)
    }
    const reassembled = Buffer.concat(names.map(name => readFileSync(join(eventsDir(commonDir), name))))
    assert.equal(reassembled.equals(original), true,
      'reorganization reassigns event lines byte-for-byte; the logical stream is unchanged')
    const observer = eventStore.create(commonDir, 'reorg-observer')
    try {
      assert.deepEqual(eventStore.heads(observer, 'proof/segment-cap'), [events.at(-1).id])
    } finally {
      eventStore.dispose(observer)
    }
  })
})

test('WHAT[durable-events-005] reorganization_is_idempotent_across_repeated_syncs', async () => {
  await withRepo(async (repo, commonDir) => {
    const events = chain(6, CAP / 4)
    const legacy = writeLegacy(commonDir, 'reorg-idem-law', events)
    const now = Date.now()

    const first = await retention.syncAtWithSegmentCap(repo, commonDir, null, now, CAP)
    assert.equal(first.ok, true, JSON.stringify(first.error))
    const segmentZero = join(eventsDir(commonDir), 'reorg-idem-law-0000.ndjson')
    const statBefore = statSync(segmentZero)

    const second = await retention.syncAtWithSegmentCap(repo, commonDir, first.root, now, CAP)
    assert.equal(second.ok, true, JSON.stringify(second.error))
    assert.equal(second.root, first.root, 'an already-reorganized store re-materializes to the identical root')
    assert.equal(statSync(segmentZero).mtimeMs, statBefore.mtimeMs,
      'repeat sync must not touch reorganized segment bytes or stats')
    assert.equal(existsSync(legacy), false)
  })
})

test('WHAT[durable-events-005] an_interrupted_reorganization_recovers_by_prefix_validation', async () => {
  await withRepo(async (repo, commonDir) => {
    const events = chain(6, CAP / 4)
    const legacy = writeLegacy(commonDir, 'reorg-recover-law', events)
    const original = readFileSync(legacy)

    // 手造中断现场：旧文件完整，段 0 只落了前四行（旧文件的字节前缀）。
    const partial = join(eventsDir(commonDir), 'reorg-recover-law-0000.ndjson')
    writeFileSync(partial, events.slice(0, 4).map(canonicalLine).join(''))

    const snap = await retention.syncAtWithSegmentCap(repo, commonDir, null, Date.now(), CAP)
    assert.equal(snap.ok, true, JSON.stringify(snap.error))
    assert.equal(existsSync(legacy), false, 'recovery completes the interrupted reorganization')
    const names = readdirSync(eventsDir(commonDir)).sort()
    assert.deepEqual(names, ['reorg-recover-law-0000.ndjson', 'reorg-recover-law-0001.ndjson'])
    const reassembled = Buffer.concat(names.map(name => readFileSync(join(eventsDir(commonDir), name))))
    assert.equal(reassembled.equals(original), true)
  })
})

test('WHAT[durable-events-005] divergent_partial_segments_fail_closed_without_touching_bytes', async () => {
  await withRepo(async (repo, commonDir) => {
    // 旧文件承载一条 A 链；段 0 承载内容无关的 B 链——与旧文件无前缀关系。
    const legacy = writeLegacy(commonDir, 'reorg-divergent-law', chain(6, CAP / 4))
    const original = readFileSync(legacy)

    // A distinct stream makes every divergent line differ from the legacy
    // chain, so no byte-prefix relation can hold between the two forms.
    const divergent = chain(6, CAP / 4).map((e) => ({ ...e, stream: 'proof/segment-divergent' }))
    const divergentPath = join(eventsDir(commonDir), 'reorg-divergent-law-0000.ndjson')
    writeFileSync(divergentPath, divergent.slice(0, 4).map(canonicalLine).join(''))
    const partialBytes = readFileSync(divergentPath)

    const snap = await retention.syncAtWithSegmentCap(repo, commonDir, null, Date.now(), CAP)
    assert.equal(snap.ok, false, 'coexisting forms without a prefix relation must fail closed')
    assert.equal(readFileSync(legacy).equals(original), true,
      'fail-closed must not silently delete or rewrite the legacy file')
    assert.equal(readFileSync(divergentPath).equals(partialBytes), true,
      'fail-closed must not silently rewrite the partial segment')
  })
})

test('WHAT[durable-events-005] line_continuation_convergence_appends_legacy_lines_onto_the_series_tail', async () => {
  await withRepo(async (repo, commonDir) => {
    // 版本过渡竞态现场：段序列已落地（A 链），旧进程向旧名追加全新事件（B 链，
    // parents 指向 A 链尾）——两种形态互不为字节前缀，但构成行级延续。
    const aChain = chain(4, CAP / 8)
    const bChain = [
      lineOf(0x21, CAP / 8, [aChain.at(-1).id]),
      lineOf(0x22, CAP / 8, [hexId(0x21)]),
    ]
    const segmentPath = join(eventsDir(commonDir), 'continuation-law-0000.ndjson')
    mkdirSync(eventsDir(commonDir), { recursive: true })
    writeFileSync(segmentPath, aChain.map(canonicalLine).join(''))
    const legacy = join(eventsDir(commonDir), 'continuation-law.ndjson')
    writeFileSync(legacy, bChain.map(canonicalLine).join(''))
    const legacyBytes = readFileSync(legacy)

    const snap = await retention.syncAtWithSegmentCap(repo, commonDir, null, Date.now(), CAP)
    assert.equal(snap.ok, true, JSON.stringify(snap.error))
    assert.equal(existsSync(legacy), false, 'line-continuation convergence removes the legacy name')
    assert.deepEqual(readdirSync(eventsDir(commonDir)).sort(), ['continuation-law-0000.ndjson'],
      'the tail headroom fits every legacy line, so no fresh segment rolls')

    // 行字节原样：尾段尾部字节 == 旧名原字节。
    const tail = readFileSync(segmentPath)
    assert.equal(tail.subarray(tail.length - legacyBytes.length).equals(legacyBytes), true,
      'the legacy lines land verbatim at the series tail')

    const observer = eventStore.create(commonDir, 'continuation-observer')
    try {
      for (const e of [...aChain, ...bChain]) {
        assert.equal(eventStore.read(observer, e.id) != null, true, 'event must be readable after convergence')
      }
      assert.deepEqual(eventStore.heads(observer, bChain.at(-1).stream), [bChain.at(-1).id])
    } finally {
      eventStore.dispose(observer)
    }
  })
})

test('WHAT[durable-events-005] line_continuation_with_a_half_line_still_fails_closed', async () => {
  await withRepo(async (repo, commonDir) => {
    // 旧名末行无 LF（半行）——不是完整 canonical 行，不构成行级延续。
    const aChain = chain(2, CAP / 8)
    const b1 = lineOf(0x21, CAP / 8, [aChain.at(-1).id])
    const segmentPath = join(eventsDir(commonDir), 'halfline-law-0000.ndjson')
    mkdirSync(eventsDir(commonDir), { recursive: true })
    writeFileSync(segmentPath, aChain.map(canonicalLine).join(''))
    const legacy = join(eventsDir(commonDir), 'halfline-law.ndjson')
    const halfLine = canonicalLine(b1) + '{"event_id":"truncated'
    writeFileSync(legacy, halfLine)
    const segmentBytes = readFileSync(segmentPath)

    const snap = await retention.syncAtWithSegmentCap(repo, commonDir, null, Date.now(), CAP)
    assert.equal(snap.ok, false, 'a half line is not a line continuation and must fail closed')
    assert.equal(readFileSync(legacy, 'utf8'), halfLine, 'fail-closed must not touch the legacy file')
    assert.equal(readFileSync(segmentPath).equals(segmentBytes), true, 'fail-closed must not touch the series')
  })
})

test('WHAT[durable-events-005] line_continuation_with_overlapping_ids_still_fails_closed', async () => {
  await withRepo(async (repo, commonDir) => {
    // 旧名含段中已有事件 ID（重叠）——完整 canonical 行但 ID 重叠，仍 diverged。
    const aChain = chain(4, CAP / 8)
    const overlapChain = [lineOf(0x31, CAP / 8, [aChain.at(-1).id]), aChain[1]]
    const segmentPath = join(eventsDir(commonDir), 'overlap-law-0000.ndjson')
    mkdirSync(eventsDir(commonDir), { recursive: true })
    writeFileSync(segmentPath, aChain.map(canonicalLine).join(''))
    const legacy = join(eventsDir(commonDir), 'overlap-law.ndjson')
    writeFileSync(legacy, overlapChain.map(canonicalLine).join(''))
    const legacyBytes = readFileSync(legacy)
    const segmentBytes = readFileSync(segmentPath)

    const snap = await retention.syncAtWithSegmentCap(repo, commonDir, null, Date.now(), CAP)
    assert.equal(snap.ok, false, 'overlapping event ids are divergence, not continuation')
    assert.equal(readFileSync(legacy).equals(legacyBytes), true)
    assert.equal(readFileSync(segmentPath).equals(segmentBytes), true)
  })
})

test('WHAT[durable-events-005] readStreams_reads_line_continuation_as_series_plus_legacy_lines', async () => {
  await withRepo(async (repo, commonDir) => {
    // 读取层不改磁盘：段 + 旧名延续形态直接可读，逻辑流 = 段事件在前、旧行在后。
    const aChain = chain(2, CAP / 8)
    const bChain = [
      lineOf(0x21, CAP / 8, [aChain.at(-1).id]),
      lineOf(0x22, CAP / 8, [hexId(0x21)]),
    ]
    const segmentPath = join(eventsDir(commonDir), 'read-cont-law-0000.ndjson')
    mkdirSync(eventsDir(commonDir), { recursive: true })
    writeFileSync(segmentPath, aChain.map(canonicalLine).join(''))
    const legacy = join(eventsDir(commonDir), 'read-cont-law.ndjson')
    writeFileSync(legacy, bChain.map(canonicalLine).join(''))
    const legacyBytes = readFileSync(legacy)

    const observer = eventStore.create(commonDir, 'read-cont-observer')
    try {
      for (const e of [...aChain, ...bChain]) {
        assert.equal(eventStore.read(observer, e.id) != null, true, 'the continuation form must stay readable')
      }
      assert.deepEqual(eventStore.heads(observer, bChain.at(-1).stream), [bChain.at(-1).id])
      assert.equal(existsSync(legacy), true, 'the read layer must not converge physically')
      assert.equal(readFileSync(legacy).equals(legacyBytes), true)
    } finally {
      eventStore.dispose(observer)
    }
  })
})

test('WHAT[durable-events-005] rolling_a_new_segment_only_blobs_the_new_segment', async () => {
  await withRepo(async (repo, commonDir) => {
    const store = eventStore.createWithSegmentCap(commonDir, 'blob-reuse-law', CAP)
    try {
      for (const e of chain(8, CAP / 8)) {
        assert.equal((await eventStore.append(store, [e])).ok, true)
      }
      const now = Date.now()
      const first = await retention.syncAtWithSegmentCap(repo, commonDir, null, now, CAP)
      assert.equal(first.ok, true, JSON.stringify(first.error))

      for (const e of chain(10, CAP / 8).slice(8)) {
        assert.equal((await eventStore.append(store, [e])).ok, true)
      }
      const second = await retention.syncAtWithSegmentCap(repo, commonDir, first.root, now, CAP)
      assert.equal(second.ok, true, JSON.stringify(second.error))

      const blobOid = (root, name) =>
        execFileSync('git', ['-C', repo, 'rev-parse', root + ':writers/' + name], { encoding: 'utf8' }).trim()
      assert.equal(
        blobOid(second.root, 'blob-reuse-law-0000.ndjson'),
        blobOid(first.root, 'blob-reuse-law-0000.ndjson'),
        'durable-convergence-010: the sealed segment blob OID is reused; only the new segment is blobbed')
    } finally {
      eventStore.dispose(store)
    }
  })
})
