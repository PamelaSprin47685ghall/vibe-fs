import assert from 'node:assert/strict'
import test from 'node:test'

import { mapOfEntries, toList } from '../../verification-system/tests/support/domain.mjs'
import { AStarProblem, GraphEdge, solveGraph } from '../../../dist/Sphinx/Search.js'

const map = (entries) => mapOfEntries(entries)
const edges = (rows) => toList(rows.map(([from, to, cost]) => new GraphEdge(from, to, cost)))

test('WHAT[EPI-010] graph_astar_degenerates_to_standard_g_plus_h_shortest_path', () => {
  const problem = new AStarProblem(
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
  )

  const solved = solveGraph(problem)
  assert.equal(solved.Cost, 3)
  assert.deepEqual([...solved.Path], ['S', 'A', 'C', 'G'])
})

test('WHAT[EPI-010] graph_astar_reopens_closed_node_when_better_g_is_discovered', () => {
  const problem = new AStarProblem(
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
  )

  const solved = solveGraph(problem)
  assert.equal(solved.Cost, 5)
  assert.deepEqual([...solved.Path], ['S', 'B', 'C', 'G'])
  assert.ok([...solved.Expanded].filter((node) => node === 'C').length >= 2)
})

test('WHAT[EPI-010] graph_astar_rejects_negative_cost_graph', () => {
  const problem = new AStarProblem('S', 'G', edges([['S', 'G', -1]]), map([['S', 0], ['G', 0]]))
  assert.equal(solveGraph(problem), undefined)
})
