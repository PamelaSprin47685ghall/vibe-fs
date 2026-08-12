# runtime-checked-builder — Main

## What To Do Now
把纯仪式性的 mutable construction 收缩成一个 atomic constructor，或一个类型上只暴露合法下一步的 staged API。已经知道的 required fact 直接要求；真正只能 runtime 判断的约束继续保留 runtime validation。

Construction boundary 才是 owner。晚到的 `validate()` 不应该替一个主动制造 invalid intermediate state 的 API 擦屁股。

## Why This Matters
如果 incomplete builder 本身没有业务意义，它就是 API 虚构出来的临时世界。caller 因此承担一串本可消失的错误：忘 setter、重复 setter、顺序错、failed build 后复用、矛盾字段先堆起来再统一爆炸。

测试也会被迫围绕这些“API 自己邀请的错误”增长。

目标绝不是“零 runtime validation”。那是另一种类型系统迷信。目标是只把**现实确实无法提前知道**的事实留在 runtime，而把“caller 有没有按顺序把已知字段填完”这种程序仪式从状态空间里删除。

## Repair Strategy
1. 给每个 builder field 标记：required / optional-with-default / real-stage-dependent / dynamic-validation。
2. required data 变 constructor/function 参数。
3. 真实语义阶段用 explicit state/type，只允许合法 transition。
4. 真 optional 明确 default，不要伪装成“忘了也行”。
5. dynamic constraint 留在一个 constructor/result 边界。
6. unavoidable mutable accumulator 必须 private、不能逃逸。
7. API 能表达顺序后，删除“请按 A→B→C 调用”的文档仪式。

## Decision Branches
- 所有 required data 同时可得：优先一个 constructor/function。
- 事实确实在不同 semantic stage 才到达：用 staged type / explicit state，不用 maybe-valid bag。
- constraint 依赖 runtime fact：老老实实返回 typed validation failure，不要硬装成静态可证明。
- intermediate object 本身就是业务对象（如 Draft）：诚实叫 Draft，不要叫“半成品 Final”。

## Common Wrong Fixes
- 给原 builder 再加更多 `isValid`。
- 每个 setter 都早一点 throw，但 invalid public state space 完全没变。
- build 后 freeze object，却仍允许任意 incomplete instance 公开存在。
- 只在 docs/comment 规定 method order。
- 用极端复杂的 phantom type 让 construction 比原问题更难理解。类型必须买来 clarity，不是 ceremony。

## Verification
尝试漏 required fact、错序、重复互斥 stage、复用 failed state。公共 API 应让这些错误不可表达，或在唯一 honest dynamic boundary、domain value 逃逸前拒绝。

再专门构造一个**真正 dynamic invalid value**，证明 runtime validation 没有被类型教条误删。

Invariant：**不存在仅仅因为 caller 还没做完 API 仪式而 incomplete 的 escaped object。**

## Done When
Required construction fact 明确；真实 stage 被真实建模；动态验证保留在需要之处；caller 不再依赖 procedural memory 才能构造合法值。