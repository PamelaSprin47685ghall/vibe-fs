import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const merge = await import("../../../dist/Persistence/EventStore/MergeSurface.js");

const eventId = fc
  .uint8Array({ minLength: 20, maxLength: 20 })
  .map((bytes) => Buffer.from(bytes).toString('hex'))
const uniqueEventIds = (count) =>
  fc.uniqueArray(eventId, { minLength: count, maxLength: count })
const reverseCausalEventIds = (count) =>
  uniqueEventIds(count).map((ids) => {
    const [lower, higher] = [ids[0], ids[1]].sort()
    return [higher, lower, ...ids.slice(2)]
  })
const node = fc.record({
  writerSeed: fc.nat(),
  parentSeeds: fc.array(fc.nat(), { maxLength: 4 }),
  payload: fc.record({ sequence: fc.integer(), text: fc.string({ maxLength: 24 }) }),
})
const scenario = fc
  .tuple(fc.integer({ min: 2, max: 8 }), fc.integer({ min: 2, max: 18 }))
  .chain(([writerCount, eventCount]) =>
    fc.record({
      writerCount: fc.constant(writerCount),
      nodes: fc.array(node, { minLength: eventCount, maxLength: eventCount }),
      eventIds: reverseCausalEventIds(eventCount),
      writerOrder: fc.shuffledSubarray(
        Array.from({ length: writerCount }, (_, index) => index),
        { minLength: writerCount, maxLength: writerCount },
      ),
    }),
  )
const independentScenario = fc.integer({ min: 3, max: 24 }).chain((eventCount) =>
  fc.record({
    nodes: fc.array(node, { minLength: eventCount, maxLength: eventCount }),
    eventIds: uniqueEventIds(eventCount),
  }),
)
const eventOf = (nodes, eventIds, index) => ({
  id: eventIds[index],
  stream: 'property/k-way',
  type: 'JobRequested',
  parents:
    index === 0
      ? []
      : [
          ...new Set([
            ...(index === 1 ? [eventIds[0]] : []),
            ...nodes[index].parentSeeds.map((seed) => eventIds[seed % index]),
          ]),
        ].sort(),
  payload: nodes[index].payload,
  payloadRefs: [],
})
const copyEvent = (event) => ({
  ...event,
  parents: [...event.parents],
  payload: { ...event.payload },
  payloadRefs: [...event.payloadRefs],
})
const streamsOf = ({ writerCount, nodes }, events) => {
  const writers = Array.from({ length: writerCount }, (_, index) => [`writer-${index}`, []])
  events.forEach((event, index) => writers[nodes[index].writerSeed % writerCount][1].push(event))
  return writers
}
const successfulEvents = (streams) => {
  const result = merge.merge(streams)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.events
}
const assertSetAndCausalOrder = (events, expectedEvents) => {
  const ids = events.map((event) => event.id)
  assert.equal(new Set(ids).size, expectedEvents.length)
  assert.deepEqual(new Set(ids), new Set(expectedEvents.map((event) => event.id)))

  const positions = new Map(ids.map((id, index) => [id, index]))
  for (const event of events) {
    for (const parent of event.parents) {
      assert.ok(positions.get(parent) < positions.get(event.id), `${parent} must precede ${event.id}`)
    }
  }
}
const hasReverseLexicalCausalEdge = (events) =>
  events.some((event) => event.parents.some((parent) => parent > event.id))

test('WHAT[DURABLE-CONVERGENCE-002] cross-writer dependency is ordered before its child', () => {
  const parent = {
    id: 'ffffffffffffffffffffffffffffffffffffffff',
    stream: 'property/k-way',
    type: 'JobRequested',
    parents: [],
    payload: { sequence: 0, text: 'parent' },
    payloadRefs: [],
  }
  const child = {
    id: '0000000000000000000000000000000000000000',
    stream: 'property/k-way',
    type: 'JobRequested',
    parents: [parent.id],
    payload: { sequence: 1, text: 'child' },
    payloadRefs: [],
  }

  assert.deepEqual(
    successfulEvents([
      ['writer-child', [child]],
      ['writer-parent', [parent]],
    ]).map((event) => event.id),
    [parent.id, child.id],
  )
})
test('WHAT[DURABLE-CONVERGENCE-002] generated k-way merge preserves union across writer permutations and exact duplicates', () => {
  fc.assert(
    fc.property(scenario, (generated) => {
      const events = generated.nodes.map((_, index) =>
        eventOf(generated.nodes, generated.eventIds, index),
      )
      assert.equal(
        hasReverseLexicalCausalEdge(events),
        true,
        'generated DAG must contain a parent whose EventId sorts after its child',
      )
      const streams = streamsOf(generated, events)
      const canonical = successfulEvents(streams)
      const permuted = generated.writerOrder.map((index) => streams[index])
      const exactCopies = events.map(copyEvent)

      assert.deepEqual(successfulEvents(permuted), canonical)
      assert.deepEqual(successfulEvents([...permuted, ['writer-exact-copy', exactCopies]]), canonical)
      assertSetAndCausalOrder(canonical, events)
    }),
    { seed: 0x4b574d47, numRuns: 500 },
  )
})
test('WHAT[DURABLE-CONVERGENCE-002] generated independent streams compose associatively', () => {
  fc.assert(
    fc.property(independentScenario, ({ nodes, eventIds }) => {
      const streams = Array.from({ length: 3 }, (_, writer) => [
        `writer-${writer}`,
        nodes
          .map((_, index) => eventOf(nodes, eventIds, index))
          .filter((_, index) => index % 3 === writer)
          .map((event) => ({ ...event, parents: [] })),
      ])
      const mergedStream = (name, inputs) => [name, successfulEvents(inputs)]
      const leftGrouped = successfulEvents([mergedStream('writer-ab', streams.slice(0, 2)), streams[2]])
      const rightGrouped = successfulEvents([streams[0], mergedStream('writer-bc', streams.slice(1))])

      assert.deepEqual(leftGrouped, rightGrouped)
      assert.deepEqual(leftGrouped, successfulEvents(streams))
    }),
    { seed: 0x4153534f, numRuns: 300 },
  )
})
}

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

test('WHAT[DURABLE-CONVERGENCE-002] writer enumeration is commutative', () => {
  const a = ['writer-a', [make(A), make(C, [A])]]
  const b = ['writer-b', [make(B)]]
  assert.deepEqual(ids(merge.merge([a, b])), ids(merge.merge([b, a])))
})
test('WHAT[DURABLE-CONVERGENCE-002] duplicate stream input is idempotent by EventId', () => {
  const event = make(A, [], 'merge/main', { x: 1 })
  const result = merge.merge([
    ['writer-a', [event]],
    ['writer-copy', [event]],
  ])
  assert.deepEqual(ids(result), [A])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const merge = await import("../../../dist/Persistence/EventStore/MergeSurface.js");

const make = (id, parents = [], stream = 'replica/law', type = 'JobRequested', payload = {}) => ({
  id,
  stream,
  type,
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
const R = 'd'.repeat(40)
const withStore = async (writerId, fn) => {
  const root = mkdtempSync(join(tmpdir(), `wxs-replica-${writerId}-`))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  const handle = eventStore.create(commonDir, writerId)
  try {
    await fn(handle)
  } finally {
    eventStore.dispose(handle)
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[DURABLE-CONVERGENCE-002] merge is commutative associative idempotent at writer stream level', () => {
  const sa = ['writer-a', [make(A)]]
  const sb = ['writer-b', [make(B)]]
  const sc = ['writer-c', [make(C)]]
  const abc = ids(merge.merge([sa, sb, sc]))
  const cba = ids(merge.merge([sc, sb, sa]))
  assert.deepEqual(abc, cba)
  assert.deepEqual(ids(merge.merge([sa, ['copy', [make(A)]]])), [A])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { readFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const retention = await import("../../../dist/Persistence/EventStore/RetentionSurface.js");

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')
const make = (id, stream, parents = []) => ({ id, stream, type: 'JobRequested', parents, payload: {}, payloadRefs: [] })

test('WHAT[DURABLE-CONVERGENCE-002] one k-way primitive is shared by integrator and sync', async () => {
  const primitive = await read('src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs')
  const integrator = await read('src/Wanxiangshu/Persistence/EventStore/IntegratorEngine.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(primitive, /module EventKWayMerge/)
  assert.match(primitive, /checkIdentity/)
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(sync, /EventKWayMerge\.merge/)
  assert.doesNotMatch(integrator, /sortBy.*EventId.*writerId/is, 'Integrator must not own a second k-way implementation')
  assert.doesNotMatch(sync, /observed_at.*runtime_id.*local_seq/is, 'sync must not invent a second event-ordering algorithm')
})
test('WHAT[DURABLE-CONVERGENCE-002] k-way cursor readiness is one finite state not parallel mutable axes', async () => {
  const primitive = await read('src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs')

  assert.match(primitive, /type private CursorReadiness\s*=\s*[\s\S]*Waiting[\s\S]*Queued[\s\S]*Exhausted/)
  assert.doesNotMatch(primitive, /mutable\s+(Remaining|Generation|MissingParents|Queued)\b/)
  assert.doesNotMatch(primitive, /\bGeneration\b/, 'the writer offset itself must be the waiter generation witness')
})
}
