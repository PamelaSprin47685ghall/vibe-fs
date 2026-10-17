import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')

const read = (path) => readFileSync(path, 'utf8')

// Algorithm G（PLAN S3）：泄漏兜底三件套——交付硬门禁、每回合提醒、会话终结
// 强制 reap。本文件验证调用侧接入点与双语资源成对性。现场表与 reap/审计
// append 接口由 lane-a-core / lane-b-durable 定义，本文件不重复定义它们，
// 只核对本任务独占文件中的调用契约与资源存在性。

test('WHAT[repository-programming-031] deliver hard gate is wired at the deliverSpec execute chain', () => {
  const registry = read(join(SOURCE_ROOT, 'OpenCode/Tools/ToolRegistry.fs'))

  // G1 门禁路径常量与资源路径一致。
  assert.match(registry, /DeliverBlockedByParked = "tool\/js-program\/parked-deliver-blocked"/)

  // 门禁调用 lane-a-core 的只读查询接口，且位于 deliver 执行链（plan/manager
  // 分派之前），拒绝时列出全部 handle。
  assert.match(registry, /let parkedHandles = JsParkedSites\.parkedHandles \(\)/)
  assert.match(registry, /if not \(List\.isEmpty parkedHandles\)/)
  assert.match(registry, /parked_handles/, 'refusal must carry the parked handle list')
  assert.match(registry, /Path\.DeliverBlockedByParked/)
  assert.match(registry, /Some \[ "S1"; "S2"; "S3" \] -> return! planSpec\.Execute args ctx/)
})

test('WHAT[repository-programming-031] parked reminder rides the PairHint composeMarkerDocument', () => {
  const transform = read(join(SOURCE_ROOT, 'OpenCode/Host/PairProgrammingThoughtTransform.fs'))

  // G2 资源路径常量。
  assert.match(transform, /ParkedHintPath = "tool\/js-program\/parked-hint"/)

  // 提醒并入 composeMarkerDocument：查询全部未表态 handle，非空时渲染提醒并
  // 追加为 marker 文档指令。无载体回合不走到 composeMarkerDocument（由既有
  // decideCurrentPlacement 保证），因此不会凭空注入。
  assert.match(transform, /let parkedHandles = JsParkedSites\.parkedHandles \(\)/)
  assert.match(transform, /LlmFacing\.withInstructions parkedHint/)
  assert.match(transform, /ProviderProse\.render language ParkedHintPath/)
})

test('WHAT[repository-programming-031] session close force-reaps parked sites owned by the session', () => {
  const scope = read(join(SOURCE_ROOT, 'OpenCode/Host/PluginSessionScope.fs'))

  // G3：ClearSession 清理链中调用 lane-a-core 的 reap 接口，对 session 名下
  // 全部 Parked 现场执行算法 F，durable 记 Reaped。
  assert.match(scope, /JsParkedSites\.reapSession sessionId/)
  assert.match(scope, /do! attempt \(fun \(\) -> JsParkedSites\.reapSession sessionId\)/)
})

test('WHAT[repository-programming-031] parked reminder and deliver-block resources are paired bilingual files', () => {
  for (const name of [ 'parked-deliver-blocked', 'parked-hint' ]) {
    const zh = join(ROOT, `resources/provider/tool/js-program/${name}/zh-CN.md`)
    const en = join(ROOT, `resources/provider/tool/js-program/${name}/en.md`)
    assert.equal(existsSync(zh), true, `${name}/zh-CN.md must exist`)
    assert.equal(existsSync(en), true, `${name}/en.md must exist`)

    // provider-language-006/007: 双语资源成对，且占位符集合一致。
    const zhText = read(zh)
    const enText = read(en)
    const placeholders = (text) => [ ...text.matchAll(/\{\{(\w+)\}\}/g) ].map((m) => m[1]).sort()
    assert.deepEqual(placeholders(zhText), placeholders(enText), `${name} placeholder parity`)
    assert.ok(zhText.includes('{{handles}}'), `${name}/zh-CN.md must carry {{handles}}`)
    assert.ok(enText.includes('{{handles}}'), `${name}/en.md must carry {{handles}}`)
  }
})

test('WHAT[repository-programming-031] the three call sites stay within the owned files and do not define the parked table', () => {
  const registry = read(join(SOURCE_ROOT, 'OpenCode/Tools/ToolRegistry.fs'))
  const transform = read(join(SOURCE_ROOT, 'OpenCode/Host/PairProgrammingThoughtTransform.fs'))
  const scope = read(join(SOURCE_ROOT, 'OpenCode/Host/PluginSessionScope.fs'))

  // 调用方身份：只调用 JsParkedSites，不定义现场表或 reap 接口本身。
  for (const source of [ registry, transform, scope ]) {
    assert.doesNotMatch(source, /type JsParkedSite\b/, 'caller must not define the parked site type')
    assert.doesNotMatch(source, /module JsParkedSites\s*=|module JsParkedSites\b[^\n]*=/, 'caller must not define the parked site table module')
  }
})
