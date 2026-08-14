# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。

## 类型与函数地图（action-affordance）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001/002 | `resources/provider/tool/<name>/description/{en,zh-CN}.md`（fork/commission/inspect/run/query-shell/establish-behavior/repair-behavior/fetch/join/horizon/judge/suicide/fission/chronicle/js-program/js-bookkeeper/...） | 每个高风险 verb 的合同正文；success/failure/arg 分文件（如 `tool/run/arg-command`、`tool/run/timeout`、`tool/fork/description`） |
| 003/004/005/006 | `resources/provider/tool/{inspect,repair-behavior,establish-behavior,run,query-shell}/description/*.md` | 具体负边界/后果/参数语义 |
| 007/008 | `scripts/checks/tool-referential-integrity.mjs`（Gate A：`scanRepo` / `extractToolSpecNames` / `LEGACY_FORBIDDEN_NAMES`）；`src/Wanxiangshu/Infrastructure/OpenCode/Tools/*Tool.fs` | 同 name 唯一 owner；semantic contract 面归本包，schema 执行面归 `capability-enforcement` |
| 009/010 | `resources/provider/tool/{fork,commission}/description/*.md`（五 Office 后果 + `calling` 语义）；`OFFICE_CAPABILITY_ANCHORS`（Gate F，canonical 归 `office-capability`） | mirror 完整性 |
| 011/012 | `TOOL_DESCRIPTION_ANCHORS`（`scripts/checks/semantic-anchors.mjs`）；`requirements/office-capability/tests/eval/provider-office-boundary/oracles.mjs`（`evaluateCoderInspectOwnership`） | caller 面 mirror |
| 013 | `resources/provider/lifecycle/magic-todo/todowrite-description/*.md`；`scripts/checks/language-parity-gate.mjs`（`scanToolDescriptionAnchorParity` / `scanToolDescriptionAnchorCatalog`） | description 覆盖纪律 + 双语 anchor |

## 关键机制：description 资源是合同的家

每个高风险动词的合同住在 `resources/provider/tool/<name>/description/{en,zh-CN}.md`。
ToolRegistry / OpenCode `Tool.Def` 只把已本地化的 description 抬上 wire（PROMPT-019：
`ToolHostCodec 接收已按 SessionProviderLanguage 本地化的 Description`）；`ToolHostCodec` 只拥有布局与
转义，不拥有 prose 语义。

```text
semantic owner（本包/office-capability/…）
  → resources/provider/tool/<name>/description/{en,zh-CN}.md
  → ProviderResources 装载（成对、缺 locale fail closed）
  → SyntheticToml / ToolHostCodec（布局/转义 only）
```

## 防退化的门禁（MECHANISM，逐 ID 归包）

| 门禁 | 归属 |
|---|---|
| `scripts/checks/semantic-anchors.mjs` → `TOOL_DESCRIPTION_ANCHORS` | 7 个高风险 description 的双语认知锚点 → 本包（逐 ID 清单见 PROOF.md） |
| `scripts/checks/language-parity-gate.mjs` → `scanToolDescriptionAnchorParity` / `scanToolDescriptionAnchorCatalog` | anchor 机制共享；语义断言唯一 owner = 本包 |
| `scripts/checks/tool-referential-integrity.mjs` | Gate A 机制；「semantic act 同一」→ 本包，「schema/name 唯一」→ `capability-enforcement` |

## 历史与弃权

| 历史材料 | 裁决 | 记录位置 |
|---|---|---|
| 当前高风险 verb 名单与 allowlist（`fork, commission, inspect, run, query-shell, establish-behavior, repair-behavior, fetch, join, horizon, judge, suicide, fission, chronicle, js-*`） | **证据，非永久 ontology**（boundary card DOES NOT OWN：「当前动作名清单与高风险 allowlist」）；「高风险 verb 必须有合同」是命题（002），名单本身可重构 | WHAT 002 |
| `LEGACY_FORBIDDEN_NAMES`（verdict/list/executor/return/fork-pty/...） | **迁移 ratchet**：已删工具名的 absence 证明迁移完成；新世界基线稳定后 DELETE（PROOF-MAP §92）。schema/name 面归 `capability-enforcement` | HOW + PROOF |
| `js-capability-projected-tools.md` / `js-tools-toml-result.md` | **不归本包**（`repository-programming` / `provider-projection` / `capability-enforcement`）：JS 工具面是 repository programming 面；本包只取「`js-*` 属高风险 verb、需要合同」的宽命题 | WHAT 002 |
| `archive/docs/why/js-tools.md` 的 JS-001..020 | 不归本包（repository-programming 的 HOW） | — |
| exact `calling` 枚举值（navigator/researcher/coordinator/lead/...） | 证据；命题是「calling 是 capability 选择，不是裸 enum」（009） | WHAT 009 |

## 依赖说明

INDEX.md 依赖骨架：`action-affordance → office-capability, participant-horizon`。
- `office-capability`：本包镜像五 Office consequence（010/011），canonical 只有 ARCH-017 一处；
- `participant-horizon`：合同要出现在 decision surface，先决条件是该 surface 有资格存在（什么可看）。
