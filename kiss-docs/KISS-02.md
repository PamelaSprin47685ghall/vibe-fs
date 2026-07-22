# 领域动词契约

承 KISS-01。`do! s.SomeVerb()` 成功后，**在本 Runtime 可见状态上**调用者能依赖什么？

---

## 一、三条通道 [NORMATIVE]

成功 DU · Flow `'error` · 异常/OCE（领域 run 映射）。  
禁止 `match! Ok|Error`。见既有 SendOutcome 模式。

跨 Runtime：不保证他进程实时后置；只保证本进程 commit 后的 read-your-writes 与重启后时间线 Fold（KISS-04）。

### 1.1 为什么需要成功 DU

`TrySendContinue` 的 transient failure 不是流程坏了，而是 `ContinueWork` 仍可决定重试、换模或最终失败的业务输入。若直接成为 Flow Error，调用方失去合法匹配点；若抛异常，策略会依赖 try/with。

```
TrySendContinue → SendOutcome
ContinueWork   → 消化 Retryable / Unknown / Fatal
Session Flow   → 只暴露完成或明确终止
```

### 1.1 为什么要区分成功 DU

`TrySendContinue` 的 transient failure 不是“流程坏了”，而是 `ContinueWork` 仍可以决定重试、换模或最终失败的业务输入。若它直接成为 Flow Error，调用方没有合法的模式匹配位置；若它被异常抛出，重试策略会依赖 try/with 控制流。

```
TrySendContinue → SendOutcome
ContinueWork   → 消化 Retryable / AcceptanceUnknown / Fatal
Session Flow   → 只暴露完成或明确终止
```

这让主流程保持 `do! s.ContinueWork()`，同时让内部策略拥有强类型分支。

---

## 二、成功后置 [NORMATIVE]

|动词|本 Runtime 成功后置|
|---|---|
|`ContinueWork`|本侧一轮终态；本侧 Todo 投影已更新；本地 ProgressStamp 前进；本地 sendOnce 完成|
|`RequestReview`|`ReviewApplied` 已写入本日志；本侧 Required/Todo 已更新|
|`Finish`|本侧收尾或回传|
|prompt|本侧协议进入明确态|
|`Child.Run`|本侧 waiter→send→terminal→本日志 ChildCompleted|

禁止调用后再 Refresh/CheckLease。

“本 Runtime 成功后置”不声称其他进程已经看见变化。成功边界是：事实已写入并 flush，相关本地投影已纯 Apply，调用者读取的 View 已更新。盘写成功但 Apply 失败时，本 Runtime 必须 ProjectionBroken/Fail Closed，不能假装事实未发生。

“本 Runtime 成功后置”不声称其他进程已经看见变化。成功的边界是：事实已写入本 Runtime 文件并 flush，相关本地投影已纯函数 Apply，调用者读取的 View 已更新。若盘写成功、Apply 失败，结果不是成功而是本 Runtime `ProjectionBroken`/Fail Closed；不能回滚或假装事实未发生。

---

## 三、进展 [NORMATIVE]

变更型循环体须前进**本 Runtime** ProgressStamp。  
Stamp = 本进程已 Apply 进展；禁手增。  
无新本侧事实却成功 → NoProgress。

Stamp 只回答“循环条件是否可能因权威事实改变而改变”，不是调用次数或业务轮次。读取 View、创建尚未产生事实的句柄不要求前进；另一个 Runtime 的事件在当前生命周期不可见，也不能替本 Runtime body 提供 progress。

Stamp 的目的不是统计调用次数，也不是业务轮次。它只回答：“本 Runtime 观察的循环条件是否可能因权威事实改变而改变？”因此：

- `TodoChanged`、`ReviewApplied` 等新事实可以前进；
- 重复追加相同快照若领域 Fold 判定内容未变，不应依靠调用次数制造 progress；
- 读取 View、创建一个尚未产生事实的句柄不要求前进；
- 另一个 Runtime 的事件在当前生命周期不可见，不会替本 Runtime body 提供 progress。

---

## 四、幂等 [NORMATIVE]

稳定键入本日志事实。sendOnce 范围 = 启动快照所见 + 本 Runtime 新事实。  
不跨存活 Runtime 做全局 in-flight 锁。  
测：本进程 transport 次数；重启后不盲目双发。

键：SessionId+Epoch+Purpose+Model+Attempt+TriggerMessageId+版本锚点。禁伪 ContinuationRound。

幂等分三层：同 Runtime 重复调用外部效果一次；崩溃后新 Runtime 读取已 flush Key 不盲发；两个存活 Runtime 同时操作同一 Session 时不承诺跨进程去重，双方事实均保留，重启归并。

幂等测试要区分三层：

1. 同一 Runtime 同一输入重复调用：transport/child spawn 等外部效果只发生一次；
2. 本进程崩溃后新 Runtime 启动：读取已 flush 的 Key，不盲目重复；
3. 两个存活 Runtime 同时使用同一 Session：不承诺跨进程去重，双方事实均保留，下一次启动交由时间线 Fold。

---

## 五、资源

`use!` / IAsyncDisposable。Child 默认可 continue 物理会话策略不变。

---

## 六、前置 / 错误

同前：协议允许、Round/MaxRound、ReviewExhausted 等。  
`ProjectionBroken` / Journal 写失败 → 本 Runtime Fail Closed。

前置条件由动词内部验证，不要求调用者先查一次再动作一次。这样避免检查与动作之间的窗口，也保证重启重入使用同一规则。

前置条件不是“调用前让使用者自己检查一遍”。领域动词必须在自己内部验证，并把失败归为可匹配 DU 或 Flow Error。这样避免检查与动作之间出现“检查通过后另一个 Hook 改了状态”的时间窗口，也保证重启重入使用同一规则。

---

## 七、Script 形状

SessionScript：Todo/Review View、ContinueWork、RequestReview、Finish；Inbox 仅动词内部。  
Tool 改投影 → SessionCommandPort（KISS-Tools）。

---

## 八、契约测

本侧后置；Stamp；NoProgress；本地幂等；AcceptanceUnknown；跨 Runtime 场景归 KISS-04/13。

---

*KISS-02 终。*
