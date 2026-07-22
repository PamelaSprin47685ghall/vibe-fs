namespace Wanxiangshu.Next.Journal

open System
open System.Text.Json.Nodes

module FactDecodersHelpers =

    let tryGetProperty (name: string) (node: JsonNode) : JsonNode option =
        if obj.ReferenceEquals(node, null) then
            None
        else
            try
                let objNode = node.AsObject()

                if not (obj.ReferenceEquals(objNode, null)) && objNode.ContainsKey(name) then
                    Some objNode.[name]
                else
                    None
            with _ ->
                None

    let tryStr (name: string) (node: JsonNode) : Result<string, string> =
        match tryGetProperty name node with
        | Some n ->
            (try
                Ok(n.GetValue<string>())
             with ex ->
                 Error(sprintf "Failed parsing string property '%s': %s" name ex.Message))
        | None -> Error(sprintf "Missing required string property '%s'" name)

    let tryInt (name: string) (node: JsonNode) : Result<int, string> =
        match tryGetProperty name node with
        | Some n ->
            (try
                Ok(n.GetValue<int>())
             with ex ->
                 Error(sprintf "Failed parsing int property '%s': %s" name ex.Message))
        | None -> Error(sprintf "Missing required int property '%s'" name)
