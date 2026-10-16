# todo-bomb — Main 中文版

## 现在该做什么

对 reachable required behavior 二选一：完整实现，或在真正 boundary 明确把该 case 标成 unsupported 并返回 typed refusal。不要让 placeholder 藏在深层路径等生产流量发现。

## 为什么重要

TODO bomb 把 known defect 伪装成 scheduling decision。系统表面上说“支持”，内部却写着“以后再说”，于是 caller 没有机会提前理解限制。

## 修复策略

追查 placeholder 是否可从 supported inputs 到达。实现真实 behavior 并测试它，或如果产品有意不支持该 case，把 rejection 移到 public boundary，返回 typed unsupported outcome。

## 决策分支

- 若 supported input 能到达 placeholder，实现 behavior，或在 public boundary 拒绝该 case。
- 若 note 是不支持 path 上的可选未来工作，把它留作 backlog，并保持不可达。

## 常见假修复

- 把 TODO 改成 warning log 后继续返回 dummy success。
- catch `NotImplemented` 并当 generic failure。
- 在 README 写“此 case 暂不完整”，但 API 仍 accept。
- 给 placeholder 加默认值，让它“不崩”。

## 验证

枚举 supported input space；任何有效输入都不应进入 placeholder/panic/dummy branch。明确 unsupported 的输入必须在 owner boundary 稳定拒绝。

## 完成条件

当前 contract 不依赖未来工作才能为真；future TODO 只描述 enhancement，不掩盖已知的 shipped correctness hole。