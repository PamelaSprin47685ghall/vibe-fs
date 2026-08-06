# Host — 理由

碎片事件拼真相会把因果绑在传输噪声上；粗粒度唤醒 + SDK snapshot 把真相源固定在完整消息。

Compaction 预防+收容分层：预防依赖上游键名会漂，收容只认 transcript 事实，故收容是主防线。关掉配置单独不算已证明——必须首轮探测。

Transform input 为空对象是 Host 能力现实；绑定必须用「已创建未完成 assistant」因果读，不能猜。Canary 用 journal 代理等式，因为 transform 内存 id 与 ToolContext 不共盘。

多实例按 directory 分叉时，跨实例共享的只能是身份注册表，不能是 Journal writer——实测第二实例读不到主实例 verdict 注册即来自此边界。

## 备选与被拒

**真相源：碎片事件积分 vs 粗粒度唤醒 + snapshot。** 拒碎片积分：流式碎片顺序/形状随 Host 版本漂，把因果绑在传输噪声上（ARCH-002）。醒后读完整 SDK snapshot 固定真相源。

**Compaction 收容：配置关闭 vs 运行时探测 + 收容。** 拒「只关配置」：上游键名可漂，预防层不可单独证明（HOST-006 必须首轮伪-run 为零，否则启动失败）。收容层把任何观察到的 compaction 转 `ContextReanchored`，是主防线——只认 transcript 事实。

**ProviderRunIdentity 绑定：唯一未完成 assistant 因果读 vs same-root 猜测。** 拒猜测：Host 重排消息时假绿。选因果读（role=assistant、completed 未设、parentID 匹配、id 最大，命中 0/≥2 放弃写 seal → fail closed）。宁可放弃 seal，不赌唯一胜出（REVIEW-003 双 PERFECT 依赖此边界）。

**跨实例：Journal 共享 writer vs 身份注册表共享。** 拒共享 writer：实测第二实例读不到主实例 verdict；只共享不可变身份注册表，避免折叠写盘。
