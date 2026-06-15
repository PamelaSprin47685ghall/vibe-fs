# REF88: Deepthink Agent 特定 Python 访问策略

## 1. ELigible Agents

```typescript
const PYTHON_TOOL_AGENTS = new Set<DeepthinkPythonAgentKind>([
    'Hypothesis Testing',     // 每个假设独立会话
    'Solution Attempt',       // 按策略+版本持久会话
    'Solution Critique',      // 按策略+版本持久会话
    'Self-Improvement',       // 单通道修正持久会话
    'Solution Correction',    // EDFS 修正持久会话
])
```

## 2. 会话持久性

| 代理 | 会话 ID 范围 | 持久性 |
|------|-------------|--------|
| Hypothesis Testing | per-hypothesis | 单次测试内 |
| Solution Attempt | strategy+version | 分支生命周期 |
| Solution Critique | strategy+version | 分支生命周期 |
| Self-Improvement | strategy+version | 分支生命周期 |
| Solution Correction | strategy+version | 分支生命周期 |

## 3. 代理间隔离

```
Solution Attempt (策略 1) ──→ session A
Solution Critique (策略 1) ──→ session B
Solution Correction (策略 1) ──→ session C

Solution Attempt (策略 2) ──→ session D
Solution Critique (策略 2) ──→ session E

// session A/B/C/D/E 互不共享变量和文件
```

## 4. 分支替换的影响

```typescript
// PQF 替换策略时:
// 旧 session 保留旧版本号 → 不再使用
// 新 session 用新版本号 → 全新开始

const sessionId = buildDeepthinkPythonSessionId(process, [
    kind, strategyId, subStrategyId || 'direct',
    `v${branchVersion || 1}`  // 版本号变化
])
```

## 5. 文件系统规则注入

在系统提示词中注入 VFS 规则：
```typescript
const rules = [
    '- Deepthink Python access is available only to specific agents.',
    '- Solution Attempt agents keep isolated Python memory and VFS by strategy/branch.',
    '- Solution Critique agents keep isolated Python memory and VFS by strategy/branch.',
    '- Solution Correction agents keep isolated Python memory and VFS across iterations.',
    '- Surviving branches keep Python state; replaced branches start fresh.',
    '- Hypothesis Testing agents get isolated per-hypothesis sessions.',
]
```
