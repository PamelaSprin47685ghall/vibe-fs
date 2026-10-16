# dead-code-delivered — Lesson

## 评价

### 定义

Dead code delivered 指 production source 仍可编译、仍像系统一部分，却已经没有 caller、activation contract 或可达行为。它让 working tree 同时陈述“这是系统”和“这些已废弃路径也许仍重要”。

### 支配原则

Version control 是 archive；working tree 是“这就是系统”的陈述。Dead code 通过保留不再参与行为的备选路径削弱这个陈述：读者仍必须推理它们，工具仍索引它们，未来维护者无法判断该路径是有意休眠还是只是被遗忘。

### 何时触发

- 最后 caller 已删，helper/module 仍留着；
- superseded implementation 仍可被 import；
- 永远不可能满足的 branch 继续存在；
- alias/entry point 没有现役 consumer；
- 搜索与工具持续把 abandoned path 当 live surface。

### 不应触发

- off-by-default extension point 有明确 activation contract、owner 与 tests；
- bounded compatibility surface 有具体 consumer 与退役条件；
- live feature flag 仍在有效 rollout 中；
- test fixture 被 suite 使用，不是 production dead code。

### 与相邻规则区分

`commented-out-code` 把旧代码存在 comment 里；`legacy-cruft-retained` 保留过期的 compatibility surface。本规则管的是不再属于任何当前行为的可执行 source。Tie-break：如果这段 source 仍可编译、可能运行，却没有 present caller 或 contract，这个案子归本规则。

### 判定程序

要求这段 code 出示“生命证明”：谁调用、什么条件激活、哪条 contract 承诺它存在？没有具体答案，就不该靠“也许以后有用”继续占据现在。

### 例子

- positive：production helper 在最后一个 caller 被删后仍留着；搜索仍把它当作 live surface 呈现。
- near-miss：plugin hook 有显式 owner、activation test 与文档化的 off-by-default contract。
- counterexample：删除不可达路径，让 version control 保留历史。

## 建议

### 现在该做什么

找到 present caller / activation / contract。没有就删。

### 为什么重要

Dead code 会继续进入搜索、IDE、静态分析、AI context 和人的 mental model。它不执行，却让所有维护者继续为“它是不是还有用”支付判断成本。

### 修复策略

追踪 references 与 activation paths，删除 dead surface，然后清理只为支持它而存在的 imports、tests、flags 与 documentation。把历史可用性保留在 version control，而不是 source tree。

### 决策分支

- 若没有 caller、activation path 或 contract 残留，删除 production path 及其专属 support files。
- 若它其实是必须保留的 extension/compatibility surface，就把 owner、activation 与 retirement 条件补成真实 contract，而不是靠“先别动”。
- 若 path 是被 comment 掉而不是可执行，这不属本规则；按 comment 规则恢复或删除。

### 常见假修复

- 加 `deprecated` 标签但 repository 内已无 consumer。
- 移到 `legacy/` 目录。
- 留一份“以防 rollback”；rollback 应由 version control/release 机制负责。
- 仅从 public export 去掉，内部 abandoned implementation 仍存在。

### 验证

搜索 caller/exports/activation paths；删除后 build/test 应证明没有 current behavior 依赖它。

### 完成条件

production tree 中每段可执行 source 都有现役职责；历史价值不再冒充 runtime 价值。

### Nudge

Version control 负责可能的过去与未来；working tree 负责现在。没有 present role 的 production code 应被删除，而不是被年龄赋予合法性。
