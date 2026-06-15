# REF04: 状态序列化系统 StateSerializer

## 1. 架构组成

StateSerializer 位于 `Core/StateSerializer/`，由四部分组成：

### ModeStateHandler
- 接口定义：`getFullState()` / `restoreState()` / `renderAfterImport()`
- 每种模式实现自己的 handler
- 在 `handlers/index.ts` 中自动注册

### StateSanitizer
- 深度克隆状态对象
- 重置处理中状态（`isProcessing` → false，`status: processing` → `pending`）
- 移除不可序列化字段（`abortController`、DOM 元素引用等）

### StateVersion
- 版本号管理（当前 `CURRENT_STATE_VERSION = 1`）
- 版本迁移机制：从旧版自动升级到新版
- 旧版兼容：`convertLegacyToVersioned()` 处理无版本号的旧配置

### SerializationEngine
- 支持两种格式：JSON 和 MessagePack
- 支持 gzip 压缩
- 自动检测格式和压缩
- 提供进度回调

## 2. 序列化数据流

```
状态对象 → encode (JSON/MessagePack) → compress (gzip) → Blob → 下载文件
文件 → 读取 → decompress → decode → 版本迁移 → 状态恢复
```

## 3. 导出配置结构

```typescript
interface VersionedState {
    _version: number          // 版本号
    _exportedAt: string       // 导出时间
    _mode: ApplicationMode    // 当前模式
    _appVersion?: string      // 应用版本
    data: ExportedConfigV1    // 实际配置数据
}
```
