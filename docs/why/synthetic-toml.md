# 合成 TOML — 理由

运行时往 LLM 上下文塞 continuation、delta、tool 文本、guard 时，若继续用裸英语，模型必须在文本层猜：这是人类、Host 指令、工具输出还是历史引用。混写 instruction/data 时，视觉边界与机械检验同时失败。

选 TOML **不是**为了系统 parse 业务：是因为 comment/field 对模型有稳定视觉语义，且可被 parser 机械检验「这是合法文档」。

不统一 envelope：强制 schema/kind/origin 会造假外壳，并把 authority 偷运进文本。局部最小 schema + 全局记法公理，比第二套 envelope 协议更短。

不反向解析：字符串反推控制流会主动丢掉类型（ARCH-011）。表示层只投影。

## 备选与被拒

**载体：TOML comment/field vs 裸英语。** 拒裸英语：模型须在文本层猜「人类/Host 指令/工具输出/历史」；混写 instruction/data 时视觉边界与机械检验同时失败。TOML 的 comment/field 对模型稳定且可 parse 校验（ARCH-010）——选它不是为系统 parse 业务。

**envelope：统一 schema vs 局部最小 schema + 全局记法公理。** 拒统一：强制 schema/kind/origin 造假外壳、偷运 authority；比第二套协议更短的是局部最小 + 公理（ARCH-011）。

**解析方向：反解 authority/origin vs 只投影。** 拒反向解析：字符串反推控制流丢类型（ARCH-011）。

**字符串：literal-first + basic fallback。** 普通多行正文用三单引号逐字承载；正文自身含 `'''` 或非法控制字符时，literal string 无合法表示，只能由同一 owner 回退到 canonical basic escape。拒绝用 `"""` 建第二套 multiline 方言。instruction 只认最前连续 comments（`Scope` containment）。
