# WHY — provider-language

## 一句话理由

一个 participant life 必须生活在**单一、稳定**的自然语言世界里；机器 protocol
identifiers 则必须保持同一 identity，永远不翻译。

## 不可替代性：为什么不能并进别的包

语言绑定不是 identity、horizon、projection 或 guidance 的附属字段（HANDOFF §6.2 裁决）：

- **不是 `participant-identity`**：换 execution binding（fast→deep fallback、Strength
  replica）不换人，也不换语言。把语言绑在 Role/EffectiveAgent 上，换 Peer 就会误换世界语言
  （`docs/why/host.md` §21 被拒方案）。
- **不是 `participant-horizon`**：horizon 回答「什么信息有资格进入 experience」；
  language 回答「这些信息用哪种语言呈现」。
- **不是 `provider-projection`**：projection 回答「已决定可见后如何确定性表示」；
  language 回答「这个 life 说哪种语言」。
- **不是 guidance/cognition**：语言是承载 prose 的 session 事实，不是 prose 的语义内容。

## 失败模式（RED 长什么样）

只要满足下列任一情形，世界就是 RED 的（`06-language.md` FAILURE MEANING）：

1. **同一 session 出现多个自然语言世界**：例如 `zh-CN` system prompt + English tool
   contract（PROMPT-019 明文禁止）。
2. **child 与 owner 语言漂移**：fallback / Strength / restart / reanchor / BlindPlan T1
   之后子会话或下一轮突然换语——Opening、Office Library、tool 后果与历史 marker 不再
   属于同一世界，前缀缓存与身份连续性同时碎（`docs/why/host.md` §21）。
3. **翻译改变 tool/wire identity**：把 `exit_code` 译成「退出码」、把 wire field 名本地化。
   机器标识不翻译——翻译改变的是世界的语言，不是机器的标识（PROMPT-017）。

历史上为什么发生过：PromptRestoration（`changes/completed/PromptRestoration.md`）记录
Gate 0 之前的半 i18n 状态——system prompt = zh-CN、role law / library = zh-CN、tool
description / consequence / runtime / finality = English。同一个 participant 同时面对
两个语言世界；散落 prose 由业务代码直接拥有，`match lang` 遍布代码。这是本包存在的
考古起点。

## 独立变化测试（Independent Change Test）

新增 locale（例如 fr）或改变 locale resource layout，完全不需要动 identity / horizon /
projection 的任何命题；反之亦然。语言语义可以独立重大变化 → 必须独立成包。

## DEPENDS ON

- `session-ontology`：语言是 **session** 级事实（`SessionProviderLanguage`），绑定与
  继承发生在 session 创建时；session 的存在与归属是前提（`requirements-design/INDEX.md`
  依赖骨架唯一来源）。一个理由：没有 session 概念，「session 创建绑定不可变」无处落点。
