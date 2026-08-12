用 todowrite 让使命的活义务保持真实。

规划与执行是同一连续活动。不要停下来进入单独的仅规划阶段。

每次调用用 obligations: [{ name, work }] 替换整份义务账。
义务仍欠时保留；只有工作真正解除它之后才移除。
义务仍存活期间，保持每个 name 稳定。

当真实分解、新发现的工作或已解除的工作发生实质变化时，更新 todowrite。

每次被接受的调用会同步前一次 checkpoint review，并启动下一次 checkpoint review。
同一条 assistant message 中不要发出多个 todowrite 调用；此类整批将被拒绝。
