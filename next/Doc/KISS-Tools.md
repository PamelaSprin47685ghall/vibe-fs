# 静态 Tools 与纯 MessageTransform

补 Phase D/E。无动态 Registry、无 Stage 平台。与 KISS-Driver「本 Runtime 内 Driver 唯一写该 Session 投影」对齐；跨进程见 KISS-04 快照隔离。

---

## 一、绝对 Deadline [NORMATIVE]

```
type Deadline =
    private
    | Deadline of expiresAt: DateTimeOffset

module Deadline =
    val ofBudget: now: DateTimeOffset -> budget: TimeSpan -> Deadline
    val remaining: clock: (unit -> DateTimeOffset) -> Deadline -> TimeSpan
    val isExpired: clock: (unit -> DateTimeOffset) -> Deadline -> bool
```

入口只创建一次（如 Tool 请求到达时 `ofBudget now budget`）。  
下层只读 `remaining`。process wait、kill drain、prompt reconcile、CommandPort 等待 **不得**各自重新拿完整 duration。

---

## 二、ToolContext 与 SessionCommandPort [NORMATIVE]

```
type SessionCommand =
    | UpsertTodo of TodoSnapshot
    | QuerySnapshot of reply: (SessionSnapshot -> unit)  // 或 TaskCompletionSource 式有界回复
    // 最小集；禁止变成第二套 EventBus

type SessionCommandPort =
    /// 经本 Runtime Inbox 投递；由本侧 Driver Fold / commit 本日志
    abstract Request:
        SessionCommand ->
        CancellationToken ->
        Deadline ->
        Task<Result<SessionCommandResult, SessionCommandError>>

type ToolContext =
    { SessionId: SessionId
      Workspace: string
      Cancellation: CancellationToken
      Deadline: Deadline
      Session: SessionCommandPort }
```

```
type Tool =
    { Name: string
      Description: string
      Schema: JsonSchema
      Execute:
          ToolContext ->
          ToolInput ->
          Task<ToolOutput> }
```

### 2.1 写权限分类 [NORMATIVE]

|工具类|可直接做|禁止|
|---|---|---|
|文件 / 搜索 / 普通 Process|本地 IO、ProcessScript|写 Session Fact / 改投影|
|改 Session 投影（如 todowrite）|`Session.Request(UpsertTodo …)` 有界等待|`Journal.append` / 直接改 Runtime|
|任意 Tool|遵守 Deadline 剩余|启第二 Driver；业务 EventBus；无限等|

Driver 在 FIFO `Receive` 循环中处理 `SessionCommand`（含等待 terminal 期间），commit 事实后回复 Tool Hook。

[FORBIDDEN]

```
Tool.Execute → journal.Append(TodoChanged …)  // 第二 Session writer
```

---

## 三、装配与执行规则 [NORMATIVE]

- 装配：`Plugin.Tools = [ yield! … ]` + 配置条件。  
- 名冲突：启动失败，不静默覆盖。  
- 输入：边界 decode → 强类型；失败 → 结构化 ToolOutput，不抛穿宿主。  
- 输出：有界 + 截断标记。  
- 权限：执行前检查；拒绝 = 值。  
- 意外异常：诊断 + 失败输出；不击穿。  
- Cancellation + Deadline 传入 Execute 全路径。  

禁止：运行时注册、反射 handler、中间件平台。

---

## 四、MessageTransform [NORMATIVE]

```
val transform:
    snapshot: SessionSnapshot ->
    messages: HostMessage list ->
    HostMessage list
```

固定顺序：

```
sanitize
>> addCaps snapshot
>> addReviewContext snapshot
>> addParallelHint snapshot
>> ensureToolIntegrity
```

- **纯函数**：无 IO / 盘 / 网 / 全局 / Flow。  
- 不修改调用方共享可变缓冲；返回新结构。  
- 幂等：快照不变时 `transform s (transform s m) = transform s m`。  
- 只读 snapshot（Gateway 只读视图）。  
- Hook 同步类别，返回前算完。  

禁止：动态 Stage、优先级流水线、Transform 内发 prompt。

---

## 五、测试

- 工具名唯一  
- 坏 input  
- 输出超限  
- 权限拒绝  
- todowrite 只经 SessionCommandPort（架构测 / 契约）  
- Tool 在 Driver 等 terminal 时仍能完成 Todo 更新  
- Deadline 耗尽返回  
- Transform 顺序 golden + 幂等 + 输入未变异  

---

*KISS-Tools 终。*
