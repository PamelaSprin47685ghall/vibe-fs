# 架构 DNA — 理由

三条 DNA 解决的是同一类失败：在业务层复制运行时、从不可靠碎片拼真相、以及绑定不可升级的 Host fork。

**结构化程序（ARCH-001）。** Stage/Phase 字段描述的是“下一步去哪”，那是语言运行时的职责。把程序计数器固化为领域状态后，恢复变成“恢复协程”，测试变成“断言枚举序数”，复杂度以假领域概念增殖。

**事件是信号（ARCH-002）。** 流式碎片的顺序与形状随 Host 版本漂移。业务层若依赖它们，等于把因果建立在传输噪声上。粗粒度唤醒 + SDK snapshot 把真相源固定在完整消息上。

**不改 Host（ARCH-003）。** 改本体换取便利，用升级断裂与维护分叉偿还。现有 Hook/SDK 不够时，先证明能力缺口，再谈产品边界，而不是默默依赖未公开 API。

**前缀缓存（ARCH-004）。** 冷边界必须可预期且由事实驱动；否则 KV-cache 收益被随机重写前缀吞掉，且与 CTX 的“失败驱动恢复”冲突。

**状态先于表示（ARCH-011）。** 字符串反推控制流是主动丢弃类型。表示层只投影，不证明身份。

**Provider Horizon。** Horizon 无状态机、无 UUID。机器可持有全部相关态；参与者只需「发生了什么 + 下一步可做什么」。把 `status/code/error` DTO、AgentId/SessionId/RunId、cursor/offset、spool 元数据塞进 tool surface，等于逼模型解码 discriminated union 与相关 ID，而不是生活在后果里。错误属于机器；后果属于经验。已删文件的 `spool_path` 是虚假 affordance——路径指向不存在之物，却假装可再打开。

## 备选与被拒

**程序表达：结构化程序 vs 状态机字段。** 拒 `Stage/Phase/Lease` 当程序计数器：那是运行时职责；固化后恢复=恢复协程、测试=断言枚举序数，复杂度以假领域概念增殖（ARCH-001）。这字段描述的若是「程序下一步去哪」而非物理世界真实事物，必删。

**真相源：碎片事件 vs SDK snapshot。** 拒碎片积分：顺序/形状随 Host 漂（ARCH-002）。只 `status=idle/retry`、`deleted` 进业务层，其余在最早边界丢弃。

**Host 约束：只改 hook vs 改本体。** 拒改本体：升级断裂与维护分叉（ARCH-003）。能力不足先读源码证明缺口，不依赖未公开 API。

**前缀：失败驱动冷边界 vs 估算压缩。** 拒估算：吞 KV-cache 收益且违背 CTX（ARCH-004）。边界由已提交事实驱动（epoch/probe/reanchor）。

**Provider surface：状态机/UUID/DTO vs 后果叙述。** 拒把内部拓扑投影成 `status/code/message/error` 或 UUID 相关字段：LLM 被迫当 union decoder，下一步行动不因字段取值改变时仍在烧注意力。拒 `spool_path` 指向已删 spool：不可达路径不是测量，是谎言。选「已发生的事实 + 可行动后果」；机器态、相关 ID、dedup 标志、cursor 全部 behind horizon。