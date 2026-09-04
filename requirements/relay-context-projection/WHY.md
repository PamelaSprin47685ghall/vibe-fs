# relay-context-projection — WHY

同一 OpenCode SessionId 需要为用户保留连续会话，又不能把退休 Manager 的原始消息、suicide 和 provider 私有失败继续喂给 successor。删除物理消息会破坏审计；保留全部 provider history 又会让逻辑任期并未真正重开。必须把 audit、provider 与 user narrative 三种 projection 分离。
