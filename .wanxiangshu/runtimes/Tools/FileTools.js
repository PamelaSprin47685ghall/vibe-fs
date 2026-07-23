
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { object, string, field, fromString } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { Auto_generateBoxedDecoder_Z6670B51, fromString as fromString_1 } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { uncurry2 } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { statSync, writeFileSync, readFileSync, existsSync } from "fs";
import { replace, printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { Tool, ToolOutput } from "./ToolContext.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";

export function fileReadTool() {
    return new Tool("read", "Read file content from filesystem.", "{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"}},\"required\":[\"filePath\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let filePath;
            try {
                const matchValue = fromString((path_1, value_1) => field("filePath", string, path_1, value_1), input.Payload);
                if (matchValue.tag === 1) {
                    const matchValue_1 = fromString_1(uncurry2(Auto_generateBoxedDecoder_Z6670B51(string_type, undefined, undefined)), input.Payload);
                    filePath = ((matchValue_1.tag === 1) ? input.Payload : matchValue_1.fields[0]);
                }
                else {
                    filePath = matchValue.fields[0];
                }
            }
            catch (matchValue_2) {
                filePath = input.Payload;
            }
            if (!existsSync(filePath)) {
                return builder$0040.Return(new ToolOutput(toText(printf("File not found: %s"))(filePath), false));
            }
            else {
                const content = readFileSync(filePath, "utf8");
                return builder$0040.Return(new ToolOutput(content, false));
            }
        }));
    });
}

export function fileWriteTool() {
    return new Tool("write", "Write file content to filesystem.", "{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"filePath\",\"content\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let parsedOpt;
            try {
                const matchValue = fromString((path_3, v) => object((get$) => {
                    let objectArg, objectArg_1;
                    return [(objectArg = get$.Required, objectArg.Field("filePath", string)), (objectArg_1 = get$.Required, objectArg_1.Field("content", string))];
                }, path_3, v), input.Payload);
                parsedOpt = ((matchValue.tag === 1) ? undefined : matchValue.fields[0]);
            }
            catch (matchValue_1) {
                parsedOpt = undefined;
            }
            if (parsedOpt != null) {
                const filePath = parsedOpt[0];
                const content = parsedOpt[1];
                writeFileSync(filePath, content, "utf8");
                const stat = statSync(filePath);
                const size = ((Operators_IsNull(stat) ? true : Operators_IsNull(stat.size)) ? content.length : stat.size) | 0;
                return builder$0040.Return(new ToolOutput(toText(printf("Wrote %s (%d bytes)"))(filePath)(size), false));
            }
            else {
                return builder$0040.Return(new ToolOutput(toText(printf("Failed to parse JSON payload for write tool: %s"))(input.Payload), false));
            }
        }));
    });
}

export function fileEditTool() {
    return new Tool("edit", "Edit file content in filesystem using exact string replacement.", "{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"},\"oldString\":{\"type\":\"string\"},\"newString\":{\"type\":\"string\"}},\"required\":[\"filePath\",\"oldString\",\"newString\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let parsedOpt;
            try {
                const matchValue = fromString((path_4, v) => object((get$) => {
                    let objectArg, objectArg_1, objectArg_2;
                    return [(objectArg = get$.Required, objectArg.Field("filePath", string)), (objectArg_1 = get$.Required, objectArg_1.Field("oldString", string)), (objectArg_2 = get$.Required, objectArg_2.Field("newString", string))];
                }, path_4, v), input.Payload);
                parsedOpt = ((matchValue.tag === 1) ? undefined : matchValue.fields[0]);
            }
            catch (matchValue_1) {
                parsedOpt = undefined;
            }
            if (parsedOpt != null) {
                const oldString = parsedOpt[1];
                const newString = parsedOpt[2];
                const filePath = parsedOpt[0];
                if (!existsSync(filePath)) {
                    return builder$0040.Return(new ToolOutput(toText(printf("File not found: %s"))(filePath), false));
                }
                else {
                    const content = readFileSync(filePath, "utf8");
                    if (!(content.indexOf(oldString) >= 0)) {
                        return builder$0040.Return(new ToolOutput(toText(printf("oldString not found in file %s"))(filePath), false));
                    }
                    else {
                        writeFileSync(filePath, replace(content, oldString, newString), "utf8");
                        return builder$0040.Return(new ToolOutput(toText(printf("Edited %s"))(filePath), false));
                    }
                }
            }
            else {
                return builder$0040.Return(new ToolOutput(toText(printf("Invalid edit payload: %s"))(input.Payload), false));
            }
        }));
    });
}

