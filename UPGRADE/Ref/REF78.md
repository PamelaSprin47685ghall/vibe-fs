# REF78: 各代理 JSON Schema 总览

## 1. 策略生成输出

```json
{ "strategies": ["Strategy 1: ...", "Strategy 2: ..."] }
```

## 2. 子策略生成输出

```json
{ "sub_strategies": ["Sub-strategy 1: ...", ...] }
```

## 3. 假设生成输出

```json
// parallel/strategy_aware:
{ "hypotheses": ["Hypothesis 1: ...", ...] }

// selective:
{ "hypotheses": [
    { "text": "...", "target_strategies": ["main1"] },
    ...
  ] 
}
```

## 4. PQF 输出

```json
{
  "analysis_summary": "...",
  "strategies": [
    { "strategy_id": "main1", "decision": "keep", "reasoning": "..." }
  ]
}
```

## 5. 方案池输出

```json
{
  "strategy_id": "main1",
  "solutions": [
    {
      "title": "...",
      "content": "...",
      "confidence": 0.88,
      "internal_critique": "...",
      "key_insights": "..."
    }
  ]
}
```

## 6. 裁判输出

```json
{
  "best_solution_id": "main1-direct",
  "final_reasoning": "..."
}
```

## 7. 各代理的输出类型

| 代理 | 输出类型 | 是否 JSON | 使用解析器 |
|------|----------|-----------|-----------|
| 策略生成 | JSON | 是 | parseJson + asStringArray |
| 子策略生成 | JSON | 是 | parseJson + asStringArray |
| 假设生成 | JSON | 是 | parseJson |
| 假设测试 | 自由文本 | 否 | 文本提取 CLASSIFICATION |
| 执行 | 自由文本 | 否 | 直接使用 |
| 批判 | 自由文本 | 否 | 直接使用 |
| 修正 | 自由文本 | 否 | 直接使用 |
| PQF | JSON | 是 | parseJson |
| 策略更新 | JSON | 是 | parseJson |
| 方案池 | JSON | 是 | parseJson + parsePoolResponse |
| 记忆 | 自由文本 | 否 | 直接使用 |
| 裁判 | JSON | 是 | parseJson |
