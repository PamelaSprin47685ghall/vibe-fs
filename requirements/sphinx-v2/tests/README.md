# sphinx-v2 测试说明

本目录收录 `sphinx-v2` 相关的自保与回归测试用例。行为规范见 [WHAT.md](../WHAT.md)。

## 测试范围与职责划分

1. **核心命令与状态收束（001–021）**
   - 验证 `Composition/Commands`、规范存储（Store）与唯一 Reducer 之间的因果推进。
   - 验证目标快照在冷重开（cold replay）下的文本保持，提案（proposal）不自动升级为正式修订。
   - 验证 `Applied`、`Failed`、引用先建后用及冲突批次（batch）的原子拒绝。
   - 覆盖 MCP 与 JS 交叉入口的命令一致性与冷重开幂等性。

2. **交接与委托边界（022–036）**
   - 验证适配器（Adapter）对最终提示词（Prompt）与公开问题的捕获，确保私有映射标签不泄露。
   - 验证委托运行时（DelegateRuntime）中的嵌套所有权、委托派发以及缺乏活跃权限（active authority）时的同步拒绝。
   - 验证拓扑枚举去重与执行清单（manifest）的一致性。

## 运行方式

测试套件由 Node.js 官方测试运行器驱动：

```bash
node --test requirements/sphinx-v2/tests/*.test.mjs
```

【程序待改】：当前部分跨进程与真实宿主端到端用例仍依赖外部环境接线，宿主特异性交互后续收敛至万象通层。
