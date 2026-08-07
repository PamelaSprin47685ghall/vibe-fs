# F# DSL 静态治理增强

未裁决候选。不是当前规范，不得直接据此修改生产代码。

## Current baseline

结构化程序的现行行为、边界、算法和证明分别由 `DSL-`、`FLOW-`、`ARCH-001`
及其正式文件定义。当前实现差距只见 `status/dsl-structured-program-gap.md`。

现有门禁已检查业务 Interpreter、程序计数器命名、声明式 mutable、跨文件同构 DU、
大 DU 分类和部分行为 bool。它尚不能可靠识别 record 中多个状态型字段形成的正交状态乘积；
`ControlState` 分类也没有要求作者给出“为何不能用普通 CE 表达”的机器可见理由。

早期研究中的进程等待、Companion waiter、重复类型、RecoveryTrace 和 AgentFact 分域等项目
已经进入正式规范或实现，不再是本 Proposal 的 Delta。其历史由 Git 保存。

## Proposed delta

仅增强 DSL proof 门禁，不改变 DSL 的产品语义：

1. 在有可靠 F# 语法/类型信息的前提下，识别同一 record 中两个以上状态型 DU/option/bool
   形成的组合轴，并要求显式分类：真实领域组合、物理资源组合或待拆除程序状态。
2. `DSL-class: ControlState` 必须带一段可审查理由，说明为什么普通函数调用、`match!`、
   `return!`、资源作用域、真实 waiter 或有界递归不能表达同一流程。
3. 新门禁必须解析结构而不是依赖字段名、文件数量或历史 allowlist；无法可靠判断时报告，
   不以高误报率规则阻断合入。
4. 门禁只检查所有权声明与结构证据，不替代 DSL-002/DSL-005 的人工语义判断。

## Impact map

- what/shape/how：无产品规范变化。
- proof：若裁决，更新 `proof/dsl-structured-program.md` 的静态证明义务。
- scripts/tests：`scripts/checks/dsl-ownership.mjs` 及其永久 fixtures。
- production：不因本候选直接修改；发现的真实差距另写 Status 并按正式条款修复。

## Alternatives

1. 继续只靠名称黑名单：成本低，但可被等价改名绕过。
2. 对任何含多个 DU/option 的 record 一律判红：误伤合法领域模型，不可接受。
3. 只做报告：适合作为语法分析器落地的第一阶段，但不能长期替代可执行门禁。
4. 使用编译器服务取得类型信息后做窄规则：推荐；实现复杂度较高，但能限定误报面。

## Migration / cutover

1. 先建立合法/非法最小 fixtures，固定预期诊断。
2. 以 report-only 模式对当前源码运行并人工分类结果。
3. 只有零误报或显式窄豁免时，才把确定规则切成 fail-closed。
4. 删除被新结构规则覆盖的脆弱名称规则；不长期双跑两套等价门禁。

## Compatibility disposition

Compatible。该候选只加强 proof；若扫描发现生产差距，修复兼容性由对应产品条款另行决定。

## Proof plan

1. 两个独立流程状态轴形成非法乘积时判红。
2. 真实领域组合和物理资源组合不误报。
3. 字段改名不改变判定。
4. 缺少或空泛的 ControlState 理由判红；具体、可定位的理由通过。
5. 故意恢复一个非法 fixture，仓库门禁必须红；恢复后 `npm run lint` 通过。

## Decision owner

Wanxiangshu 项目 Owner。

## Admission blockers

- 需要先选择能稳定解析当前 F# 方言和 Fable 条件编译的语法来源。
- 需要 Decision Owner 确认“理由文本”的最小机械格式，避免把自然语言质量伪装成可判定事实。
