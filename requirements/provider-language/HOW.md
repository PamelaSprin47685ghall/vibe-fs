# HOW — provider-language

> 非 normative。描述实现模型与约束；真实规范见 `WHAT.md`。

## 实现模型

### 类型与绑定

```text
ProviderLanguage = English | SimplifiedChinese        Session/ProviderLanguage.fs
SessionProviderLanguage                                process-local Dictionary(sessionId → language)
    bindOnce:   未绑 → 绑定；同值 → Ok 同值；异值 → Error（fail-closed）
    inheritFromOwner(ownerLang, childId) = bindOnce childId ownerLang
```

- `ProviderLanguageBinding.fs`（`Wanxiangshu.OpenCode`）：
  - `readGlobalPreference()`：读 `WANXIANGSHU_PROVIDER_LANGUAGE`（`en` | `zh-CN`），
    缺省 `English`；无法识别 → 抛错。
  - `ensureRoot(sessionId)`：首触达绑定——`tryGet` 命中即返回，否则按全局偏好 `bindOnce`。
  - `ensureInherited(ownerId, childId)`：先 `ensureRoot(ownerId)`，再继承绑定，**不重读全局**。
- 真实 browsing/session 生命周期外的进程级绑定（`SessionProviderLanguage` 是 process-local，
  见 `ProviderLanguage.fs` 注释）是当前实现的 HOW；durable journal 事实随 Phase 17
  资源 parity 落地。

### 资源装载（Class A 唯一入口）

```text
semantic owner（Domain 文本 owner）
  → ProviderResources.readText(lang, semanticPath)      // resources/provider/<semantic>/<en.md|zh-CN.md>
  → already-localized string
  → SyntheticToml / ToolHostCodec（layout/escaping only）
```

- `ProviderResources.fs`（`Wanxiangshu.Infrastructure.Resources`）：
  `relativePath` / `exists` / `readText` / `tryReadText` / `requireLanguagePair`
  （缺任一 locale → 抛错）/ `languageRootsPresent`。
- `ProviderProse.fs`（`Wanxiangshu.Resources`）：
  - `languageOf(sessionId)`：已绑 → 该语言；未绑 → English（首触达，不 bind）。
  - `substitute(template, subs)`：`{{name}}` 替换；缺参或残留 → 抛错（fail-closed）。
  - `render` / `instructionLines` / `document` / `documentFor`：装载 + 替换 + 交
    `SyntheticToml` 布局。
- 装载装配顺序（历史 how/prompt 条款）：Common Law → Role Law → Office Library；Tools
  面不并入 system 串。tool description 按已绑 `SessionProviderLanguage` 装载。

### 结构 parity 机制（Gate C）

`scripts/checks/language-parity-gate.mjs` 提供四个扫描器（机制共享，语义 owner 各归其主）：

| 扫描器 | 检查 | 语义 owner |
|---|---|---|
| `scanParity` | 每个 semantic 目录 en.md + zh-CN.md 成对 | provider-language（006） |
| `scanPlaceholderParity` | EN/zh-CN `{{name}}` 集合一致 | provider-language（007） |
| `scanIdentifierParity` | code span / tip / tool 名跨语言一致 | provider-language（011） |
| `scanSemanticAnchorParity` | 同 id 双语命中；每个 role 在 catalog | 机制归 provider-language（010），anchor 内容归各 owner |

### prose ownership ratchet（Gate E）

`scripts/checks/provider-prose-ownership.mjs`：扫描已知 provider-surface owner 源码，
禁止新增 Class A 自然语言 literal；per-file baseline 只减不增（当前 baseline `{}`）。

## 失败路径

- `bindOnce` 异值 → `Error`（PROVIDER-LANGUAGE-002 RED）。
- `requireLanguagePair` 缺 locale → 抛错（PROVIDER-LANGUAGE-006 RED）。
- `substitute` 缺参/残留 → 抛错（PROVIDER-LANGUAGE-007 RED）。
- Gate E 新增 prose literal → baseline 回归（PROVIDER-LANGUAGE-009 RED）。
- Gate C 任一 parity 失配 → 门禁红（PROVIDER-LANGUAGE-006/007/010/011 RED）。

## 历史与弃权

| 源 | 判定 | 说明 / 落点 |
|---|---|---|
| 历史 change（PromptRestoration） | EVIDENCE | WHY 考古：Gate 0 前「system=zh-CN + tool=English」半 i18n 病灶；Class A/B/C；Gate E ratchet 580→0。落点：WHY.md 失败模式 + WHAT 005/009 |
| 历史 why/host §21 | EVIDENCE | 被拒方案：每 attempt/child 重读全局；语言绑 Role/Agent。落点：WHY.md + WHAT 002/003/004 |
| 历史 why/prompt「ProviderLanguage」节 | EVIDENCE | 被拒方案：运行中切语言、译 protocol id。落点：WHY.md + WHAT 004/011 |
| Gate 0/Batch 1–5 迁文日程（PromptRestoration §Migration batches） | HOW | 迁移执行记录，非永久命题。记录于本 HOW 历史节 |
| `ProviderLanguage.tryParse` 别名（`zh`/`chs`/`cn`/`eng`…） | HOW | 解析容错，非规范 |
| `WANXIANGSHU_PROVIDER_LANGUAGE` env 名 | HOW | 当前偏好源机制 |
| 双语资源文件具体内容 | HOW/GARBAGE 弃权 | 内容语义归各 semantic owner（office/action/cognition/…），本包只拥有结构 parity 机制；未逐字消费 |
| `SessionProviderLanguage` process-local Phase 2 状态 | HOW | 临时内存绑定；durable 落地在 Phase 17，非本包命题 |

## DEPENDS ON

- `session-ontology`：语言绑定/继承是 session 创建事实（理由见 `WHY.md` DEPENDS ON）。

## 验证与测试落点

> 每条 WHAT 命题恰好一行落点。类型：`MOVE` = 物理移入本包；`REUSE` = 留在原处（多-owner，
> cutover 时按 `SPLIT@cutover` 拆分）；`NEW` = 本包新写。
> 运行命令统一为 `node --test <file>`（在仓库根执行）。

### 落点表

| 命题 | 落点测试（文件 + test 锚点） | 类型 | 运行命令 |
|---|---|---|---|
| PROVIDER-LANGUAGE-001（二元类型 + locale 映射） | `requirements/provider-language/tests/provider-language.test.mjs` `WHAT[PROVIDER-LANGUAGE-001] ProviderLanguage parses en and zh-CN with locale mapping`（parse/label/resourceDirectory）、`WHAT[PROVIDER-LANGUAGE-001] provider resource language roots map en.md and zh-CN.md`（relativePath 映射 en.md/zh-CN.md）；`provider-system-transform.test.mjs` `WHAT[PROVIDER-LANGUAGE-001] system transform is stable for an English session` | MOVE | `node --test requirements/provider-language/tests/provider-language.test.mjs` |
| PROVIDER-LANGUAGE-002（bind-once 不可变 + 异值 fail-closed） | `provider-language.test.mjs` `WHAT[PROVIDER-LANGUAGE-002] bind once is immutable and conflicting rebind fails closed`（同值 Ok、异值 `already bound` Error）；`provider-prose-and-preference.test.mjs` `WHAT[PROVIDER-LANGUAGE-002] bound session language follows the session binding` | MOVE + NEW | 见各文件行 |
| PROVIDER-LANGUAGE-003（child 继承，不重读全局） | `provider-language.test.mjs` `WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without re-reading global`（`inheritFromOwner` → child `tryGet` = owner 语言）；`provider-prose-and-preference.test.mjs` `WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without reading the global preference`（全局偏好已切换时 child 仍继承 owner） | MOVE + NEW | 见各文件行 |
| PROVIDER-LANGUAGE-004（全局偏好只影响未来 session） | `provider-prose-and-preference.test.mjs` `WHAT[PROVIDER-LANGUAGE-004] preference change only affects future sessions`（绑定后改全局，旧 session 不变、新 session 取新值）、`WHAT[PROVIDER-LANGUAGE-004] unbound session language is English (first touch)`（ensureRoot 首触达）；`provider-language.test.mjs` `WHAT[PROVIDER-LANGUAGE-004] global preference defaults to English when env unset` | NEW + MOVE | 见各文件行 |
| PROVIDER-LANGUAGE-005（Class A/B/C 分类） | `provider-prose-ownership.test.mjs` `WHAT[PROVIDER-LANGUAGE-005] heuristic excludes paths and identifiers from Class A`（Class B 路径/标识排除）；`provider-system-transform.test.mjs` `WHAT[PROVIDER-LANGUAGE-005] system transform localizes only the wanxiangshu-owned segment`（Class A 运行时应用）；REUSE `requirements/finality/tests/lifecycle.test.mjs` `WHAT[PROVIDER-LANGUAGE-005] frozen texts use lf only`（SURFACE-002 固定文案英文/LF） | MOVE + REUSE | 见各文件行 |
| PROVIDER-LANGUAGE-006（locale 成对 + bound fail-closed） | REUSE `language-parity-gate.test.mjs` `WHAT[PROVIDER-LANGUAGE-006] parity detects missing zh-CN leaf` / `WHAT[PROVIDER-LANGUAGE-006] parity detects missing en leaf in the real tree` / `WHAT[PROVIDER-LANGUAGE-006] locale leaves are en.md and zh-CN.md under the provider root` / `WHAT[PROVIDER-LANGUAGE-006] ProviderResources hook must require the language pair`；NEW `provider-prose-and-preference.test.mjs` `WHAT[PROVIDER-LANGUAGE-006] require language pair fails closed on missing semantic path` | REUSE + NEW | 见各文件行 |
| PROVIDER-LANGUAGE-007（placeholder parity + 填值不译 + 未替换 fail-closed） | REUSE `language-parity-gate.test.mjs` `WHAT[PROVIDER-LANGUAGE-007] placeholder parity passes on equal sets` / `WHAT[PROVIDER-LANGUAGE-007] placeholder parity mismatch reports diff` / `WHAT[PROVIDER-LANGUAGE-007] placeholder extraction dedupes and skips plain text`；NEW `provider-prose-and-preference.test.mjs` `WHAT[PROVIDER-LANGUAGE-007] substitute replaces values and fails closed on missing or leftover` | REUSE + NEW | 见各文件行 |
| PROVIDER-LANGUAGE-008（tool prose 与 session 语言一致） | REUSE `language-parity-gate.test.mjs` `WHAT[PROVIDER-LANGUAGE-008] repo scan is green across every semantic surface`（全 semantic 面成对）+ MOVE `provider-language.test.mjs` `WHAT[PROVIDER-LANGUAGE-008] bound language loads its own locale leaf`（lang → locale leaf 装载映射） | REUSE + MOVE | 见各文件行 |
| PROVIDER-LANGUAGE-009（三向所有权 + 禁 match lang + Gate E ratchet） | `provider-prose-ownership.test.mjs`（MOVE）`WHAT[PROVIDER-LANGUAGE-009] Gate E scan roots cover Gate 0 owners` / `WHAT[PROVIDER-LANGUAGE-009] green fixture is zero hits` / `WHAT[PROVIDER-LANGUAGE-009] red fixture counts english and chinese literals`（禁散落 NL literal）/ `WHAT[PROVIDER-LANGUAGE-009] baseline ratchet blocks regression` / `WHAT[PROVIDER-LANGUAGE-009] repo scan with generated baseline is green` / `WHAT[PROVIDER-LANGUAGE-009] zero hits is closed` / `WHAT[PROVIDER-LANGUAGE-009] committed baseline matches repo` | MOVE | `node --test requirements/provider-language/tests/provider-prose-ownership.test.mjs` |
| PROVIDER-LANGUAGE-010（Role Law semantic-anchor 同 id 双语命中） | REUSE `language-parity-gate.test.mjs` `WHAT[PROVIDER-LANGUAGE-010] semantic anchor parity detects missing zh id`（fixture 缺 id → 红）+ `WHAT[PROVIDER-LANGUAGE-010] every role law directory must appear in the catalog`（role 目录必须在 catalog）+ `WHAT[PROVIDER-LANGUAGE-010] repo lists role semantic dirs for the catalog`（role/ 目录枚举） | REUSE | `node --test requirements/provider-language/tests/language-parity-gate.test.mjs` |
| PROVIDER-LANGUAGE-011（protocol identifiers 永不翻译） | REUSE `language-parity-gate.test.mjs` `WHAT[PROVIDER-LANGUAGE-011] identifier parity passes when both locales keep the same spans` / `WHAT[PROVIDER-LANGUAGE-011] identifier parity mismatch reports semantic and diff` / `WHAT[PROVIDER-LANGUAGE-011] tip and tool catalog hits must match across locales` / `WHAT[PROVIDER-LANGUAGE-011] protocol identifier extraction unions sources` / `WHAT[PROVIDER-LANGUAGE-011] code span extraction skips fenced blocks` | REUSE | `node --test requirements/provider-language/tests/language-parity-gate.test.mjs` |

### MOVE 记录

| 源 → 目标 | 适配 | 验证 |
|---|---|---|
| `requirements/provider-language/tests/provider-language.test.mjs` → `requirements/provider-language/tests/provider-language.test.mjs` | 直接引入所有者模块 | `node --test` 6/6 绿 |
| `requirements/provider-language/tests/provider-prose-ownership.test.mjs` → `requirements/provider-language/tests/provider-prose-ownership.test.mjs` | import 深度不变（同级） | `node --test` 8/8 绿 |

### SPLIT@cutover（REUSE 项拆 owner 计划）

- `requirements/provider-language/tests/language-parity-gate.test.mjs`：
  - provider-language 拿走：`gate_c_*`（locale leaves / placeholder parity / semantic
    anchor parity 机制 / repo scan）、`ac20_*`（identifier parity）。
  - 留给 `office-capability` / `action-affordance`：`gate_f_*`（Office capability
    integrity）、`gate_c_tool_description_anchor_parity_*`（tool description 语义锚点）。
  - cutover：拆成两个文件，各归其包；本包保留 `requirements/provider-language/tests/language-parity-gate.test.mjs` 结构部分。
- `requirements/prefix-stability/tests/system-prompt-stability.test.mjs`（Gate D；原 tests/unit/invariants/prompt-stability.test.mjs 拆分）：
  - provider-language 交叉引用行：SessionProviderLanguage 冻结由 `HOST_026_*`（MOVE 项）证明；
    本文件断言的是 persona/system 字节稳定（`participant-identity` + `prefix-stability`），
    不双 owner。

### 本包拥有的 semantic anchor id

**0 个。** provider-language 不拥有任何 `ROLE_SEMANTIC_ANCHORS` / `TOOL_DESCRIPTION_ANCHORS`
/ `OFFICE_CAPABILITY_ANCHORS` 语义 id（anchor 内容归 office/action/cognition/各域 owner）；
本包拥有的是「同 id 双语命中」的**结构 parity 机制**（`scanSemanticAnchorParity` 的
机制断言，落在 REUSE `WHAT[PROVIDER-LANGUAGE-010] semantic anchor parity detects missing zh id`）。
