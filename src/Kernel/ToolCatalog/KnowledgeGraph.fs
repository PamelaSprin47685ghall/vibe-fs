module VibeFs.Kernel.ToolCatalog.KnowledgeGraph

open VibeFs.Kernel.ToolCatalog.ToolSpec

let internal fetchKnowledgeGraphSpec: ToolSpec =
    { name = "knowledge_graph_fetch"
      description = "Fetch facts for a knowledge graph entity from this session's knowledge graph snapshot. "
      paramDocs = map [ "entity", "Knowledge graph entity from the prelude." ]
      requiredFields = [ "entity" ] }

let internal submitKnowledgeGraphSpec: ToolSpec =
    { name = "return_bookkeeper"
      description =
        "Return knowledge graph draft entries for the current knowledge graph job context. Entries with an id update existing facts, and entries without an id receive a host-assigned id."
      paramDocs =
        map
            [ "entries",
              "Array of knowledge graph draft entries. Each entry: optional id, required entity (string array), required fact."
              "id", "Optional entry id; omit for new facts, set to update an existing fact."
              "entity", "Knowledge graph entity path segments."
              "fact", "Knowledge graph fact text." ]
      requiredFields = [ "entries" ] }
