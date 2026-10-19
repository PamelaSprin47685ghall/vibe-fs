mv(from, to) — 移动或重命名文件或目录（POSIX mv）；支持覆盖，源缺失报错。继承编程面保护语义：resolveInside 路径边界、写前 FILE_CHANGED、durable 单笔记账。EXDEV 跨设备移动必须报错并回滚，不得保留两侧并存的中间态。
