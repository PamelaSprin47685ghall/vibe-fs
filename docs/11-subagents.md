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

## 相关

- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
- [06-review-and-nudge.md](./06-review-and-nudge.md)