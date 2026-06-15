# REF37: JSON 解析器 (Core/JsonParser.ts)

## 1. 设计目的

AI 模型输出的 JSON 常常格式不标准，需要多重解析策略。

## 2. 解析链

```typescript
parseJsonSafe(raw: string, context: string): any
```

1. **原生 JSON.parse**（最快，处理标准 JSON）
2. **JSON5.parse**（回退，处理宽松 JSON：尾逗号、未引号键、单引号）

如果都失败，抛出错误。

## 3. 精度保护解析

```typescript
parseJsonLossless(raw: string): any
```

使用 `lossless-json` 库，防止大数字精度丢失。适用于预期有超出 JS number 范围的大数字的场景。

## 4. Deepthink 的额外 JSON 清理

```typescript
cleanJsonText(raw): string
// 移除 markdown 代码块标记
// 查找第一个 { 和最后一个 }
// 提取纯 JSON
```

用于 Deepthink 模式下额外的容错（部分模型会在 JSON 外包 markdown 代码块）。

```typescript
parseJson(raw, context): any
// 先调用 parseJsonSafe
// 失败时调用 cleanJsonText + JSON.parse
```
