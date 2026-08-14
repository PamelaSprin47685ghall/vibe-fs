# WHAT：requirement-system 必须成立的规则

本文件是 `requirement-system` 的**唯一 normative 合同**。WHY/HOW/PROOF 非 normative。

命题编号 `REQUIREMENT-SYSTEM-NNN`；每条命题 = 当前世界必须同时成立的事实。证据指针 →
`PROOF.md` 行号。引用别的包一律用包名，不复制其它包命题。

---

## REQUIREMENT-SYSTEM-001：唯一语义所有权

**规范陈述**：当前接受的每一条产品真理（normative proposition）在任意时刻恰有一个 package
owner；不存在无 owner、双 owner 或互相矛盾的 normative authority。

**含义/动机**：docs、gate、test、change 若各自宣称同一事实的不同版本，仓库就没有可裁决的
「当前系统是什么」。唯一 owner 使每个判断都有归属，矛盾发生时能定位到具体包。

**边界**：本命题管「归属关系」，不管「证明技术」（那是 `verification-system`）与任一领域
事实内容。文件/模块共址不是 owner 判据；owner 由 WHY + independent-change + failure
meaning 裁决。

**证据指针**：→ PROOF.md L8。

## REQUIREMENT-SYSTEM-002：包身份独立于物理布局

**规范陈述**：package 的语义身份 = 包名；manifest 格式（TOML/JSON/…）、目录物理布局、
文件名组织是 HOW，可整体更换而不改变任何包的 WHAT。

**含义/动机**：独立变化测试——把 manifest 从 TOML 改为其它机器格式、重排包目录，所有产品
WHAT 不动。schema 未裁决期间不得为投机格式建 verifier 依赖。

**边界**：5 份文档文件名（README/WHY/WHAT/HOW/PROOF）当前是迁移契约规定的固定结构，属
HOW 的可换面；包名一旦进入 INDEX 即稳定。

**证据指针**：→ PROOF.md L9。

## REQUIREMENT-SYSTEM-003：全部包同时为真

**规范陈述**：accepted repository state 中所有 packages 同时为真；dependency 表示
guarantee consumption，不表示优先级、冻结或 override。

**含义/动机**：不存在一个「master package」可以定义横跨所有包的产品事实；横切 invariant
也必须有自己的 semantic owner。所有已接受命题构成单一真值集。

**边界**：dependency 不排序、不冻结；被依赖包不因下游消费而获得对下游命题的 authority。

**证据指针**：→ PROOF.md L10。

## REQUIREMENT-SYSTEM-004：每个 executable proof 恰一个 owner

**规范陈述**：每条新世界可执行 assertion 恰有一个 package owner；共享 checker/harness
可以，双 owner 不行。proof ownership 是 assertion 级，不是文件级。

**含义/动机**：一个测试文件可以含多个 owner 的断言（SPLIT）；一个 checker 可为多包服务
（MECHANISM）。「大家都负责」= 没人负责。

**边界**：本命题管「断言归属」；「怎么证明、如何可红」归 `verification-system`。cutover
时按断言逐条拆 oracle（SPLIT@cutover，见 HOW.md）。

**证据指针**：→ PROOF.md L11。

## REQUIREMENT-SYSTEM-005：无裸规范权威

**规范陈述**：跨包治理规则本身也必须被 package 拥有；路由/导航文件（README、AGENTS、
CHANGELOG、Change 文件）不得定义正式条款。

**含义/动机**：「规则要写在某处」若不规定「某处归谁」，散文会重新成为权威。「no naked
normative authority」= 连元规则都有归属文件。

**边界**：导航文件可以引用与路由，不可以定义；`CHG-NNN` 是 Change 编号，不是产品 Clause。

**证据指针**：→ PROOF.md L12。

## REQUIREMENT-SYSTEM-006：索引完整性

**规范陈述**：requirements/ 树只含 INDEX 列出的包目录；45 个包每个都有
`{README,WHY,WHAT,HOW,PROOF}.md`；树入口（requirements/README.md）与
requirements-design/INDEX.md 命名同一包集。

**含义/动机**：包清单是机器可校验的边界；目录级越界（INDEX 外神秘包）与文件级残缺
（缺 WHY）都必须可红。

**边界**：包数量（当前 45）是设计期结果不是稳定 API；但「树 == 索引」的封闭性永久成立。

**证据指针**：→ PROOF.md L13。

## REQUIREMENT-SYSTEM-007：WHAT 是唯一 normative 合同

**规范陈述**：WHAT.md 是 package 的唯一 normative semantic contract；WHY 解释理由、
HOW 说明实现（不另造 normative owner）、PROOF 指向证据。正式产品语义只定义在被认可的
正式层（当前 archive/docs/；cutover 后 requirements/<pkg>/WHAT.md），README/AGENTS/CHANGELOG/
Changes 不是规范正文。

**含义/动机**：读者找「这条规则权威在哪」只有一个答案。HOW 写错不构成产品违约，WHAT
写错才是 RED。

**边界**：本命题定义「哪个文件是合同」，不定义合同内容（各产品包 WHAT）。

**证据指针**：→ PROOF.md L14。

## REQUIREMENT-SYSTEM-008：条款 ID 唯一性与稳定性

**规范陈述**：正式 Clause ID 只能在其唯一定义位置定义；其它位置只能引用；引用必须可解析
（无悬空引用、无未知前缀）；既有 ID 移动定义时保留编号；删除后编号永久空缺。

**含义/动机**：ID 是跨 archive/docs/requirements 讨论的共同锚点。同 ID 双定义、悬空引用、后缀伪造
（`ARCH-010-TOOL-BOUND` 冒充）都会让「引用条款」失去语义。

**边界**：ID 前缀表与当前 5 层文件层级是当前 HOW（迁移载体），ID 稳定性原则本身是 WHAT。

**证据指针**：→ PROOF.md L15。

## REQUIREMENT-SYSTEM-009：条款层归属

**规范陈述**：可观察行为/语义/不变量权威在 what；所有权与 writer 在 shape；算法与数据转换
在 how；证明义务在 proof；理由在 why。Change 文件不得承担任何一层的正式定义职责；代码与
资源对齐 how，行为不匹配时以 what 为准。

**含义/动机**：同一事实只在一个层定义，其它位置引用。层间冲突（what vs how）有明确裁决
顺序（what 胜）。

**边界**：当前 5 层目录是迁移前载体；cutover 后各层职责按同构并入 requirements/ 树。

**证据指针**：→ PROOF.md L16。

## REQUIREMENT-SYSTEM-010：生命周期目录即状态；废止路径不引用；实现不依赖 Change 历史

**规范陈述**：变更生命周期状态只由 `archive/changes/proposed|active|completed` 目录位置表达；
正文不维护重复 status 字段。废止工作流路径（`archive/docs/proposal/`、`archive/docs/status/`）不得被当前
仓库引用。当前规范与实现不得从具体 Change 历史文件解释当前语义（不得依赖
`archive/changes/completed/<file>.md`）。

**含义/动机**：目录位置是唯一状态源，正文状态字段 = 第二真相源。Completed 是历史，解释
当前产品行为 = 让历史设计重新成为影子规范。

**边界**：`archive/changes/active/<file>.md` 作为工作范围定位允许；proposed/completed 作为当前
依赖禁止。cutover 后 archive/changes/ 归档，本条承接为 requirements/ 树的变更治理规则。

**证据指针**：→ PROOF.md L17。

## REQUIREMENT-SYSTEM-011：用户所有权与启动授权

**规范陈述**：`archive/changes/proposed/` 由用户管理；进入其中的 Proposal 已完成人工裁决。Agent
不重裁决、不扫描自选工作、不修改批准范围；用户明确请求启动指定 Proposal 即充分授权；
发现正式冲突时记录 blocker 报告用户，不自改范围。

**含义/动机**：Admission 与裁决是用户的事；Agent 重复裁决不会增加安全，只会阻塞。安全
边界是「不自选工作、不改范围、遇矛盾上报」。

**边界**：本条是过程合同（人工评审承接），机器可红面只有「proposed 不作为当前依赖」。

**证据指针**：→ PROOF.md L18。

## REQUIREMENT-SYSTEM-012：单文件 Change 生命周期

**规范陈述**：每项 Change 恰对应一个文件，在 proposed→active→completed 之间移动；不创建
平行 Proposal/Status/Decision/Outcome 文件；不引入 manifest、中央注册表、状态数据库或
复杂状态机；`CHG-NNN` 是独立 Change 编号，不是产品 Clause ID，不建立中央登记。

**含义/动机**：单文件 + 目录即状态 = 零同步债务；平行账本会让范围、进度与结果漂移。

**边界**：正文追加内容（Active work、Amendments、Blockers、Final outcome）允许，不得改写
冻结的 Original proposal。

**证据指针**：→ PROOF.md L19。

## REQUIREMENT-SYSTEM-013：Active/Completed 合同

**规范陈述**：Active 只保存冻结原文、工作来源、有限 Remaining work、Completion criteria、
客观 blocker 与用户批准的 Amendments；禁止完成百分比、逐提交流水、代码快照、未经批准的新
设计。Completed 永久保存原文与 Final outcome，不解释当前产品行为。

**含义/动机**：Active 是工作记录不是状态日志；Completed 是历史证据不是当前规范。两类文件
的正文合同由人工评审把关（GOV-008）。

**边界**：机器可红面当前为空（正文内容检查无 gate），由人工评审承接；cutover 后如建
change-lifecycle verifier 再补机器落点。

**证据指针**：→ PROOF.md L20。

## REQUIREMENT-SYSTEM-014：矛盾与 blocker

**规范陈述**：实现已批准范围时发现正式规范矛盾、能力缺失或客观不可实施条件：停止受影响的
产品语义修改 → 在 Active 的 Blockers 追加事实与证据 → 报告用户 → 用户修订范围时才追加
Amendment 继续。普通规范冲突不得由实现者按偏好选边。

**含义/动机**：执行者不是裁决者。把矛盾压下去或挑一边继续 = 把用户已经完成的裁决偷走。

**边界**：本条是过程合同；与 `verification-system` 的「验收口径不缩水」互补（后者管判据，
本条管流程）。

**证据指针**：→ PROOF.md L21。

## REQUIREMENT-SYSTEM-015：直接闭环小变更

**规范陈述**：不改变正式规范的小修复、局部重构、测试补充、格式修复，能在一次修改内完整
对齐 docs、实现与 proof 的，不需要 Change 文件；线上事故可原子修补但不得借机实现 Proposed
或降低正式条款。

**含义/动机**：工作流服务需要显式批准范围的工作，不变成每次小修的仪式成本；同时堵住
「顺手塞进未批准语义」的口子。

**边界**：若工作已由用户指定 Change 启动，仍按单文件生命周期闭环。

**证据指针**：→ PROOF.md L22。

## REQUIREMENT-SYSTEM-016：依赖声明 ⊆ 骨架

**规范陈述**：每个包 README/WHY/WHAT 中出现的 DEPENDS ON 引用集合是 INDEX 依赖骨架的子集
（允许子集，不允许多出边）；引用别的包用包名，不得复制别的包的命题。

**含义/动机**：依赖骨架（requirements-design/INDEX.md，87 edge / 0 cycle）是唯一来源；
多出的边 = 未裁决的 coupling。子集允许 = 包可以只声明它实际消费的 guarantee。

**边界**：骨架是迁移期协调文件；cutover 后骨架迁入 requirements/ 树，解析源同步迁移
（SPLIT@cutover）。本命题管「边不超集」，不管边的理由（理由逐条写各包 HOW）。

**证据指针**：→ PROOF.md L23。

## REQUIREMENT-SYSTEM-017：meta-verifier 机器执行

**规范陈述**：存在一个可执行 meta-verifier（`requirements/requirement-system/tests/meta-verifier.test.mjs`）
扫描 requirements/ 全树，机器断言：5 文档齐备、WHAT 命题 ID 在 PROOF 中有行、落点测试文件
真实存在、无 INDEX 外目录、DEPENDS ON ⊆ 骨架；删一个已存在包的 PROOF 行必须变红。

**含义/动机**：树结构合同若只活在散文里就是裸权威；meta-verifier 把 REQUIREMENT-SYSTEM-
003/004/006/007/016 变成可红测试。「绿」可以检查，「红」有失败价值。

**边界**：meta-verifier 只查结构事实，不裁决语义归属内容；归属裁决在设计期完成
（requirements-design/）。

**证据指针**：→ PROOF.md L24。
