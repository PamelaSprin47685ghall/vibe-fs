# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。

## 类型与函数地图（cognitive-environment）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/003 | `Infrastructure/Resources/PromptResources.fs` → `PromptCatalog`（10 角色各一个 system prompt）、`systemForRole`、`semanticPaths` | 唯一组合源；World → Role Law → Office Library（`libraryPaths` 按角色决定继承卷） |
| 001/005 | `resources/provider/role/*/{en,zh-CN}.md`（11 个角色对） | Role Law 是每 office 一份；Bookkeeper 走 `loadBookkeeperSystemFor`（Common Law + Bookkeeper Role Law） |
| 004 | `PromptResources.fs` `systemForRole`（只拼 System 层）；`resources/provider/role/*/`（无工具枚举） | Tools 面由 ToolRegistry 另生，不进 Role 章节 |
| 006/007/008/009 | `resources/provider/library/{ingress,closing,kolmogorov,scarcity,reviewer/quality-ledger}/**`；`PromptResources.fs` `libraryPaths` | ingress 明言「do not enlarge your authority」；closing 明言书从属于 assignment |
| 005/011 | `resources/provider/role/*/`（无 fast/deep 字样）；`Session/CompanionPrompt.fs` 等使用 `PromptResources` 组合 | 自我模型稳定；`prompt-stability` 测试（byte-stability）归 `prefix-stability`/`participant-identity` |
| 012 | `resources/provider/role/reviewer/*` + `resources/provider/library/reviewer/quality-ledger/*` | REVIEW-012：双 PERFECT 不入 prompt |
| 013 | `resources/provider/host/pair-programming-guideline/{en,zh-CN}.md`；HOST-013 transform（`Infrastructure/OpenCode/Host/*`）把同一 canonical 正文投影到 wire | craft 单源；`cursor-pair-hint.md`/`pair-parallel-tools.md`/`increase-strength.md` 考古 |

## 关键机制：PromptResources 是唯一组合源

```text
systemForRole(lang, role)
  = compose [ Common Law
            ; Role Law(role)
            ; (若有继承卷) Library ingress
            ;   + libraryPaths(role) 的书
            ;   + Library closing ]
```

- `ensureParity`：每个 semanticPath 必须 EN/zh-CN 成对存在（缺 → fail closed）；
- `libraryPaths`：Manager → [kolmogorov, scarcity]；Coder → [kolmogorov]；Reviewer → [kolmogorov,
  quality-ledger]；Inspector/DevOps → [scarcity]；其余无书；
- 生命周期/Runtime/Mission 材料在其它资源树（`resources/provider/lifecycle/**`、
  `resources/provider/delegation/**`、`resources/provider/host/**`），不进入 SYSTEM 组合。

## 防退化的门禁（MECHANISM，逐 ID 归包）

| 门禁 | 归属 |
|---|---|
| `scripts/checks/prompt-depth-ratchet.mjs` + `prompt-depth-baseline.json` | Role Law 深度 anti-amputation ratchet → 本包（认知义务不得被意外切除）；机制共享 |
| `scripts/checks/semantic-anchors.mjs` → `ROLE_SEMANTIC_ANCHORS` | Role Law cognition anchors（逐 ID 归属见 PROOF.md）→ 本包（除 browser/inquiry/reviewer 与 manager consequence 镜像） |
| `scripts/checks/language-parity-gate.mjs` | 双语成对 + placeholder/anchor parity → `provider-language`（结构面）+ 本包（Role Law 内容面，经 Gate C role-law anchors） |

## 历史与弃权

| 历史材料 | 裁决 | 记录位置 |
|---|---|---|
| Common Law / Role Law / Office Library 这些**名字** | **HOW/证据**：boundary card 明言「当前 Common Law / Role Law / Office Library 是证据，不要求保留名称」。包身份是「长期认知分层」，结构可整体重写 | WHY.md |
| `fast-*` / `deep-*` 当前 machine names、22-agent catalog、Persona 名字表 | **GARBAGE**（HANDOFF §12）：不进入永久 WHAT；本包只取「fast/deep 共享同一 Role Law」命题（005） | WHY.md 被拒方案 |
| Student/Teacher/Meditator/Executor absence ratchet | **GARBAGE**（CHANGES-AUDIT：universal.md / ce-student-teacher-collapse / Student & Teacher.md）：迁移沉积，新世界基线稳定后删除 | WHY.md |
| `pair-parallel-tools.md` 的 metrics（Coalescing Rate、Round Trips） | **HOW**：度量是验证机制，不是产品命题；本包取 craft 正文（013） | WHAT 013 |
| `increase-strength.md` §5-§9（detection/abort/escalation/consultation） | **不归本包**：fast→deep escalation 与 consultation 的 authority/调度语义归 `interaction-authority` / `delegation` / `provider-attempt-recovery`；本包只取 §3 的 craft 面 | WHAT 反向覆盖 |
| `PromptRestoration.md` 的 Gate 0/批量迁移日程 | **HOW/GARBAGE**（实施记录）：本包取最终态纪律（组合、Role Law 厚度、无工具清单）；语言管辖归 `provider-language` | WHY.md |
| NEEDHELP 的 consultation/authority/wire 各部分 | **边界如实标注**（HANDOFF §10.2 WATCH）：craft → 本包；consultation → `delegation`；authority continuity → `interaction-authority`；wire injection → `provider-projection` / `prefix-stability` | WHAT 反向覆盖 |

## 依赖说明

INDEX.md 依赖骨架：`cognitive-environment → participant-identity, office-capability`。
- `participant-identity`：Role/Persona 稳定是「自我模型不漂移」的前提；
- `office-capability`：本包只**引用** authority facts（005/007 说「authority 不随知识流动」，不定义
  authority 本身）。
