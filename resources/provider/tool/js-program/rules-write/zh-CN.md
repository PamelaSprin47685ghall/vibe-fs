write(path, content) 覆盖已有 UTF-8 文件，或创建缺失文件：一次原子写，
即写即生效（as-is）。若目标已经过 cat 或 file 读取，且盘上内容自该快照
以来已变：FILE_CHANGED。Edit 与 Write 仍是不同的 capability。

mkdir(path) 只建一个目录（POSIX，非递归）：已存在报 DIRECTORY_EXISTS，
父目录缺失报 PARENT_DIRECTORY_MISSING。

查找替换是组合，不是原语：
  const text = await this.cat(path);
  const [begin, end] = await this.where(text, "old");
  this.write(path, text.slice(0, begin) + "new" + text.slice(end));
批量替换 = 一次 whereAll 加反向循环。append = cat + write。
