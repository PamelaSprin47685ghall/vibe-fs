# REF35: Python 工具与 Deepthink 集成

## 1. Deepthink Python 访问范围

当 Deepthink 代码执行启用时，Python 访问限于：
- 假设测试 (Hypothesis Testing)
- 方案尝试 (Solution Attempt)
- 方案批判 (Solution Critique)
- 单通道自我改进 (Self-Improvement)
- EDFS 解决方案修正 (Solution Correction)

**无 Python 访问**的策略生成、子策略生成、假设生成、合成、PQF、记忆、方案池生成、最终裁判。

## 2. 会话隔离策略 (DeepthinkCore.ts)

### 假设测试
- 每个假设有隔离的 per-hypothesis 会话
- 会话 ID: `dtpy-{runId}-hypothesis-testing-{hypId}-round-{n}`

### 执行/批判/修正
- 范围：运行 × 角色 × 策略 × 子策略 × 分支版本
- 持久性：存活的分支在迭代间保持 Python 状态
- 替换的分支获得新的版本化会话

## 3. 执行/批判/修正的角色隔离

三个角色**不共享** Python 会话：
- 执行代理的 Python 变量和文件与其他角色隔离
- 批判代理不能访问执行代理的 `.py` 变量
- 修正代理不能访问批判代理的状态

## 4. 文件系统规则

```typescript
getDeepthinkPythonFilesystemRules(): string[]
// 返回描述 Deepthink 特定 VFS 规则的字符串数组
// 包括各角色会话描述、假设会话隔离等
```
