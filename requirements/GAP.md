# GAP 台账

> 全仓已知 proof gap 的聚合台账。包内 PROOF.md 的 GAP 记录是**事实源**；本文件只聚合
> 状态与计划，关闭时两处同步更新。GAP = 某条 WHAT 命题尚无独立可执行 oracle（机器可红
> 落点），或该 oracle 由 e2e / 人工评审 / 其它包交叉承载。

状态：

- `OPEN`：无机器落点，靠人工评审或散文规范。
- `PARTIAL`：有部分可红承载（e2e 承担 / 交叉 REUSE / role-lock），但本命题无独立 unit oracle。
- `CLOSED`：已在包内 `tests/` 落地独立 oracle（记录测试路径与关闭 commit）。

## 台账

| GAP | 包 | 命题 | 缺口 | 状态 | 现状承载 | 补法计划 | Owner |
|---|---|---|---|---|---|---|---|
| GAP-001 | `finality` | FINALITY-028（ManagerJob 不复活） | 无单测落点；现有 proof 在 e2e Long Stroke 剧本与 Orchestrator 域 | OPEN | e2e `manager-unhappy-path` 剧本（Long Stroke） | 补 unit oracle：ManagerJob 不复活断言（terminal 后无 re-enlist / 无二次 run） | finality |
| GAP-002 | `external-investigation` | EXTERNAL-INVESTIGATION-011（外部事实不自动产生义务） | 无独立可执行断言；义务产生归 `office-capability`/`obligation-ledger`，本包持负边界 | PARTIAL | `stealth-browser-role-lock`（010 role-lock 交叉可红） | 若需机器化：负边界断言（外部 observation 不触发 obligation 事实） | external-investigation |
| GAP-003 | `requirement-system` | REQUIREMENT-SYSTEM-013（change-lifecycle 治理：Active 原文冻结 / Completed 不作当前依据） | 机器落点缺失 | OPEN | 人工评审（archive/docs/proof/document-governance.md 人工评审表） | change-lifecycle verifier（扫描 requirements/ 变更纪律） | requirement-system |
| GAP-004 | `requirement-system` | REQUIREMENT-SYSTEM-014（GOV-009 blocker 协议） | 机器落点缺失 | OPEN | 人工评审 + blocker 协议文本 | 同上 verifier | requirement-system |
| GAP-005 | `requirement-system` | REQUIREMENT-SYSTEM-015（普通小修复不要求自动 Change） | 机器落点缺失 | OPEN | 人工（AGENTS.md 文档生命周期节） | 同上 verifier | requirement-system |
| GAP-006 | `verification-system` | VERIFICATION-SYSTEM-003（「禁止跨级」物理契约论证） | 人工裁决面无机器落点 | OPEN | VERIFY-002 文本 + review 过程 | 若需机器化再补（先回答「依赖哪个不可模拟 physical contract」） | verification-system |
| GAP-007 | `host-boundary` | HOST-BOUNDARY-008（HOST-010 因果读 canary：transform 内存 id ≡ ToolContext.messageID 共时等价） | unit 无 oracle | PARTIAL | e2e canary（verification-system/tests/e2e） | 若需 unit 化再补 | host-boundary |
| GAP-008 | `host-boundary` | HOST-BOUNDARY-019（Magic Todo membrane canaries A..R） | 未落地实现（release gate 清单） | OPEN | 无 | 实现后由 obligation-ledger + host-boundary 补 H（定位）/A（时序）/C（原地 mutation） | host-boundary + obligation-ledger |
| GAP-009 | `prefix-stability` | HOST-013 dynamic elapsed sampling（cutover 丢失；PREFIX-STABILITY-011 仅保留 historical replay half） | 新 marker 应采样 `SessionStartedAt → now` 并把 human-readable elapsed 冻结进当次 `MarkerText`；当前正式 WHAT 缺独立正向命题，production 也未注入，且无 oracle | OPEN | `archive/docs/what/host.md` / `archive/changes/active/GrandRewrite.md §14.5` 保留迁移前语义；当前 `PREFIX-STABILITY-011` 仅证明历史 marker 不重算 | 恢复正向 WHAT owner；在 HOST-013 marker 组装处按新 occurrence 一次采样并持久化；补 fresh-sample + replay-byte-freeze oracle | prefix-stability + time-capability + guidance-delivery |

## 纪律

1. 新增 GAP：先在包 PROOF.md 记录事实，再在本表加行（ID 递增）。
2. 关闭 GAP：在包 `tests/` 落地独立 oracle（可红、单跑绿）→ 包 PROOF 落点改 `NEW` →
   本表状态标 `CLOSED`（附测试路径 + commit）。
3. 本表不替代包内命题定义；命题文本以包 WHAT.md 为准（一项知识只有一个定义）。
