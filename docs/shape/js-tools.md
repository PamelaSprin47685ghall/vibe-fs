# JS Tools — 所有权与边界

## 所有权

| Owner | 拥有 | 不拥有 |
|---|---|---|
| `AttemptExecutionProfile`（Domain） | 权限 authority：`ToolCapabilitySet: Set<ToolPermission>` | 生成逻辑、工具名、描述 |
| `JsToolGenerator` | surface projection：从 profile 生成 js-ROLE（name/schema/description/base class/examples/runtime bindings） | 权限裁决；runtime 执行 |
| Capability Fragment Registry | SDK member / description / canonical example / runtime binding 的单一事实源（Read/Glob/Grep/Edit/Write） | 权限决定；Host I/O |
| BuiltinToolDescriptionHook | builtin 工具 description 的 `Prefer js-ROLE` 推荐文案 | builtin schema / executor |
| Sandbox Runner | 任意 JS 进程执行：deadline / kill / reap / resource budgets | 业务语义、权限 |
| JsTransaction | staged filesystem effect：preflight / prepare / commit / rollback / recovery | 模型 JS 执行 |
| SyntheticToml | LLM-visible result 渲染 | 执行语义 |

## 责任区

```text
src/Wanxiangshu/Domain/JsTools/      primitive capability algebra、anchor rules、surface projection rules、failure algebra
src/Wanxiangshu/Application/         JsTool workflow、transaction orchestration
src/Wanxiangshu/Infrastructure/OpenCode/Tools/   provider specs、GeneratedJsSurface adapter、ToolRegistry bridge、Synthetic TOML result bridge
src/Wanxiangshu/Process/             sandbox runner、deadline、kill/reap、resource budgets
Infrastructure filesystem adapter + EventStore ports   snapshot、gitignore glob / Host grep、ephemeral staging、EventStore durable prepare facts + payloads、commit、rollback、crash recovery
```

Domain 不做 Host I/O。

## 硬边界

```text
Generator 不重新决定权限
Runtime 不从 description 解析权限
Builtin 工具名不决定 js-* 权限
Description 钩子不改变 builtin schema/executor
Model JS 不拥有 ambient OS authority
Transaction engine 不执行模型 JavaScript
同一路径同一 program 只允许一个 mutation 意图
mutation 在 commit 前不可见（含同一 program 内后续读取）
```

禁止另建：`js-transaction.db` / `transaction-v2.json` / special feature ref / feature-owned durable store（JS-012）。

## 语言边界

万象术 production 继续是 `src/Wanxiangshu/**/*.fs`。模型 program 是数据，不是 production source；不得新增第二套业务实现语言（如 `src/js-runner.mjs`）。sandbox bootstrap 若需 JS source，由正式 resource/production owner 持有。
