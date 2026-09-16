import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const factMap = JSON.parse(read('resources/ablation/fact-map.json'))
const nodes = JSON.parse(read('resources/ablation/nodes.json'))

test('WHAT[ABL-011] ABL_011_every_mapped_fact_targets_manifest_node', () => {
  for (const [tag, node] of Object.entries(factMap.facts)) {
    assert.ok(nodes.nodes.some((entry) => entry.id === node), `${tag} -> unknown node ${node}`)
  }
})
