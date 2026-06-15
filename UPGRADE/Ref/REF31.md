# REF31: 模式配置控制器 (ModelConfig & Routing)

## 1. ModelConfig 的作用

`ModelConfig` 存储所有 Deepthink 参数并提供限制的 getter：
- `strategiesCount`, `subStrategiesCount`, `hypothesisCount`
- `pqfAggressiveness`
- `refinementEnabled`, `skipSubStrategies`, `dissectedObservationsEnabled`
- `evolvingDfsEnabled`, `evolvingDfsDepth`
- `hypothesisInjectionMode`

## 2. 路由管理器 (RoutingManager)

全局单例，提供：
- AI 提供商管理
- 模型选择
- 参数配置（温度、top-p）
- 各模式提示词管理器
- Deepthink 配置控制器

### DeepthinkConfigController

```typescript
controller.getState()  // 返回当前所有配置
controller.addEventListener('configchange', handler)
controller.setStrategiesCount(n)
controller.setSubStrategiesCount(n)
controller.setHypothesisEnabled(bool)
controller.setPqfMode(mode)
controller.setEvolvingDfsEnabled(bool)
// ...等
```

配置变更时自动应用约束（EDFS 开启 → 强制 selective, 禁用子策略等）。

## 3. 模型参数同步

模型参数（温度、top-p）的变更通过路由管理器同步到 UI 和 Deepthink 核心。每次 Handle Generate 时读取当前值。

## 4. Thinking Level

支持 `low | medium | high | minimal` 四种思考深度级别，通过 `ThinkingConfig` 传入 AI 调用。
