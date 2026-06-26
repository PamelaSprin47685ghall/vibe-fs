module Wanxiangshu.Kernel.KnowledgeGraph.Types

open System

type KnowledgeGraphId = KnowledgeGraphId of string

type KnowledgeGraphEntry = { id: KnowledgeGraphId; entity: string list; fact: string }

type KnowledgeGraphDraft = { id: string option; entity: string list; fact: string }

type KnowledgeGraphHeader =
    | DayHeader of date: string * rewritten: bool

type KnowledgeGraphFile = { header: KnowledgeGraphHeader; entries: KnowledgeGraphEntry list }

type KnowledgeGraphProjection = Map<KnowledgeGraphId, KnowledgeGraphEntry>

type KnowledgeGraphJobKind =
    | AppendAfterWork
    | DailyRewrite of date: string

type KnowledgeGraphJobContext =
    { workspaceRoot: string
      kind: KnowledgeGraphJobKind }
