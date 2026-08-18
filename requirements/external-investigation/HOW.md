# HOW — external-investigation

> 非 normative。描述实现模型与约束；真实规范见 `WHAT.md`。

## 实现模型

### 证据载体（当前实现）

本包**没有 F# runtime provenance 类型**——真实 browsing 在外部 `stealth-browser-mcp`，
Wanxiangshu 只做三件事：

```text
1. 注入 MCP 服务器         Kernel/StealthBrowserMcp.fs + Infrastructure/OpenCode/Host/StealthBrowserMcpConfig.fs
                           （serverName = "stealth-browser-mcp"；permissionKey = "stealth-browser-mcp_*"）
2. 按角色锁                只有 Browser office allow；其它 role deny
                           （Agent/AgentProgram.fs browser office；AGENT_026）
3. 固化 provenance 合同    resources/provider/role/browser/{en,zh-CN}.md（Browser Role Law）
```

- Browser office consequence 与权限矩阵：`Kernel/StealthBrowserMcp.fs`、
  `Agent/AgentProgram.fs`；矩阵归 `capability-enforcement`，consequence 归
  `office-capability`。
- Host adapter 机制（uvx command / ref / env / fixture 启动判定）：归 `host-boundary`
  HOW；`requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` 的 `AGENT_026_kernel_identity_and_commands`
  等测试锁的是机制。
- role-lock 断言（`AGENT_026_browser_only_wildcard_permission`）：`capability-enforcement`
  交叉 owner；external-investigation 复用其「Browser 是唯一外部事实采集 office」的事实。

### 契约固化机制（本包 proof 的主干）

```text
resources/provider/role/browser/en.md + zh-CN.md   ← 散文合同（规范文本）
scripts/checks/semantic-anchors.mjs
  ROLE_SEMANTIC_ANCHORS.browser                    ← 8 个 provenance 实质区分（本包拥有）
  BROWSER_OBLIGATION_BOUNDARY_ANCHORS              ← 011 负边界 observation-not-obligation
scripts/checks/language-parity-gate.mjs
  scanSemanticAnchorParity(providerAbs, {browser}) ← 同 id 双语命中（结构 parity 机制，
                                                      机制 owner = provider-language）
requirements/external-investigation/tests/
  browser-provenance-canary.test.mjs               ← provenance canary（Oracle 1）
  facts-not-obligations.test.mjs                   ← 011 负边界 canary
```

锚点不是单词级正则：每条都锁定**实质区分**（例：`disagreement-not-averaged` 锁
`Disagreement is not a confidence average|Do not average conflicting authorities`，
反面句子「Just average the disagreement.」必须不命中）。8 条 id 见
`WHAT.md` 002–009。

### runtime oracle 边界

真实 provenance runtime oracle（真实 browse → 断言 claim 带 provenance）需要 browser
MCP adapter / Long Stroke，落在无 browser 的 unit 套件之外。本包 unit proof =
canary（contract 锁定）+ role-lock（能力归属）+ 散文合同（规范文本），不模拟真实浏览。

## 失败路径

- 双语 Role Law 任一锚点缺失/退化 → `browser-provenance-canary.test.mjs` 红
  （EXTERNAL-INVESTIGATION-002..009 RED）。
- 8 条 id 清单被增删 → pin 断言红（提示显式更新 pin）。
- `disagreement-not-averaged` 被改回单词级 → 反面句子测试红（008 RED）。
- 非 Browser role 获得 `stealth-browser-mcp_*` → role-lock 测试红（010 交叉，owner =
  capability-enforcement）。
- Role Law 丢掉「观察不是义务」或退化成单词级 obligation 匹配 → `facts-not-obligations.test.mjs` 红（011 RED）。

## 历史与弃权

| 源 | 判定 | 说明 / 落点 |
|---|---|---|
| HANDOFF §29 Oracle 1 | EVIDENCE | 调查结论（lead 已完成，勿重新考古）：8 锚点强化 + canary 要求 + role-lock 已覆盖 + runtime oracle 落套件外。落点：WHY.md 历史病灶 + WHAT 002–009 + 本 HOW + canary 测试 |
| `resources/provider/role/browser/{en,zh-CN}.md` | EVIDENCE（规范文本） | 散文合同全文吸收为 WHAT 001–011 的规范陈述与锚点 |
| `scripts/checks/semantic-anchors.mjs` browser 锚点 | EVIDENCE | 8 条 provenance id 对应 WHAT 002–009；`observation-not-obligation` 对应 011 |
| `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` | REUSE | `AGENT_026_browser_only_wildcard_permission`（role-lock）归 capability-enforcement 交叉；`AGENT_026_kernel_identity_and_commands`（uvx/ref/fixture）归 host-boundary HOW。本包只 REUSE 权限事实 |
| ARCH-017 Browser consequence | EVIDENCE | office 后果投影（`OFFICE_CAPABILITY_ANCHORS.browser` id `browser-external-provenance`）——归属 office-capability，不重复收 |
| `Kernel/StealthBrowserMcp.fs` uvx command / ref / env 前缀 / fixture 启动判定 | HOW | Host adapter 机制（COVERAGE AGENT-026 HOW 行）；本包不拥有 |
| 真实 browsing 行为测试（默认 disabled，不打真实 git） | 弃权 | runtime oracle 属 Long Stroke 层，明确落在 unit 套件之外；canary 是 unit 内可红替代 |

## DEPENDS ON

- `office-capability`：Browser office 的 entitled consequence 是前提。
- `participant-horizon`：外部事实进入 experience 的准入过滤。
- `host-boundary`：外部浏览的物理能力由 Host 提供（理由见 `WHY.md`）。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 物理移入本包；`REUSE` = 留在原处（多-owner，
> cutover 时按 `SPLIT@cutover` 拆分）；`NEW` = 本包新写。
> 运行命令统一为 `node --test <file>`（在仓库根执行）。

### 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| EXTERNAL-INVESTIGATION-001（外部事实以 provenance 建立） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs`（NEW）`WHAT[EXTERNAL-INVESTIGATION-001] provenance contract is stated in Role Law in both locales`（provenance 合同全文双语命中）+ 散文规范文本（`resources/provider/role/browser/en.md`「Provenance, compression, and certainty」） | NEW | `node --test requirements/external-investigation/tests/browser-provenance-canary.test.mjs` |
| EXTERNAL-INVESTIGATION-002（provenance-not-reachability） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-002] browser_provenance_anchor_ids_are_pinned_to_the_eight_distinctions`（id pin）+ `WHAT[EXTERNAL-INVESTIGATION-002] provenance-not-reachability anchor hits real Role Law in both locales`（锚点双语命中） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-003（far-shore） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-003] far-shore anchor hits real Role Law in both locales`（锚点双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-004（source-closest） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-004] source-closest anchor hits real Role Law in both locales`（锚点双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-005（visual-truth） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-005] visual-truth anchor hits real Role Law in both locales`（锚点双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-006（condition-preserved） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-006] condition-preserved anchor hits real Role Law in both locales`（锚点双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-007（inference-not-observation） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-007] inference-not-observation anchor hits real Role Law in both locales` + `WHAT[EXTERNAL-INVESTIGATION-007] removing_one_distinction_from_the_fixture_turns_red`（删掉该区分 → 红，证明锚点有区分力） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-008（disagreement-not-averaged） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-008] disagreement-not-averaged anchor hits real Role Law in both locales` + `WHAT[EXTERNAL-INVESTIGATION-008] disagreement_not_averaged_is_not_a_word_level_regex`（「Just average the disagreement.」必须不命中） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-009（no-cross-sea-certainty） | `requirements/external-investigation/tests/browser-provenance-canary.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-009] no-cross-sea-certainty anchor hits real Role Law in both locales`（锚点双语命中 + id pin） | NEW | 同上 |
| EXTERNAL-INVESTIGATION-010（外部/本地证据分离） | REUSE `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office`（Browser 是唯一网络能力 office；其它 role deny）；散文规范文本 Role Law「Reachability is not ownership」节 | REUSE | `node --test requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs` |
| EXTERNAL-INVESTIGATION-011（外部事实不自动产生义务） | `requirements/external-investigation/tests/facts-not-obligations.test.mjs` `WHAT[EXTERNAL-INVESTIGATION-011] observation-not-obligation is pinned` / `WHAT[EXTERNAL-INVESTIGATION-011] Role Law hits observation-not-obligation in both locales` / `WHAT[EXTERNAL-INVESTIGATION-011] removing the distinction turns red` / `WHAT[EXTERNAL-INVESTIGATION-011] is not a word-level obligation regex`；义务产生路径仍归 office-capability/obligation-ledger | NEW | `node --test requirements/external-investigation/tests/facts-not-obligations.test.mjs` |

### 新写测试清单

| 文件 | 断言 | 结果 |
|---|---|---|
| `requirements/external-investigation/tests/browser-provenance-canary.test.mjs`（Oracle 1） | ① pin 8 anchor id；② 每个 provenance 锚点 `scanSemanticAnchorParity(realProvider, {browser})` 双语绿；③ 001 provenance 合同全文双语命中；④ 删一条区分 → fixture 红；⑤ `disagreement-not-averaged` 非单词级（反面句子不命中） | `node --test` 12/12 绿 |
| `requirements/external-investigation/tests/facts-not-obligations.test.mjs`（011） | ① pin `observation-not-obligation`；② 真实 Role Law 双语绿；③ 删区分 → 红；④ 反面「网上应该 = 仓库义务」不命中 | `node --test` 4/4 绿 |

### SPLIT@cutover（REUSE 项拆 owner 计划）

- `requirements/external-investigation/tests/stealth-browser-role-lock.test.mjs`：
  - `AGENT_026_browser_only_wildcard_permission` 的「Browser 是唯一 network office」事实
    → external-investigation（010）；「permission 矩阵 allow/deny」→ `capability-enforcement`。
  - `AGENT_026_kernel_identity_and_commands` / `launch_disabled_fixture_test_uvx` /
    `apply_preserves_other_mcp_servers` / `configure_injects_mcp_on_ok_and_error` →
    `host-boundary`（Host adapter 机制）。

### GAP 声明

- 聚合台账见 `requirements/GAP.md`（GAP-002 CLOSED）。
- EXTERNAL-INVESTIGATION-011 负边界 oracle：`tests/facts-not-obligations.test.mjs`
  （`observation-not-obligation` 双语命中 / 删区分红 / 反面句子不命中）。义务产生路径仍归
  `office-capability` / `obligation-ledger`；本包无 F# observation 类型，机器落点在 Role Law。
- 真实 runtime provenance oracle（真实 browse → claim 带 provenance）需 browser MCP
  adapter / Long Stroke，明确落在 unit 套件之外；canary 是 unit 内可红替代
  （测试注释已写明该边界）。

### 本包拥有的 semantic anchor id

`ROLE_SEMANTIC_ANCHORS.browser` 全部 8 个 provenance id（`scripts/checks/semantic-anchors.mjs`）：

```text
provenance-not-reachability / far-shore / source-closest / visual-truth /
condition-preserved / inference-not-observation / disagreement-not-averaged /
no-cross-sea-certainty
```

011 负边界另册，不混入上述 8 id：`BROWSER_OBLIGATION_BOUNDARY_ANCHORS.browser` 的
`observation-not-obligation`。

（`OFFICE_CAPABILITY_ANCHORS.browser` 的 `browser-external-provenance` 属
`office-capability`，不归本包。）
