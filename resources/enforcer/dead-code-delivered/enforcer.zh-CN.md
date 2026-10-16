# dead-code-delivered — Enforcer 中文版

## 定义

Dead code delivered 指 production source 仍可编译、仍像系统一部分，却已经没有 caller、activation contract 或可达行为。它让 working tree 同时陈述“这是系统”和“这些已废弃路径也许仍重要”。

## 支配原则

Version control 是 archive；working tree 是“这就是系统”的陈述。Dead code 通过保留不再参与行为的备选路径削弱这个陈述：读者仍必须推理它们，工具仍索引它们，未来维护者无法判断该路径是有意休眠还是只是被遗忘。

## 何时触发

- 最后 caller 已删，helper/module 仍留着；
- superseded implementation 仍可被 import；
- 永远不可能满足的 branch 继续存在；
- alias/entry point 没有现役 consumer；
- 搜索与工具持续把 abandoned path 当 live surface。

## 不应触发

- off-by-default extension point 有明确 activation contract、owner 与 tests；
- bounded compatibility surface 有具体 consumer 与退役条件；
- live feature flag 仍在有效 rollout 中；
- test fixture 被 suite 使用，不是 production dead code。

## 与相邻规则区分

`commented-out-code` 把旧代码存在 comment 里；`legacy-cruft-retained` 保留过期的 compatibility surface。本规则管的是不再属于任何当前行为的可执行 source。Tie-break：如果这段 source 仍可编译、可能运行，却没有 present caller 或 contract，这个案子归本规则。

## 判定程序

要求这段 code 出示“生命证明”：谁调用、什么条件激活、哪条 contract 承诺它存在？没有具体答案，就不该靠“也许以后有用”继续占据现在。

## 例子

- positive：production helper 在最后一个 caller 被删后仍留着；搜索仍把它当作 live surface 呈现。
- near-miss：plugin hook 有显式 owner、activation test 与文档化的 off-by-default contract。
- counterexample：删除不可达路径，让 version control 保留历史。

## Nudge

Version control 负责可能的过去与未来；working tree 负责现在。没有 present role 的 production code 应被删除，而不是被年龄赋予合法性。