grep(needle, pattern = "**/*") 在同一套 gitignore 风格 glob 选出的
UTF-8 文件上搜索。needle 是非空 string（字面量）或 RegExp
（忽略调用方 g/y/lastIndex）。不可读或非 UTF-8 文件被跳过。
返回 { matches: [{ path, line, column, text }], truncated }。line 与
column 从 1 起算。text 是匹配子串。界限打在匹配
条数。grep 不授予 file()。
