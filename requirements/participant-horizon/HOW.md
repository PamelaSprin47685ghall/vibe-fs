# HOW —— 实现模型与约束（非 normative）

> 本文件描述当前实现怎么承载 WHAT；**不**另造 normative owner。实现可以重写而不改 WHAT。
> 新工程师用它把命题对到代码。

## 类型与函数地图（participant-horizon）

| WHAT 命题 | 实现载体 | 说明 |
|---|---|---|
| 001 | `docs/what/architecture.md` ARCH-014 decision filter（规范本体）；`scripts/checks/provider-leak-gate.mjs` 只做反向 enforcement | filter 六问目前是文档级律；正向可红性由 Gate B + 本包 admission-law 测试承接 |
| 002/003/004/005 | `scripts/checks/provider-leak-gate.mjs` → `FORBIDDEN_TOKENS` / `FORBIDDEN_DTO_PATTERNS` / `FAST_DEEP_BINDING_RE`，扫描 `PROVIDER_SCAN_ROOTS` 列出的 renderer（JoinResultRenderer、HorizonTool、JoinTool、ForkTool、PtyTool、ExecutorTool、InspectorTool、FetchTool、FinalityTool、BashHoneypotTool、FileMutationTools、ChronicleTool、CoderTool、JudgeTool、JsBookkeeperTool） | Gate B（ARCH-016）；baseline ratchet 见「历史与弃权」 |
| 003/011 | `Infrastructure/OpenCode/Tools/HorizonTool.fs`、`JoinTool.fs`、`Infrastructure/OpenCode/Codec/JoinResultRenderer.fs` | 自然语言后果渲染；`tests/unit/tools/join-tool-family.test.mjs` 有真实 wire 断言 |
| 006/007/010 | `Infrastructure/OpenCode/Tools/ForkTool.fs`、`ToolRegistry.fs`（role predicate）；`resources/provider/tool/{fork,commission}/description/*.md` | 可见集合的运行时执行面 + 文案面 |
| 008 | `Application/Reconciliation/CompanionTransform.fs`（`ProviderWireCapture.decodeMessageView |> ProviderProjection.toSemantic`）：wire→semantic 单向降级丢弃 call id | 语义投影只保留「交换意味着什么」，不留机器身份（provider-projection 消费） |
| 009 | `Infrastructure/OpenCode/Tools/ForkTool.fs`（generic unavailable 文案）；`tests/unit/tools/fork-tool.test.mjs` | GLORY-032 |
| 011 | `Infrastructure/OpenCode/Tools/HorizonTool.fs`（pull-only；名册按 Byname；最新 BlogFrame） | EXEC-005 |
| 012/013 | `Session/RepositoryWarmStartPrompt.fs`（`RepositoryWarmStartSearch`）；`tests/unit/agent/repository-warm-start.test.mjs` | 准入 + data 标注 |

## 关键机制：Gate B 是反向 enforcement，不是正向律

`provider-leak-gate.mjs` 扫描 provider renderer 源码，禁止泄漏词汇出现在 provider-visible 组装行
（`Description =`、`field "..."`、`tomlObject*` 等）。它是**反向**保护：漏了就红。正向律（001 六问）
是 ARCH-014 的规范文本 + 本包 admission-law 测试钉住的资源面。两者互补：源码扫描抓 renderer 实现，
资源断言抓固定 prose。

```text
正向律（应该准入什么）    → ARCH-014 filter + admission-law.test.mjs（资源面）
反向保护（不许泄漏什么）  → provider-leak-gate.mjs（源码面）+ provider-leak-gate.test.mjs（机制）
```

## 接线示例：DevOps join 超时

`EXEC-004`：`DevOpsJoinTimeoutMs = 10_000`（`Process/Deadline.fs` 注入）→ 无完成项时结束本次等待
（Host 事实 `DeadlineExpired`）→ `JoinResultRenderer` 渲染自然语言「等待结束」，不渲染
`TIMED_OUT` / `status="failed"` / `code=...`。测试：`tests/unit/execution/devops-join-timeout.test.mjs`。

## 历史与弃权

| 历史材料 | 裁决 | 记录位置 |
|---|---|---|
| `provider-leak-gate.mjs` 的 `FORBIDDEN_TOKENS` / `FORBIDDEN_DTO_PATTERNS` 黑名单 | **迁移 ratchet**：历史 DTO token 名（SessionId、AgentId、status、code…）是 proof fixtures，不是永久 taxonomy（boundary card DOES NOT OWN 明言）。未来 horizon proof 应逐步转 positive admission law；基线稳定后 DELETE 黑名单累加（PROOF-MAP §91/§126、HANDOFF §9.5） | 本文件 + `PROOF.md` |
| `provider-leak-gate-baseline.json` | HOW/MECHANISM：baseline ratchet 是防回归工具，无独立 semantic 内容 | 本文件 |
| `horizon-surface.test.mjs` 的 `fast-coder is still away` 之类断言用机器名 | 仅测试 fixture：断言的是「名册按名字渲染、无 DTO」，不是「fast-coder 应可见」。真实 provider 面看不到 binding 名（`FAST_DEEP_BINDING_RE`） | 本文件 |
| GrandRewrite（active change）把机器拓扑撤出 horizon、普通 completion 取代 `return` | EVIDENCE：语义已进 EXEC-026/031 等；本包吸收其 horizon 面（EXEC-030），不复制其迁移细节 | WHY.md 失败模式表 |
| `repository-warm-start.md` 的 `MaxKeywords=8 / TopK=4 / 24 hints / 64 KiB` 具体值 | **GARBAGE/HOW**（HANDOFF §12：tuning values 不升级为永久 WHAT）：本包只取准入法则（012/013），数值归 `knowledge-reuse` 的 HOW | 本文件 |
| `PromptRecoveryTailWindow=50 / Budget=3` 等其它常量 | 不归本包（`dispatch-protocol` / `provider-attempt-recovery` 领域） | — |

## 依赖说明

INDEX.md 依赖骨架：`participant-horizon → 无`（可独立定义）。消费方（`provider-projection`、
`guidance-delivery`、`delegation`、`finality` 等）引用本包 guarantee，本包不反向依赖。
