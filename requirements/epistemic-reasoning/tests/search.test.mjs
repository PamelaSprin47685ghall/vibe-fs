import assert from 'node:assert/strict'
import test from 'node:test'

import { mapOfEntries, solveGraph } from './support.mjs'

const map = (entries) => mapOfEntries(entries)
const edges = (rows) => rows.map(([from, to, cost]) => ({ from, to, cost }))
const problem = (start, goal, graphEdges, heuristic) => ({ start, goal, edges: graphEdges, heuristic })

test('WHAT[EPI-010] graph_astar_degenerates_to_standard_g_plus_h_shortest_path', () => {
  const solved = solveGraph(
    problem(
      'S',
      'G',
      edges([
        ['S', 'A', 1],
        ['S', 'B', 4],
        ['A', 'C', 1],
        ['C', 'G', 1],
        ['B', 'G', 1],
      ]),
      map([
        ['S', 3],
        ['A', 2],
        ['B', 1],
        ['C', 1],
        ['G', 0],
      ]),
    ),
  )
  assert.equal(solved.cost, 3)
  assert.deepEqual(solved.path, ['S', 'A', 'C', 'G'])
})

test('WHAT[EPI-010] graph_astar_reopens_closed_node_when_better_g_is_discovered', () => {
  const solved = solveGraph(
    problem(
      'S',
      'G',
      edges([
        ['S', 'A', 2],
        ['S', 'B', 2],
        ['A', 'C', 2],
        ['B', 'C', 1],
        ['C', 'G', 2],
      ]),
      map([
        ['S', 4],
        ['A', 1],
        ['B', 3],
        ['C', 0],
        ['G', 0],
      ]),
    ),
  )

  assert.equal(solved.cost, 5)
  assert.deepEqual(solved.path, ['S', 'B', 'C', 'G'])
  assert.ok(solved.expanded.filter((node) => node === 'C').length >= 2)
})

test('WHAT[EPI-010] graph_astar_rejects_negative_cost_graph', () => {
  const solved = solveGraph(problem('S', 'G', edges([['S', 'G', -1]]), map([['S', 0], ['G', 0]])))
  assert.equal(solved, null)
})
