# Host — 理由

碎片事件拼真相会把因果绑在传输噪声上；粗粒度唤醒 + SDK snapshot 把真相源固定在完整消息。

Compaction 预防+收容分层：预防依赖上游键名会漂，收容只认 transcript 事实，故收容是主防线。关掉配置单独不算已证明——必须首轮探测。

Transform input 为空对象是 Host 能力现实；绑定必须用「已创建未完成 assistant」因果读，不能猜。Canary 用 journal 代理等式，因为 transform 内存 id 与 ToolContext 不共盘。

多实例按 directory 分叉时，跨实例共享的只能是身份注册表，不能是 Journal writer——实测第二实例读不到主实例 verdict 注册即来自此边界。
