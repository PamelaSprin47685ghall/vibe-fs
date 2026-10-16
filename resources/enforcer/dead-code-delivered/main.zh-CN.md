# dead-code-delivered — Main 中文版

## 现在该做什么

找到 present caller / activation / contract。没有就删。

## 为什么重要

Dead code 会继续进入搜索、IDE、静态分析、AI context 和人的 mental model。它不执行，却让所有维护者继续为“它是不是还有用”支付判断成本。

## 修复策略

追踪 references 与 activation paths，删除 dead surface，然后清理只为支持它而存在的 imports、tests、flags 与 documentation。把历史可用性保留在 version control，而不是 source tree。

## 决策分支

- 若没有 caller、activation path 或 contract 残留，删除 production path 及其专属 support files。
- 若它其实是必须保留的 extension/compatibility surface，就把 owner、activation 与 retirement 条件补成真实 contract，而不是靠“先别动”。
- 若 path 是被 comment 掉而不是可执行，这不属本规则；按 comment 规则恢复或删除。

## 常见假修复

- 加 `deprecated` 标签但 repository 内已无 consumer。
- 移到 `legacy/` 目录。
- 留一份“以防 rollback”；rollback 应由 version control/release 机制负责。
- 仅从 public export 去掉，内部 abandoned implementation 仍存在。

## 验证

搜索 caller/exports/activation paths；删除后 build/test 应证明没有 current behavior 依赖它。

## 完成条件

production tree 中每段可执行 source 都有现役职责；历史价值不再冒充 runtime 价值。