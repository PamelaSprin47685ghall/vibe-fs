namespace Wanxiangshu.Next.Session

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Kernel.Identity

type ProjectionSnapshot = string

module CompanionDelta =
    let private deterministicPatch (previous: obj) (current: obj) : string =
        emitJsExpr
            (previous, current)
            """
            (function (previous, current) {
                function canonical(value) {
                    if (Array.isArray(value)) return value.map(canonical);
                    if (value !== null && typeof value === "object") {
                        const result = {};
                        Object.keys(value).sort().forEach(function (key) {
                            result[key] = canonical(value[key]);
                        });
                        return result;
                    }
                    return value;
                }

                function isObject(value) {
                    return value !== null && typeof value === "object" && !Array.isArray(value);
                }

                function pointer(base, key) {
                    return base + "/" + String(key).replace(/~/g, "~0").replace(/\//g, "~1");
                }

                const operations = [];

                function walk(oldValue, newValue, path, oldPresent, newPresent) {
                    if (!oldPresent && !newPresent) return;
                    if (!oldPresent) {
                        operations.push({ op: "add", path: path, value: canonical(newValue) });
                        return;
                    }
                    if (!newPresent) {
                        operations.push({ op: "remove", path: path });
                        return;
                    }
                    if (JSON.stringify(canonical(oldValue)) === JSON.stringify(canonical(newValue))) return;

                    if (Array.isArray(oldValue) && Array.isArray(newValue)) {
                        const common = Math.min(oldValue.length, newValue.length);
                        for (let index = 0; index < common; index += 1) {
                            walk(oldValue[index], newValue[index], pointer(path, index), true, true);
                        }
                        for (let index = oldValue.length - 1; index >= newValue.length; index -= 1) {
                            operations.push({ op: "remove", path: pointer(path, index) });
                        }
                        for (let index = common; index < newValue.length; index += 1) {
                            operations.push({
                                op: "add",
                                path: pointer(path, index),
                                value: canonical(newValue[index])
                            });
                        }
                        return;
                    }

                    if (isObject(oldValue) && isObject(newValue)) {
                        const keys = Array.from(
                            new Set(Object.keys(oldValue).concat(Object.keys(newValue)))
                        ).sort();
                        for (const key of keys) {
                            walk(
                                oldValue[key],
                                newValue[key],
                                pointer(path, key),
                                Object.prototype.hasOwnProperty.call(oldValue, key),
                                Object.prototype.hasOwnProperty.call(newValue, key)
                            );
                        }
                        return;
                    }

                    operations.push({ op: "replace", path: path, value: canonical(newValue) });
                }

                walk(previous, current, "", true, true);
                return JSON.stringify({ ops: operations });
            })($0, $1)
            """

    let jsonDelta (previous: ProjectionSnapshot option) (current: ProjectionSnapshot) : ProjectionSnapshot option =
        match previous with
        | None -> Some current
        | Some prev ->
            try
                let patch =
                    deterministicPatch (Fable.Core.JS.JSON.parse prev) (Fable.Core.JS.JSON.parse current)

                if patch = "{\"ops\":[]}" then None else Some patch
            with _ ->
                Some current


    let private textOfMessage (message: obj) : string =
        try
            if isNull message then
                ""
            elif not (isNull message?parts) then
                let parts = unbox<obj array> message?parts

                parts
                |> Array.choose (fun part ->
                    if isNull part then
                        None
                    elif not (isNull part?text) then
                        Some(unbox<string> part?text)
                    else
                        None)
                |> String.concat ""
            elif not (isNull message?text) then
                unbox<string> message?text
            else
                ""
        with _ ->
            ""

    let private hasPromptKeyMetadata (message: obj) : bool =
        try
            if isNull message || isNull message?info then
                false
            elif
                not (isNull message?info?metadata)
                && not (isNull message?info?metadata?wanxiangshu_prompt_key)
            then
                true
            elif
                not (isNull message?metadata)
                && not (isNull message?metadata?wanxiangshu_prompt_key)
            then
                true
            else
                false
        with _ ->
            false

    /// Guard / zero-width / PromptKey continuations are transport, not semantic delta.
    let isBareContinuationMessage (message: obj) : bool =
        try
            if isNull message then
                true
            else
                let role =
                    if not (isNull message?info) && not (isNull message?info?role) then
                        unbox<string> message?info?role
                    elif not (isNull message?role) then
                        unbox<string> message?role
                    else
                        ""

                if not (role.Equals("user", System.StringComparison.OrdinalIgnoreCase)) then
                    false
                elif hasPromptKeyMetadata message then
                    true
                else
                    let text = textOfMessage message
                    let normalized = text.Replace("\u200B", "").Trim()
                    String.IsNullOrWhiteSpace normalized
        with _ ->
            false

    let semanticMessages (messages: obj list) : obj list =
        messages |> List.filter (fun message -> not (isBareContinuationMessage message))

    let jsonOfMessages (canonicalJson: obj -> string) (messages: obj list) : string =
        // Full projection baseline must be the same canonical bytes as
        // Projection.canonicalJson — not insertion-order JSON.stringify.
        // Bare continuation user messages never form a semantic delta alone.
        canonicalJson (List.toArray (semanticMessages messages))
