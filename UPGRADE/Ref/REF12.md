# REF12: Contextual 模式——Python 工具运行时

## 1. 概述 (ContextualPythonToolRuntime.ts)

为 Contextual 模式的代理提供 Python 虚拟文件系统执行能力。

## 2. 核心流程

```
runPythonToolAgent(options)
  → 构建系统提示（含 VFS 规则）
  → 上传图片种子
  → 循环（最多 32 轮）:
    → 调用 LLM 获取回应（可能含 python_virtual_filesystem 工具调用）
    → 执行 Python 代码（通过后端 /execute）
    → 处理生成的图片
    → 继续直到无工具调用
  → 返回最终文本 + 执行追踪 + 循环消息
```

## 3. 工具定义

唯一工具：`python_virtual_filesystem`
- 参数：`code: string`（Python 代码）
- 与 LangGraph 工具定义兼容

## 4. 会话管理

- 每个代理角色有独立的 Python session
- session ID 格式：`ctx-{agentName}-{randomId}`
- 会话持久化：变量/导入/函数/类/文件跨调用保持

## 5. 图片处理

- 上传的图片作为种子文件挂载到 VFS
- 生成的图片和修改的图片返回为原生 vision 输入
- 查看的图片也作为 vision 输入返回

## 6. 执行追踪

```typescript
interface PythonToolExecutionTrace {
    schema: 'python_tool_execution_trace.v1'
    python_tool_name: string
    agent: { name, provider, model, session_id }
    final_text: string
    messages: PythonToolExecutionTraceMessage[]
}
```

追踪中会 redact 敏感内容（thinking 内容、图片 data URI、二进制数据）。
