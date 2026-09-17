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
