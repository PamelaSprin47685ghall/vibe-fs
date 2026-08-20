# execution-model-routing — HOW

## 架构与核心机制

`execution-model-routing` 通过单向流水线将 MJS 策略与 Host 消息拦截打通：

```text
~/.config/opencode/wanxiangshu.mjs (唯一策略权威)
       │ (route: role, running, previous -> target | null)
       ▼
ModelRoutingRuntime (进程单例，管理 Lease multiset 与 Capacity Token)
       │
       ├──► chat.message hook (物理准入: (SessionId, PhysicalUserMessageId) -> ModelTarget)
       └──► messages.transform hook (容量仲裁: Lineage Token 借用与 Step Fence 拦截)
```

1. **Bootstrap 与 MJS 策略加载**：
   - 进程启动时探测 `~/.config/opencode/wanxiangshu.mjs`，若缺失则以原子方式写出内置推荐模板文件并加载。
   - 加载后保持函数引用，不维护多级 runtime 兜底策略。

2. **物理准入与租约管理**：
   - 调度请求仅在 Host `chat.message` 阶段触发，根据 `(SessionId, PhysicalUserMessageId)` 绑定目标模型并修改 Host message。
   - 新物理消息到达时原子取代并取消旧 pending demand；`null` 返回值进入等待队列并在租约归还时事件驱动重试。

3. **Lineage 令牌借用与召回**：
   - 真实 Token Ledger 记录全局占用；Borrowing Decorator 维护 session 派生树。
   - 子节点在 step 级别借用祖先等待中的 token，并在祖先恢复或 step 终结时按序归还。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| EMR-001 | `requirements/execution-model-routing/tests/scheduler-module-config.test.mjs` |
| EMR-002 | `requirements/execution-model-routing/tests/scheduler-module-config.test.mjs` |
| EMR-003 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-004 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-005 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs` |
| EMR-006 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-007 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
| EMR-008 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs` |
| EMR-009 | `requirements/execution-model-routing/tests/routing-authority-boundary.test.mjs` |
| EMR-010 | `requirements/execution-model-routing/tests/model-routing-runtime.test.mjs` |
