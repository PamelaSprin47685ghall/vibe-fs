# REF44: 模型调用参数策略

## 1. 温度 (Temperature)

控制输出的随机性/创造性：
- 范围: 0.0 - 1.0+
- Deepthink 各代理：使用全局温度
- Agentic 模式：使用全局温度
- Contextual 模式：使用全局温度
- Verifier（Agentic）：硬编码 0.2（低温确保一致性）

## 2. Top-P (核采样)

控制输出的多样性：
- 范围: 0.0 - 1.0
- 各模式均使用全局 Top-P
- 默认为 0.95

## 3. Thinking Level

仅 Gemini 提供商支持：

| Level | 含义 |
|-------|------|
| `minimal` | 最小思考 |
| `low` | 少量思考 |
| `medium` | 中等思考 |
| `high` | 大量思考（默认） |

通过 `ThinkingConfig` 传递给 AI 调用。

## 4. JSON 模式

`isJson` 参数控制是否强制 JSON 输出：
- Deepthink 的策略生成、子策略生成、假设生成、PQF、裁判：true
- Deepthink 的执行、批判、修正、假设测试、记忆、方案池：false
- Agentic 模式：不使用
- Contextual 模式：不使用

## 5. 代理特定模型覆盖

每种模式允许为特定代理覆盖模型选择：

```typescript
// Deepthink 示例
model_initialStrategy: string | null  // null = 使用全局模型
model_solutionAttempt: string | null
model_solutionCritique: string | null
// ...
```

覆盖在 `modelFor(stepDescription)` 中实现，查找 `MODEL_MAP` 表。
