  async fission(payload) {
    // 返回 { ok, groupId, lanes }: 同一身份分几条道做同一份差事，宿主负责绑定与收敛；本组的道由 join() 收。
    // 例如: const group = await this.fission({ charge: "探索三种设计", lanes: 3 });
  }
