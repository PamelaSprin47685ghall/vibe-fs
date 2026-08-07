# 合成 TOML — 适用面

Synthetic TOML 的行为由 ARCH-010、ARCH-011 和 ARCH-012 唯一定义。本文件只把这些条款
路由到本主题的 surface；不重述 comment/field、顺序、单向表示或大小边界。

## 纳入 surface

对下列运行时构造且供 LLM 读取的文本，应用 ARCH-010：continuation、repair、guard、
nudge、review challenge、companion memory、Blogger delta、executor summary、tool 文本结果、
join/fork 合成外壳。

## 排除 surface

- 原生 system/developer/角色 Prompt；
- 未经包装的人类原始消息；
- 不进入模型的内部数据；
- provider/tool 原生 binding 结构。

这些排除只划定 ARCH-010 的适用面，不授予从文本推断 Authority 的例外；身份边界仍见
PROMPT-001 与 ARCH-011。

## 领域交叉入口

- Blogger delta 的字节合同：CTX-013
- Join/LWR wire：EXEC-004
- tool 文本结果上限：ARCH-012
- 所有权与可信 renderer：`shape/synthetic-toml.md`
