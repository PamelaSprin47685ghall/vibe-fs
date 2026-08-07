# 文档治理 — 边界

规则定义在 `what/document-governance.md`。执行程序在 `how/document-governance.md`。

## 权威边界

| 面 | 可写内容 | 不可写 |
|----|----------|--------|
| 规范面 why/what/shape/how/proof | 理由、行为、边界、目标实现、证明要求 | 实现进度百分比、提交列表 |
| 流动面 proposal | 未裁决候选 | 被代码当现行合同 |
| 流动面 status | 活跃差距与阻塞 | 条款定义（`## ID`）、完成墓地 |
| 实现面 code/resources | 可执行实现与实例数据 | 发明文档未声明语义 |

## 写入口

- 产品行为变更：生命周期由 GOV-006 定义；本层只要求每个受影响主题明确行为、边界、算法和证明的写入口。
- 条款定义：每个 ID 恰好一处 `## ID` 标题  
- 导航：`docs/README.md` 与 `scripts/checks/spec.mjs` 同步  

## 与工程入口

`AGENTS.md` 只拥有 Agent 工作协议；`docs/README.md` 只拥有文档导航。两者不得成为正式条款 writer。
