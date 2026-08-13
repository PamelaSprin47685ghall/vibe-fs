用 {"obligations":[{"name":"稳定且可读的名称","work":"为了真正满足用户请求仍必须成为真的事项，以及证明它已经为真的证据"}]} 替换 mission 的完整 living obligation account。
每个 obligation 必须包含非空且在本 account 唯一的 "name"，以及具体仍欠的 "work"。
Obligation 是 mission debt，不是你对自己思考过程的备忘。不要写「先做计划」「分析请求」「列 todo」「决定下一步」之类 meta-obligation；这些认知动作直接完成。
把规划伪装成调查，并不会让它变成 mission debt。如果「调查仓库」「追踪启动路径」「寻找热点」「理解架构」「盘点风险」之类工作只是为了弄清真正 obligations 应该是什么，就应当在本次调用之前直接完成，而不是写进 account。
使用完成反事实测试：如果某项工作即使被完美完成，用户真正要求的世界状态或交付物仍然完全没变，而唯一结果只是你更理解了、得到一份清单、得到一个计划、或知道下一步，那么它就不是 obligation。只有当调查/分析/报告本身就是用户要求的交付物时，它才可以成为 obligation；否则应写最终必须成为真的结果，以及证明该结果所需的证据。
如果这是一个新 Manager Life 的第一次 todowrite，它就是你在 Planning Table 已经完成的整份计划。不要仅仅为了宣布「现在开始规划」而调用 todowrite，也不要用「survey-startup-and-complexity」之类调查名称包装这种占位调用。
只要 obligation 仍未解除就保留它；只有真实工作已经完成该义务后才移除。
每次 accepted call 立即成为当前 obligation account，同时同步前一个 process review，并启动下一次 checkpoint review。
同一个 assistant message 中不得发出多个 todowrite 调用；出现时整批拒绝。
