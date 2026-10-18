import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const merge = await import("../../../dist/Persistence/EventStore/MergeSurface.js");

const make = (id, parents = [], stream = 'merge/main', payload = {}) => ({
  id,
  stream,
  type: 'JobRequested',
  parents,
  payload,
  payloadRefs: [],
})
const ids = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.events.map((event) => event.id)
}
const A = 'a'.repeat(40)
const B = 'b'.repeat(40)
const C = 'c'.repeat(40)

test('WHAT[durable-convergence-003] identity collision is fail closed not LWW', () => {
  const left = make(A, [], 'merge/main', { x: 1 })
  const right = make(A, [], 'merge/main', { x: 2 })
  const result = merge.merge([
    ['writer-left', [left]],
    ['writer-right', [right]],
  ])
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'IdentityCollision')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const retention = await import("../../../dist/Persistence/EventStore/RetentionSurface.js");

// What durable-events [010]/[011] retired: Git-object snapshots, remote writer
// trees, writer-manifest v2 and the sync entry points. The local contract that
// remains is segment aggregation and retention over the local state root.

test.todo('WHAT[durable-convergence-003] concurrent append and hook convergence preserve the retained union across physical replacement and crash cuts (GAP-151)')

  test('WHAT[durable-convergence-003] segment_files_of_one_writer_aggregate_into_a_single_stream_before_k_way', () => {
    // 旧实现把这两个文件当作两个独立 writer（writer-agg-0000 / writer-agg-0001）；
    // 新合同（[003] + durable-events-005/014）要求同 WriterId 的段聚合为单一逻辑流。
    const idA = 'a'.repeat(40)
    const idB = 'b'.repeat(40)
    const idC = 'c'.repeat(40)
    const commonDir = join(mkdtempSync(join(tmpdir(), 'wxs-seg-aggregate-')), '.Herdchestra')
    try {
      const directory = join(commonDir, 'wanxiangshu', 'events')
      mkdirSync(directory, { recursive: true })
      const line = (id, parents = []) => JSON.stringify({
        event_id: id, event_type: 'JobRequested', parents: [...parents].sort(),
        payload: {}, payload_refs: [], stream_id: 'proof/aggregate',
      }) + '\n'
      writeFileSync(join(directory, 'writer-agg-0000.ndjson'), line(idA) + line(idB, [idA]))
      writeFileSync(join(directory, 'writer-agg-0001.ndjson'), line(idC, [idB]))
      assert.deepEqual(retention.retainedWriterIdsAt(commonDir, Date.now()), ['writer-agg'],
        'segments of one WriterId are one writer stream, never independent writers')
    } finally {
      rmSync(commonDir, { recursive: true, force: true })
    }
  })
}
