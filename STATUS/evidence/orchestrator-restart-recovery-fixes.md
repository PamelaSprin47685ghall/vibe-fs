# evidence — orchestrator restart recovery: five production defects (2026-08-01)

> 机器输出与修复记录。绑定提交 `783caf3b` + `be5fd1ee` + `fe9a…`（Events null guard）。
> 全部结论来自 canary 实测（journal 事实、SEAL 诊断、WX- 探针），非静态阅读。

## 修复链

`orchestrator-restart-publish` 从「restart 后 `OrchestratorPublished=0`」到单 canary 绿，逐层剥出五个缺陷：

### 1. REVIEW-010 seal 不读 session 形状的 tool part（Projection.fs）

实测：deep-reviewer 每个请求的 `ProviderInputSealed.IncludedToolResultDigests` 恒为
`[08af61ac…]`（首个 user 文本 digest）——`messages.transform` 收到的是 Host session
形状的 part：`{type:"tool", tool, callID, state:{status,input,output}}`，而 decode 的
`tool` 分支只读 callID/name（args 也恒为 `{}`），`state.output`（模型实际看到的工具结果）
从不进入投影。挑战文本住在「上一次 verdict 的 tool result」里，因此第二次 PERFECT 的
seal 永远不含挑战 digest → 恒 `ChallengeUnproven` → `reverify` 在挑战轮失败。

修复：`tool` 分支在 `state.status = completed|error` 时产出 `WireToolResult`（digest 覆盖
结果），pending 调用才产出 `WireToolCall`（args 从 `state.input` 取）。修复后
`IncludedToolResultDigests` 含挑战 digest（journal 实测 `ch=True`），双 PERFECT 直接确认。

### 2. sweep 把活跃 job 的 branch 当 stale（WorktreeResource.listBranches）

实测：post-restart engine init 报
`orchestrator cleanup failed: stale manager branch cleanup failed for + manager/<job>`。
`git branch --list` 对「在另一个 worktree 中检出」的分支加 `+` 前缀；`TrimStart('*')` 不剥
`+`，`isStale` 于是把 job 自己的 branch 判为 stale。剥 `+` 后 sweep 保留 owned worktree。

### 3. existing-child idle fork 不发 ARCH-010 信封（HostForkRuntimeFork/HostForkChildDispatch）

实测：restart 后恢复链的 `forkReviewer` 命中 restored 的 deep-reviewer session（idle 路径），
把 OpeningPrompt 原样发成 continuation——canary 声明锚定在信封上，`no-declared-turn`。
new-child 路径发 `ForkChildPayload.relay`（信封），existing-child idle 路径不封装，同一 fork
两种形状。修复：`Fork` 增 `firstPrompt` 标志（默认 true），信封一次性计算、两路共用；
continuation（challenge nudge、manager resume、busy nudge）显式 `firstPrompt = false` 保持原样。

### 4. 已确认 barrier 被多余 PERFECT 重新开挑战（ReviewController）

实测（journal 时间线）：restart 后恢复轮——verdict #1 用 pre-restart 挑战确认（witness #1），
verdict #2（无 pending challenge）→ 又 `ChallengeIssued`，`applyChallengeIssued` 把已确认的
witness 换成 pending——`reverify` 的 read1 于是恒 PENDING，nudge→answer→确认→再开挑战……
四个 ReviewConfirmation claim 循环，永不终局。修复：`satisfiesGuard`（本树已确认）时多余
PERFECT 判 `AlreadyCounted`，不开新挑战。

### 5. 同一 (session, providerRun) 的 Completed 终端双投递（Events.HostEventPort）

实测：共享 terminal port 上同一终端的第二次 NotifyTerminal（HOST-012 多实例）在 nudge
窗口内完成刚安装的 R2——`await #2` 提前返回，挑战答案还没到就读到 stale 状态。
journal 显示同一 reviewer handle 两次 `HandleCompleted`。修复：port 按
(session, 最近 Completed 的 providerRun) 去重。null ProviderRun（fixture）容忍。

## 验证

- `test:mjs`：442 passed / 0 failed
- `test:harness`：278 passed / 0 failed
- `gate:static` 六门全过
- `orchestrator-restart-publish` 单 canary：3/3 绿（提交 `783caf3b` 后）
- P0 一轮：12/15 绿；3 红 = restart-publish（guard 轮 flake）、orchestrator-publish
  （bindChild/join flake）、conflict（seal 残留，见下）

## 追加修复（`3a2944f2`）：blogger 钉到 root workspace

继续追 seal 残留时发现断裂不止 blogger：manager 家族全部 session（manager、reviewer、
coder、blogger）的目录都是 manager worktree，Host 按 session 目录加载 workspace
instructions（`globUp`），worktree 在 publish 时按 ORCH-006 释放后，家族 session 的
后续请求（实测 fast-reviewer 第二请求、manager guard 继续轮）都会丢 AGENTS.md 块 →
system prompt 变短 → ARCH-004 seal 断裂。

修复：blogger 是 companion 不是 worktree worker——其 session 改在
`SharedState.RootWorkspace` 创建（首个 boot 写入；主 workspace 先于 worktree 实例
加载），该目录在 worktree 释放后仍存活且携带相同 AGENTS.md 内容。blogger 的 system
全程字节稳定。

## 残留（诚实记录，更新）

1. **manager 家族 session 的 seal 残留**：blogger 已钉住；manager/reviewer/coder 的
   worktree 目录仍随释放丢失指令。家族 session 在 publish 后的后续请求（guard 继续轮、
   reviewer 续答）仍会 seal 断裂。修复方向：家族 session 目录与工具 cwd 分离，或 guard
   轮在 publish 后停止——均需进一步设计，未在本次范围内。
2. **guard 轮 flake/循环**：`manager-guard.2` 偶发超时 + guard 多轮触发（实测第四轮
   fast-reviewer），与 `orchestrator-publish` 的 bindChild/join 竞争同类，属运行期时序。
3. **orchestrator-publish**：bindChild(fast-coder) 偶发 awaitEvent 超时 + join 链 flake。
