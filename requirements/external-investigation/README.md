# external-investigation

**一句话 WHY**：external / public-web facts 以 provenance、source quality 与
disagreement-aware observation 建立；可达性不决定所有权，外部可能性不自动变成 repository
obligation。

## 这个包保证什么

- **provenance first**：外部事实选择尽量接近事实源的来源，保留来源/时间/条件足以支撑
  claim。
- **8 条实质区分**（Browser Role Law 锁定）：provenance-not-reachability、far-shore、
  source-closest、visual-truth、condition-preserved、inference-not-observation、
  disagreement-not-averaged、no-cross-sea-certainty。
- **证据分离**：外部证据与本地 repository 证据分属不同 source law；browser 能力不授予
  本地检查权。
- **不越权**：外部事实只建立外部世界事实，不自动产生 repository/product obligation。

## WHAT 概览（11 条命题）

`WHAT.md` 编号 `EXTERNAL-INVESTIGATION-001..011`：provenance 建立（001）、可达性≠所有权
（002）、远岸（003）、source-closest（004）、visual-truth（005）、condition-preserved
（006）、inference-not-observation（007）、disagreement-not-averaged（008）、
no-cross-sea-certainty（009）、外部/本地证据分离（010）、外部事实不自动产生义务（011）。

## HOW 概览

本包无 F# runtime provenance 类型——真实 browsing 在外部 `stealth-browser-mcp`；
Wanxiangshu 注入服务器（`Kernel/StealthBrowserMcp.fs`）+ 按角色锁（Browser-only）+ 以
Browser Role Law（`resources/provider/role/browser/{en,zh-CN}.md`）固化 contract。
契约 proof = `scripts/checks/semantic-anchors.mjs` 的 8 锚点 + `scanSemanticAnchorParity`
双语命中 + canary。真实 runtime oracle（真实 browse）需 browser MCP adapter / Long
Stroke，落在 unit 套件之外。

## Proof 概览

- NEW：`tests/browser-provenance-canary.test.mjs`（Oracle 1：pin 8 id、真实散文双语绿、
  删区分变红、`disagreement-not-averaged` 非单词级）。
- REUSE：`tests/unit/agent/stealth-browser-mcp.test.mjs`（role-lock，capability-enforcement
  交叉，`SPLIT@cutover`）。

## 阅读顺序（零上下文读者）

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样、历史病灶。
2. `WHAT.md` —— 11 条命题（唯一 normative）。
3. `HOW.md` —— 证据载体 / 契约固化机制 / runtime oracle 边界。
4. `PROOF.md` —— 每条命题的测试落点与怎么跑。

## 运行

```text
node --test requirements/external-investigation/tests/browser-provenance-canary.test.mjs
node --test tests/unit/agent/stealth-browser-mcp.test.mjs
```

## DEPENDS ON

- `office-capability`：Browser office 的 entitled consequence 是前提。
- `participant-horizon`：外部事实进入 experience 的准入过滤。
- `host-boundary`：外部浏览的物理能力由 Host 提供（理由见 `WHY.md`）。
