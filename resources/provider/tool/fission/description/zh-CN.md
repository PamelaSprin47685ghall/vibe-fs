暂时把同一个 logical participant 展开成若干对等 execution presents。

只有当本 agent 正运行在 subsession 中，且你自己承接的工作里存在多个真正可分离、并行能降低延迟的切片时，才使用 fission。user-facing/root session 不得 fission。应裂分可分离工作，而不是仅仅因为工作很多就裂分。

prompts 是一个字符串：每个非空行对应一条 lane，至少两条；每一行都会原样成为该 lane 的本地 charge。

Fission 不是把工作委托给新的 agent。所有 lanes 保持同一 logical identity、office、authority、parent relation、child ownership 与共享 worktree。裂变前已经在外面的工作属于共享既有债权；裂变后某 lane 新发起的工作，其 completion 只归该 lane。

只有全部 lanes 都建立成功后，当前 physical present 才会被替换。你的 parent 仍只观察到一个 logical participant 和一次最终 completion。
