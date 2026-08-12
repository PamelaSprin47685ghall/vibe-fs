> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# Prompt Restoration

---

# Original proposal

> **A prompt is not documentation compressed for a human reader. It is part of the cognitive environment in which the Agent decides what kind of participant to become.**
>
> **That cognitive environment has one language at a time. No feature may quietly speak another.**
>
> **Meaning belongs to its semantic owner. Language belongs to the session. Rendering belongs to machinery.**

## Total goal

> **All natural language that enters a participant's horizon is ProviderLanguage-owned.**
>
> 凡进入 participant horizon 的自然语言，全部受 ProviderLanguage 管辖。

Language is centralized as a law. Meaning remains distributed by owner.

```text
ProviderLanguage        decides language
Feature / semantic owner owns meaning
ProviderResources       stores localized prose
SyntheticToml / codecs  own representation only
```

禁止巨型 `TranslationRegistry`。禁止业务代码 `if lang then English sentence`。

## Blocking track — Provider-visible prose ownership sweep = Gate 0

当前半 i18n 状态不可接受：

```text
system prompt = zh-CN
role law / library = zh-CN
tool description / consequence / runtime / finality / Manager narrative = English
```

`ProviderLanguage` / `ProviderResources` 骨架已在；散落 prose 仍由代码直接拥有。

Gate 0：先禁止新增散落文本，再一边恢复提示词厚度、一边把每一块语义资产完整搬进 EN/ZH ProviderResources。

## String classes

| Class | Rule |
|-------|------|
| A Provider prose | 必须 i18n（凡模型会读到的自然语言） |
| B Technical literals | 永不翻译（tool/arg/wire/enum/path/command） |
| C Internal diagnostics | 不进 horizon → 不属 Provider i18n |

`A translation changes the language of the world, not the identifiers of its machinery.`
`Values cross languages unchanged. The sentence that gives them meaning does not.`
`A tool name names one contract in every provider language.`

Bound session：missing localization → fail closed；禁止 silent English fallback。

## Migration batches

0. Gate — provider-prose-ownership ratchet（禁新增污染）
1. Runtime common surfaces（RuntimeNudge / SyncDelegate / ForkChild / recovery…）
2. Manager lifecycle（Narrative / Lifecycle / Finality / MagicTodo guidance）
3. Tool surfaces（description / arg / success / failure；按 owner）
4. Assistance / Strength / Recovery / warm-start
5. Role Law restoration（完整厚度；周围世界已无语言泄漏）

逐 semantic owner：freeze EN canonical → author zh-CN → wire ProviderLanguage → delete hardcoded owner → semantic parity gate。

## Gates

- Gate C 扩展：成对存在 + placeholder structural parity + semantic-anchor parity（与 Role Law depth 可共用机制）
- Gate E（新）：`provider-prose-ownership.mjs` — 已知 provider-surface owner fail-closed；baseline ratchet 只减不增

## Non-goals

- ICU / plural / gender DSL
- 翻译内部 diagnostics
- 其它 locale（第一版仅 EN / zh-CN）
- 与本 Change 无关的 GrandRewrite AC15/AC16 WorkRecordStart 接线（仍属 GrandRewrite）

---

# Active work

## Specification impact

- PROMPT-019：Provider-visible prose ownership
- ARCH-016：Gates A–D → A–E（Gate E = prose ownership ratchet）
- PROMPT-017 / Gate C：交叉引用；`{{placeholder}}` 结构 parity 已现行；semantic-anchor 仍后续
- GrandRewrite Phase 17 完整文案迁移义务移交本 Change；GrandRewrite 保留 AC15/AC16 关闭路径

## Remaining work

全部闭合。Gate 0 + Batch 1–5 + legacy prompts 清除；Gate E baseline `{}`。semantic-anchor parity 仍属 PROMPT-019 / ARCH-016 C 后续义务，不在本 Change 关闭范围内。

## Done this integration

- Spec：PROMPT-019 + ARCH-016 E；`ProviderProse`（`{{name}}` + `languageOf`）
- Role Law restore + depth ratchet；legacy prompts 包面清除
- Batch 1/2/3/4 迁文完成；ToolRegistry `match lang` overlay 删除
- Gate C：`{{placeholder}}` 结构 parity 现行
- Gate E：**580 → 0**

## Completion criteria

- PROMPT-019 / ARCH-016 E 五层 docs 对齐且 proof 可红 — **已满足**
- Gate E baseline → 0 — **已满足**（`{}`）
- Batch 3 迁完；`npm run check` 绿 — **已满足**（unit 2413；harness 275）
- Final outcome 追加后移入 `completed/` — **已满足**

## Blockers

无。

---

# Final outcome

## Outcome

进入 participant horizon 的 Class A 自然语言全部经 `ProviderResources` 装载、由 session `ProviderLanguage` 管辖。Gate E 静态面与 baseline 均为 0。Batch 1–5 迁完；Role Law 是身份文本，不列工具名。

## Final specification

- `docs/what/prompt.md` PROMPT-019：Meaning = semantic owner；Language = session；Rendering = machinery。Class A/B/C。bound 缺 locale → fail closed。
- `docs/what/architecture.md` ARCH-016 Gate E：已知 owner 禁新增 NL literal；baseline 只减不增。
- Gate C：叶对 + `{{placeholder}}` 集合一致为现行；semantic-anchor parity 仍后续。

## Implementation result

- `ProviderProse` 命名空间 `Wanxiangshu.Resources`：`render` / `instructionLines` / `document` / `languageOf`。`{{name}}` 未替换 fail closed。unbound `languageOf` → English，不 `bindOnce`。
- Description 走全局偏好；Execute 后果走 session bind（空 sessionId → 全局偏好）。
- ToolRegistry 删除 `match lang` 中文 overlay；gate deny 走资源。
- Manager Role Law 去掉工具清单与 witness 词；SURFACE-006 改为禁外国工具反引号名。
- Application 只 `open Wanxiangshu.Resources`，不穿 Infrastructure。

## Verification

- Gate E：`scripts/checks/provider-prose-ownership-baseline.json` = `{}`；`provider-prose-ownership: OK`。
- Gate C：`language-parity-gate: OK` — 278 semantic pairs；placeholder 集合一致。
- `npm run check` 绿：unit 2413/2413；integration；harness 275/275。

## References

- `docs/what/prompt.md` PROMPT-019
- `docs/proof/prompt.md` Provider-visible prose ownership
- `docs/how/architecture.md` ARCH-016 Gate E
- `src/Wanxiangshu/Infrastructure/Resources/ProviderProse.fs`
- `scripts/checks/provider-prose-ownership.mjs`