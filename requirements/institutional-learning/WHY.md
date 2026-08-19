# institutional-learning — 为什么必须独立存在

## 1. 一个不可替代的存在理由

现有 Enforcer 已能把规则变成 Blogger 的检测语料，并把 diagnosis 送回 Main；真正缺的一环是规则从哪里出生。如果经验只留在一次会话记忆里，组织没有学习；如果每次经历都直接新增一条规则，Rulebook 又会长成 institutional scar tissue。

本包把一次经历压成两个显式 speech acts：

```text
celebrate(experience)  这次为什么做对了？把偶然成功变成可复制能力
regret(experience)     这次为什么付了不必要代价？把痛苦变成组织免疫
```

两者都触发一个私有 Enhancer。Enhancer 不是日志转规则器，而是压缩器：寻找“比这次经历更一般、未来还能再次识别的机制”，然后只做 `ABSORB / BIRTH / DISCARD` 三选一。于是 Enforcer 可以从系统自己的经历中长出制度，但经验并不必然增加规则。

`celebrate` 还有一个刻意的 attention closure：学习处理完成后，最后再把本 participant 之前 `defer` 的旁支弹出来。这样“压住旁支”不等于“永远忘掉”，又不会把旁支提前变成 mission debt。

## 2. 为什么不并入其它包

- 不并入 `behavior-diagnosis`：后者判断一条既有规则何时成立；本包决定经验是否值得改变规则集。
- 不并入 `guidance-delivery`：后者投递已有规则；本包拥有 absorb/birth/discard 的学习裁决。
- 不并入 `knowledge-reuse`：Casebook 是 repository answer cache；这里学习的是可泛化行为机制，不是 Q/A。
- 不并入 `cognitive-environment`：Role Law/Pair Hint 是稳定 authored environment；本包是运行中 experience → institution 的 evolution boundary。

## 3. FAILURE MEANING

RED = 成功经验永远不沉淀，只从失败学习；每次 celebrate/regret 都机械新增 rule；一次偶然事件永久征税；Enhancer 复制已有规则而不吸收；新规则绕开 behavior-diagnosis 的 trigger/negative/distinction 语义；Enhancer 自己递归学习自己的输出；celebrate 在 deferred reminder 前就返回或自动执行 deferred work。

## 4. 被拒方案

- 每次 experience → new rule：规则只增不减，必然 prompt inflation。
- reward model / score：把可读制度变成隐式数值目标。
- knowledge graph / memory database：无法替代未来 Pair Hint/Enforcer 真正消费的规则。
- runtime 直接改 shipped `resources/enforcer` / npm 安装目录：安装介质未必可写、升级会覆盖、还会把 workspace 局部经验错误扩散成全局产品规则；institutional rule 必须走 `behavior-diagnosis` 的 durable workspace admission。
- Enhancer 暴露成 provider 新工具：使用者只需要 `celebrate/regret` 两个 act，不需要管理 triage workflow。
- 只做 `regret`：规则库会退化成禁止事项大全，错过无事故的高价值成功机制。

## DEPENDS ON

- `attention-regulation`：celebrate 尾部 resurfacing DeferredWork 的语义 owner。
- `behavior-diagnosis`：Enforcer canonical Rulebook 身份、检测合同与规则有效性是学习结果的消费边界。
