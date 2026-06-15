# REF10: Contextual 模式——架构概览

## 1. 定位

Contextual 模式是一个**多代理迭代精炼**系统，包含四个角色协同工作：
- **主生成器** (Main Generator)：产生解决方案并自我修正
- **迭代代理** (Iterative Agent)：提供批判性分析
- **策略池代理** (Strategic Pool Agent)：生成多样化策略方向
- **记忆代理** (Memory Agent)：维护探索历史

## 2. 文件结构

| 文件 | 职责 |
|------|------|
| ContextualCore.ts | 核心循环、状态管理、代理调用 |
| Contextual.tsx | React 入口和 UI 绑定 |
| ContextualUI.tsx | UI 组件 |
| ContextualPrompts.ts | 所有代理的系统提示词 |
| ContextualPromptsManager.ts | 提示词管理器 |
| ContextualPromptsContent.tsx | 提示词编辑 UI |
| ContextualPythonToolRuntime.ts | Python 工具后端集成 |
| ContextualUI.css | 样式 |

## 3. 核心工作流

```
每轮迭代:
  1. 主生成器: 根据需求 + 之前反馈生成/改进方案
  2. 迭代代理: 批判方案 → 5个关键问题 + 反例
  3. 策略池代理: 根据观察更新策略方向池
  4. 每10轮: 记忆代理压缩历史
  5. 将批判+策略注入主生成器 → 继续下一轮
```

## 4. 退出条件

策略池代理检测到迭代代理连续 3 次未发现缺陷时输出 `<<<Exit>>>` 停止流程。
