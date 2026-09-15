import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const nodesDoc = JSON.parse(read('resources/ablation/nodes.json'))
const profilesDoc = JSON.parse(read('resources/ablation/profiles.json'))

const packageDirs = () =>
  readdirSync(join(ROOT, 'requirements'), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .filter((name) => {
      try {
        read(`requirements/${name}/WHAT.md`)
        return true
      } catch {
        return false
      }
    })

test('WHAT[ABL-001] ABL_001_primary_nodes_cover_index_packages', () => {
  const primary = nodesDoc.nodes.filter((node) => node.kind === 'package').map((node) => node.id)
  const packages = packageDirs()
  assert.deepEqual(new Set(primary), new Set(packages))
})

test('WHAT[ABL-003] ABL_003_station_order_edges_are_acyclic_by_construction', () => {
  const edges = nodesDoc.edges.filter((edge) => edge.kind === 'station-order')
  const graph = new Map()
  for (const edge of edges) {
    if (!graph.has(edge.from)) graph.set(edge.from, [])
    graph.get(edge.from).push(edge.to)
  }
  const visiting = new Set()
  const visited = new Set()
  const visit = (node) => {
    if (visited.has(node)) return
    if (visiting.has(node)) throw new Error(`cycle at ${node}`)
    visiting.add(node)
    for (const next of graph.get(node) ?? []) visit(next)
    visiting.delete(node)
    visited.add(node)
  }
  for (const node of graph.keys()) visit(node)
})

test('WHAT[ABL-004] ABL_004_profiles_cover_all_primary_nodes', () => {
  const primary = nodesDoc.nodes.filter((node) => node.kind === 'package').map((node) => node.id)
  for (const [profileId, profile] of Object.entries(profilesDoc.profiles)) {
    for (const node of primary) {
      assert.ok(profile.modes?.[node], `${profileId} missing ${node}`)
    }
  }
})

test('WHAT[ABL-004] ABL_004_station_profiles_track_segment_unablation', () => {
  const primary = nodesDoc.nodes.filter((node) => node.kind === 'package')
  const segmentEnd = (profileStation) => {
    if (profileStation <= 4) return 4
    if (profileStation <= 14) return 14
    if (profileStation <= 21) return 21
    if (profileStation <= 30) return 30
    if (profileStation <= 41) return 41
    if (profileStation <= 49) return 49
    if (profileStation <= 54) return 54
    return 56
  }
  for (let station = 5; station <= 56; station++) {
    const profile = profilesDoc.profiles[`station-${String(station).padStart(2, '0')}`]
    assert.ok(profile, `missing station profile ${station}`)
    const activeThrough = segmentEnd(station)
    for (const node of primary) {
      const mode = profile.modes[node.id]
      if (node.id === 'delegation' && station >= 15 && station < 42) {
        assert.equal(mode, 'borrowed')
        continue
      }
      if (node.id === 'managed-session-lifecycle' && station >= 15 && station < 42) {
        assert.equal(mode, 'borrowed')
        continue
      }
      if (node.station <= activeThrough) {
        assert.equal(mode, 'active', `${node.id} should be active at station ${station}`)
      } else {
        assert.equal(mode, 'ablated', `${node.id} should stay ablated before segment unlock ${station}`)
      }
    }
  }
})
