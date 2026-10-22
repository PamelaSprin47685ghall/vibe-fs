可选的调用名。每次 park 分配带此名称的唯一 handle；unpark 与 abandon 使用返回的 handle。显式空名及已挂起的同名在执行前拒绝；复用名称不会让已消费的 handle 再次生效。
