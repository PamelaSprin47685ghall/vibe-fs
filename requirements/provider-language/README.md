# provider-language

**一句话 WHY**：一个 participant life 必须生活在单一、稳定的自然语言世界里，而 protocol
identifiers（tool 名 / wire field / enum / path / command）永远不翻译。

## 这个包保证什么

- 语言是 **session 级事实**：创建时绑定一次，不可变；child/attached/internal 继承；
  全局偏好变化只影响未来 session。
- 进入 horizon 的自然语言分三类：**Class A**（必须 i18n）、**Class B**（技术标识，永不译）、
  **Class C**（内部诊断，不进 horizon）。
- 每个 provider semantic 资源 EN + zh-CN 成对；bound 缺语言 **fail-closed**，禁 silent
  English fallback。
- 三向所有权：**Meaning → semantic owner；Language → session；Rendering → machinery**。
  禁 TranslationRegistry、禁业务 `match lang` 散落句子。

## WHAT 概览（11 条命题）

`WHAT.md` 编号 `PROVIDER-LANGUAGE-001..011`：二元类型（001）、bind-once（002）、child
继承（003）、偏好只影响未来 session（004）、Class A/B/C（005）、locale 成对 +
fail-closed（006）、placeholder parity（007）、tool prose 同 session 语言（008）、三向
所有权（009）、semantic-anchor 双语 parity 机制（010）、protocol id 永不译（011）。

## HOW 概览

类型 `Session/ProviderLanguage.fs`（`English | SimplifiedChinese` + `SessionProviderLanguage`
bind-once 字典）；绑定 `Infrastructure/OpenCode/Host/ProviderLanguageBinding.fs`
（`ensureRoot` / `ensureInherited`）；装载 `Infrastructure/Resources/{ProviderResources,
ProviderProse}.fs`；结构 parity 门 `scripts/checks/language-parity-gate.mjs`（Gate C）；
prose ownership ratchet `scripts/checks/provider-prose-ownership.mjs`（Gate E）。

## Proof 概览

- MOVE：`tests/provider-language.test.mjs`（bind-once/inherit/parse/资源根）、
  `tests/provider-prose-ownership.test.mjs`（Gate E）。
- NEW：`tests/provider-prose-and-preference.test.mjs`（fail-closed 装载 + 偏好作用域）。
- REUSE：`requirements/provider-language/tests/language-parity-gate.test.mjs`（Gate C 结构 parity 部分，
  `SPLIT@cutover` 拆给 office/action 的语义锚点）。

## 阅读顺序（零上下文读者）

1. `WHY.md` —— 为什么必须独立存在、RED 长什么样、历史病灶。
2. `WHAT.md` —— 11 条命题（唯一 normative）。
3. `HOW.md` —— 类型/绑定/装载/parity 机制怎么落地。
4. `PROOF.md` —— 每条命题的测试落点与怎么跑。

## 运行

```text
node --test requirements/provider-language/tests/provider-language.test.mjs
node --test requirements/provider-language/tests/provider-prose-ownership.test.mjs
node --test requirements/provider-language/tests/provider-prose-and-preference.test.mjs
```

## DEPENDS ON

- `session-ontology`：语言绑定/继承是 session 创建事实（理由见 `WHY.md` DEPENDS ON）。
