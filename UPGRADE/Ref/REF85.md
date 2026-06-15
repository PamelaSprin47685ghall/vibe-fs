# REF85: 序列化兼容性与迁移策略

## 1. 版本管理系统

```typescript
CURRENT_STATE_VERSION = 1

const migrations = new Map<number, MigrationFn>()
// 如 [1, (state) => { return { ...state, newField: 'default' } }]
```

## 2. 迁移流程

```typescript
function migrateToLatest(state: VersionedState): VersionedState {
    let currentVersion = state._version
    let data = state.data
    
    while (currentVersion < CURRENT_STATE_VERSION) {
        const migration = migrations.get(currentVersion)
        if (migration) {
            data = migration(data)
        }
        currentVersion++
    }
    
    return { ...state, _version: CURRENT_STATE_VERSION, data }
}
```

## 3. 旧版本兼容

```typescript
function convertLegacyToVersioned(legacyConfig): VersionedState {
    // 检测模式
    // 提取古字段映射到新结构
    // 构建 ExportedConfigV1
    return {
        _version: CURRENT_STATE_VERSION,
        _exportedAt: new Date().toISOString(),
        _mode: currentMode,
        data: { ... }
    }
}
```

## 4. 状态清理

```typescript
function sanitizeState(state): T {
    const cloned = deepClone(state)
    return sanitizeRecursive(cloned)
}

// 清理规则:
- status: processing → pending, running → stopped
- isProcessing, isRunning → false
- 删除 abortController, DOM 元素引用
- 删除函数类型字段
```

## 5. 导入后的自动恢复

```typescript
async function applyConfiguration(config):
    // 1. 恢复模式
    globalState.currentMode = data.currentMode
    
    // 2. 恢复提示词（缺失时用默认值）
    restoreCustomPrompts(data.customPrompts)
    
    // 3. 恢复参数
    setTimeout(() => restoreModelParameters(data.modelParameters), 150)
    
    // 4. 恢复模式状态
    const sanitized = sanitizeState(data.modeState)
    handler.restoreState(sanitized)
    
    // 5. 渲染
    handler.renderAfterImport()
```
