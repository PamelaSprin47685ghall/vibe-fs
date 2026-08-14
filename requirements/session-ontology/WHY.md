# WHY — session-ontology

## 一句话

「有一个 session」不等于「出现一个新 participant」。execution topology、ownership 与 personhood
必须分离，否则 attached work、internal leaf、replica、companion 会制造假的角色与错误能力继承。

## 不可替代的存在理由

历史教训（`archive/changes/completed/universal.md` §13.5、`archive/docs/shape/host.md` HOST-008）：

1. **单轴 SatelliteKind 装不下现实**。旧模型只有 `SatelliteKind = { Companion, Teacher }`。当
   Dedicated Inspector / Coder 出现——它们是长期 hot-knowledge Work Session，需要 Companion /
   context 能力——把它们塞进 SatelliteKind 会把「能发普通请求、可挂 Companion 的工作会话」和
   「短命无 Companion 的叶子」揉成一个分类，两个执行能力边界不同的事实被假装成一个。
2. **每个 feature 复制 parent/child 框架就会分叉**。若所有权事实（谁是 owner、谁级联取消、谁
   retire）没有单一 owner，崩溃恢复与级联取消必然分叉（`why/host.md` §15）。
3. **物理拓扑不能冒充逻辑归属**。Host 树是扁平的（深度 2，HOST-015），UI 只渲染两层；
   fork↔child、Work↔Companion、Work↔Sync*、Work↔Bookkeeper、Work↔StrengthReplica 的关系
   只由 durable journal 事实承载。用物理 parentID 推断归属，恢复时就会收养同 root 下别人的 child。
4. **runtime topology 不决定 Role/Persona/Authority**。一个 session 是 Work 还是 InternalLeaf、
   是 Root 还是 Attached，只由 `SessionExecutionClass × SessionOwnership` 决定；Role / Tier /
   工具面 / Logical Run / Authority / Fallback 一律不参与分类。否则「换执行者」会被误写成「换人」，
   把机器拓扑泄漏成业务身份（`archive/docs/why/host.md` §15、boundary card DO-NOT-OWN）。

## RED 是什么样

```text
RED = execution class、logical ownership 与 participant identity 只能靠彼此猜测，不能独立表达。
```

具体症状：

- 想知道「这个 session 是不是 Companion」只能靠 agent 名字或工具白名单猜 → RED（COMPANION-001 删除
  了 eligibility 白名单：问题是「此 session 本身是不是 Y」，结构事实只有一个答案）。
- 想知道「Dedicated Inspector 有没有资格挂 Companion」要从 Role 推导 → RED（分类是 Work+Attached，
  与 Role 无关）。
- 恢复时按物理 parentID 收养 child → RED（必须按 journal 关联的 SessionId + agent + title 精确匹配，
  无关联一律新建，冲突 fail closed）。

## 边界（DOES NOT OWN）

- 当前具体 `AttachmentKind` 列表的增删（`AttachmentKind` 案例本身是本包所拥有；**未来新增哪种 kind**
  是独立变化——本包只保证分类轴正交且每种 kind 落在一个 cell）。
- managed session 的 create/reuse/closure/replacement 机制（→ `managed-session-lifecycle`）。
- delegation 的 charge/return 业务含义（→ `delegation`）。
- Role / Persona / ExecutionBinding 身份规则（→ `participant-identity`）。本包只拥有「分类不跟随
  身份」这一负命题，以及 canonical durable role label 的稳定性（`AgentRoleIdentity.roleName`）。
- `SatelliteKind` 案例、已删除 Student/Teacher 等历史兼容形状（历史沉积，见 HOW「历史与弃权」）。
- SyncDelegate 的语义 batch / canonical / serialization（→ `delegation`；EXEC-026/028/031）。

## Independent Change Test

新增一种 Attached Work 类型（例如未来的新 Sync*），只要填入 `AttachmentKind` 并沿用
`Work + Attached` 分类，不需要改 Persona 规则、不需要改 lifecycle protocol —— 证明分类轴与
身份/生命周期独立。
