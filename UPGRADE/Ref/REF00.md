# REF00: 系统架构总览

## 1. 整体架构

vibe-fs 是一个多模式（multi-mode）AI 推理系统，支持多种不同的"思考模式"来处理用户问题。每种模式代表一种不同的认知搜索策略。

## 2. 五种模式

| 模式 | 标识符 | 核心思想 |
|------|--------|----------|
| Agentic | `agentic` | 单代理自主精炼，使用 LangGraph 工具图 |
| Contextual | `contextual` | 多代理上下文迭代（主生成器+批判代理+策略池+记忆代理） |
| Deepthink | `deepthink` | 多分支并行策略探索 + EDFS 深度优先搜索 |
| Adaptive Deepthink | `adaptive-deepthink` | 自适应深度思考 |
| Dynamic Compute | `dynamic-compute` | 动态计算分配 DCA |

## 3. 核心模块

- **Core**: 应用核心（App.ts, State.ts, Types.ts, ConfigManager.ts, ModeLoader.ts, LangGraphToolRuntime.ts）
- **Routing**: AI 提供商路由、模型选择、参数配置
- **UI**: 控制面板、布局管理、全局弹窗
- **Styles**: 样式组件、Markdown 渲染、Diff 视图
- **Agentic**: Agentic 模式完整实现
- **Contextual**: Contextual 模式完整实现
- **Deepthink**: Deepthink 模式 + DCA 子模式
- **AdaptiveDeepthink**: 自适应深度思考模式
- **Backend**: Python 虚拟文件系统后端

## 4. 状态管理

全局状态通过 `GlobalStateManager` 管理（Core/State.ts），包含：
- 当前模式
- 各模式专用状态（懒加载）
- 生成/运行状态
- 自定义提示词
- 模型参数

状态序列化通过 `StateSerializer` 支持 JSON/MessagePack + gzip 压缩导出导入。

## 5. 模式加载

使用 `ModeLoader` 实现所有模式的懒加载，避免初始加载过重。每个模式有独立的 `ensureXxxInitialized()` 函数。

## 6. 核心设计原则

- 分支策略相互独立（Strategy-as-lens）
- 纯函数内核优先
- 可定制提示词系统
- 可插拔 AI 提供商
- 状态可序列化、可导入导出
