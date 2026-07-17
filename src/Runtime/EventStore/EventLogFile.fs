module Wanxiangshu.Runtime.EventLogFile

open Wanxiangshu.Runtime.FileSys

let eventLogFileName = ".wanxiangshu.ndjson"

let eventPath (workspaceRoot: string) : string = resolve workspaceRoot eventLogFileName

let lockFileName = ".wanxiangshu.ndjson.lock"
