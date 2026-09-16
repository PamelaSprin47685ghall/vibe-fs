# relay-context-projection — WHY

同一物理 session 需要为用户保留连续会话，又不能把退休迭代的原始消息、suicide 和 provider 私有失败继续喂给下一迭代。删除物理消息会破坏审计；保留全部 provider history 又会让逻辑迭代并未真正重开。Loop 的答案是三种 projection 分离：audit 保留一切，provider 只保留当前权威事实与当前迭代消息，wake 自身不构成需求。每一轮迭代都从相同的权威用户消息与当前工作区出发独立判断，不继承前任的私有上下文。

消除 Manager 分身后，固定 DevOps 的执行成果通过客观的工作区文件、测试产物与显式工作记录对后继迭代可见，而不是通过污染 provider 上下文注入前任 Manager 的多路私有提示词。这保证了下一任 Manager 能够不受历史噪音干扰地执行独立评估与派工。
