  async cat(path) {
    // 返回 string: 读文件内容，并登记这份读快照（写前 FILE_CHANGED 校验拿它比对）；缺失报 FILE_NOT_FOUND，非 UTF-8 报 INVALID_UTF8。
    // 例如: const text = await this.cat("README.md");
  }

  async stat(path) {
    // 返回 { exists, size, mtime, isDir }: 查一个路径；路径不存在是正常观察（exists = false），不是失败。
    // 例如: const info = await this.stat("package.json");
  }

  whereAll(content, needle, from = 0) {
    // 返回 [[begin, end], ...]: 在文本里做规范化精确匹配（空白折叠为一个空格），给的是原字符串的 UTF-16 偏移；无匹配返回 []。
    // 例如: const spans = this.whereAll(text, "const value");
  }

  async where(content, needle, from = 0) {
    // 返回 [begin, end]: 唯一匹配返回它的区间；零个或多个匹配会把程序挂起，经 park 通道交回 { needle, candidates, nearest }，unpark 注入选中的区间，程序一定拿到一个区间。
    // 例如: const [begin, end] = await this.where(text, "const value");
  }
