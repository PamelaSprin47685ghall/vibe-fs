# REF62: Deepthink 配置面板——假设注入可视化

## 1. Blind Trust 模式 (parallel)

可视化：
- 8 条加载线（模拟假设测试进度）
- 中央蓝色节点 → 4 个执行代理的连线

## 2. Strategy-Aware 模式 (strategy_aware)

可视化：
- 2 个策略卡片
- 向下的路径箭头
- 加载线（信息包内容）
- 执行代理区域：中央节点 → 4 个代理

## 3. Selective 模式 (selective_injection)

可视化：
- 3 个策略卡片
- 向下的路径箭头
- 3 个 Sub-Packet（子包），每个包含加载线
- 4 个执行代理（单独面板）

## 4. 执行代理可视化

多路连接线：
- Blind Trust/Strategy-Aware: 中央汇聚
- Selective: 多源到多目标

## 5. 执行代理文本

- Blind Trust/Strategy-Aware: "Execution & Refinement Agents"
- Selective: "Execution-1" 到 "Execution-4" 独立面板
