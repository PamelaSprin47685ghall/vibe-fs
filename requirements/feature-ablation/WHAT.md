# feature-ablation — WHAT

本文件是 `feature-ablation` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## ABL-001: 节点注册表

每个 requirement package 恰有一个 primary 消融节点，ID 等于包目录名。允许在同一包内声明 sub-node（`<package>.<slice>`），sub-node 必须声明 `parent` 指向包节点。节点清单与主审站序号由 [`resources/ablation/nodes.json`](../../resources/ablation/nodes.json) 机器承载；仓内 INDEX 包集合必须与节点 primary 集合一致。

## ABL-002: 三态语义

每个节点处于 `ablated | borrowed | active` 之一：

- **ablated**：该切面不参与 owner 路径；不得改变 provider 可见工具、Host hook 行为或等价副作用。
- **borrowed**：仅 HOW 与 manifest 明示的借用面可运行；不得触发完整包级下游语义。
- **active**：包级机制按各自 WHAT 正常运行。

未配置时 production 默认全部为 `active`。

## ABL-003: 消融 DAG

[`resources/ablation/nodes.json`](../../resources/ablation/nodes.json) 的 `edges` 构成有向无环图。`station-order` 边表示主审站拓扑：仅当 `from` 已 `active` 或 `borrowed` 时，`to` 才可设为 `active`。`borrow` 边表示借用前提：目标为 `borrowed` 或 `active` 时，前提节点至少为 `borrowed`。图不得含环；加载时必须验证。

## ABL-004: Profile 合法性

[`resources/ablation/profiles.json`](../../resources/ablation/profiles.json) 定义命名 profile（含 `station-05`…`station-56` 与 `production`）。profile 中每个节点的模式必须通过 ABL-003 校验。显式环境变量 `WANXIANGSHU_ABLATION_<node>=ablated|borrowed|active`（节点 ID 中 `.` 换为 `_`）覆盖 profile 对应项，覆盖后仍须通过 DAG 校验。非法组合必须 fail-closed，拒绝加载 registry。

## ABL-005: 零影响

节点为 `ablated` 时：对应工具不得出现在 provider schema 允许集；execute gate 必须拒绝并返回 `tool/registry/denied-ablation`；Strength 等 Host hook 必须等价于 Off/无操作。Borrowed 面仅允许 manifest 与 HOW 映射表列出的行为；其余与 ablated 相同。

## ABL-006: Fail-closed 加载

`WANXIANGSHU_ABLATION_PROFILE` 指向未知 profile、manifest 缺失、JSON 非法、DAG 校验失败或 tool-map 引用未知节点时，`AblationSettings.load` 必须返回错误，不得回退 silent 全 active。

## ABL-007: 审计字段

成功加载的 registry 必须携带 `profile`（若有）、解析后的节点模式快照、以及 manifest 版本指纹（nodes 文件 sha256 前缀），供诊断与测试断言。

## ABL-008: Strength 纳入同一 registry

`speculative-investigation` 节点为 `ablated` 时，Strength rollout 必须强制等价 `Off`，优先于 `WANXIANGSHU_STRENGTH_MODE` 的非 off 值。节点为 `borrowed` 或 `active` 时，Strength env 控制 Shadow/DryRun/Treatment 细节；ablation 不重复定义 Strength 经济策略。

## ABL-009: 工具映射完整性

[`resources/ablation/tool-map.json`](../../resources/ablation/tool-map.json) 必须为 `StaticTools.knownToolNames` 中每个需门禁的工具名指定 owner 节点。新增 known tool 未更新映射时，release gate 必须失败。

## ABL-010: Primary agent 与 MCP

manager、orchestrator primary agent 配置在 `relay-incumbency` 与 `change-integration` 相应节点非 `active` 时必须拒绝或等价不可选。`external-investigation` / `epistemic-reasoning` 为 `ablated` 时 Browser/Sphinx MCP 必须处于 Disabled。

## ABL-011: Durable fact gate

[`resources/ablation/fact-map.json`](../../resources/ablation/fact-map.json) 为每个 `AgentFact` 族与 `MagicTodo` 指定 owner 节点。owner 为 `ablated` 时 journal append 必须返回 `journal/denied-ablation`；`borrowed` 或 `active` 时允许写入。
