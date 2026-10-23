  async review(findings) {
    // 提交评审结果，findings 为 { key, value: string | null } 数组。
    // 值为 null 删除该键；[] 不会清空既有台账。
    // 仅首次评审结果台账为空时确立晋级资格；后续调用只做逐键更新，不改变资格。返回数据结果。
    // 例如: const result = await this.review([{ key: "gap-1", value: "补充测试" }]);
  }
