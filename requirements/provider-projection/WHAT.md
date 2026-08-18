# WHAT — provider-projection

> 本文件是本包的**唯一 normative 合同**。所有命题同时为真；世界 RED 当且仅当某条命题被违反。
> 每条命题的证据指针 → `HOW.md` 对应行。

命题前缀：`PROVIDER-PROJECTION-`。

---

## PROVIDER-PROJECTION-001：投影是代数，不是 AST + Interpreter

**规范**：provider-visible 消息投影必须用 typed 组合子 / 直接执行的 computation
expression 管线表达（FLOW-001）。不存在 `ProjectionProgram` AST + Interpreter 中间层。
禁止各功能直接接收并任意修改 `Message list`（历史 PROJ-001）。

**含义**：投影的唯一生产路径是同构纯管线；「解释器」意味着第二次控制流，等于再造一个
程序计数器。
**边界**：`replaceMessagesInPlace` 作为 Host 适配写回原语保留是 HOW（projection-algebra-gap
Final outcome），不是对「功能不直接改消息」的豁免。
**证据**：PROJ-001、历史「投影形态」被拒方案。

## PROVIDER-PROJECTION-002：输入是不可变 ProjectionSnapshot（消费者驱动字段子集）

**规范**：投影 DSL 核心输入为不可变 `ProjectionSnapshot`；字段集是消费者驱动子集
（DSL-003），只承载当前已接线 intent 实际读取的事实，不得假装与完整目标形态同构
（`src/Wanxiangshu/Domain/ProjectionIntent.fs` `ProjectionSnapshot`：CurrentProjection /
CommittedPrefix / BlogFrames / TransportMessages / HostReanchor）。

**含义**：快照是 attempt-local 的只读事实；投影不读 Journal、不查 Host、不猜状态。
**边界**：快照字段集与完整目标形态的差距是 HOW 演进空间，不是本包命题。
**证据**：PROJ-002。

## PROVIDER-PROJECTION-003：输出管线 SemanticEventTree → Semantic → Wire；Semantic ≠ Wire

**规范**：核心输出依次为 `SemanticEventTree → ProviderSemanticProjection →
ProviderWireProjection`。`ProviderWireProjection` 与
`ProviderSemanticProjection` 是**不同类型**（VERIFY-007），禁止隐式互转。

**含义**：Semantic 去 ID（语义等价、跨会话可比较、canonical digest 唯一来源）；Wire 含
ID、字节相等（前缀缓存 / 本地时间线用）。两者相等键不同，混用必错。
**边界**：Wire 补合成 identity（COMPANION-013）的确定性派生属 `prefix-stability` 交叉；
「不同型」的结构事实归本包。
**证据**：PROJ-003、历史「投影分层」被拒方案。

## PROVIDER-PROJECTION-004：三层结构 Coordinator / Planner / Renderer

**规范**：实现必须分三层：Effectful Coordinator（读 Host、生成不可变快照）、Pure
Projection Planner（汇总 intent、排序、冲突检查）、Canonical Renderer（渲染 provider
wire bytes / 前缀 digest 所需确定性表示）（历史 shape/projection 条款 PROJ-004）。

**含义**：副作用只发生在 Coordinator；Planner/Renderer 是同入同出的纯函数。
**边界**：三层是当前实现合同；若未来证明其它分层同样满足本包命题，是 HOW 变化。
**证据**：PROJ-004、历史「装配：三层」说明。

## PROVIDER-PROJECTION-005：功能只声明 ProjectionIntent；禁止直接改消息

**规范**：功能模块只能声明以下形态的 intent（PROJ-005 固定集合）：

```text
keepPhysicalPrefix / activatePrefixEpoch / insertBlogFrames / insertRepair /
useStrengthMirror / insertStrengthFrames / suppressTransportOnly /
reanchorAfterCompaction
```

HOST-013 pair-programming marker 不占 intent（wire 级无消息地址，由
`PairProgrammingThoughtTransform` 在 raw 域按 durable gap anchor replay）。
功能模块不得接收/修改 `Message list`。

**含义**：intent 是功能与渲染器之间的唯一海关；渲染器收敛字节，编译期拦非法组合。
**边界**：intent case 列表**永久同构现有代码**不是承诺（`07-projection.md` DOES NOT
OWN）——intent 集合可随消费方变化，封闭性（变更必须显式）才是命题。
**证据**：PROJ-005、历史 PROJ-005。

## PROVIDER-PROJECTION-006：canonical order + 显式合并/冲突；禁注册顺序选边

**规范**：不同 intent 修改同一锚点时：有明确定义的合并律，或返回 `ProjectionConflict`；
**不允许依赖注册顺序**。同锚互斥 intent（如 `KeepPhysicalPrefix` 与
`ActivatePrefixEpoch`）冲突 fail-closed，输入排列不改变结局。重放型 intent 必须幂等；
有序追加型 intent 必须保持 canonical order（不能被虚构为可交换）。

**含义**：同一 intent 集无论以什么顺序装配，产出同一投影世界或同一冲突结局。
**边界**：Strength 的 `useStrengthMirror`/`insertStrengthFrames` 专属冲突律
（STRENGTH-009/016）语义归 `speculative-investigation`；「冲突必须显式」的代数性质归本包。
**证据**：PROJ-006、历史「Intent 冲突解决」被拒方案。

## PROVIDER-PROJECTION-007：DSL 不负责生命周期

**规范**：投影管线只负责不可变快照 → 确定性 provider-visible projection。它不启动/等待
任何 Agent/provider、不执行工具、不写 Journal、不恢复 Prompt、不管理 ProviderRunIdentity、
不推进生命周期状态、不控制器在线更新。

**含义**：投影层若长出第二套编排运行时，就翻回 Program AST 反模式。
**边界**：生命周期语义归各自 owner（session-ontology / managed-session-lifecycle /
dispatch-protocol / …）；本包只拥有「投影不承担生命周期」这条负边界。
**证据**：PROJ-007、历史「DSL 是否承载生命周期」被拒方案。

## PROVIDER-PROJECTION-008：SyntheticToml 是唯一字符串/值树/布局/转义 owner；无 parser

**规范**：运行时 synthetic TOML 的字符串写法、值树编码、文档布局只有一个 owner
（`src/Wanxiangshu/Domain/SyntheticToml.fs`）。每个 synthetic surface 都经它渲染；
`SyntheticToml` 不拥有任何本地 schema（Blogger/Join/js-tools 的 schema 归各自 producer）。
它**故意没有 parser**——ARCH-010 禁止业务逻辑把渲染文本读回（解析可作测试用，业务不可
用作控制流）。

**含义**：`同一 semantic input 必须产生相同 bytes`；禁止第二套引号方言；禁止业务从结果
TOML 反解析出控制流。
**边界**：各 surface 的 schema（哪些字段、什么顺序）归各自 owner；「唯一写法 owner」的
机制归本包。
**证据**：ARCH-010、历史 change（js-tools-toml-result）、历史 synthetic-toml 条款。

## PROVIDER-PROJECTION-009：instruction/data plane 由投影 owner 的消费语义决定

**规范**：每个 synthetic surface 的 owner 投影一段内容时，按「当前接收 agent 应把这段
内容当作行动/认知指导，还是当作结构化数据读取」分类：当前指导 → instruction plane
（顶层 `#` comment）；状态/参数/证据值 → data plane（TOML field/table/value）
（历史 change（corrective）§1）。

以下**都不是**合法判据：trusted→comment、untrusted→data；current→comment、
historical→data；来自 child→data、来自 Host→comment；像祈使句→comment、像事实→data。
分类发生在**每一次投影边界**；同一来源内容在不同 surface 可合法采用不同 plane。
典型方向不对称：同一段 `LifecycleWorkRecord` 在**父→子** fork payload 进 data field
（`commissioner_record` / `attached_work_record`），在**子→父** join completed 进
entry-local `# LWR` comment（`SyntheticToml.comment`）——禁止把一侧的裁决套到另一侧。

**含义**：显式采用是安全边界——内容不能自行升格为指令；可信度/来源/历史性只影响 owner
是否愿意采用，不决定 wire plane。
**边界**：具体 surface（FinalityPrompt、JoinResult、ForkChildPayload、ReviewChallenge…）
的 schema 与语义归各 owner；「分类判据」的本体归本包。
**证据**：corrective.md §1/§2（已正确 surface 清单）；方向互补硬锁见
`requirements/delegation/tests/join-v2-wire.test.mjs`
`EXEC_004_child_to_parent_lwr_is_hashed_comment_not_toml_field` 与
`requirements/delegation/tests/fork-child-payload.test.mjs`
`FORK_CHILD_PAYLOAD_commissioner_lwr_is_toml_field_not_hashed_instructions`。

## PROVIDER-PROJECTION-010：representation 不反向创造 authority/state/lifecycle

**规范**：投影是单向关系：typed state → representation。representation 不得被反解析成
authority/state/lifecycle：

- synthetic role（user/system/assistant 文本投影）不得成为 HumanRoot / AuthorityRoot /
  Opening / semantic completion / provider evidence（cursor-pair-hint authority firewall）；
- 禁止从结果 TOML / wire 反解析出控制流或业务状态；
- 禁止把 wire 反解析回 Semantic 当 digest。

**含义**：`Provider role user is not Domain user authority.` 表示层不能偷偷写权威事实。
**边界**：synthetic identity 的确定性派生（SealRoot/frameEpoch/ordinal）归
`prefix-stability`；「反解析禁令」的投影侧归本包。
**证据**：ARCH-011、历史 change（cursor-pair-hint）§8、corrective.md §8。

## PROVIDER-PROJECTION-011：semantic equality ≠ wire equality；canonical digest 只从 Semantic 投影算

**规范**：缓存比较 / 语义相等只用进模型字段（排除 timestamp / cost / usage / runtimeId 等
transport-only 字段，COMPANION-012）。canonical digest = SHA-256(规范序列化
(ProviderSemanticProjection(tree)))（COMPANION-007）——禁止 parse TOML/wire 反推正文当
digest。同语义对话跨 ID 产出同一 digest。

**含义**：digest 是语义的函数，不是字节形状的函数；wire 形状变化（例如
transport-only 剔除）不得改 digest。
**边界**：CoveredPrefixDigest 的消费点（fail-closed、canary）归各消费 owner；Finality
review 不消费 provider-projection digest；「digest 从 Semantic 算」的投影侧归本包。
**证据**：COMPANION-007/012、历史 how/projection「Canonical digest」。

## PROVIDER-PROJECTION-012：确定性 renderer：同 semantic 输入同 bytes

**规范**：同一 semantic 输入必须产生相同 bytes：CRLF/lone CR 归一化为 LF、字符串转义与
literal 选择由规则决定、文档顺序固定、`byteCount` 按 UTF-8 字节测量。同输入同输出、无
随机、无时钟、无进程序。

**含义**：确定性是 semantic digest / 前缀缓存成立的底座。
**边界**：前缀 byte 稳定性跨请求的保证归 `prefix-stability`；单次投影内「同输入同 bytes」
归本包。
**证据**：ARCH-010、历史 why/synthetic-toml 条款。

---

## 反向覆盖（OWNED clause → 命题）

| 源 Clause | 命题 |
|---|---|
| PROJ-001/002/003/007 | 001/002/003/007 |
| PROJ-004（shape） | 004 |
| PROJ-005（shape） | 005 |
| PROJ-006（shape） | 006 |
| PROJ-009 MagicTodoProjection 投影侧 | 010（禁止反推 canonical）/ 011 |
| ARCH-010 | 008/012 |
| ARCH-011 | 010 |
| COMPANION-007 | 011 |
| COMPANION-012 | 011 |
| PROMPT-019 SyntheticToml 只布局转义 | 008（layout/escaping 不拥有 prose） |
| PROMPT-013「禁止并入 host/pair-programming-guideline」 | 005（不造第二投影） |
| AGENT-031 Pair Hint 附着真实 terminal tool result | 010（不造 synthetic role） |
| AGENT-032 SyntheticToml 渲染 instruction/data 分界 | 008/009 |
| HOST-013 renderer（auto-injected；Cursor `NUL+BOM` suffix） | 005/010（wire 机制） |
| SURFACE-003/004（typed data 不可反解；surface 唯一 owner） | 010 |
| ENFORCER-026（Transport ≠ Semantic schema） | 011 |
| `07-projection.md` OWNS 全表 | 001–012 |

## DOES NOT OWN

- Repair/Review/Todo/Companion/Strength intent 是否应该存在（各 feature owner）。
- horizon admission、language choice、prefix epoch commitment。
- lifecycle / provider execution。
- 当前 intent case 列表永久同构现有代码。
- 各 surface 的 schema 与 prose 语义（各 owner）。