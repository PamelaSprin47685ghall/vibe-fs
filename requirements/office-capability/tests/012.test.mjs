import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[OFF-012] orchestrator_commissions_manager_roads_not_phases', () => {
  const en = readRole('orchestrator', 'en.md')
  const zh = readRole('orchestrator', 'zh-CN.md')
  assert.match(en, /You commission independent destinations, not technical phases/i)
  assert.match(en, /give it its own Manager and its own road/i)
  assert.match(zh, /你委派的是彼此独立的目的地，而不是技术阶段/)
  assert.match(zh, /给它自己的 Manager，给它自己的道路/)
})
