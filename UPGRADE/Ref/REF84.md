# REF84: Session 隔离与数据安全

## 1. Python 会话隔离级别

| 层级 | 隔离方式 |
|------|----------|
| 跨模式 | 不同模式完全独立 |
| 跨代理（Contextual） | 每个角色独立 session |
| 跨假设测试 | 每个假设独立 session |
| 跨分支（EDFS） | 策略 + 角色 + 版本 唯一 session ID |
| 跨管道 | 每次 generate 新 pipeline ID |

## 2. 会话 ID 生成

```typescript
// Contextual:
`ctx-sess-{pipelineId}-{agentName}`

// Deepthink:
`dtpy-{pipelineId}-{role}-{strategyId}-{subStrategyId}-v{branchVersion}`

// 假设测试:
`dtpy-{pipelineId}-hypothesis-testing-{hypId}-round-{n}-global-{m}`

// 安全验证:
function isSafeSessionId(id: string): boolean {
    return /^[a-zA-Z0-9_-]{8,80}$/.test(id)
}
```

## 3. 文件系统隔离

- 每个 session 的工作空间: `{VFS_ROOT}/{sessionId}/`
- `os.chdir` 守卫: 只能在 `{VFS_ROOT}/{sessionId}/` 内
- 路径遍历保护: `safeJoin()` 验证绝对路径前缀

## 4. 图片工件隔离

- 生成的图片→不可变工件目录 `{ARTIFACT_ROOT}/{artifactId}/`
- `artifactId` 使用 `randomUUID()`
- 工件通过 `/api/python/artifacts/{artifactId}/{filename}` 访问

## 5. Agent 历史隔离

```typescript
const deepthinkPythonHistories = new Map<string, BaseMessage[]>()
// key = sessionId (含策略+角色+版本)
// 确保不同分支、不同角色的 Python 调用历史互不干扰
```
