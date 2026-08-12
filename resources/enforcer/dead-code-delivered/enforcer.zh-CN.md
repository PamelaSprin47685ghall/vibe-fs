# dead-code-delivered — Enforcer 中文版

## 定义
Dead code delivered 指 production source 仍可编译、仍像系统一部分，却已经没有 caller、activation contract 或可达行为。它让 working tree 同时陈述“这是系统”和“这些已废弃路径也许仍重要”。

## 何时触发
- 最后 caller 已删，helper/module 仍留着；
- superseded implementation 仍可被 import；
- 永远不可能满足的 branch 继续存在；
- alias/entry point 没有现役 consumer；
- 搜索与工具持续把 abandoned path 当 live surface。

## 不要误判
- off-by-default extension point 有明确 activation contract、owner 与 tests；
- bounded compatibility surface 有具体 consumer 与退役条件；
- live feature flag 仍在有效 rollout 中；
- test fixture 被 suite 使用，不是 production dead code。

## 刀口
要求这段 code 出示“生命证明”：谁调用、什么条件激活、哪条 contract 承诺它存在？没有具体答案，就不该靠“也许以后有用”继续占据现在。

## 提醒
Version control 负责可能的过去与未来；working tree 负责现在。没有 present role 的 production code 应被删除，而不是被年龄赋予合法性。
