# 合成 TOML — 可观察行为

主条款定义在 **ARCH-010**（`what/architecture.md`）。  
本文件陈述纳入范围与不变量；不重写 ARCH-010。  
所有权见 `shape/synthetic-toml.md`；可执行记法与迁移见 `how/synthetic-toml.md`；证明见 `proof/synthetic-toml.md`。

## 纳入（合取）

文本 payload 同时满足下列四条时，必须使用 Synthetic TOML 形态：

1. 最终由 LLM 按文本 token 阅读  
2. 不是原生 system / developer prompt  
3. 不是未经重新包装的人类原始消息  
4. 由运行时、Host、插件、工具、Agent 协作层或 projection 构造、包装、复制或重投影  

典型 surface：continuation、repair、guard、nudge、review challenge、companion memory、Blogger delta、executor summary、tool 文本结果、join/fork 合成外壳等。

## 核心不变量

```text
instruction = 顶层 comment，且永远在 data 之前
data        = field / table / 表数组 / value
```

- instruction-only、data-only、instruction+data 三种形态均合法。  
- 不得为“凑格式”伪造空 data 或无意义 instruction。  
- 不建立统一 envelope（禁止强制 schema/kind/origin/authority 外壳）。  
- 该文本**只供 LLM 阅读**，永不反向解析为 origin / authority / 控制流（ARCH-011、PROMPT-001）。  

## 明确排除

| 排除 | 理由 |
|------|------|
| system / developer / 角色 prompt assets | 原生 instruction 通道，不 TOML 化 |
| 人类原始消息（未包装） | 不改写用户原文 |
| 不进入模型的内部数据 | 非 LLM-visible |
| provider/tool 原生 binding 结构 | 不改变 Host 绑定协议 |

## 与其它条款

- 身份与 Authority：PROMPT-001 / ARCH-011  
- Blogger delta 字节合同：CTX-013（200 KiB 计量点 = 渲染后 UTF-8，含可选 instruction header）  
- Join/LWR wire：EXEC-004 + `how/synthetic-toml.md` §9.6  
