# 文档治理 — 所有权与边界

规则见 `what/document-governance.md`；执行程序见 `how/document-governance.md`。

## 权威边界

| 面 | writer | 可写内容 | 不可写 |
|---|---|---|---|
| 正式 docs | 对应 Clause owner | 理由、行为、边界、算法、证明 | 实施进度与历史 Proposal |
| `changes/proposed` | 用户 | 已批准、等待启动的 Proposal 原文 | Agent 自选、重裁决、正式 Clause 定义 |
| `changes/active` | 实施该指定 Change 的 Agent/用户 | 冻结原文、Remaining work、blocker、完成条件 | 新设计、持续日志、正式 Clause 定义 |
| `changes/completed` | 关闭该 Change 的 Agent/用户 | 原文、批准修订、Final outcome | 当前产品规范、事后改写原文 |
| code/resources | 实现 owner | 对齐 how 的可执行实现 | 从 Changes 发明未进入正式 docs 的语义 |

## 文件所有权

- 每项 Change 的唯一 writer 目标是它自己的单文件；生命周期转换只做目录移动。
- 不存在独立 Status、Decision、Outcome 或 Change manifest writer。
- Original proposal 的写权限在进入 Active 时关闭；后续章节只能追加。
- `docs/README.md` 拥有正式文档导航，`changes/README.md` 拥有变更工作协议。
- `AGENTS.md` 拥有 Agent 执行协议，不成为产品 Clause writer。

## 依赖方向

```text
changes/active ──范围──► docs 正式层 ──目标──► code/resources
                                   └──证明──► proof
```

正式 docs 和实现不得反向依赖 Proposed 或 Completed。Active 可以定位工作范围，但不能替代正式目标。
