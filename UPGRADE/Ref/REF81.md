# REF81: Agent 进程管理与 Worker 生命周期

## 1. PythonSession 生命周期

```typescript
class PythonSession {
    constructor(sessionId, workspace, python, onDispose)
    
    execute(code, timeoutMs)
    dispose()
    
    private child: ChildProcess | null
    private queue: PendingExecution[]
    private active: PendingExecution | null
    private activeTimer: Timer
    private idleTimer: Timer
}
```

## 2. Worker 进程池管理

```typescript
const pythonSessions = new Map<string, PythonSession>()

async function getOrCreateSession(sessionId, workspace, python): PythonSession {
    let session = pythonSessions.get(sessionId)
    if (!session) {
        session = new PythonSession(sessionId, workspace, python, onDispose)
        pythonSessions.set(sessionId, session)
    }
    return session
}
```

## 3. 空闲超时

```typescript
// 20 分钟无活动后自动 dispose:
private armIdleTimer() {
    this.idleTimer = setTimeout(() => this.dispose(), SESSION_IDLE_TTL_MS)
    // SESSION_IDLE_TTL_MS = 20 * 60 * 1000 = 1200000
}
```

## 4. 执行队列

```
入队 → processQueue()
  → 如果 active 忙，等待
  → 如果 child 不存在，启动
  → 将 code JSON 写入 stdin
  → 从 stdout 读取 JSON 行
  → resolve / reject
```

## 5. 进程启动

```typescript
this.child = spawn(python, [RUNNER_PATH], {
    cwd: this.workspace,
    stdio: ['pipe', 'pipe', 'pipe'],
    env: {
        VIRTUAL_FS_ROOT: this.workspace,
        MPLBACKEND: 'Agg',
        PYTHONUNBUFFERED: '1',
        // ...
    }
})
```

## 6. 进程退出处理

```typescript
this.child.on('close', () => {
    this.child = null
    if (this.closed) return
    this.failActive(new Error('Python session process exited unexpectedly.'))
})
```
