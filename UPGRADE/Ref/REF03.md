# REF03: 模式懒加载系统 ModeLoader

## 1. 设计目的

避免所有模式代码在启动时一次性加载，通过动态 import 实现按需加载。

## 2. 加载机制

每个模式有四种状态变量：
- `xxxModule`: 缓存已加载的模块引用
- `xxxModulePromise`: 防止重复加载
- `xxxInitialized`: 标记是否已初始化（调用一次初始化函数）

## 3. 各模式入口

| 模式 | 加载函数 | 初始化函数 | 模块路径 |
|------|----------|------------|----------|
| Deepthink | `loadDeepthinkModule()` | `ensureDeepthinkInitialized()` | `../Deepthink/Deepthink` |
| SolutionPool | `loadSolutionPoolModule()` | - | `../Deepthink/SolutionPool` |
| Agentic | `loadAgenticModule()` | `ensureAgenticInitialized()` | `../Agentic/AgenticUI_Bridge` |
| Contextual | `loadContextualModule()` | `ensureContextualInitialized()` | `../Contextual/Contextual` |
| Adaptive Deepthink | `loadAdaptiveDeepthinkModule()` | `ensureAdaptiveDeepthinkInitialized()` | `../AdaptiveDeepthink/AdaptiveDeepthinkMode` |
| DCA | `loadDCAModule()` | `ensureDCAInitialized()` | `../Deepthink/DCA/DCA` |

## 4. Deepthink 初始化的特别之处

`ensureDeepthinkInitialized()` 会向 Deepthink 模块注入大量依赖：
- AI 提供商、模型调用函数
- JSON 解析器
- UI 控制更新函数
- 各配置参数读取函数
- DOM 容器引用
- Pipeline 状态设置函数
