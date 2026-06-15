# REF60: 文件上传与图片处理

## 1. 支持的图片格式

| 格式 | MIME |
|------|------|
| PNG | image/png |
| JPEG | image/jpeg |
| GIF | image/gif |
| WEBP | image/webp |
| BMP | image/bmp |
| TIFF | image/tiff |

## 2. 图片在全局状态中的存储

```typescript
globalState.currentProblemImages: Array<{
    base64: string
    mimeType: string
    name?: string
    size?: number
}>
```

## 3. 不同模式的图片处理

### Agentic 模式
- 不支持图片上传

### Contextual 模式
- 支持图片，仅限 image/ 类型
- 可作为 Python VFS 的种子文件
- 注入到各代理的 context 中

### Deepthink 模式
- 支持图片
- 转换为 `challengeImageBase64` + `challengeImageMimeType`
- 附加到每个 API 调用
- 也 send 到 Python VFS（适用于 Python 工具代理）
- 在 strategy/sub-strategy/hypothesis/execution/critique/correction 中均可见

### Adaptive Deepthink 模式
- 支持图片
- 通过 `currentProblemImages` 传递

## 4. 提供商标识兼容性

| 提供商 | 支持 |
|--------|------|
| Google/Gemini | 所有格式 |
| OpenAI | PNG/JPEG/GIF/WEBP |
| Anthropic | PNG/JPEG/GIF/WEBP |
| OpenRouter | 不支持文件上传 |

验证在 `App.handleGenerate()` 中执行，不兼容时给出明确错误提示。
