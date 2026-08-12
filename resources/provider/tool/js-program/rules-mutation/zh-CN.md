一个 program 对每个 canonical path 只能 mutation 恰好一次。对同一路径的第二次 rewrite/write
是 DUPLICATE_MUTATION_TARGET。多阶段编辑应放在
JavaScript 变量里，然后一次 rewrite/write。

生成的 class 没有 commit、rollback、snapshot 或 transaction 方法。
run() 正常返回 → Host preflight → prepare → commit。run() 抛出或
任何 file()/glob()/grep() 失败都会丢弃全部已暂存 mutation。

run() 必须返回 JSON 兼容值：null、boolean、有限 number、string、
array 或 plain object（递归）。undefined、BigInt、NaN、Infinity、function、
symbol、循环或奇异对象在 commit 前失败为 INVALID_RETURN_VALUE。
