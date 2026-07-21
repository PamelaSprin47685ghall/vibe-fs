# 01 — 第一性原理

本文档说明万象术**为何**采用当前架构。公理表亦见 [01-overview.md](./01-overview.md)；此处展开工程推论与纪律。**权威顺序：实现 > docs**。

## 1. 稳定资产是领域规则，不是宿主 API

宿主会变，tool schema 会变，消息对象形态会变。真正稳定的是：

- **review** = 有限状态机（`/loop`、`submit_review`、verdict）— `src/Kernel/ReviewSession/StateMachine.fs`
- **nudge** = 有限状态机（待办 / review / runner 等优先级）— `src/Kernel/Nudge/`
- **todo folding** = 用 durable 事件保留可重放的进度，而非依赖对话窗口
- **工具权限** = 角色 → 工具语义的规则矩阵 — `src/Kernel/ToolPermission.fs`

**推论**：稳定规则进 `src/Kernel/`；四套宿主（OpenCode、Mimocode、Mux、OMP）只做薄适配。判断法则：去掉 Node 与宿主 `obj` 后仍成立的逻辑 → Kernel。

## 2. 事件流才是事实，内存状态只是积分

review 任务、待办快照、nudge 去重等 **不能** 只靠进程内存或宿主 `session.messages`——compaction 会裁掉上下文，对话历史不是 durable 真相。

**SSOT**：工作区根目录 `.wanxiangshu.ndjson` + `.wanxiangshu.ndjson.lock`。

- **意图不落盘**：未校验的自然语言、草稿参数不写入。
- **事实只追加**：修正靠补偿事件，非覆盖旧行。
- **内存 = fold**：`ReviewStore`、backlog 投影、nudge 表 = 对该 `session` 事件行的纯 fold（可加进程内缓存，磁盘为先）。
- **先盘后内存**：append 成功后才更新投影；失败 = 该事实未发生。

进程重启：**先重放 NDJSON，再服务 hook**。细节见 [05-event-sourcing.md](./05-event-sourcing.md)。

## 3. 副作用不可避免，但必须被压到边界

文件系统、网络、子进程、宿主 session、MCP、tree-sitter、第三方库是现实接口，不是领域规则。

**推论**：IO 与动态对象编解码集中在 `src/Runtime/`；Kernel 写成可单测的纯函数（无 `Dyn`、不直接绑 Node API）。复杂流程在 Runtime 或宿主层编排，不在 Kernel 堆副作用。

## 4. LLM 边界必须尽快强类型化

工具参数与宿主消息本质是动态对象。若 `obj` 在业务核心里长距离流动，错误会在深处爆炸。

**策略**：

1. 宿主边界先读 `obj`
2. 尽快转成 Kernel 里的 DU / record
3. 纯逻辑只消费强类型
4. 结果再编码回宿主形态

可预见的业务失败用 **`Result` 具体分支**，不伪装成异常；异常只留给无法继续的事故。

## 5. 宿主上下文可折叠，万象术进度靠事件

宿主侧：compaction 压缩 LLM 窗口即可。万象术侧：每次 `todowrite` / `task` **校验通过** → append 带有 `openTodosJson` 的 `assistant_completed` 事件，携带**全量** `todos`（及 `select_methodology`）。

## 6. 并发的本质是共享可变状态

JS/Node 没有线程级共享内存并发，但有大量异步交错（多 hook、多子代理、append 与 nudge 交错）。

**策略**：

- 单进程关键路径用 **`PromiseQueue.SerialQueue`**（`src/Runtime/Execution/PromiseQueue.fs`）
- 按 **session / workspace** 切分串行域
- nudge：**该不该**（Kernel 纯判断）与 **怎么发**（宿主 API）分离

## 7. 测试必须时间无关

测试不得依赖系统时钟、随机数、不可控的外部服务或「睡 N 毫秒」等脆弱等待。时钟与 IO 可注入；异步收敛用轮询 / 事件门闩 / `Promise` 链上的确定性断言。

## 8. 外部效应必须容忍崩溃（Durable Effect Law）

持久化事件与发起外部效应之间存在**崩溃窗口**：事件已落盘但 Effect 尚未执行时若进程崩溃，仅依靠内存通知会永久丢失该 Effect。

**推论**：外部副作用必须满足以下二者之一：
- 从已提交状态确定性重建（重放后可自动重新发起）
- 作为持久化 Outbox Intent 写入，与领域事件同一提交屏障落盘；Effect Supervisor 消费 Outbox，完成后写入确认事件

**交付语义**：**At-least-once delivery + idempotent host operation + correlation / reconciliation**。

## 公理速查

| # | 公理 | 工程落点 |
| :---: | :--- | :--- |
| 1 | 稳定资产是领域规则 | `src/Kernel/` |
| 2 | 事件流是事实 | `.wanxiangshu.ndjson` |
| 3 | 副作用压到边界 | `src/Runtime/` + codec |
| 4 | 边界强类型 | DU/record |
| 5 | 进度不靠 compaction 锚点 | `assistant_completed` |
| 6 | 并发 = 共享可变状态 | `PromiseQueue` / session 域 |
| 7 | 测试时间无关 | 注入 + 正式 tests |
| 8 | 外部效应必须容忍崩溃 | Outbox + Stable Resource Identity |

## 一句话

> 先把多代理系统里真正稳定的语义抽成纯内核，再把宿主、IO、消息对象、schema、并发与持久化都压成外围适配问题。
