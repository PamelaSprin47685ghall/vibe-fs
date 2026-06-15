# REF05: 配置导入导出 ConfigManager

## 1. 导出流程 (Core/ConfigManager.ts)

```
exportConfiguration(format?)
  → 收集全局配置 (模式/模型/参数/图片/提示词)
  → 通过 ModeStateHandler 获取当前模式状态
  → 获取嵌入状态（如果有）
  → 构建 VersionedState
  → 序列化 (JSON/MessagePack ± gzip)
  → 下载文件
```

### 支持的导出格式
- `auto`: MessagePack + gzip
- `json`: JSON (可读)
- `msgpack`: MessagePack (二进制)

## 2. 导入流程

```
handleImportConfiguration(event)
  → 读取文件
  → 反序列化（自动检测格式和压缩）
  → 版本检测和迁移
  → applyConfiguration()
```

### applyConfiguration 步骤
1. 恢复全局模式 → 设置 radio button
2. 恢复初始想法
3. 非 deepthink 模式清空问题图片
4. 触发 UI 模式变更
5. 恢复自定义提示词
6. 恢复模型参数（延迟 150ms 等待 UI 就绪）
7. 通过 handler 恢复模式状态
8. 更新控制状态

## 3. 恢复模型参数

使用 `routingManager.getModelConfigManager().updateParameter()` 逐字段恢复，自动适配新增参数。

## 4. 恢复自定义提示词

使用配置映射表，每个模式有 key → target → getDefault 的三元组映射，缺失时用默认值填充。
