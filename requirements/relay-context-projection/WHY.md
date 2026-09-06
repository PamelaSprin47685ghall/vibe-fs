# relay-context-projection — WHY

同一物理 session 需要为用户保留连续会话，又不能把退休迭代的原始消息、suicide 和 provider 私有失败继续喂给下一迭代。删除物理消息会破坏审计；保留全部 provider history 又会让逻辑迭代并未真正重开。Loop 的答案是三种 projection 分离：audit 保留一切，provider 只保留当前权威事实与当前迭代消息，wake 自身不构成需求。每一轮迭代都从相同的权威用户消息与当前工作区出发独立判断，不继承前任的私有上下文。
