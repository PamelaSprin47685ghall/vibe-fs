# REF58: Python 虚拟文件系统——文件追踪机制

## 1. 图片访问追踪

通过 monkey-patching 追踪 Python 代码访问的图片文件：

```python
# PIL.Image.open 追踪
original_open = PILImage.open
def tracked_open(fp, *args, **kwargs):
    record_image_access(fp)
    return original_open(fp, *args, **kwargs)
PILImage.open = tracked_open

# cv2.imread 追踪
original_imread = cv2.imread
def tracked_imread(filename, *args, **kwargs):
    record_image_access(filename)
    return original_imread(filename, *args, **kwargs)
cv2.imread = tracked_imread
```

## 2. 图片写入追踪

```python
# matplotlib Figure.savefig
original_savefig = Figure.savefig
def tracked_savefig(self, fname, *args, **kwargs):
    record_image_write(fname)
    return original_savefig(self, fname, *args, **kwargs)

# PIL.Image.save
original_save = PILImage.Image.save
def tracked_save(self, fp, *args, **kwargs):
    record_image_write(fp)
    return original_save(self, fp, *args, **kwargs)

# cv2.imwrite
original_imwrite = cv2.imwrite
def tracked_imwrite(filename, *args, **kwargs):
    record_image_write(filename)
    return original_imwrite(filename, *args, **kwargs)
```

## 3. 文件路径规范化

```python
normalize_image_path(value) → str | None
  // 检查文件扩展名是否为图片
  // 解析绝对路径
  // 计算相对于 WORKSPACE_ROOT 的路径
  // 如果路径逃逸了工作空间，返回 None
```

## 4. 后端文件服务 (pythonToolBackend.ts)

### 三阶段图片处理

```typescript
1. beforeImages = snapshotImages(执行前快照)
2. execution = runPython(代码)
3. afterImages = listImageFiles(执行后快照)
4. changedImages = getChangedImages(beforeImages, afterImages)
5. writtenImages = getAccessedImages(execution.writtenImageFiles, afterImages)
6. viewedImages = getAccessedImages(execution.accessedImageFiles, afterImages)
7. generatedImages = mergeImages(changedImages, writtenImages)
```

### 工件快照

生成的图片从可变工作空间复制到不可变工件目录：
```typescript
const artifactWorkspace = path.join(ARTIFACT_ROOT, artifactId)
await mkdir(path.dirname(destination), { recursive: true })
await copyFile(source, destination)
```

工件 URL：`/api/python/artifacts/{artifactId}/{filename}`
