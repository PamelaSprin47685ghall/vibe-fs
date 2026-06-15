# REF33: Python 后端架构——Worker 进程

## 1. 工作进程 (python_session_worker.py)

每个代理会话有一个持久 Python 工作进程，通过 stdin/stdout JSON 行协议通信。

### 执行循环
```
stdin 读取 JSON → execute_code() → stdout 写入 JSON
```

### execute_code 返回值
```python
{
    "ok": bool,
    "exitCode": int,
    "stdout": str,
    "stderr": str,
    "error": str | None,
    "durationMs": int,
    "accessedImageFiles": [str],  # 读取的图片
    "writtenImageFiles": [str],   # 写入的图片
}
```

## 2. 虚拟文件系统

- 工作空间根：`os.tmpdir()/iterative-studio-python-vfs/{sessionId}`
- `os.chdir()` 被守卫：只能在工作空间内切换目录
- 上传的图片文件种子化到工作空间
- 生成的图片文件通过扩展名检测（png/jpg/gif/webp/bmp/tif）

## 3. 图片追踪

通过 monkey-patching 追踪：
- **访问追踪**: `PIL.Image.open`, `cv2.imread`
- **写入追踪**: `matplotlib.Figure.savefig`, `PIL.Image.save`, `cv2.imwrite`

## 4. 会话重置

`reset_python_session()` / `clear_python_memory()`:
- 删除用户定义的名称（变量、导入、函数、类）
- 保留虚拟文件系统文件
- 返回工作空间根目录
