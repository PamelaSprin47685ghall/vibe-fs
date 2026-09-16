// requirements/output-distillation/tests/distiller-role-contract.test.mjs
//
// Owner: output-distillation.
//
// DISTILL-014: 任意输出规模零 Distiller 模型会话与彻底去角色化。
// 验证 Distiller 角色及资源从系统角色目录与配置中彻底移除（P5 实施前必红，P5 后转绿）。

import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { allRoleLabels, allInternalRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

test.before(() => {
  installDefaultResources()
})

test('WHAT[DISTILL-014] distiller_role_is_completely_removed_from_role_catalogs_and_configs', () => {
  // 1. Roles 集合中不得存在 "distiller"
  assert.equal(
    allRoleLabels.includes('distiller'),
    false,
    'allRoleLabels must not contain "distiller"',
  )
  assert.equal(
    allInternalRoleLabels.includes('distiller'),
    false,
    'allInternalRoleLabels must not contain "distiller"',
  )

  // 2. ManagedAgentConfig 不得生成 distiller 代理配置
  const config = { agent: {} }
  for (const role of ['manager', 'orchestrator', 'engineer', 'devops', 'blogger', 'bookkeeper']) {
    config.agent[role] = { model: `${role}-model` }
  }
  const result = configureManagedAgents(config)
  assert.equal(result.ok, true)
  assert.equal(config.agent['distiller'], undefined, 'distiller agent must not be configured')

  // 3. 角色资源目录 resources/provider/role/distiller/ 必须已被彻底删除
  const distillerRoleDir = join(ROOT, 'resources/provider/role/distiller')
  assert.equal(
    existsSync(distillerRoleDir),
    false,
    'resources/provider/role/distiller directory must be deleted',
  )
})
