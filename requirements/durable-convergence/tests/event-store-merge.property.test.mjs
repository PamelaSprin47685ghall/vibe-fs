import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'

const makeId = (index) => (index + 1).toString(16).padStart(40, '0')

const node = fc.record({
  writerSeed: fc.nat(),
  parentSeeds: fc.array(fc.nat(), { maxLength: 4 }),
  payload: fc.record({ sequence: fc.integer(), text: fc.string({ maxLength: 24 }) }),
})

const scenario = fc.integer({ min: 2, max: 8 }).chain((writerCount) =>
  fc.record({
    writerCount: fc.constant(writerCount),
    nodes: fc.array(node, { minLength: 1, maxLength: 18 }),
    writerOrder: fc.shuffledSubarray(
      Array.from({ length: writerCount }, (_, index) => index),
      { minLength: writerCount, maxLength: writerCount },
    ),
  }),
)

const eventOf = (nodes, index) => ({
  id: makeId(index),
  stream: 'property/k-way',
  type: 'JobRequested',
  parents:
    index === 0
      ? []
      : [...new Set(nodes[index].parentSeeds.map((seed) => makeId(seed % index)))].sort(),
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

test('WHAT[DURABLE-CONVERGENCE-002] cross-writer dependency is ordered before its child', () => {
  const parent = eventOf([{ parentSeeds: [], payload: { sequence: 0, text: 'parent' } }], 0)
  const child = {
    ...eventOf(
      [
        { parentSeeds: [], payload: { sequence: 0, text: 'parent' } },
        { parentSeeds: [0], payload: { sequence: 1, text: 'child' } },
      ],
      1,
    ),
    parents: [parent.id],
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
      const events = generated.nodes.map((_, index) => eventOf(generated.nodes, index))
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
    fc.property(fc.array(node, { minLength: 3, maxLength: 24 }), (nodes) => {
      const streams = Array.from({ length: 3 }, (_, writer) => [
        `writer-${writer}`,
        nodes
          .map((_, index) => eventOf(nodes, index))
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
