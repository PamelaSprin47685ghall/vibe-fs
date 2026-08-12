# premature-unification — Main 中文版

## 现在该做什么
按 lifecycle / invariant / reason-to-change 拆回独立 owners。允许少量重复存在，观察真正重复变化的知识；只抽取“以后必须一起变”的那一部分，而不是整块相似 shape。

## 为什么这很重要
Premature abstraction 的第一笔利息就是 flags 与 optionals：现实开始分叉，抽象却坚持它们“其实一样”。每个 escape hatch 都在证明最初统一的 claim 是假的。

## 常见假修复
- 一个 generic type + `mode/context`。
- mega-helper 接十个 optional callbacks。
- 因为“拆开会重复”拒绝恢复独立 ownership。
- 把两个独立 model copy 到不同文件但仍共享同一个 generic base 约束所有演化。

## 验证
只改变一个 concept 的 domain requirement，另一个 concept 不应需要 conditional branch、dummy field 或无关修改。真正 shared law 若改变，则所有 consumers 应自然同步。

## 完成条件
Shared abstractions 对应 shared knowledge；独立概念可以相似，而不被相似强迫成一个 owner。
