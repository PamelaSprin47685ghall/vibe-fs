# REF28: Deepthink 模式——实时事件与 Live Tab

## 1. 实时事件系统 (DeepthinkLiveEvent)

```typescript
interface DeepthinkLiveEvent {
    id: string
    timestamp: number
    agentName: string
    stepDescription: string
    eventType: 'info' | 'agent_start' | 'agent_complete' | 'agent_error' | 'agent_retry'
    systemInstruction?: string    // 系统提示词
    prompt?: string               // 用户提示
    response?: string             // 模型回应
    error?: string
    attempt?: number
    modelName?: string
    temperature?: number
    topP?: number
    codeExecutionEnabled?: boolean
}
```

## 2. Live Tab UI 布局 (DeepthinkLiveTab.tsx)

三区域布局：
- **左侧**: 执行时间线（代理完成/运行状态）
- **右上**: 代理检查器（系统指令、用户提示、模型回应）
- **右下**: 终端控制台日志

## 3. 时间线特性

- 完成和运行的代理以时间线形式显示
- 并行运行的多个代理被分组在"并行执行窗口"中
- 点击代理可查看其详细输入/输出
- 自动滚动到最新事件

## 4. 控制台日志过滤

四种过滤模式：
- ALL: 显示所有事件
- AGENTS: 只显示代理开始/完成/错误
- INFO: 只显示信息事件
- ERRORS: 只显示错误事件

还支持搜索过滤。

## 5. 代理类别颜色

| 类别 | 颜色 | 判断依据 |
|------|------|----------|
| strategy | 紫色 | 名称含 strategy/attempt/execution/solver |
| hypothesis | 蓝色 | 名称含 hypothesis |
| critique | 黄色 | 名称含 critique/critic |
| redteam | 红色 | 名称含 evolution filter/postqualityfilter |
| dissected | 绿色 | 名称含 correction/refinement/dissect |
| solutionpool | 橙色 | 名称含 pool |
| general | 默认 | 其他 |
