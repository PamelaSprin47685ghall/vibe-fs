rewrite(path, newText) 暂存对已存在 UTF-8 文件的替换。newText 是
完整的结果文件，不是 patch。目标必须存在于
transaction snapshot，否则调用失败为 FILE_NOT_FOUND。newText 必须是 string。
该调用不会立即写入；它向本 program 的 WriteSet 添加一个 StagedRewrite。
不必先 file(path)。
