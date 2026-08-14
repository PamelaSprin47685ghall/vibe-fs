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
