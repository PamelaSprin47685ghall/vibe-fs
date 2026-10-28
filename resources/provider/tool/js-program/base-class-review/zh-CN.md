  async review(findings, note) {
    // 提交评审发现，findings 为 { key, value: string | null } 数组：每个 key 命名一条义务，value 是它的剩余工作，null 删除该键，[] 不改动任何义务。
    // 义务全部清空时任期接力：接任者第一次评审无义务即推进里程碑；初任无义务时系统向用户提问。返回数据结果。
    // Reviewed 已记录；AwaitReview 表示可受理下一次评审，不是需要 join() 等待的结论。
    // note（可选字符串）：这次评审启动 Manager 时，note 就是它的标题和完整底稿；不传则发送已保存的底稿。
    // 例如: const result = await this.review([{ key: "gap-1", value: "补充测试" }]);
  }
