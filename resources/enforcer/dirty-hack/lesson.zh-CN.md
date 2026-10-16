# dirty-hack — Lesson

## 评价

### 定义

Dirty hack 是明知 governing model/invariant 在这里是错的，却加一个局部 bypass 让当前路径继续工作。它不是“代码丑”，而是系统开始同时维护两套 specification：公开模型，以及只有 workaround 知道的真实例外。

### 支配原则

每个 special case 都是关于现实的声明。如果例外没有 domain meaning、只因为当前 abstraction 表达不了真相，代码库就获得两套模型：正式的那套，以及知道它在哪儿失效的 workaround。重复这个过程不会让系统稳定，只会把真正的 specification 分散到各个 escape hatch。

### 何时触发

- magic ID/path/flag 绕过正常 ownership；
- special-case branch 只因为现有 abstraction 表达不了真实情况；
- fallback/shim 让已知错误模型继续活着；
- exception 没有 domain 名字、contract、owner，唯一解释是“否则这个 case 跑不通”。

### 不应触发

- 现实本身确实有稳定 domain exception，且有名字、测试、规则；
- foreign protocol 在 adapter 做必要翻译；
- 明确 time-boxed spike 不进入 shipping path；
- bounded compatibility branch 有真实外部 creditor。

### 与相邻规则区分

`compatibility-cruft` 保留没有正当理由的历史 surface；`facade-hides-mess` 掩盖大范围架构债。本规则是一个局部 patch，其唯一语义是“让坏掉的模型在这里活下来”。Tie-break：如果 special case 存在是因为 invariant 在此处已知为 false，这个案子归本规则。

### 判定程序

问 special case：“哪条领域事实让你合法？”如果答案不是领域事实，而是“我们的模型/架构这里坏了”，就不要给 workaround 永久身份。

### 例子

- positive：额外的 `if (id === "legacy-user")` 绕过 ownership model，让一条路径继续工作。
- near-miss：有名字、有测试的 `InsufficientBalance` domain case，因为业务本身就包含这个拒绝。
- counterexample：修复 invariant 或 ownership boundary，然后删除 bypass。

## 建议

### 现在该做什么

追到 hack 所绕过的 invariant/owner，修 canonical model，使这个 case 能通过正常路径表达；然后删除 bypass。若它其实是真实 domain exception，就给它正式名字、类型、测试与 authority，不再叫 workaround。

### 为什么重要

Hack 的危险在扩散：下一位维护者看到它，会以为“这里就是特殊”，再为邻近 case 加第二个例外。最终真实规则散在一串 `if special` 中，官方 abstraction 只剩宣传价值。

### 修复策略

把症状追到第一条被违反的 invariant。在那里改变 representation 或 owner，迁移 callers，然后删除 bypass，而不是把它当 fallback insurance 保留。

### 决策分支

- 若 special case 没有 domain fact，修复坏掉的 invariant/ownership，删除 bypass。
- 若现实本身包含该例外，用 domain language 给它名字、ownership 与测试，而不是一个秘密 `if`。
- 若 facade 掩盖了大量此类 patch，先修结构；concealment 是另一条规则。

### 常见假修复

- 给 hack 加更详细 comment。
- 把 magic branch 移进 `compatibility`/`utils` 隐藏。
- 再加一层 facade，让 caller 看不见 bypass。
- 为了尽快绿，复制一条 alternate path。

### 验证

删除 special case 后，目标 scenario 应仍通过 canonical invariant/model；若删不得，就说明根修复尚未完成或它其实应被正式建模为 domain exception。

### 完成条件

系统只有一套可解释的模型；没有局部代码在暗中承认 canonical invariant 其实不成立。

### Nudge

没有 domain meaning 的 workaround 是一份秘密模型。每多一个 hack，真正 specification 就更远离 canonical structure。
