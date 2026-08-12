用 {"obligations":[{"name":"稳定且可读的名称","work":"仍然欠下的工作与验证要求"}]} 替换 mission 的完整 living obligation account。
每个 obligation 必须包含 "name"（非空、列表中唯一）与 "work"（具体欠缺的工作与验证事实）。
只要 obligation 仍未解除就保留它；只有真实工作已经完成该义务后才移除。
每次 accepted call 会同步前一个 process review，并启动下一次 checkpoint review。
同一个 assistant message 中不得发出多个 todowrite 调用；出现时整批拒绝。
