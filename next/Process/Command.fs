namespace Wanxiangshu.Next.Process

open System

type Command =
    { FileName: string
      Arguments: string list
      WorkingDirectory: string option
      Environment: Map<string, string> option
      Stdin: string option
      Deadline: Deadline option }
