# capability-enforcement — HOW

## 架构与核心机制

`capability-enforcement` 通过单向权限派生与双层门禁阻断，确保模型视野与执行拦截的同构：

```text
Roles.permissions (Kernel 层单一真相源)
       │
       ├──► ManagedAgentConfig (Host Schema 投影: StaticTools.permissionObj)
       ├──► JsToolGenerator (四层同构生成: 基类方法 / Description / Examples / Gate)
       ├──► AttemptExecutionProfile (单次请求能力集 ToolCapabilitySet)
       └──► ToolRegistry.gateExecute (运行时前置执行拦截: DeniedUnestablished / DeniedRole)
```

1. **同源派生与 Schema 投影**：
   - 托管 Agent 配置初始化时，从 `Roles.permissions` 生成对应角色的工具白名单，写入 Host 原生配置，屏蔽无权工具的 Schema。
   - 活跃角色的 Schema 严格依据新体系派生（Engineer 拥有文件读写改删与 Fission；DevOps 拥有文件读写改删与 Exec/Pty；Manager 拥有 Fork/Resume/Join/Horizon/TodoWrite/ReviewAssessment/Finality 但无 Fission；Orchestrator 拥有 Commission/Join/Horizon）。
   - `external_directory = "allow"` 作为基础设施元权限由统一写入口注入，不混入业务权限。

2. **运行时 Gate 拦截**：
   - `ToolRegistry` 在执行工具前核验当前执行角色的合法性与权限。未决角色直接阻断（`DeniedUnestablished`）。
   - **Fission 准入拦截**：入口核验已证明的 CanonicalRole 是否为 Engineer，非 Engineer（如 Manager, DevOps, Blogger 等）直接返回类型化拒绝并 fail-closed。
   - **Fork/Resume 独立拦截**：`fork` 入口仅放行 Manager 且目标仅限 Engineer；`resume` 入口仅放行 Manager 且仅限续做固定 DevOps 道路。
   - 投机副本（StrengthReplica）执行前建立独立只读工具白名单拦截非只读调用。

3. **四层同构保证**：
   - `JsToolGenerator` 依据当前请求的 `ToolCapabilitySet` 动态合成工具定义代码。
   - 运行时 `ToolRegistry` 与 `JsToolGenerator` 保持同构映射：生成 `js-engineer` 与 `js-devops`，并放行对应角色的执行，无权限角色不生成且运行时拒绝。删除 `js-coder`、`js-inspector` 与 `js-browser`。

4. **权威值分类与真实消费点门禁**：
   - `Evidence / Decision / Witness / Capability / Receipt / PhysicalHandle` 六类值由真实 owner 模块发行，类型系统与运行时消费点 fail closed。
   - 敏感操作在消费点核验 exact subject、版本与新鲜度，不依赖静态清单或名称 allowlist。

5. **Quiescence typed owner gate**：
   - `SessionQuiescenceGate.ObserveIdle` 在 current physical attempt 的 idle edge 上发行 opaque `QuiescencePermit`。
   - `TryConsume` / `TryRelease` 返回 `Result<unit, QuiescencePermitFailure>`；owner mismatch、重复、attempt supersede、revoke、无 fresh idle 各自保持稳定 typed 分支并且 Error 零效果。
   - JS `QuiescenceSurface` 只暴露 typed result view。重启恢复 durable facts 后仍由普通 attempt composition 重新 `ObserveIdle`，不编码或复活旧 permit。

6. **复用既有离任与集成证明**：
   - 离任准入与资源闭包继续由 `RETIRE-001` ~ `RETIRE-008` 的 IncumbencyId、WorkspaceSnapshotId 与 recursive live resources closure 合同建立。
   - 确定性发布与集成门禁由 `CHGINT-001` ~ `CHGINT-006` 对有效 quality candidate 的 typed admission 发行；durable `PublicationCommitted` 是结果，不另造第二套审查权威。

`ToolRegistry` 直接消费 Fetch、Bookkeeper、Engineer、DevOps、文件变换与生成式 JS 工具的 typed admission／spec，删除模块查找、缺失模块时的备用权限表和静默漏注册路径。注册层只装配既有 provider 合同；`tool-spec-contracts.test.mjs` 与 `internal-leaf-tool-authority.test.mjs` 继续验证公开角色权限和无 attached transaction 时的内部工具拒绝，不以 source token 或生成 JavaScript 布局证明权限正确。

## GAP

- `ENF-013` / `ENF-014` / `ENF-017`（CLOSED）：权威值分类、单点发行与一次性能力不可复制消费证明已闭合，落点 `tests/013.test.mjs`、`tests/014.test.mjs` 与 `tests/017.test.mjs`。

