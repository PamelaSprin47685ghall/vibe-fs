import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ABL-012] ablation tool map syncs with active roles and excludes deprecated browser/distiller tools', () => {
  const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))
  const tools = toolMap.tools || {}

  // New engineer tool must be mapped
  assert.ok(tools['js-engineer'], 'js-engineer must be mapped in tool-map.json')

  // Deprecated tools must be excluded from active tool map
  assert.equal(tools['browser'], undefined, 'browser tool must be excluded from tool-map.json')
  assert.equal(tools['distill'], undefined, 'distill tool must be excluded from tool-map.json')
  assert.equal(tools['query-shell'], undefined, 'query-shell tool must be excluded from tool-map.json')
  assert.equal(tools['js-coder'], undefined, 'js-coder tool must be excluded from tool-map.json')
  assert.equal(tools['js-inspector'], undefined, 'js-inspector tool must be excluded from tool-map.json')
})
