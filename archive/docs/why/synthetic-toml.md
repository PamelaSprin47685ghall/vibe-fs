# 合成 TOML — 理由

本主题选择一种可机械检查的 LLM 可见表示，同时保持类型化 Authority 与文本表示分离。
具体取舍集中在下列备选比较中。

## 备选与被拒

**载体：TOML comment/field vs 裸英语。** 拒裸英语：模型须在文本层猜「人类/Host 指令/工具输出/历史」；混写 instruction/data 时视觉边界与机械检验同时失败。TOML 的 comment/field 对模型稳定且可 parse 校验（ARCH-010）——选它不是为系统 parse 业务。

**envelope：统一 schema vs 局部最小 schema + 全局记法公理。** 拒统一：强制 schema/kind/origin 造假外壳、偷运 authority；比第二套协议更短的是局部最小 + 公理（ARCH-011）。

**解析方向：反解 authority/origin vs 只投影。** 拒反向解析：字符串反推控制流丢类型（ARCH-011）。

**字符串：literal-first + basic fallback。** 普通多行正文用三单引号逐字承载；正文自身含 `'''` 或非法控制字符时，literal string 无合法表示，只能由同一 owner 回退到 canonical basic escape。拒绝用 `"""` 建第二套 multiline 方言。instruction 只认最前连续 comments（`Scope` containment）。

**codec：只有字符串 vs 同一 owner 的值树。** 拒「第二表面把 JSON 当字符串塞进 field」。bool / int / float / inline array / table / array-of-tables 由 `SyntheticToml` 一次编完；字符串选择规则不变。Blogger 等可以继续只用字符串字段，不强迫改 local schema。
