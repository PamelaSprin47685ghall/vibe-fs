fork(payload) — 把一项任务委托给子工作。payload 字段与宿主 fork 工具一致：
`charge`（必填，任务正文）、`name`（稳定的子工作名）、`calling`（由哪个角色承接）
与 `keywords`。调用返回 fork 回执文本；子工作在独立会话中运行，终值由 join 收拢。

join() — 等待本会话创建的子工作并返回 join 收执批次。按成员归属收拢：没有 handle
列表可传。可以先 fork 若干任务，再按波次 join 一次；没有待收子工作时 join 返回空收执。

两个成员与宿主委托契约完全一致：失败以回执文本返回而不是抛出，程序里自行读取。
只有持有 Fork/Join capability 的 attempt 才有这两个成员。
