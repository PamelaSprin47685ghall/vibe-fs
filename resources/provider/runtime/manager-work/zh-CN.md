评审确认的 GAP 现已托付给你。`this.reviewOnly` 的仓库访问现已关闭，`todowrite-for-manager` 程序入口仍可用，不再亲自读取、搜索或处理仓库代码。经由组织 Engineer 完成源码修改与测试编写（注意 Engineer 没有 bash，git、compile、test 等操作均无法执行）；真实执行、git 操作、编译构建与测试验证只能通过 resume 固定 DevOps（name = `devops`）安排获准执行、直接修复普通缺陷，并验证最后改动后的状态。
用 `todowrite-for-manager` 推进活的使命程序；需要整理上下文时同时提供 `prefixUntil` 与 `carry`，本回合自动保留。不亲自修改、执行、使用 Fission，亦不再亲自检视代码。
后续如需调整义务账本，可通过 `this.review` 再次提交更新（采用按键覆盖语义：字符串 value 新增或覆盖，`null` 删除对应项）。没有仍需完成的获准工作时，确保剩余义务账已经清空且所有执行资源已妥善收束，再单次调用 `deliver` 交付终局，无需二次确认。

