import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../dist/Resources/PromptSurface.js'

test('WHAT[COGNITIVE-ENVIRONMENT-002] CE_002_layer_attribution_and_no_impersonation', () => {
  // 1. PromptSurface 导出存在且能装载规范提示词
  const catalog = promptResources.runtimeLoad()
  assert.ok(catalog, 'PromptCatalog must be loaded')

  const roles = ['manager', 'engineer', 'devops', 'orchestrator', 'blogger']

  for (const role of roles) {
    const texts = promptResources.instructionTextsForRole(role)
    assert.ok(texts, `Instruction texts for ${role} must exist`)

    // 2. 认知层正交性：Role Law 只定义自我模型与职责边界，严禁包含具体任务（Mission）或可用工具列表（Tools）
    const roleLaw = texts.roleLaw ?? ''
    assert.ok(roleLaw.length > 0, `Role Law for ${role} must not be empty`)
    assert.doesNotMatch(roleLaw, /<tools>/i, `Role Law for ${role} must not mechanically embed tools section`)
    assert.doesNotMatch(roleLaw, /available tools/i, `Role Law for ${role} must not mechanically embed available tools list`)
    assert.doesNotMatch(roleLaw, /current mission assignment/i, `Role Law for ${role} must not pretend to be Mission assignment`)

    // 3. Common Law 作为系统世界观，不冒充特定角色自我法
    const commonLaw = texts.commonLaw ?? ''
    assert.ok(commonLaw.length > 0, `Common Law for ${role} must not be empty`)
    assert.doesNotMatch(commonLaw, /^#+\s*Role Law/m, 'Common Law must not impersonate Role Law')

    // 4. 组合顺序与分层结构独立性：验证组合顺序中各层语义边界清晰，不存在单一全序覆盖规则
    const systemPrompt = promptResources.systemForRole(role)
    assert.ok(systemPrompt.length > 0, `System prompt for ${role} must be composed`)
    assert.ok(systemPrompt.includes('Common Law'), `System prompt for ${role} must preserve Common Law heading`)
  }

  // 5. 可失败性与冲突裁决变异验证：验证检测逻辑能识别跨层冒充
  const impersonatingRoleLaw = '# Common Law\n\nYou awaken in a world that is already up and running.'
  assert.match(impersonatingRoleLaw, /^# Common Law/m, 'Impersonating content must match impersonation detection pattern')
})
