# missing-architecture-gate — Enforcer 中文版

## 定义
关键 architecture rule 若机器可以廉价、确定地判断，却长期只靠 reviewer 记住，就是 missing architecture gate。问题不是“CI 规则越多越好”，而是**本可机械拒绝的 forbidden state 被委托给未来注意力**。

## 何时触发
- forbidden import/layer edge 每次靠人工 review 抓；
- ownership/generated-file rule 有精确 structural predicate，却没有 standard check；
- 同一违规反复出现，原因只是局部写法很方便；
- architecture statement 已稳定，但 repository 对违反保持沉默。

## 不要误判
- 规则本质是高语义判断，静态扫描会大量 false positive；
- 一次性 review 意见还没成为 standing invariant；
- build/type system 已经机械禁止；
- 纯风格偏好无架构后果。

## 刀口
能否写出一个便宜、deterministic、low-noise 的 predicate 来识别违反？能，而 merge pipeline 不执行它，就是把机器能做的工作留给记忆。

## 提醒
Architecture 最脆弱的地方恰好是违规通常“局部更方便”。能让机器拒绝的边界，不要要求所有未来人永远自律。
