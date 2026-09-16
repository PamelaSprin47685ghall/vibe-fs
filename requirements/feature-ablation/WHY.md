# feature-ablation — WHY

## 不可替代的存在理由

人工巡检与渐进验收要求：在审早站机制时，下游未审能力不得反噬观察。若只能靠「不启动某 agent」或改源码硬造 simple mode，则无法区分「机制不存在」与「机制被正式关停」，也无法按拓扑序逐级解除隔离。

全功能消融开关 + 消融 DAG 把「借用什么、关掉什么、何时 Active」变成可配置、可审计、可机械验证的产品能力，而不是操作习惯或临时分支。

## 核心张力

- **角色与能力目录同步**：消融节点与工具映射必须同步新角色体系，废止 Browser/Distiller/Inquiry 等旧角色，同时保证 Sphinx 等保留能力拥有完全独立的消融开关，不受其他能力撤销的连带影响。
- **编号迁移不静默错位**：消融 DAG 与 Profile 编号迁移必须保持拓扑单调与显式对应，严禁静默错位。

- **零影响**：Ablated 状态下 owner 路径必须与从未装载该机制时一致（对齐 speculative-investigation 的零影响基线精神）。
- **Borrowed ≠ Active**：基础设施借用（如 SyncDelegate 调查链）允许有限切面，但不产生完整下游语义。
- **消融 DAG ≠ 语义依赖 INDEX**：巡检展开顺序由主审站驱动；语义 prerequisite 边仅在有明确 borrow 约束时进入消融图。

## 违约状态（RED）

- 撤销 Browser 等旧角色时连带误关 Sphinx（epistemic-reasoning）等独立能力。
- Ablation 节点或编号迁移导致 profile 与 DAG 静默错位。

1. 靠注释、条件编译或未文档化分支隐藏下游机制，而非走正式开关。
2. Ablated 包仍向 provider 暴露工具 schema 或可执行入口。
3. profile 组合违反 DAG（下游 Active 而上游仍 Ablated）。
4. 加载非法 profile 或 env 组合时 fail-open 继续运行。
