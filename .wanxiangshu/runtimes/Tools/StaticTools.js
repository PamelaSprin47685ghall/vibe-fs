
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { ofArray, length, empty } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { field, string, list as list_2, object, fromString } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { Auto_generateBoxedDecoder_Z6670B51, fromString as fromString_1 } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { equals, uncurry2 } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { list_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { TodoSnapshot } from "../Kernel/Fact.js";
import { SessionCommand, SessionCommandResult } from "../Session/Inbox.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { Tool, ToolOutput } from "./ToolContext.js";
import { platform } from "process";
import { Command } from "../Process/Command.js";
import { DeadlineModule_remaining } from "../Process/Deadline.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { ProcessContext } from "../Process/ProcessTypes.js";
import { execute, runFlow } from "../Process/ProcessFlow.js";
import { ChildRequest, ChildFlows_runChild } from "../Session/ChildFlows.js";
import { Flow_run } from "../Kernel/Flow.js";

export function StaticTools_todowriteTool() {
    return new Tool("todowrite", "Update task todo snapshot, report progress, and methodology.", "{\"type\":\"object\",\"properties\":{\"todos\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"todos\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let items;
            try {
                const matchValue = fromString((path_2, v) => object((get$) => {
                    const objectArg = get$.Required;
                    return objectArg.Field("todos", (path_1, value_1) => list_2(string, path_1, value_1));
                }, path_2, v), input.Payload);
                if (matchValue.tag === 1) {
                    const matchValue_1 = fromString_1(uncurry2(Auto_generateBoxedDecoder_Z6670B51(list_type(string_type), undefined, undefined)), input.Payload);
                    items = ((matchValue_1.tag === 1) ? empty() : matchValue_1.fields[0]);
                }
                else {
                    items = matchValue.fields[0];
                }
            }
            catch (matchValue_2) {
                items = empty();
            }
            const snap = new TodoSnapshot(items);
            let replyVal = new FSharpResult$2(0, [new SessionCommandResult(0, [])]);
            return builder$0040.Bind(ctx.Session.Request(new SessionCommand(0, [snap, (r) => {
                replyVal = r;
            }]), ctx.Cancellation, ctx.Deadline), (_arg) => {
                let arg_2;
                const res = _arg;
                return (res.tag === 1) ? builder$0040.Return(new ToolOutput(toText(printf("Failed: %A"))(res.fields[0]), false)) : builder$0040.Return(new ToolOutput((arg_2 = (length(items) | 0), toText(printf("Updated %d todo items"))(arg_2)), false));
            });
        }));
    });
}

export function StaticTools_executorTool() {
    return new Tool("executor", "Execute shell command within timeout budget.", "{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"}},\"required\":[\"command\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            let cmdText;
            try {
                const matchValue = fromString((path_1, value_1) => field("command", string, path_1, value_1), input.Payload);
                if (matchValue.tag === 1) {
                    const matchValue_1 = fromString_1(uncurry2(Auto_generateBoxedDecoder_Z6670B51(string_type, undefined, undefined)), input.Payload);
                    cmdText = ((matchValue_1.tag === 1) ? input.Payload : matchValue_1.fields[0]);
                }
                else {
                    cmdText = matchValue.fields[0];
                }
            }
            catch (matchValue_2) {
                cmdText = input.Payload;
            }
            const isWindows = equals(platform, "win32");
            const cmd = new Command(isWindows ? "cmd.exe" : "sh", ofArray([isWindows ? "/c" : "-c", cmdText]), undefined, undefined, undefined, ctx.Deadline, undefined);
            const procCtx = new ProcessContext(undefined, DeadlineModule_remaining(utcNow, ctx.Deadline));
            return builder$0040.Bind(runFlow(procCtx, ctx.Cancellation, execute(cmd)), (_arg) => {
                const res = _arg;
                if (res.tag === 1) {
                    return builder$0040.Return(new ToolOutput(toText(printf("Error: %A"))(res.fields[0]), false));
                }
                else {
                    const procRes = res.fields[0];
                    const resultText = toText(printf("Exit: %d\nStdout: %s\nStderr: %s"))(procRes.ExitCode)(procRes.Stdout)(procRes.Stderr);
                    return builder$0040.Return(new ToolOutput(resultText, procRes.StdoutTruncated ? true : procRes.StderrTruncated));
                }
            });
        }));
    });
}

export function StaticTools_subagentTool(name, role, script) {
    return new Tool(name, toText(printf("Spawn subagent %s for %s"))(name)(role), "{\"type\":\"object\",\"properties\":{\"prompt\":{\"type\":\"string\"}},\"required\":[\"prompt\"]}", (ctx, input) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(ctx.Cancellation);
            const flow = ChildFlows_runChild(script, new ChildRequest((() => {
                try {
                    const matchValue = fromString((path_1, value_1) => field("prompt", string, path_1, value_1), input.Payload);
                    return (matchValue.tag === 1) ? input.Payload : matchValue.fields[0];
                }
                catch (matchValue_1) {
                    return input.Payload;
                }
            })()));
            return builder$0040.Bind(Flow_run(script, ctx.Cancellation, flow), (_arg) => {
                const res = _arg;
                return (res.tag === 1) ? builder$0040.Return(new ToolOutput(toText(printf("Subagent %s flow error: %A"))(name)(res.fields[0]), false)) : ((res.fields[0].tag === 1) ? builder$0040.Return(new ToolOutput(toText(printf("Subagent %s failed: %s"))(name)(res.fields[0].fields[0]), false)) : builder$0040.Return(new ToolOutput(toText(printf("Subagent %s completed: %s"))(name)(res.fields[0].fields[0]), false)));
            });
        }));
    });
}

