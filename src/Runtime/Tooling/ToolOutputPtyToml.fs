module Wanxiangshu.Runtime.Tooling.ToolOutputPtyToml

open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

type PtySpawnInfo =
    { id: string
      title: string
      command: string
      workdir: string
      pid: int
      status: string
      notifyOnExit: bool
      timeoutSeconds: string
      message: string }

let ptySpawnDocument (info: PtySpawnInfo) : TomlValue =
    Table
        [ "message", String info.message
          "id", String info.id
          "title", String info.title
          "command", String info.command
          "workdir", String info.workdir
          "pid", Integer info.pid
          "status", String info.status
          "notify_on_exit", Boolean info.notifyOnExit
          "timeout_seconds", String info.timeoutSeconds ]

let renderPtySpawn (info: PtySpawnInfo) : string = ptySpawnDocument info |> stringify

type PtySessionItem =
    { id: string
      title: string
      command: string
      status: string
      pid: int
      lineCount: int }

type PtyListInfo =
    { count: int
      sessions: PtySessionItem list }

let ptyListDocument (info: PtyListInfo) : TomlValue =
    let tables =
        info.sessions
        |> List.map (fun s ->
            [ "id", String s.id
              "title", String s.title
              "command", String s.command
              "status", String s.status
              "pid", Integer s.pid
              "lines", Integer s.lineCount ])

    Table [ "count", Integer info.count; "sessions", TableArray tables ]

let renderPtyList (info: PtyListInfo) : string = ptyListDocument info |> stringify

type PtyKillInfo =
    { id: string
      action: string
      cleanup: bool
      title: string
      command: string
      status: string
      finalLineCount: int
      note: string
      message: string }

let ptyKillDocument (info: PtyKillInfo) : TomlValue =
    Table
        [ "message", String info.message
          "id", String info.id
          "action", String info.action
          "cleanup", Boolean info.cleanup
          "title", String info.title
          "command", String info.command
          "status", String info.status
          "final_line_count", Integer info.finalLineCount
          "note", String info.note ]

let renderPtyKill (info: PtyKillInfo) : string = ptyKillDocument info |> stringify

type PtyReadInfo =
    { id: string
      status: string
      offset: int
      returned: int
      totalLines: int
      hasMore: bool
      pattern: string option
      totalMatches: int option
      lines: string list
      continuationHint: string option }

let ptyReadDocument (info: PtyReadInfo) : TomlValue =
    let mutable fields =
        [ "id", String info.id
          "status", String info.status
          "offset", Integer info.offset
          "returned", Integer info.returned
          "total_lines", Integer info.totalLines
          "has_more", Boolean info.hasMore ]

    match info.pattern with
    | Some p -> fields <- fields @ [ "pattern", String p ]
    | None -> ()

    match info.totalMatches with
    | Some tm -> fields <- fields @ [ "total_matches", Integer tm ]
    | None -> ()

    match info.continuationHint with
    | Some h -> fields <- fields @ [ "continuation_hint", String h ]
    | None -> ()

    fields <- fields @ [ "lines", StringArray info.lines ]
    Table fields

let renderPtyRead (info: PtyReadInfo) : string = ptyReadDocument info |> stringify

type PtyWriteInfo =
    { id: string
      display: string
      bytes: int
      status: string
      message: string }

let ptyWriteDocument (info: PtyWriteInfo) : TomlValue =
    Table
        [ "message", String info.message
          "id", String info.id
          "display", String info.display
          "bytes", Integer info.bytes
          "status", String info.status ]

let renderPtyWrite (info: PtyWriteInfo) : string = ptyWriteDocument info |> stringify
