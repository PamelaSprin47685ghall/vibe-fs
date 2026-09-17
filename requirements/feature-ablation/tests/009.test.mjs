import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))

const nodes = JSON.parse(read('resources/ablation/nodes.json'))

const staticTools = read('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')

const knownMatch = staticTools.match(/let knownToolNames =\s*\[([\s\S]*?)\]/)

const knownTools = knownMatch ? [...knownMatch[1].matchAll(/"([^"]+)"/g)].map((match) => match[1]) : []

test('WHAT[ABL-009] ABL_009_every_known_tool_maps_to_a_manifest_node', () => {
  for (const tool of knownTools) {
    assert.ok(toolMap.tools[tool], `missing tool-map entry for ${tool}`)
    const node = toolMap.tools[tool]
    assert.ok(nodes.nodes.some((entry) => entry.id === node), `unknown node ${node} for ${tool}`)
  }
})

test('WHAT[ABL-009] ABL_009_tool_map_has_no_stale_entries', () => {
  for (const tool of Object.keys(toolMap.tools)) {
    assert.ok(knownTools.includes(tool), `stale tool-map entry ${tool}`)
  }
})
