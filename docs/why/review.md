# Review — 理由

单次 PERFECT 可被模型随口同意。双 PERFECT + seal 证明第二次输入里真的含有 skeptical challenge，把确认从口头变成因果消费证据。

Witness 必须自包含：Guard 若依赖外围 Map，恢复与并发 Job 会静默读到别人的确认或空确认。tree 变化作废 witness，因为审的是代码状态，不是 Session 情绪。

Seal 绑定失败 fail closed，禁止 same-root 猜测：猜测在 Host 重排消息时会假绿。

## 备选与被拒

**确认强度：单 PERFECT vs 双 PERFECT + seal。** 拒单 PERFECT：可被模型随口同意。挑 challenge + seal 证明第二次输入真含 skeptical challenge，把确认变成因果消费证据（REVIEW-003）。

**Witness 载体：自包含 vs 外围 Map。** 拒外围 Map：恢复/并发 Job 会静默读到别人的确认或空确认（REVIEW-006）。witness 自带全部证据。

**作废：tree 变化作废 vs 旧确认坚持。** 拒旧确认：审的是代码状态不是 Session 情绪；tree 变即 witness 失效，保证结论绑定被审对象（REVIEW-006）。

**绑定：唯一绑定 + fail closed vs same-root 猜测。** 拒猜测：Host 重排消息时假绿。沿用 HOST-010 因果读，命中 0/≥2 即放弃写 seal，宁可无 seal 不赌。
