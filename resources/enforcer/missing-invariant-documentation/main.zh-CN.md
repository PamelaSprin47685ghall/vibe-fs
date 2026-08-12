# missing-invariant-documentation — Main 中文版

## 现在该做什么
在 semantic owner 处记录一条精确、可证伪的 invariant；能由 type/construction/test/gate 表达的部分继续机械化，但不要复制成多份 prose。

## 为什么这很重要
隐藏 invariant 会让每代维护者重新交学费。代码可能暂时一直遵守它，却没人知道哪些 shape 是必要、哪些只是偶然；一次看似合理的 cleanup 就能删掉真正的保护机制。

## 常见假修复
- 到处 comment 同一句 rule。
- 写“注意顺序”“保持一致”这种无法 falsify 的提醒。
- 新建大文档却不放在概念 owner 附近。
- 类型已经完整表达后继续复制长 prose，制造第二 truth source。

## 验证
一个没参与原讨论的人，从 owning concept 能找到 rule，能说出什么 observation 会证明它被破坏，也能找到已有 mechanical protection。

## 完成条件
关键 correctness 不依赖口头传统；rule 一处定义、owner 清楚、可机械的部分由机制承担。
