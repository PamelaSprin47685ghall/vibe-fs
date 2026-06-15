# REF94: 树结构系统的数据流

## 1. Agentic 模式数据流

```
用户输入 / 初始内容
  ↓
AgenticEngine
  ↓
LangGraph (agent → tools 循环)
  ├─ AI 调用 → AIMessage (含 tool_calls)
  ├─ 工具执行 → ToolMessage (含结果)
  ↓
状态更新 (currentContent, messages, contentHistory)
  ↓
UI 渲染 (双列: 文本面板 + 活动面板)
```

## 2. Contextual 模式数据流

```
用户输入
  ↓
主生成器 → 方案
  ↓
迭代代理 → 批判 (5 问题 + 反例)
  ↓
策略池代理 → 策略方向
  ↓
(每 10 轮) 记忆代理 → 历史压缩
  ↓
主生成器 (接收批判 + 策略) → 改进方案
  ↓
(循环) ...
```

## 3. Deepthink EDFS 数据流

```
用户需求
  ↓
策略生成 → N 主策略
  ↓
假设生成 + 测试 → 信息包
  ↓
(选择性注入) → 各分支执行 → 批判
  ↓
方案池生成 (BFS)
  ↓
修正 (DFS) + 批判
  ↓
(每 2 轮) 假设心跳
  ↓
(每 5 轮) 记忆 + PQF
  ↓
(如需) 策略更新 + 新分支首次执行
  ↓
(循环)
  ↓
裁判 → 最终方案
```

## 4. 各模式的关键数据结构

| 模式 | 核心状态 | 追踪 |
|------|---------|------|
| Agentic | AgenticState (content, messages, history) | 消息列表 |
| Contextual | ContextualState (generation, critique, pool, memory) | 迭代编号 |
| Deepthink | DeepthinkPipelineState (strategies, hypotheses, pools, PQF) | 全局迭代 |
