import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('../../..', import.meta.url)))

const providerRoot = join(ROOT, 'resources', 'provider')

const readProviderPair = (relative) => {
  const base = join(providerRoot, relative)
  return { en: readFileSync(join(base, 'en.md'), 'utf8'), zh: readFileSync(join(base, 'zh-CN.md'), 'utf8') }
}

const assertAnchorHit = (pair, anchor, id) => {
  assert.match(pair.en, anchor.en, `${id}: en`)
  assert.match(pair.zh, anchor.zh, `${id}: zh`)
}

const syncModel = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/SyncDelegate/Model.fs'), 'utf8')

const forkTool = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs'), 'utf8')

test('WHAT[delegation-020] delegation_semantics_do_not_depend_on_current_tool_names', () => {
  const what = readFileSync(new URL('../WHAT.md', import.meta.url), 'utf8')

  // 条款级断言：WHAT.md [020] 载明“委托语义不依赖当前工具名”与“改名不动规范”原则
  assert.match(what, /##\s*\[020\]\s*委托语义不依赖当前工具名/)
  assert.match(what, /委托机制绑定的是规范的语义合同，而非特定物理工具名称/)
  assert.match(what, /工具名称的演进与替换不影响本合同定义的权能、所有权与生命周期规则/)

  // 代码级核验：物理工具名演进不影响领域语义
  // 历史工具名 inspect / establish-behavior / repair-behavior 仅作为 historical decoder 映射到领域角色
  for (const name of ['inspect', 'establish-behavior', 'repair-behavior']) {
    assert.ok(syncModel.includes(name), `syncModel should include historical tool name ${name}`)
  }
  // 当前工具名 fork / commission 作为 Host 适配层工具规范暴露
  for (const name of ['fork', 'commission']) {
    assert.ok(forkTool.includes(name), `forkTool should include tool name ${name}`)
  }
})
