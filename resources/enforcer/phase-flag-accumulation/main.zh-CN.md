# phase-flag-accumulation — Main

把 flag product 还原成真实 lifecycle。

先列出合法 phase 与 transition，不要从现有 booleans 出发。对每个 phase 标明只在该状态有意义的数据，然后用 closed union/enum/state-specific record 或明确局部 control scope 表达。

例如不要继续维护：

```text
started=true
waiting=false
retrying=true
done=false
```

而是让 world 直接说：

```text
Retrying { attempt; lastFailure }
```

这样 transition 也从 scattered assignment 变成 `Running → Retrying → Running/Failed`，illegal combination 直接消失。

常见假修复：

- 新增一个 `phase` enum，但旧 flags “为了兼容”全部保留并继续可写；这会立刻变 `duplicated-truth`；
- 每个 contradictory combination 加 assertion，却不缩小 representable state；
- 用 bitmask 代替 booleans，组合数量一点没少；
- 建一个巨大 enum，里面同时塞真正 lifecycle 与几个独立 capability/preferences；
- transition method 内仍逐个改 flags，最后再算出 phase；
- 为 recovery 保存更多 helper flags，越来越接近 program counter。

验证时枚举旧 representation 曾经允许但 domain 不允许的组合，它们应该已经无法构造。每个合法 transition 也应该有一个可读入口与明确 predecessor/successor，而不是任意 state 之间都能 set。

若某些 flags 真正独立，就从 lifecycle model 里剥离，继续作为独立 predicate/capability。不要为了“统一状态”破坏本来合法的组合自由。

Migration 时避免新旧状态双写永久化。可以在 ingress 一次把 legacy flags decode 成新 state；新 writes 只写新 model，旧形状若必须支持历史数据就保持 decode-only 并有删除条件。

完成时 representation state space 与真实 lifecycle state space一致；读代码不需要重新计算 truth table，增加一个新 phase 会迫使 transition/pattern match 显式处理。

> State type 应告诉你世界可能在哪，不应给你一盒 bits 再要求每个 reader 自己拼答案。