# REF02: 全局状态管理 GlobalStateManager

## 1. GlobalStateManager 定义 (Core/State.ts)

```typescript
class GlobalStateManager {
    currentMode: ApplicationMode = 'deepthink'
    activeDeepthinkPipeline: DeepthinkPipelineState | null = null
    isGenerating: boolean = false
    currentProblemImages: Array<{ base64, mimeType, name?, size? }> = []
    isCustomPromptsOpen: boolean = false
    
    // 各模式运行状态
    isAgenticRunning: boolean = false
    isContextualRunning: boolean = false
    isAdaptiveDeepthinkRunning: boolean = false
    isDCARunning: boolean = false
    
    // Python 工具开关
    geminiCodeExecutionEnabled: boolean = false
    
    // Thinking Level
    thinkingLevel: 'low' | 'medium' | 'high' | 'minimal' = 'high'
    
    // 各模式自定义提示词
    customPromptsDeepthinkState
    customPromptsAgenticState
    customPromptsAdaptiveDeepthinkState
    customPromptsContextualState
    customPromptsDCAState
}
```

## 2. 设计要点

- 单例模式：全局唯一实例 `globalState`
- 各模式的自定义提示词有默认值，由各自的 `createDefaultCustomPrompts*` 函数生成
- `geminiCodeExecutionEnabled` 字段名虽含 "gemini"，实际控制所有提供商的 Python 工具
- `thinkingLevel` 控制模型思考深度

## 3. 状态导入导出

通过 `ConfigManager` 与 `StateSerializer` 配合：
- 导出：收集全局状态 + 当前模式状态 → 序列化 → 下载
- 导入：文件 → 反序列化 → 恢复全局状态 → 恢复模式状态 → 渲染
- 支持 JSON (可读) 和 MessagePack (高性能) 两种序列化格式
- 支持 gzip 压缩
