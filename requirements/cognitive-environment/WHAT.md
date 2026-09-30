# cognitive-environment — WHAT

## [001] 五层认知材料

每份自然语言材料只归属一个主权威层：
- **World**：普适世界观与通用公理（Common Law）。
- **Role**：参与者职责与自我模型（Role Law，fast/deep 共享）。
- **Library**：继承的技术知识与职位经验（Office Library）。
- **Runtime**：本次调用时刻成立的生命周期与事件注入。
- **Mission**：本次委托的具体目标与当前任务。

## [002] 语义所有权

各层可以告知事实，不得冒充其他层的权威。内容冲突按事实的领域语义所有权裁决，不以固定的层级优先序覆盖。

## [003] canonical 组合顺序

标准组合顺序为：
```text
SYSTEM:  Common Law → Role Law → Office Library
TOOLS:   当前生成的工具描述集合（独立于 Role 章节）
RUNTIME: 生命周期与事件注入
USER:    当前分配的 Mission 任务
```

## [004] Tools 不是 Role Prompt 章节

System Prompt 表达职位职责、认识论原则、权能边界与易犯错误，不枚举当前环境的全部工具。工具面决定可用工具，工具可用不等于职位获权。

## [005] Role Law 是长期 self-model 层

Role Law 定义参与者的长期自我模型。同一 Office 的 fast/deep 档共享相同的 Role Law 与自我模型；提示词不得暴露 `fast-*`、`deep-*` 等机器路由名称。

## [006] Office Library 是继承的技术书籍，不是 Common Law

Office Library 提供技术经验与操作指南，服从具体任务需求，不定义系统公理或职位权能。

## [007] 知识可跨 authority 边界流动，authority 不随知识流动

知识可以跨职位传授，阅读知识不增加职位权能；修改代码、执行环境等权能仍由 office-capability 定义。

## [008] Library 三轴：Class × Delivery × Audience

Office Library 遵循三轴分类：
- **Class**：Rulebook、Handbook、Ledger、Atlas、Field Notes。
- **Delivery**：Inherited Volume、Triggered Folio、Request-Bound Volume。
- **Audience**：按职位角色或请求契约绑定，不按模型推理深度分叉。

## [009] Library 禁令

书籍不得扩大角色权能，不得成为所有角色共用的全能手册，不得按同角色的 fast/deep 档分版本，不得向评审者透露隐藏评审编排。

## [010] 生命周期文本只 orient，不 educate

生命周期文本（Activation、Reawakening、Continuation、Handoff、Fission、Departure）只说明当前处境（orient），不重复教授知识（educate），不触发 System Prompt 替换。

## [011] 瞬时 runtime/mission 不重写长期 self-model

瞬时任务与运行时事件通过会话消息传递，不得借提示词伪造激活或改写长期 Role 自我模型。

## [012] 独立评审指引不灌输隐藏流程机制

Manager assessment 的提示由 Role Law、Quality Ledger（八维准则）与上下文组成。评审只依据当前工作的事实独立、诚实打分，不为影响后续而调分；不得向模型透露双重确认、多 Reviewer 循环或隐藏 barrier 等内部编排。

## [013] Pair Hint 是 canonical craft payload

结对提示（Pair Programming Hint）是标准的技能注入负载，统一承载以下核心工作原则：
- 使用统一的中文（或对应绑定语言）思考与输出；代码、标识符、文件路径和技术术语在翻译会损害精度时保持原文。
- 若 `todowrite` 可用，把它作为完整当前工作账本：现实变化使清单失真时及时整份更新，状态使用宿主原生 `pending | in_progress | completed | cancelled`，不把账本当 authority 或 completion credit。每次显式填写 `retainCheckpoints`；1 表示可压当前 checkpoint 之前，2 表示保留当前和上一次 checkpoint。
- 持续维护就绪前沿（Ready Frontier）：一旦子任务 A 解锁后继 A1，A1 立即并发发出，不等待同批次其它未完成任务；依赖图仅为事实快照，不构成人为的阶段屏障。
- 先抽象，再 `assume`：形成将据以行动的判断后调用一次笃定，然后执行、验证；没有实质新信息时不反复推翻已经钉住的判断。`assume` 不承担记忆、待办或压缩职责。

结对指引的承载位置与重放遵循 prefix-stability-010。真实存在的 `skill` 工具保持完全可用。

## [015] Blogger 临时记账提示

仅对模型名前缀白名单（当前为 `step-3.5-flash`）中的 Blogger，每次 Provider 请求可注入一次直接记账的 assistant 文本提示。提示只作用于当次转换，不写入日志或历史。

## [016] Pair Hint 只保留微原语的高频触发，不重复完整心理合同

相应工具动作可用时，Pair Hint 只提供高频工作纪律，不重复完整工具手册。`assume` 只提醒“先抽象、再笃定、再执行验证”；`todowrite` 只提醒原生账本状态与 `retainCheckpoints` 的 1/2 直觉。不得重新注入 jq、画板 schema、Magic Todo 字段或已经退役的协作协议。
