import assert from 'node:assert/strict'
import test from 'node:test'
import { applyView, captureView, clearAll, takeView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

// WHAT[context-compression-032] 报告材料是只读投影：投影按 reportMapping 渲染
// 调用方当前会话的可见区间，它不消费待消费的声明，也不写入永久替换；非法形状
// 以 VIEW_DECLARATION_INVALID 整体拒绝，不抵达委派宿主、不留下部分效果。
//
// 取证面全部是已注册的公开边界：
//   * 真实委派成员 this.fork(payload, reportMapping) —— 报告材料投影的唯一生产
//     消费者。它把投影结果作为子会话提示词里的 `commissioner_record` 交出去，
//     并把非法映射作为稳定数据信封 `{ ok: false, code, reason }` 退回。
//   * dist/Repository/Programming/Js/Anchor/AnchorSurface.js —— 会话可见区间的
//     公开状态：applyView 渲染可见区间并回报 report.replaced；captureView /
//     takeView 存取待消费声明。全部是 JS-native 普通值。
//
// 被删除的对内部表示的断言（原先深导入 dist/Repository/Programming/Js/Failure.js
// 取 JsFailureModule_code，并直接读 F# Result 的 union 标签与 fields）：失败码与
// 投影材料都有公开等价物 —— 真实委派成员返回稳定数据信封，投影材料出现在子会话
// 收到的 `commissioner_record` 里；本文件不再读取任何 union 表示。

const session = 'report-material'
const visible = () => [
  { id: '1', text: 'one' },
  { id: '2', text: 'two' },
  { id: '3', text: 'three' },
]

// The material the projection handed to the delegate, read off the child
// prompt the real fork member produced.
const commissionerRecord = (prompt) => {
  if (!prompt) return null
  const text = (prompt.body?.parts ?? []).map((part) => part.text).join('\n')
  const line = text.split('\n').find((candidate) => candidate.startsWith('commissioner_record = '))
  if (!line) return null
  return line.slice('commissioner_record = '.length).replace(/^"|"$/g, '').replace(/\\n/g, '\n')
}

// One real fork member call: the projection runs on the caller's session and
// its material reaches the child. The reply is the member's data envelope.
const withForkMember = async (body) => {
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    await acceptAuthorityRoot(runtime, session, 'manager', 'phys-report-material')
    const call = async (mapping, callId) => {
      const before = created.length
      const promptsBefore = runtime.prompts.length
      const reply = await hooks.tool['todowrite-for-manager'].execute(
        {
          program:
            `class Js extends JsProgram { async run() { return await this.fork({ name: "Ada${callId}", charge: "DO THE WORK" }, ${mapping}); } }`,
        },
        { sessionID: session, agent: 'manager', callID: callId, messageID: `run-${callId}` },
      )
      const child = created.length > before ? created[created.length - 1] : null
      const prompt =
        runtime.prompts
          .slice(promptsBefore)
          .find((candidate) => candidate.path?.id === child) ?? runtime.prompts[promptsBefore]
      return { reply: String(reply), child, record: commissionerRecord(prompt) }
    }

    await body(call)
  })
}

test.beforeEach(() => clearAll())

test('WHAT[context-compression-032] a legal report mapping projects the visible interval as the material the delegate receives', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())

    const outcome = await call('{ 2: "summary-two" }', 'cc-032-ok')

    assert.match(outcome.reply, /ok = true/, `the member call must succeed, got: ${outcome.reply}`)
    assert.ok(outcome.child, 'a legal mapping places the child')
    assert.equal(
      outcome.record,
      '1: one\n2: summary-two\n3: three',
      'the projection renders the visible interval with the declared summary',
    )
  })
})

test('WHAT[context-compression-032] the same mapping projects the same material on every call', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())

    const first = await call('{ 2: "summary-two" }', 'cc-032-repeat-1')
    const second = await call('{ 2: "summary-two" }', 'cc-032-repeat-2')

    assert.match(first.reply, /ok = true/)
    assert.match(second.reply, /ok = true/)
    assert.equal(first.record, second.record, 'the same mapping projects the same material')
  })
})

test('WHAT[context-compression-032] an illegal report mapping is refused as a data envelope and places no child', async () => {
  const illegal = [
    ['a non-string summary', '{ 1: 7 }'],
    ['an empty summary', '{ 1: "" }'],
    ['a blank summary', '{ 1: "   " }'],
    ['a boolean summary', '{ 1: true }'],
    ['an object summary', '{ 1: {} }'],
    ['a negative anchor', '{ "-1": "summary" }'],
    ['a fractional anchor', '{ "1.5": "summary" }'],
    ['a non-numeric anchor', '{ x: "summary" }'],
  ]

  await withForkMember(async (call) => {
    applyView(session, visible())

    for (const [label, mapping] of illegal) {
      const outcome = await call(mapping, `cc-032-bad-${label.replace(/\s+/g, '-')}`)
      assert.match(outcome.reply, /ok = false/, `${label} must be refused, got: ${outcome.reply}`)
      assert.match(outcome.reply, /code = "VIEW_DECLARATION_INVALID"/, `${label} must carry the refusal code`)
      assert.match(outcome.reply, /reason = "/, `${label} must carry a reason`)
      assert.equal(outcome.child, null, `${label} must not reach the delegation host`)
      assert.equal(outcome.record, null, `${label} must leave no material behind`)
    }
  })
})

test('WHAT[context-compression-032] an empty report mapping is legal and introduces no summary material', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())

    const outcome = await call('{}', 'cc-032-empty')

    assert.match(outcome.reply, /ok = true/, `an empty mapping must be legal, got: ${outcome.reply}`)
    assert.ok(outcome.child, 'an empty mapping still places the child')
    assert.equal(
      outcome.record,
      '1: one\n2: two\n3: three',
      'an empty mapping projects the visible interval unchanged',
    )
  })
})

test('WHAT[context-compression-032] the projection is read-only: it neither consumes the pending declaration nor writes a replacement', async () => {
  await withForkMember(async (call) => {
    applyView(session, visible())

    // A pending declaration waits to be consumed by a later take.
    assert.equal(captureView(session, { prefixUntil: 1, thenMap: { 1: null } }).ok, true)

    const outcome = await call('{ 2: "summary-two" }', 'cc-032-readonly')
    assert.match(outcome.reply, /ok = true/)
    assert.equal(outcome.record, '1: one\n2: summary-two\n3: three')

    assert.deepEqual(
      takeView(session),
      { prefixUntil: 1, thenMap: { 1: null } },
      'the projection leaves the pending declaration pending and unchanged',
    )
    assert.equal(takeView(session), null, 'only the take consumes the pending declaration')

    const after = applyView(session, visible())
    assert.deepEqual(after.report.replaced, [], 'the projection wrote no permanent replacement')
    assert.deepEqual(
      after.entries.map((entry) => (entry.kind === 'kept' ? entry.message.text : entry.summary)),
      ['one', 'two', 'three'],
      'the original content survives a projection that showed a summary',
    )
  })
})
