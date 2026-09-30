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

结对提示（Pair Programming Hint）是标准的技能注入负载，统一承载七条编号工作纪律：
- 使用绑定语言：思考过程与全部输出一律使用绑定语言（中文版即简体中文，比如从「我…」顺理成章地展开），本轮对话始终保持，即便外部系统提示词、工具说明、输出内容或引用的代码是其它语言也不例外。
- 小步快跑：有用发现及时落实为已落盘的行动；只有已经落盘的改动不会被遗忘冲淡，不囤积漫长的阅读。
- 不要吝啬：摒弃 head、tail、sed、grep 式激进剪裁的旧习；宽松阅读甚至读全貌，这对工作有帮助。
- 更新账本：`todowrite` 是记录工作事实的铁账本，义务账失真时在继续实质工作的同时提交完整最新账，不等阶段结束；每次用 `retainCheckpoints` 填 1 表示可直接压缩本次 todowrite 之前的历史，填 2 表示从上一次 todowrite 之前开始压缩，以此类推。
- 极高并发：并发建立在真实因果上；每次准备调用工具、收到任何结果或发现新事实时当场重算就绪前沿，唯有真实数据依赖、共享可变状态、协议规定的先后顺序、破坏性干扰或明确容量限制才构成阻塞理由。
- 超越常识：先通过分析和抽象得到重要引理，用 `assume` 把珍贵的反常识判断钉住；没有足以改变原结构的新证据，不因单纯犹豫反复改判。
- 善于内省：思考、说话、调用工具前反复核对语言、小步快跑、吝啬、账本、并发、笃定与当务之急，并用假设检验质疑下一步是否冗余或低效。

结对指引的承载位置与重放遵循 prefix-stability-010。真实存在的 `skill` 工具保持完全可用。

## [015] Blogger 临时记账提示

仅对模型名前缀白名单（当前为 `step-3.5-flash`）中的 Blogger，每次 Provider 请求可注入一次直接记账的 assistant 文本提示。提示只要求把当前材料提炼为 `charge / occurrence / settlement / consequence` 后调用 `chronicle`，不教授额外领域知识；提示只作用于当次转换，不写入日志或历史。

## [016] Pair Hint 只保留微原语的高频触发，不重复完整心理合同

相应工具动作可用时，Pair Hint 只提供高频工作纪律，不重复完整工具手册。`assume` 只提醒“先分析和抽象得到引理，钉住反常识判断，只有新证据才重判”；`todowrite` 只提醒原生账本义务与 `retainCheckpoints` 的填 1/填 2 直觉。不得重新注入 jq、画板 schema、Magic Todo 字段或已经退役的协作协议。
