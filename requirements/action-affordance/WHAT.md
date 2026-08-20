# action-affordance — WHAT

## ACTION-AFFORDANCE-001: 工具描述是局部调用契约而非 tooltip

每个非平凡动词的描述必须在调用边界上使用户能够明确回答五问：
1. 发生什么动作（What act happens）；
2. 何时适用（When does this act fit）；
3. 不执行哪些临近的诱惑行为（What tempting nearby act does this NOT perform）；
4. 成功返回确立了什么事实（What does a successful return establish）；
5. 各非显然参数的具体含义（What does each non-obvious argument mean）。

描述文本必须包含充分的正向能力（positive affordance）、负向边界（negative affordance）、边界镜像（boundary mirror）、返回后果（returned consequence）与参数语义。

## ACTION-AFFORDANCE-002: 高风险动词具备最低契约与认知锚点约束

所有高风险动词（包括 `fork`、`commission`、`inspect`、`run`、`query-shell`、`establish-behavior`、`repair-behavior`、`fetch`、`join`、`horizon`、`judge`、`suicide`、`fission`、`chronicle`、`js-*` 等）必须具备完备的契约定义。

高风险动作的工具描述必须在多语言下具备对等的语义认知锚点，确保其核心约束在各语言环境中一致成立。

## ACTION-AFFORDANCE-003: inspect 显式声明不实现与不修复代码

`inspect` 的契约必须明确其“does not implement or repair code”的负边界。因果只读（causal read-only）意味着允许检查源码、历史、配置、构建产物及静态调查，但严禁直接修改文件或运行程序以制造新的行为证据。

## ACTION-AFFORDANCE-004: repair-behavior 明确 mechanical 的语义定义

`repair-behavior` 必须明确声明 mechanical 的语义为“行为含义已被决定”，而非“代码改动物理规模小”。返回的 WorkRecord 仅代表工作完成记录，不作为修复已通过验证的证明。

## ACTION-AFFORDANCE-005: establish-behavior 分离源码修改与执行证据

`establish-behavior` 必须明确声明：写入或修改源码仅代表在授权范围内完成代码落地，不等于行为已获验证的执行证据，也不代表已实际运行测试。

## ACTION-AFFORDANCE-006: run 与 query-shell 是实际动作而非运行时预测

`run` 契约必须声明命令执行是真实的发起动作与资源承诺，而非无副作用的预测。`query-shell` 契约必须声明其属于只读观察而非通用执行，不适用于构建、测试或校验。

## ACTION-AFFORDANCE-007: 动作名称表达语义动作而非运行拓扑

工具命名必须采用动词，表达语义动作（semantic act），严禁使用名词或与 Role/Persona/Office 共用名称以承载不同语义。不同语义动作必须采用完全不同的工具名称。

## ACTION-AFFORDANCE-008: 同一工具名称在全系统中处处代表同一契约

相同的工具名称必须对应：
1. 相同的语义动作；
2. 相同的参数 Schema 与各参数语义；
3. 相同的生命周期后果；
4. 相同的返回语义与关键失败语义。

仅仅 Schema 结构相同不足以构成复用理由。角色可见性隔离不能成为同名异义的借口。

## ACTION-AFFORDANCE-009: 能力选择禁止退化为裸枚举

`calling` 等参数属于能力与责任的显式选择，严禁退化为无说明的裸枚举。不同 calling 选项需清晰标明其在推理深度或 Persona 定位上的差异，而不改变其 Office 权限边界。

## ACTION-AFFORDANCE-010: fork 与 commission 必须明确受托人职能与后果

`fork` 与 `commission` 的契约必须明确回答“工作被委托给具备何种职责的角色”，依据各 Office 的法定后果写明委派边界，并标明网络浏览角色仅从公开网络建立事实、不得用于本地仓库调查。

## ACTION-AFFORDANCE-011: 关键边界镜像在所有改变决策的界面上

关键语义区分必须出现在所有可能影响行为选择的决策边界上。单一语义所有权并不要求单一呈现：被调用方的 Role Law 约束必须在调用方的工具描述中镜像展现，严禁因被调用方已定义而省略调用方的边界说明。

## ACTION-AFFORDANCE-012: 调用方边界镜像必须包含易混淆相邻动作与禁止请求

调用方工具描述必须明确指出最易混淆的相邻行为与禁止的请求形态。例如 `inspect` 的调用方严禁在 charge 中包含代码修复指令，`commission` 的调用方严禁将其视同 `fork`。

## ACTION-AFFORDANCE-013: 描述文本覆盖可见纪律并隔离隐藏编排

工具描述必须准确覆盖用户可见的操作纪律，同时严禁向模型泄露专职 Reviewer、隐式会话、终止屏障（barrier）等隐藏编排机制。所有描述资源必须成对完成本地化，并在语义锚点上保持严格一致。
