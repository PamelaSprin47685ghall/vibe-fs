import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync } from 'node:fs'
import { admissionAuthority, capabilityToolNames, deliverDescription } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

test('WHAT[work-018] 对外注册名是 deliver，注册表和资源加载都不再认 suicide', () => {
  const names = capabilityToolNames('manager', 'work-main')
  assert.equal(names.includes('deliver'), true)
  assert.equal(names.includes('suicide'), false)
  assert.equal(admissionAuthority('deliver'), 'office')
  assert.equal(admissionAuthority('suicide'), 'unknown')

  const description = deliverDescription()
  assert.equal(description.includes('deliver'), true)
  assert.equal(description.includes('suicide'), false)
  assert.equal(existsSync('resources/provider/tool/suicide'), false)
})
