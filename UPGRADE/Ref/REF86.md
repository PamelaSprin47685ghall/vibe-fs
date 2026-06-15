# REF86: 安全设计模式

## 1. 路径安全

```typescript
function safeJoin(workspace: string, relativePath: string): string {
    // 禁止: null 字符、绝对路径、.. 目录遍历
    if (!normalized || normalized.includes('\0') || 
        normalized.startsWith('/') || normalized.split('/').includes('..')) {
        throw new Error('Invalid path')
    }
    
    const resolved = path.resolve(workspace, normalized)
    const root = path.resolve(workspace)
    
    // 验证: 解析后的路径必须在 workspace 下
    if (resolved !== root && !resolved.startsWith(`${root}${path.sep}`)) {
        throw new Error('Path escapes the virtual filesystem')
    }
    
    return resolved
}
```

## 2. Python chdir 守卫

```python
def guarded_chdir(pathname):
    target = os.path.abspath(os.fspath(pathname))
    if not is_inside_workspace(target):
        raise PermissionError(
            "Cannot change directory outside the Python virtual filesystem. "
            "Use relative paths inside the current workspace."
        )
    ORIGINAL_CHDIR(target)
```

## 3. Session ID 验证

```typescript
function isSafeSessionId(sessionId: string): boolean {
    return /^[a-zA-Z0-9_-]{8,80}$/.test(sessionId)
}
```

## 4. 文件名安全

```typescript
function sanitizeFilename(name: string | undefined, mimeType: string, index: number): string {
    const fallback = `uploaded-image-${index + 1}${extensionForMimeType(mimeType)}`
    const base = path.basename(name || fallback).replace(/[^\w.\- ()]/g, '_')
    return path.extname(base) ? base : `${base}${extensionForMimeType(mimeType)}`
}
```

## 5. Agent prompt 安全

系统提示词中明确指令：
- 不暴露系统内部机制给用户
- 不引用隐藏的上下文片段
- 不在最终输出中包含协调过程

## 6. API Key 安全

- ProviderConfig 不暴露完整 apiKey（序列化时排除）
- ProviderManager 安全存储
- `hasValidApiKey()` 检查是否至少有一个提供商已配置
