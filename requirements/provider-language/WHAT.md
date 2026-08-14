# WHAT — provider-language

> 本文件是本包的**唯一 normative 合同**。所有命题同时为真；世界 RED 当且仅当某条命题被违反。
> 每条命题的证据指针 → `PROOF.md` 对应行。

命题前缀：`PROVIDER-LANGUAGE-`。

---

## PROVIDER-LANGUAGE-001：ProviderLanguage 是二元封闭类型

**规范**：`ProviderLanguage` 是 `English | SimplifiedChinese` 的封闭类型
（`src/Wanxiangshu/Domain/ProviderLanguage.fs`）。第一版 EN / zh-CN 双语同时上线。
locale leaf 文件名与资源目录名由类型决定：`English → en.md`、`SimplifiedChinese →
zh-CN.md`。

**含义**：语言是类型，不是字符串；编译器拦截「第五种语言」的非法状态。
**边界**：当前只支持 EN/zh-CN **不是**永久承诺（`06-language.md` DOES NOT OWN）；
新增 locale 属于本包独立变化范围。语言值 ≠ prose 内容。
**证据**：`archive/docs/what/prompt.md` PROMPT-017。

## PROVIDER-LANGUAGE-002：语言在 session 创建时绑定一次，不可变

**规范**：`SessionProviderLanguage` 在 session 创建瞬间绑定，此后不可变。重复绑定同一
语言返回同一值；绑定不同语言返回 `Error`（fail-closed，`bindOnce` 语义）。
任何运行中事件（fallback / Strength / restart / reanchor / BlindPlan T1 / process
review / Finality / compaction / recovery）都**不得**改写已绑语言。

**含义**：世界语言是 session 的创建事实，不是可随请求漂移的配置。
**边界**：绑定「谁执行」不在本包——execution binding 变化（换 Peer）由
`participant-identity` 管辖；本包只保证 binding 变了语言不变。
**证据**：`archive/docs/what/host.md` HOST-026、`archive/docs/what/prompt.md` PROMPT-017/014。

## PROVIDER-LANGUAGE-003：child / attached / internal 继承 owner 语言，不各自重读全局

**规范**：child / attached / InternalLeaf execution 继承 owner（或 commissioner）已绑
语言；`ProviderLanguage.inheritFrom owner = owner`。继承路径不得再次读取全局偏好
（`ProviderLanguageBinding.ensureInherited`）。

**含义**：父会话说 zh-CN，子会话不能说 English——否则同一段工作出现两个认知环境。
**边界**：继承的是语言值，不是 authority 或 persona。
**证据**：`archive/docs/what/host.md` HOST-026、`archive/docs/proof/host.md`「SessionProviderLanguage
证明」child 继承行。

## PROVIDER-LANGUAGE-004：全局偏好变化只影响未来 session

**规范**：用户后续切换全局语言偏好，只作用于此后新建的 session。已绑定 session 的语言
不随偏好改写（bind-once 拒绝重绑）；第一触达（`ensureRoot`）只发生在未绑定 session。

**含义**：已开 Life 的世界语言与 Opening / Library / tool 后果必须字节连续；中途换语
等于重写前缀。
**边界**：全局偏好源（`WANXIANGSHU_PROVIDER_LANGUAGE` env）是 HOW，不是命题。
**证据**：`archive/docs/what/host.md` HOST-026、`archive/docs/why/host.md` §21。

## PROVIDER-LANGUAGE-005：localizable / invariant 分类（Class A/B/C）

**规范**：进入 participant horizon 的自然语言分三类：

| Class | 规则 | 例子 |
|---|---|---|
| A Provider prose | 必须 i18n | system / Role Law / Common Law / Office Library / tool description / consequence / runtime / Finality / hints / WorkRecord headings |
| B Technical literals | 永不翻译 | tool names / argument names / wire field names / enum literals / paths / source identifiers / commands（含 `exit_code`） |
| C Internal diagnostics | 不进 horizon → 不属 Provider i18n | 内部日志 / 诊断详情 |

**含义**：`A translation changes the language of the world, not the identifiers of its
machinery.`
**边界**：Class A 的**语义内容**归各 semantic owner；本包只拥有「语言」这一轴。
**证据**：`archive/docs/what/prompt.md` PROMPT-017（Localizable/Invariant 表）、PROMPT-019。

## PROVIDER-LANGUAGE-006：每个 provider semantic resource 必须 en.md + zh-CN.md 成对存在；bound 缺语言 fail-closed

**规范**：每个 provider semantic 资源目录必须同时含 `en.md` 与 `zh-CN.md`（Gate C
locale leaves）。已绑定 session 请求缺失的 locale → 失败；**禁止 silent English
fallback**（`ProviderResources.requireLanguagePair` 抛错）。

**含义**：缺 localization ≠ 许可换语言。
**边界**：资源文件的**内容语义**归 semantic owner；「成对存在」的结构保证归本包。
**证据**：`archive/docs/what/prompt.md` PROMPT-019、`archive/docs/what/architecture.md` ARCH-016 Gate C。

## PROVIDER-LANGUAGE-007：`{{name}}` placeholder 结构 parity；填值不译；未替换 fail-closed

**规范**：参数化散文在资源模板内用 `{{name}}`；EN 与 zh-CN 的 placeholder **集合必须
一致**（结构 parity）；运行时填入的值不翻译；残留未替换 placeholder → 失败
（`ProviderProse.substitute` 抛错）。

**含义**：placeholder 是语言无关的操作数，两边模板必须同构；缺参是程序错误不是排版。
**边界**：placeholder 的**值**语义归各 surface owner。
**证据**：`archive/docs/what/prompt.md` PROMPT-019、PromptRestoration Gate C。

## PROVIDER-LANGUAGE-008：同一 participant 的工具 prose 与其 session 语言一致

**规范**：一个 participant 看见的 tool prose **必须**与其 `SessionProviderLanguage`
一致（HOST-026）。禁止 `zh-CN` system + English tool contract。Tool description 按已绑
语言装载（`how/prompt.md`），不得与 system 混语。

**含义**：语言是一整个世界的属性，不是每块 prose 各自挑的。
**边界**：tool description 的**调用合同语义**（act/时机/负边界/后果/参数）归
`action-affordance`。
**证据**：`archive/docs/what/prompt.md` PROMPT-019、`archive/docs/how/prompt.md`「Tool description 按
已绑语言装载」。

## PROVIDER-LANGUAGE-009：prose 三向所有权分离；禁 TranslationRegistry、禁业务 `match lang`

**规范**：

```text
Meaning belongs to its semantic owner.
Language belongs to the session.
Rendering belongs to machinery.
```

禁止巨型 `TranslationRegistry`；禁止业务代码 `match lang` / `if lang then …` 散落
自然语言句子。Class A 只经 `ProviderResources` 装载；`SyntheticToml` /
`ToolHostCodec` 只拥有 layout / escaping，接收 already-localized 串。

**含义**：Language 集中为律；Meaning 仍按 owner 分布。渲染机制不拥有 prose 意义。
**边界**：`SyntheticToml` 的布局/转义机制本身归 `provider-projection`；本包只拥有
「谁的语言、何时装载」这条轴。
**证据**：`archive/docs/what/prompt.md` PROMPT-019、`archive/docs/how/prompt.md`「Provider-visible
prose 装载」、PromptRestoration Gate E。

## PROVIDER-LANGUAGE-010：Role Law semantic-anchor 同 id 双语命中（结构 parity 机制）

**规范**：同一 semantic anchor id 必须在 EN 与 zh-CN 两份 Role Law 中同时命中
（`scanSemanticAnchorParity` 机制）。每个 `role/` 目录必须在 catalog 中。

**含义**：两条语言版本表达同一组区分；结构 parity 是语言一致性在语义面的机械证明。
**边界**：anchor id 的**内容语义**归各 owner（office → `office-capability`、tool →
`action-affordance`、cognition → `cognitive-environment`、browser provenance →
`external-investigation`）；本包只拥有「同 id 双语都命中」的结构保证。
**证据**：`archive/docs/proof/prompt.md`「EN / zh-CN 语言面」Role Law semantic-anchor 行。

## PROVIDER-LANGUAGE-011：protocol identifiers 永不翻译

**规范**：tool 名、argument 名、wire field 名、enum literal、路径、source identifier、
命令——跨语言**原样**（invariant 面）。任何 locale 下同一标识符指向同一 contract。
`A tool name names one contract in every provider language.`

**含义**：机器标识是 identity，不是 prose；翻译它等于换 contract。
**边界**：wire layout 本身（消息怎么排）归 `host-boundary`；本包只保证标识符的语言不变性。
**证据**：`archive/docs/what/prompt.md` PROMPT-017 invariant 列、PROMPT-019 Class B。

---

## 反向覆盖（OWNED clause → 命题）

| 源 Clause | 命题 |
|---|---|
| PROMPT-017 全文 | 001/002/003/004/005/011 |
| PROMPT-019 三向分离 + Class A/B/C | 005/006/007/008/009/010/011 |
| PROMPT-014 SessionProviderLanguage 行 | 002/004 |
| HOST-026 全文 | 002/003/004/006/008 |
| ARCH-016 Gate C（locale leaves / placeholder / anchor parity） | 006/007/010 |
| ARCH-016 Gate E（prose ownership ratchet） | 009 |
| SURFACE-001/002（新增固定文案英文/LF） | 005（Class A 必须 i18n 的应用） |
| `06-language.md` OWNS 全表 | 001–011 |

## DOES NOT OWN（明确不归我）

- prose 的业务意义（各 semantic owner）、Persona、Role/Tool/Runtime contract。
- provider wire layout（`host-boundary`）。
- 当前只支持 EN/zh-CN 是否永久（开放问题，非命题）。
- 真实 bilingual 资源文件的内容正确性（各 semantic owner 的 semantic anchor 内容）。
