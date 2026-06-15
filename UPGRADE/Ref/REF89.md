# REF89: Prompt 设计模式

## 1. 分层结构

每个 Agent 的 prompt 由两部分组成：

```
系统提示词（System Instruction）:
  - 角色定义（你是谁）
  - 行为规则（怎么做）
  - 共享上下文（环境知识）
  - 输出格式（返回什么）

用户提示词（User Prompt）:
  - Core Challenge
  - 策略/子策略文本
  - 假设信息包
  - 历史/记忆
  - 方案池
```

## 2. 共享上下文（DeepthinkContext）

```typescript
const DeepthinkContext = `
<SharedDocumentAmongAllDeepthinkAgents>
Deepthink 只是一个 LLM 群，每个代理专注于一件事。
...
</SharedDocumentAmongAllDeepthinkAgents>
`
```

关键指令：
- 不要在最终输出中暴露系统机制
- 不要与其他代理通信
- 不要自我引用"作为一个代理"

## 3. JSON 输出约束

```typescript
const systemInstructionJsonOutputOnly = `
**关键输出格式要求:**
你的响应必须是唯一的有效 JSON 对象。
不允许额外文本、解释、markdown 格式或代码块。
响应必须以 { 开头，以 } 结束。
`
```

## 4. Build-and-Break 模式

每个执行代理必须在输出中包含：
```
- keyAssumptions: 方案依赖的前提
- keyRisks: 可能失败的条件
- validationChecks: 验收检查点
- selfCritique: 自我批判
```

## 5. 隔离指令

```typescript
// 修正代理必须包含:
"你的修正承诺和修正工件构成一份绑定契约。"

// 执行代理必须包含:
"Core Challenge 总是具有最高优先级。"

// 批判代理必须包含:
"永远不要过早满足。"
```
