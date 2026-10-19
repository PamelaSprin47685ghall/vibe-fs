# SM-03 施工证据

固定输入与状态以[施工计划](../../proposals/extraction-kanban/UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)为准。SM-03本批范围READY，整包TODO及监督边界保留，不代表整批交付或部署。实际日志名均带`vibe-fs-sm03-`前缀。

- `build-01` 因独立worktree写权限失败，未编译；`build-02` 为首轮接口编译红；`build-03` 源码通过但测试冲突标记未解除；`build-04` 为退役surface清单残留；`build-05` 正式构建generation1通过，1510编译项、171surface、825模块闭包。
- `regression-01` 和 `storage-work-01` 为正式监督失败，5秒静默门禁与排空失败均保留；没有完整计数。后者当前阻塞为真实长Fable编译，不能把原生通过替代它。
- `storage-work-native-01` 为同一生产产物的84文件原生回归，474项452pass/11fail/11TODO。`counterexamples-02` 定向178项168pass/6fail/4TODO；剩余6项Journal失败修正测试根后，`journal-024-03` 为23项21pass/0fail/2TODO。不是一轮全绿，历史TODO仍在。
- `focused-native-01` 为68项64pass/1fail/3TODO；该Git错误前提随后按实际命令修正，见counterexamples。`git-fixtures-02`、`state-root-01` 是生成输入新鲜度拒绝，未执行测试。
- `distribution-011`、`-02`、`-03` 是逐步增加反例的定向运行，最终3pass/0fail/0TODO；只证明源码固定与不可改写证据例外，不证明完整npm pack。
- `build-06`为generation2构建通过。`scope-formal-03`为8文件正式32pass/0fail/0skip/0TODO，全部排空且进程组回收accepted=true。`targeted-formal-02`为11文件72pass/0fail/5TODO；`change-formal-02`为14文件202pass/0fail/5TODO；`crash-formal-02`为21文件55pass/0fail/18TODO，三组均排空、进程组回收accepted=true，但因既有TODO正式入口返回失败，不记为整包通过。选集重叠，不合计数量。

日志按原字节保存，旧红不覆盖。后续验收增加新文件，并更新计划中的证据与剩余边界。
