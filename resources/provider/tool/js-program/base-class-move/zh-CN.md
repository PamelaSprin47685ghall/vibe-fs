  mv(from, to) {
    // 移动或重命名文件或目录（POSIX mv），允许覆盖，源缺失是错误；跨设备（EXDEV）报错并回滚，不留两侧并存的中间态。
    // 例如: this.mv("src/old.js", "src/new.js");
  }
