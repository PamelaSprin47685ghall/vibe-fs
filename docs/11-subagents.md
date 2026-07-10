# 11 — 子代理

## 模型

子代理 = 独立会话（OMP 可为子 workspace）。委派工具：`coder`、`investigator`、`browser`、`meditator` 等；参数 `intents[]` 可多项并发。

## Continue 多轮（`continue` 工具）

**问题**：spawn 类工具原先 fire-and-forget，子会话存活时外层无法追问。

**Iterator**：

- id 形如 `sai_s…`
- 绑定 `{ childID; agent }`
- 存储：`Shell` 侧 `SubagentIteratorStore`（scope 内 LRU，默认上限约 50）
- scope 清理时 `clearTypedIteratorScope` 一并回收

**首次 spawn**：子代理返回后注册 iterator；工具输出 YAML front matter 含 `iterator`（`ToolOutputInfo.withIterator`）。

**continue 调用**：

| 参数 | 必填 |
| :--- | :--- |
| `iterator` | 上一步返回的 id |
| `prompt` | 追问内容 |

流程：`consumeSubagentIterator`（单次有效）→ 宿主 `ContinueSubagent(childID, agent, prompt)` → 成功则 **新** iterator 写回输出。

**Catalog**：`Kernel.ToolCatalog` 含 `continueSpec`。

## 各宿主 Spawn

| 宿主 | 路径 |
| :--- | :--- |
| OpenCode | `SubagentIo.continueSubagentCoreResult`、`SubagentTools.fs` |
| Mux | `MuxSubagentToolExecute`、delegate |
| OMP | `Omp/SubagentTools.fs`、`ChildSession.fs` |

共用：`SubagentPromptBuild`、`SubagentIntentsCodec`。**Omp 禁止**引用 Opencode/Mux。

## Reviewer

`submit_review` 拉起 reviewer；仅 `return_reviewer` + read 类工具。

## 测试

`SubagentIteratorStoreTests`、`IntegrationSubagentSpecs`、`ArchitectureTestsSubagent*`。

## 恢复错误与子会话完成协议

`session.prompt` 网络错误（`ECONNREFUSED`、`network connection lost` 等）是**可恢复中间事实**，不等于子会话终态。Fallback 系统可能已启动重试（`FallbackPhase.Retrying`），但子会话本身可能已完成工作（`TaskComplete=true`）。

**完成协议**：父工具（`continue`、`coder`、`investigator` 等）返回必须发生在终态之后：
- `TaskComplete=true`（子会话明确完成）
- `FallbackPhase.Exhausted`（重试链耗尽）
- 明确不可恢复失败（如 `MessageAborted`、`ClientCancellation`）

**等待门**：`waitForSubagentSettle` 在 `FallbackPhase.Retrying` 时保持等待，**但当 `TaskComplete=true` 时必须释放**。终态事实覆盖残留相位——相位是过程，终态是结果。

**测试纪律**：验证事件顺序（状态转换、promise resolve 时机），不验证文本内容。使用 `yieldMicrotask`、`Promise.create` 事件门闩、状态断言，禁用 `Promise.sleep` 和 `Date.now`。对于 E2E 测试中的所有异步等待，必须应用统一的 1 秒超时定时器辅助方法 `withTimeout` 进行 race 包装，以实现 fail-fast 效果，绝对不能无限期超时挂起。

## 相关

- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
- [06-review-and-nudge.md](./06-review-and-nudge.md)