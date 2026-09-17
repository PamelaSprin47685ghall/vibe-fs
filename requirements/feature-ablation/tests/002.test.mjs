import assert from 'node:assert/strict'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'

const withEnv = (entries, run) => {
  const previous = Object.fromEntries(entries.map(([name]) => [name, process.env[name]]))
  try {
    for (const [name, value] of entries) {
      if (value === undefined) delete process.env[name]
      else process.env[name] = value
    }
    run()
  } finally {
    for (const [name, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[name]
      else process.env[name] = value
    }
  }
}

test('WHAT[ABL-002] ABL_002_tri_state_semantics_distinction', () => {
  // 1. ablated 状态：对应工具零副作用，既不得出现在 provider schema，也不得被 execution gate 放行
  withEnv([
    ['WANXIANGSHU_ABLATION_PROFILE', 'station-41'],
    ['WANXIANGSHU_ABLATION_speculative_investigation', 'ablated'],
  ], () => {
    Ablation.load()
    assert.equal(Ablation.allowsTool('speculate'), false, 'ablated node tool must not be allowed')
    assert.equal(Ablation.allowsToolSchema('speculate'), false, 'ablated node tool schema must not be allowed')
  })

  // 2. active 状态：包级机制按各自 WHAT 正常运行，工具在 schema 暴露且门禁放行
  withEnv([
    ['WANXIANGSHU_ABLATION_PROFILE', 'production'],
    ['WANXIANGSHU_ABLATION_speculative_investigation', 'active'],
  ], () => {
    Ablation.load()
    assert.equal(Ablation.allowsTool('read'), true, 'active node tool must be allowed')
    assert.equal(Ablation.allowsToolSchema('read'), true, 'active node tool schema must be allowed')
  })

  // 3. borrowed 状态：仅 HOW 与 manifest 明示的借用面可运行；不得触发完整包级下游语义
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-14']], () => {
    Ablation.load()
    // station-14 中 delegation 处于 borrowed/ablated 切面：允许读，但不允许完整 async fork/fission 语义
    assert.equal(Ablation.allowsTool('read'), true, 'borrowed foundation tool read must be allowed')
    assert.equal(Ablation.allowsTool('fork'), false, 'borrowed package full downstream tool fork must be denied')
    assert.equal(Ablation.fissionVisible(), false, 'borrowed package downstream fission must remain ablated')
  })

  // 4. 非法状态组合 fail-closed：传入非法模式字符串必须抛出异常拒绝加载
  withEnv([['WANXIANGSHU_ABLATION_speculative_investigation', 'invalid_mode']], () => {
    assert.throws(() => {
      Ablation.load()
    }, /InvalidMode|fail-closed|ablation/i, 'Invalid ablation mode string must fail-closed')
  })
})
