// WHAT[repository-programming-065] 同一助理消息内的多个工具调用由宿主按确定
// 顺序逐个执行，各为独立事务，后者读取前者提交后的状态。
//
// 为什么这条不能用「测试自己串行调用」来证明：
//
//   * WHAT 的主语是**宿主**。早先的 017 用例自己写了一个 for 循环逐个调用
//     `WorkflowSurface.run`，那证明的是「两次 WorkflowSurface 调用串起来不丢
//     更新」，**没有**证明宿主把同一条助理消息里的多个工具调用按序交给产品。
//     一个把调用串起来的测试，在宿主并发派发时同样会绿 —— 它测的是自己。
//   * 同一条助理消息的身份就是 provider run。`Foundation/Identity.fs` 的
//     HOST-010/011：宿主在 `messages.transform` 之前创建并持久化那条助理消息，
//     并把**同一个 id** 交给该 run 里的每一个工具调用。所以「同一条助理消息」
//     = 一个 `ProviderRunIdentity`，不需要第二个身份类型，也不必给
//     `ToolInvocation` 加字段。
//   * 真实 OpenCode 宿主**会**在同一轮里并发派发工具调用：它的每个工具
//     `execute` 被包成独立 promise，其 MCP/工作流路径按 `pendingToolCount`
//     计数，计数归零前流不关闭。所以「宿主碰巧串行」不是一个可以依赖的前提。
//
// 本用例在**真实插件夹具**上，同一个 `messageID`（= 同一个 provider run）下
// **并发**发起两个真实 `todowrite-for-engineer` 工具调用，并断言 WHAT 的结果：
//
//   1. 两个调用都**成功**（并发下写会撞 CAS，得到 FILE_CHANGED —— 那是丢失
//      更新被挡住的表现，不是 WHAT 要求的「按确定顺序逐个执行」）；
//   2. 第二个调用读到的是第一个**已提交**的状态；
//   3. 同一文件的最终内容是顺序叠加的结果。
//
// 观测面全部是产品侧既有事实：工具入口的进入/离开时刻由本夹具在宿主工具
// 注册处打点（只观测，不改语义），最终状态由磁盘字节与渲染结果读回。

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import {
  acceptAuthorityRoot,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const SESSION = 'ses-017-host-dispatch'
const RUN = 'run-017-host-dispatch'

// 一条助理消息里的两个工具调用：各自读对方要写的那个文件，再追加自己的标记。
// 两段程序只有在**第二个读到第一个已提交的状态**时，才会得到 'base' 与
// 'base->call1' 这对观测值；若两者真重叠，第二个会读到 'base'，而写会撞上
// 第一个已提交的文件，撞出 FILE_CHANGED。
const appendProgram = (mark) => `class Js extends JsProgram {
  async run() {
    const text = await this.cat('shared.txt');
    this.write('shared.txt', text + '->${mark}');
    return { observed: text };
  }
}`

// 渲染结果是一个 TOML 文档：成功是 '# ok' 加 data 表，失败是 '# failed' 加
// code/reason。失败时没有 data 表。
const parsed = (result) => parseToml(String(result))

const observedOf = (result) => parsed(result).data?.observed

// 把一个工具注册包成「打点后转发」：只观测宿主把两个调用派发进来的时刻，
// 不改变任何语义。overlapped 报告两个调用的在飞区间是否真的交错 —— 这是
// 「宿主确实并发派发」的证据，失败消息里用来说明并发确实发生了。
const instrument = (hooks, log) => {
  const original = hooks.tool['todowrite-for-engineer']

  hooks.tool['todowrite-for-engineer'] = {
    ...original,
    execute: async (args, context) => {
      const entry = { callID: context.callID, enteredAt: Date.now(), leftAt: null }
      log.push(entry)

      try {
        return await original.execute(args, context)
      } finally {
        entry.leftAt = Date.now()
      }
    },
  }
}

const overlapped = (log) => {
  if (log.length < 2) return false

  const [first, second] = log

  return (
    first.enteredAt < (second.leftAt ?? Number.MAX_SAFE_INTEGER) &&
    second.enteredAt < (first.leftAt ?? Number.MAX_SAFE_INTEGER)
  )
}

test('WHAT[repository-programming-065] two tool calls of one assistant message commit in a deterministic serial order, the second reading the first commit', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'engineer')

    // 没有注册会话目录时，编程面在插件自己的工作区里执行（JsToolPolicy
    // .rootForSession 的 fallback 分支）。
    const root = directory
    writeFileSync(join(root, 'shared.txt'), 'base', 'utf8')

    const log = []
    instrument(hooks, log)

    // 同一条助理消息里的两个工具调用，**并发**发起：Promise.all 的元素按序求
    // 值，所以 call1 先被派发、call2 后被派发，而两者在产品侧同时在飞。
    const [first, second] = await Promise.all([
      hooks.tool['todowrite-for-engineer'].execute(
        { name: 'call1', program: appendProgram('call1'),
          prefixUntil: 0,
          carry: []
        },
        { sessionID: SESSION, agent: 'engineer', callID: 'call-017-1', messageID: RUN },
      ),
      hooks.tool['todowrite-for-engineer'].execute(
        { name: 'call2', program: appendProgram('call2'),
          prefixUntil: 0,
          carry: []
        },
        { sessionID: SESSION, agent: 'engineer', callID: 'call-017-2', messageID: RUN },
      ),
    ])

    // 观测 1：宿主把同一条助理消息的两个工具调用都派发到了产品侧。
    assert.equal(log.length, 2, 'both tool calls of the one assistant message must reach the product')
    assert.deepEqual(
      log.map((entry) => entry.callID).sort(),
      ['call-017-1', 'call-017-2'],
      'the two calls of the one assistant message must both be dispatched',
    )

    const concurrency = overlapped(log)
      ? 'the two calls were dispatched concurrently by the host'
      : 'the two calls did not overlap in the product'

    // 观测 2：两个调用都成功。按确定顺序逐个执行时，第二个调用看到的是第
    // 一个已提交的状态，不会撞上 CAS 而拿到 FILE_CHANGED。
    assert.equal(
      parsed(first).code,
      undefined,
      `the first call of the one assistant message must succeed: ${String(first)} (${concurrency})`,
    )
    assert.equal(
      parsed(second).code,
      undefined,
      `the second call of the one assistant message must succeed: ${String(second)} (${concurrency})`,
    )

    // 观测 3：第二个调用读到的是第一个**已提交**的状态。两者同属一条助理
    // 消息，WHAT [017] 要求它们按确定顺序逐个执行；真重叠的两个调用读到同
    // 一个旧快照，就会在这里表现为两个相同的观测值。
    assert.deepEqual(
      [observedOf(first), observedOf(second)],
      ['base', 'base->call1'],
      `the second call must read the first call's committed state (${concurrency})`,
    )

    // 观测 4：最终磁盘内容是顺序叠加的结果，没有丢失更新。
    assert.equal(
      readFileSync(join(root, 'shared.txt'), 'utf8'),
      'base->call1->call2',
      `the two calls of the one assistant message must commit in order, without a lost update (${concurrency})`,
    )
  })
})
