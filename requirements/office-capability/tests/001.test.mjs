import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, 'resources/provider', rel), 'utf8')

test('WHAT[OFF-001] OFF_001_office_capability_is_consequence_not_tool_whitelist', () => {
  const managerEn = read('role/manager/en.md')
  assert.match(managerEn, /Know another office by its promises, not by its keys/i)
  assert.match(managerEn, /not by the instruments hidden[\s\S]{0,20}inside it/i)
  assert.match(managerEn, /Do not prescribe the hidden instruments of another office/i)
})
