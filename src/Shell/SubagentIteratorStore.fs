module Wanxiangshu.Shell.SubagentIteratorStore

open Wanxiangshu.Kernel.HostTools

type SubagentIteratorItem =
    { childID: string
      agent: string
      host: Host }

[<Literal>]
let subagentIteratorNamespace = "sai_s"

type SubagentIteratorStore =
    private
        { mutable iterators: Map<string, SubagentIteratorItem>
          mutable counter: int
          maxIterators: int }

let createSubagentIteratorStore (max: int) : SubagentIteratorStore =
    let limit = if max > 0 then max else 50

    { iterators = Map.empty
      counter = 0
      maxIterators = limit }

let private nextId (store: SubagentIteratorStore) (scope: string) : string =
    store.counter <- store.counter + 1
    let suffix = string store.counter
    let cleanScope = if isNull scope then "global" else scope.Trim()

    if cleanScope = "global" || cleanScope = "default" || cleanScope = "" then
        subagentIteratorNamespace + suffix
    else
        let ns = subagentIteratorNamespace
        cleanScope + ":" + ns + ":" + suffix

let private trim (max: int) (m: Map<string, 't>) : Map<string, 't> =
    if Map.count m > max then
        let firstKey = m |> Map.toSeq |> Seq.head |> fst
        Map.remove firstKey m
    else
        m

let parseSelfContainedIterator (id: string) : SubagentIteratorItem option =
    let parts = if isNull id then [||] else id.Split(':')

    if parts.Length = 4 && parts.[0] = "sci_s" then
        let childID = parts.[1]
        let agent = parts.[2]

        let host =
            match parts.[3] with
            | "Opencode" -> Opencode
            | "Mux" -> Mux
            | "Mimocode" -> Mimocode
            | "Omp" -> Omp
            | _ -> Opencode

        Some
            { childID = childID
              agent = agent
              host = host }
    else
        None

let toSelfContainedIterator (item: SubagentIteratorItem) : string =
    let hostStr =
        match item.host with
        | Opencode -> "Opencode"
        | Mux -> "Mux"
        | Mimocode -> "Mimocode"
        | Omp -> "Omp"

    let cid = item.childID
    let ag = item.agent
    "sci_s:" + cid + ":" + ag + ":" + hostStr

let storeSubagentIterator (store: SubagentIteratorStore) (scopeId: string) (item: SubagentIteratorItem) : string =
    toSelfContainedIterator item

let preserveSubagentIterator (store: SubagentIteratorStore) (id: string) (item: SubagentIteratorItem) : unit = ()

let consumeSubagentIterator (store: SubagentIteratorStore) (id: string) : SubagentIteratorItem option =
    let cleanId = if isNull id then "" else id.Trim()
    parseSelfContainedIterator cleanId

let clearSubagentIteratorScope (store: SubagentIteratorStore) (scopeId: string) : unit = ()

let clearSubagentIteratorStore (store: SubagentIteratorStore) : unit = ()
