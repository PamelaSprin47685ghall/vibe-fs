# REF71: Agentic Verification 详细设计

## 1. 验证触发规则

Agentic 模式的 verify 机制有严格的触发条件：

```typescript
function buildAgentSystemPrompt(state, systemPrompt): string {
    const verificationStatus = state.lastVerifiedContent === state.currentContent
        ? '当前草稿已验证。仅当不需要进一步编辑时调用 Exit。'
        : '当前草稿尚未验证最新状态。在 Exit 前使用 verify_current_content。'
    // ...
}
```

## 2. 验证器调用

```typescript
executeVerifyCurrentContent(state, modelName, verifierPrompt):
    // 使用独立 verifier 调用的低温柔
    const verifierResponse = await callAI(
        [{ role: 'user', content: `<current_content>\n${state.currentContent}\n</current_content>` }],
        0.2,                                // 低温
        modelName,
        verifierPrompt || VERIFIER_SYSTEM_PROMPT,
        false,
        0.95
    )
```

## 3. Exit 前的验证强制

```typescript
executeExit(state):
    if (state.lastVerifiedContent !== state.currentContent) {
        return {
            content: 'Exit 被拒绝：请在完成前验证最新草稿。',
            status: 'error'
        }
    }
```

## 4. 验证后的状态变更

```typescript
statePatch: {
    verifierReports: [...state.verifierReports, report],
    verificationCount: state.verificationCount + 1,
    lastVerifiedContent: state.currentContent   // 标记为已验证
}
```

## 5. 编辑后验证重置

`multi_edit` 工具在内容变更时自动重置验证状态：
```typescript
lastVerifiedContent: contentChanged ? null : state.lastVerifiedContent
```

## 6. Verifier 提示词

```typescript
export const VERIFIER_SYSTEM_PROMPT = `
你是严格审查当前工作草稿的验证器。
要求：
- 识别具体缺陷、错误、不一致、无根据假设、遗漏边界情况、安全问题、性能问题、架构缺陷
- 直接、简洁、信息密集
- 不提出修复方案
- 不包含对话填充或元评论
只返回验证结果。
`
```
