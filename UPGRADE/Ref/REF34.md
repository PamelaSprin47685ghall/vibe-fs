# REF34: Python 后端——会话管理器 (pythonToolBackend.ts)

## 1. Session 类

管理一个 Python 子进程的生命周期：

```
PythonSession:
  - sessionId / workspace / python 路径
  - child: ChildProcess 引用
  - queue: 等待执行的队列
  - active: 当前活跃的执行
  - activeTimer: 超时定时器
  - idleTimer: 空闲超时（20分钟）
```

## 2. 核心流程

```
execute(code, timeoutMs):
  → 入队
  → processQueue()
      → 确保子进程启动
      → JSON 序列化 code 写入 stdin
      → 等待 stdout 读取 JSON 行
      → 解析并 resolve/fail
```

## 3. 超时处理

- 默认超时：120 秒
- 最大超时：300 秒
- 超时后：kill 子进程（SIGKILL），返回 `timedOut: true`
- 空闲超时：20 分钟无活动后 dispose

## 4. 图片追踪

```
seedWorkspaceFiles() → 将上传的图片种子化到工作空间
listImageFiles() → 递归扫描工作空间中的图片
getChangedImages() → 比较执行前后的图片快照
snapshotImagesForTranscript() → 将生成的图片复制到不可变工件目录
```

## 5. API 端点

| 端点 | 方法 | 功能 |
|------|------|------|
| `/api/python/execute` | POST | 执行 Python 代码 |
| `/api/python/files/{sessionId}/{path}` | GET | 提供工作空间文件 |
| `/api/python/artifacts/{artifactId}/{path}` | GET | 提供工件快照文件 |

## 6. 会话隔离

- 每个 sessionId 有独立工作空间
- sessionId 格式：`/^[a-zA-Z0-9_-]{8,80}$/`
- 路径安全检查：防止目录遍历攻击
