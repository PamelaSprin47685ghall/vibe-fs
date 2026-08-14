# HOW — provider-language

> 非 normative。描述实现模型与约束；真实规范见 `WHAT.md`。

## 实现模型

### 类型与绑定

```text
ProviderLanguage = English | SimplifiedChinese        Domain/ProviderLanguage.fs
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
- 装载装配顺序（`archive/docs/how/prompt.md`）：Common Law → Role Law → Office Library；Tools
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
| `archive/changes/completed/PromptRestoration.md` | EVIDENCE | WHY 考古：Gate 0 前「system=zh-CN + tool=English」半 i18n 病灶；Class A/B/C；Gate E ratchet 580→0。落点：WHY.md 失败模式 + WHAT 005/009 |
| `archive/docs/why/host.md` §21 | EVIDENCE | 被拒方案：每 attempt/child 重读全局；语言绑 Role/Agent。落点：WHY.md + WHAT 002/003/004 |
| `archive/docs/why/prompt.md`「ProviderLanguage」节 | EVIDENCE | 被拒方案：运行中切语言、译 protocol id。落点：WHY.md + WHAT 004/011 |
| Gate 0/Batch 1–5 迁文日程（PromptRestoration §Migration batches） | HOW | 迁移执行记录，非永久命题。记录于本 HOW 历史节 |
| `ProviderLanguage.tryParse` 别名（`zh`/`chs`/`cn`/`eng`…） | HOW | 解析容错，非规范 |
| `WANXIANGSHU_PROVIDER_LANGUAGE` env 名 | HOW | 当前偏好源机制 |
| 双语资源文件具体内容 | HOW/GARBAGE 弃权 | 内容语义归各 semantic owner（office/action/cognition/…），本包只拥有结构 parity 机制；未逐字消费 |
| `SessionProviderLanguage` process-local Phase 2 状态 | HOW | 临时内存绑定；durable 落地在 Phase 17，非本包命题 |
