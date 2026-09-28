# feature-ablation — WHAT

## [001] 节点注册表

每个包恰有一个同名的主消融节点。允许在同一包内声明子节点，必须声明 `parent` 指向父节点。节点清单与序号由 `resources/ablation/nodes.json` 承载。

## [002] 三态语义

每个节点处于 `ablated | borrowed | active` 之一：

- ablated：该切面不启用；不得改变可见工具、行为或等价副作用。
- borrowed：仅 resources/ablation/nodes.json 明示的借用面可运行；不得触发完整包级下游语义。
- active：包级机制按各自 WHAT 正常运行。

未配置时 production 默认全部为 `active`。

## [003] 消融 DAG

`resources/ablation/nodes.json` 构成有向无环图。

- `station-order` 边表示主审站拓扑：仅当 `from` 已 `active` 或 `borrowed` 时，`to` 才可设为 `active`。
- `borrow` 边表示借用前提：目标为 `borrowed` 或 `active` 时，前提节点至少为 `borrowed`。图不得含环；加载时必须验证。

## [004] 配置集

`resources/ablation/profiles.json` 定义配置集。显式环境变量 `WANXIANGSHU_ABLATION_<node>=ablated|borrowed|active` 覆盖后仍须通过 DAG 校验。

## [005] speculative-investigation 消融节点不是生产启用开关

Predictor 模型配置是只读委托唯一的生产启用依据：未配置 Predictor 即无功能；已配置即默认启用；预算为 0 只表示本次不委托，不等于未配置或关闭功能。
`speculative-investigation` 消融节点保留，仅服务研究对照：测试 harness 可以以分开的完整运行比较新旧行为，但该节点（含 profile、环境变量单点覆盖与 DAG 约束）不得否决、延迟或替代 Predictor 配置决定的启用状态，不得成为 Predictor 配置之外的第二个启用条件。
其他特性的消融行为不变，仍按 [001]-[004] 的三态与 DAG 规则运行。
