我按新快照重新审了一遍。**这次比上一版扎实很多，我会撤销上次大约一半的判红；但还不能给“状态机余孽清零 / completed 全部可信”盖章。**

这份新文件确实是整个仓库的 Repomix 合并快照，可用于直接做代码级复审。

### 先说已经真修掉的

**1. DSL ownership 漏扫 `Infrastructure/Journal`：已修。**

现在 `dsl-ownership.mjs` 从整个 `src/Wanxiangshu` 递归取所有 `.fs`，不再用原来的 `PROGRAM_DIRS` 半仓扫描。

我把新 Repomix 解出来直接跑了：

```text
dsl-ownership: OK — 246 Program/Domain files
architecture: OK — 246 文件
dsl ownership + ratchet 单测：65/65 pass
ratchet baseline：OK
```

这里输出字符串还叫 `Program/Domain files`，只是旧文案；实际扫描确实是 **246 个生产 F# 文件**。

所以我上次说的“44.5% 源码盲区”现在可以正式撤销。

**2. `TurnUnknown`：这次是真 Clean Break。**

当前 `TurnOutcome` 只剩 InProgress / NeedsContinuation / Completed / Aborted / Failed；`TurnUnknown` 被降成 reconciliation-private 的 `SnapshotObservation`，而 `PublishTurn.Outcome` 只能是正式 `TurnOutcome`。

这是我上次要求的结构性修复，不是加一个 if。

**3. Executor 的 100ms timer re-probe：也真拆了。**

现在 FamilyWaiting 后不会 `timerTask 100 → probe → timerTask 100`。它先记录 Journal revision，重试条件来自 `AwaitJournalChangeFrom`，wall clock 只与总 deadline race。

这个现在属于：

```text
真实 Journal event
        ↓
重新检查 permit
```

而不是：

```text
100ms
 ↓
看看好了没
 ↓
100ms
```

我接受这个整改。

而且 `fix-revise.md` 后来主动撤销了“DSL 静态门已经能识别 timer re-probe”这个 overclaim，没有继续硬吹自动证明，这点反而是好的。

---

## 但还有两个问题，我认为一个是明确未闭环，一个是状态机核心嫌疑

### P0/P1：`corrective.md` 的完整 E2E close 条件其实仍然没完成

原冻结验收明确写的是：

> `build / unit / integration / e2e / architecture+spec gates 全绿`。

而 `package.json` 非常明确：

```text
npm run check
= lint
+ build
+ unit
+ integration

test:e2e 是另外一条命令
check:release 才会执行 test:e2e -- --repeat 3
```



更关键的是，`corrective.md` / `fix.md` 最后自己已经承认：

> 本次 `npm run check` 不包含完整 `test:e2e`；close 证据只有 `manager-unhappy-path` 和 `devops-mechanical-repair-loop` 两条定向 canary，**并没有跑完整 E2E suite**。

因此这里逻辑非常简单：

```text
冻结条件：
e2e gate 全绿

实际证据：
2 个 targeted e2e 全绿

2 targeted e2e ≠ e2e gate 全绿
```

这次比上一版好在**它没有继续隐瞒，而是在文末诚实澄清**。

但诚实澄清并不能让验收条件自动消失。

所以 `corrective.md` / `fix.md` 现在仍存在：

> **“Final outcome 宣布 close，但自己又承认一个冻结 close 条件实际上没执行。”**

如果按仓库自己“批准范围不得自行缩水”的规则，这仍然不能算严格 Completed。

最简单的修法不是再写文档：**跑一次完整 `npm run test:e2e`；如果你们真正的发布 proof 要求 repeat 3，则直接跑 `check:release`。**

---

# 最关键：Student–Teacher 仍然有“分布式状态机”的实质嫌疑

这是我这轮最不满意的地方。

新正式 DSL 规则自己写得非常好：

> 多个长期 registry / Dictionary / HashSet 的 presence，如果被同一个 `HandleTurn` / `observe` 联合用于决定下一步业务动作，就是**隐式程序计数器，等价于单 record 状态机**。

但是 Student–Teacher 的 manual proof 宣称六个 registry：

```text
runs
teacherOwners
teacherCalls
teacherCompletions
studentFinalCompletions
skillMutations
```

都是 physical mailbox，因此“不构成 lifecycle stage PC”。

我重新对了真实 `HandleTurn`。

Student 路径实际上是：

```text
tryRun presence
  ↓
tryFinalCompletion presence
  ↓
currentStudentRequestKind
  ↓
选择：
  final complete / release
  StudentLearn → sendCompile
  StudentCompile → sendCompile nudge
```

生产代码就是这样。

Teacher 路径更加明显：

```text
tryTeacherCall presence
        ↓
tryTeacherCompletion presence
        ↓
如果 Some:
    consume completion
    remove teacherCompletions
    remove teacherCalls
    complete parent waiter

如果 None:
    sendTeacherNudge
```



这和它自己的 DSL 判据存在非常直接的张力。

换句话说，现在只是把原来的：

```fsharp
State = WaitingTeacherReturn
State = WaitingTeacherCompletion
State = Compile
```

变成：

```text
teacherCalls exists?
teacherCompletions exists?
studentFinalCompletions exists?
PromptAuthority says Compile?
```

然后在 `HandleTurn` 里重新推导“程序现在在哪一步”。

### 我还专门打了一个反作弊实验

我用当前 `scanText` 构造了与这里同形的最小代码：

```text
registry A
registry B

tryA()
tryB()

handle:
    match tryA() with
    | Some ->
        match tryB() with
        | Some -> sendComplete
        | None -> sendNudge
```

当前 DSL gate 返回：

```text
[]
```

**完全不报。**

原因也被 proof 自己承认了：自动 `registry-joint-branch` 只抓“同一个 match/if 里直接 `.TryGetValue` 两个 registry”的窄语法；只要包进 `tryTeacherCall` / `tryTeacherCompletion` 两个辅助函数，自动门就看不到。 

这正好就是当前生产代码的写法。

---

## 所以 Student–Teacher 到底算不算状态机？

我会判：

**目前不能接受“已人工证明不是状态机”的结论。**

这和上次不同：上次我是说“门禁证明不了，所以有风险”；这次把代码和它自己新写的判据放在一起后，问题更明确了。

特别是 Teacher 这一段：

```text
teacherCalls = Some
teacherCompletions = None
→ send nudge

teacherCalls = Some
teacherCompletions = Some
→ consume completion / complete waiter
```

这里 `teacherCompletions` 的 **presence 本身就在选择下一业务动作**。

它当然携带真实物理 payload，这一点没错。但：

> **“里面装着真实 payload”并不能证明“它的 presence 没有同时充当程序 counter”。**

一个对象完全可以既是 mailbox，又被滥用为 stage bit。

这就是现在 manual proof 最偷换概念的地方。

我不会把它定性为故意“阳奉阴违”，因为文档已经诚实披露自动 detector 看不见跨 helper 的组合；但是：

**manual proof 给自己的核心嫌疑代码判了无罪，而它引用的正式判据反而很像应该判有罪。**

这项我会给 **REVISE**。

---

# 怎么彻底解决 Student–Teacher，而不是再加强 regex

我不建议继续做：

```text
registry-joint-branch-v2
try* helper scanner
更多字符串规则
```

那会进入猫鼠游戏。

应该让 Teacher 的物理 call scope 自己拥有完整 await：

```fsharp
task {
    let! call = beginTeacherCall ...
    do! sendQuestion call

    let! returned =
        call.Returned.Task

    do! sendTeacherReturnCompletion returned

    let! terminal =
        call.Completion.Task

    return completeParent terminal
}
```

即：

> **Teacher call 的生命周期由一个 CE 的调用栈拥有。**

Host event 只 resolve 相应 capability/TCS：

```text
tool return event
→ resolve Returned

provider terminal event
→ resolve Completion
```

而不是：

```text
Host event
→ 查 teacherCalls
→ 查 teacherCompletions
→ 反推出下一步做什么
```

Student compile 也一样：

```text
StudentLearn CE
   ↓
await terminal
   ↓
send compile
   ↓
await compile terminal
   ↓
await final return
```

PromptAuthority 可以继续是 durable authority evidence；registry 可以继续存在作为**事件投递地址**。

但是 registry presence 不应决定 program counter。

这才是真正符合 `ce.md` 最初那句话：

> **F# 调用栈就是流程栈。**

---

## 其他几个整改点，我目前没有再判红

Finality 这一轮看起来比之前健康很多。`FinalityController` 的 record-ready 已经用 `RecordReady | AwaitJournal | RecordUnavailable` 临时 decision + `AgentJournal.awaitChangeFrom` 递归等待，没有看到重新持久化 `ReviewRound/NextReviewerIndex` 之类程序位置；并发 `remaining/ref/results` 仍是函数内部 algorithm scratch，我仍判合法。

`TurnUnknown` 修复也合格。

Executor 的 deadline + Journal waiter 我现在也判合格：timer 只结束总 deadline，不负责推进业务状态。

DevOps behavioral e2e 也确实不只是 prompt regex：场景要求真实 executor/read/coder、RED→GREEN、文件内容最终变化，而且 DevOps 自己不能拿 write/edit。虽然 provider 是 strict scripted mock，它至少证明了 runtime/tool contract 能跑完整闭环，不再是纯提示词测试。

---

# 新版总评级

| 项目                        |  上次 |                      本次 |
| ------------------------- | --: | ----------------------: |
| DSL 全仓覆盖                  |  🔴 |               **🟢 已修** |
| Infrastructure/Journal 逃逸 |  🔴 |               **🟢 已修** |
| `TurnUnknown` 稳定态         |  🟠 |    **🟢 已 Clean Break** |
| Executor 100ms polling    |  🔴 |           **🟢 已改事件驱动** |
| DevOps 行为测试               |  🔴 |               **🟢 已补** |
| 第二 Manager idle E2E       |  🔴 |            **🟢 看实现已补** |
| EXEC_025 无界等待             |  🔴 |             **🟢 已有界化** |
| Finality LWR race         | 新发现 |            **🟢 看起来闭环** |
| 完整 E2E close gate         |   — |         **🔴 仍未满足冻结条件** |
| Student–Teacher 隐式状态机     |  🟠 | **🔴 我现在反而更明确判 REVISE** |

### 最终裁决

这版已经不能再说“主要是表面修复”。**大量整改是真的，而且针对性很强。**

但如果你的问题仍然是：

> “现在能不能宣布 completed/ 已经彻底消灭状态机余孽？”

我的答案仍然是：

**不能。**

现在剩下的核心不再是显眼的 `RunState` / `CurrentStage` / bool 地狱，而是更高级的一种：

> **用若干真实 physical mailbox/registry 的 presence 组合，重新推导程序位置。**

Student–Teacher 当前就是最值得继续开刀的一处。

此外，`corrective/fix` 的完整 E2E gate 还差一次真正的全套执行；文件自己已经承认只跑了两条 targeted canary，因此在治理意义上也还不应完全封板。

**我的建议：下一刀不要再改静态 regex，直接把 Student/Teacher 的 call → return → completion 生命周期收回一个递归 CE / capability-await 结构；然后跑完整 `test:e2e`（最好直接 `check:release`）再封板。**
