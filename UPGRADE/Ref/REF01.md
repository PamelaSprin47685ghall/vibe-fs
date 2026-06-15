# REF01: ApplicationMode 类型与模式路由

## 1. ApplicationMode 定义

```typescript
type ApplicationMode = 'deepthink' | 'agentic' | 'contextual' | 'adaptive-deepthink' | 'dynamic-compute';
```

## 2. 模式路由逻辑 (Core/AppRouter.ts)

### 渲染入口 `renderActiveMode()`
- 使用 `renderToken` 防竞态
- 根据 `globalState.currentMode` 分发到各模式的渲染函数
- 每种模式通过 `ensureXxxInitialized()` 懒加载模块

### 模式切换 `updateUIAfterModeChange()`
- 通知路由管理器模式变更
- 分发 `appModeChanged` 自定义事件
- 清理旧模式的状态（停止正在运行的进程）
- 调用 `renderActiveMode()`

### Tab 激活 `activateTab()`
- 专用于 Deepthink 模式的 tab 切换
- 持久化到 pipeline 状态
- 分发 `updateDeepthinkTabUI` 自定义事件

## 3. 模式切换时的清理规则

仅在非生成状态（`!globalState.isGenerating`）下清理旧模式：
- Agentic: `cleanupAgenticMode()`
- Contextual: `stopContextualProcess()`
- Adaptive Deepthink: `cleanupAdaptiveDeepthinkMode()`
- Dynamic Compute: `cleanupDCAMode()`

## 4. 生成入口 (App.ts::handleGenerate)

每个模式的生成入口：
- Deepthink: `startDeepthinkAnalysisProcess(idea, imageBase64, imageMimeType)`
- Agentic: `startAgenticProcess(idea)`
- Contextual: `startContextualProcess(idea, customPrompts)`
- Adaptive Deepthink: `startAdaptiveDeepthinkProcess(idea, prompts, images)`
- Dynamic Compute: `startDCAProcess(idea)`
