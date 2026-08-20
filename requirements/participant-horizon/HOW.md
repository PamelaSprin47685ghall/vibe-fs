# participant-horizon — HOW

## 架构与核心机制

`participant-horizon` 通过静态反向扫描门禁与运行时投影过滤相结合，筑牢信息准入边界：

```text
内部物理事件 / DTO 状态
       │
       ▼
JoinResultRenderer / HorizonTool (正向过滤: 转换为自然语言后果与 WorkRecord)
       │
       ▼
Provider-Visible Surface
       ▲
       │ (反向门禁拦截: Gate B 扫描禁止 token 与 DTO 模式)
provider-leak-gate.mjs
```

1. **正向准入与自然语言转换**：
   - `JoinResultRenderer` 负责将内部任务完成、中断、错误或超时统一转化为面向自然语言的后果说明，剥离所有底层状态机码。
   - `HorizonTool` 以只读拉取方式返回当前在场子参与者的最新工作记录摘要（Byname 索引），不暴露物理 SessionId。
   - Horizon roster 与终结门禁的 `listable/outstanding` 视图严格分离：父级可见 `Active`、`CompletedAwaitingJoin`、`Abandoned` 都进入 roster，只有 `Retired` 才退出。`Abandoned` 由 Horizon 转译成“未返回”，直到 Join 消费该后果。

2. **Gate B 反向防泄露门禁**：
   - 静态检查器 `provider-leak-gate.mjs` 扫描所有面向模型组装提示词与工具描述的代码，禁止 `SessionId`、`AgentId`、`ManagerJobId`、`PtyId`、`status`、`code` 等标记出现在输出流中。
   - 对隐藏角色（如 Reviewer、Blogger）的调用在解析层统一按通用不存在处理，避免错误信息泄露内部拓扑。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PARTICIPANT-HORIZON-001 | `requirements/participant-horizon/tests/admission-law.test.mjs` |
| PARTICIPANT-HORIZON-002 | `requirements/participant-horizon/tests/provider-leak-gate.test.mjs` |
| PARTICIPANT-HORIZON-003 | `requirements/participant-horizon/tests/join-surface.test.mjs` |
| PARTICIPANT-HORIZON-004 | `requirements/participant-horizon/tests/join-result-renderer.test.mjs` |
| PARTICIPANT-HORIZON-005 | `requirements/participant-horizon/tests/join-result-renderer.test.mjs` |
| PARTICIPANT-HORIZON-006 | `requirements/participant-horizon/tests/admission-law.test.mjs` |
| PARTICIPANT-HORIZON-007 | `requirements/participant-horizon/tests/admission-law.test.mjs` |
| PARTICIPANT-HORIZON-008 | `requirements/participant-horizon/tests/admission-law.test.mjs` |
| PARTICIPANT-HORIZON-009 | `requirements/participant-horizon/tests/fork-tool.test.mjs` |
| PARTICIPANT-HORIZON-010 | `requirements/participant-horizon/tests/admission-law.test.mjs` |
| PARTICIPANT-HORIZON-011 | `requirements/participant-horizon/tests/horizon-surface.test.mjs` |
| PARTICIPANT-HORIZON-012 | `requirements/participant-horizon/tests/warm-start-surface.test.mjs` |
| PARTICIPANT-HORIZON-013 | `requirements/participant-horizon/tests/warm-start-surface.test.mjs` |
| PARTICIPANT-HORIZON-014 | `requirements/participant-horizon/tests/fork-tool.test.mjs` |

## GAP

- `GAP-028`（OPEN）：Horizon 误复用 `HandleProjection.listable`，把仍待 Join 交付的 durable `Abandoned` 过滤掉，使已存在 child 在 roster 中表现得像从未创建；同时 fork 首 prompt 的 `AcceptanceUnknown` 被上层当成已证实发送失败并写 `HandleCompleted`，随后工具又返回“未放置”，存在重复委托与真实后台 child 脱钩风险。关闭条件：独立 horizon roster projection 保留未消费 abandonment；acceptance unknown 保留 durable Pending claim、terminal observer 与单次物理发送，不合成 terminal failure；真实 fork+horizon oracle 全绿。
