grep(needle, pattern = "**/*") 在与 glob() 同一套 gitignore 风格选出的
UTF-8 文件上搜索。needle 为非空字符串（字面量）或 RegExp
（忽略调用方 g/y/lastIndex）。不可读或非 UTF-8 文件被跳过。
返回 { matches: [{ path, line, column, text }] }。line 与
column 从 1 起算。text 为匹配子串。grep 不授予 file()。
