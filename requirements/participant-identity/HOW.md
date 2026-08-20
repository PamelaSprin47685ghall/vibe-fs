# participant-identity — HOW

## 架构与核心机制

`participant-identity` 建立在 Domain/Kernel 层的核心类型之上，为主机与协议调度提供不可变的身份凭证：

```text
Role (Domain 枚举) ───┬──► SystemPromptId (纯函数，仅由 Role 决定)
                      ├──► SessionPersona (单次解析冻结，child 继承)
                      └──► Managed Catalog (生成 fast/deep 对称 Peer 关系)
```

1. **三轴解析流程**：
   - 根 session 创建时，由 `Role × initial tier` 确定 `SessionPersona`，执行单次绑定（bind-once）并固化。
   - 子 session 及内部执行通道通过 `inheritFromOwner` 继承父级 Persona，屏蔽底层物理档位对自我模型的影响。
   - 物理模型调度仅影响 `EffectiveAgent` 与其关联的租约，不触碰身份层。

2. **身份与租约的生命周期绑定**：
   - Managed session 在生命周期内维持 base EffectiveAgent 冻结；显式降级或提升通过单次 `ExplicitExecutionOverride` 注入执行层，执行完毕后恢复基准。
   - 内部身份（如 Bookkeeper）通过专用机器通道生成，不进入公开的 `Role` 枚举与选择视图。

## 验证与测试落点

| 命题 | 落点测试 |
|---|---|
| PID-001 | `requirements/participant-identity/tests/catalog.test.mjs` |
| PID-002 | `requirements/participant-identity/tests/catalog.test.mjs` |
| PID-003 | `requirements/participant-identity/tests/session-persona.test.mjs` |
| PID-004 | `requirements/participant-identity/tests/persona-binding.test.mjs` |
| PID-005 | `requirements/participant-identity/tests/session-persona.test.mjs` |
| PID-006 | `requirements/participant-identity/tests/session-persona.test.mjs` |
| PID-007 | `requirements/participant-identity/tests/catalog.test.mjs` |
| PID-008 | `requirements/participant-identity/tests/session-execution-binding.test.mjs` |
| PID-009 | `requirements/participant-identity/tests/catalog.test.mjs` |
| PID-010 | `requirements/participant-identity/tests/session-persona.test.mjs` |
