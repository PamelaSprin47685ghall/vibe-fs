import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync } from 'node:fs'
import { rolePermissionRules } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { admissionAuthority, capabilityToolNames, deliverDescription } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

test('评审未接纳前 Manager 的写工具在执行门投影上被拒绝', () => {
  const rules = rolePermissionRules('manager')
  assert.equal(rules.edit, 'deny')
  assert.equal(rules.write, 'deny')
})

test('对外注册名是 deliver，注册表和资源加载都不再认 suicide', () => {
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
