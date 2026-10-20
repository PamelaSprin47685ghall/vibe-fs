  write(path, content) {
    // 写入一个 UTF-8 文件（覆盖已有或新建缺失）：一次原子写、写入即生效；写前按读快照做 FILE_CHANGED 校验。
    // 例如: this.write("generated/version.txt", "1.2.3\n");
  }
