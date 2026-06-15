# REF82: JSON 容错解析策略

## 1. 问题

AI 模型输出的 JSON 常有各种格式问题，系统需要多层容错。

## 2. 全局解析链

```typescript
parseJsonSafe(raw, context):
  1. JSON.parse(raw)            // 原生最快
     ↓ 失败
  2. JSON5.parse(raw)           // 尾逗号、无引号键、单引号
     ↓ 失败
  3. throw Error                // 完全失败
```

## 3. Deepthink 的额外清理

```typescript
cleanJsonText(raw):
  1. raw.trim()
  2. 替换 ```json ... ``` 为纯内容
  3. 替换 ``` ... ``` 为纯内容
  4. 找到第一个 '{' 的位置
  5. 找到最后一个 '}' 的位置
  6. 确保 start < end
  7. slice(start, end + 1)

parseJson(raw, context):
  1. try parseJsonSafe(raw, context)
  2. catch → cleanJsonText → JSON.parse
```

## 4. 容错的应用场景

```typescript
// 策略生成:
const strategies = asStringArray(parsed.strategies || parsed.features || parsed.suggestions)
// 多个字段名兼容

// PQF:
const parsedDecisions = Array.isArray(parsed.strategies) ? parsed.strategies : []

// 假设:
const hypotheses = Array.isArray(parsed.hypotheses) ? parsed.hypotheses : []

// 方案池:
parsePoolResponse(raw):
  try parseJson → 检查 solutions 数组 → 构建 SolutionPoolParsedResponse
  catch → return undefined
```

## 5. 数组字段兼容

```typescript
function asStringArray(value: unknown): string[] {
    if (!Array.isArray(value)) return []
    return value.map(item => {
        if (typeof item === 'string') return item
        if (item && typeof item === 'object') {
            return String(item.strategy || item.text || item.content || '')
        }
        return String(item ?? '')
    }).filter(Boolean)
}
```
