
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { SessionIdModule_create } from "../Kernel/Identity.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { ToolInput, ToolContext, SessionInboxCommandPort_$ctor_Z4592CC48 } from "../Tools/ToolContext.js";
import { PluginRuntime__get_CancellationToken, PluginRuntime__get_Directory, PluginRuntime__EnsureSessionDriver_2C0A04B3 } from "./PluginRuntime.js";
import { DeadlineModule_ofBudget } from "../Process/Deadline.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { fromSeconds } from "../fable_modules/fable-library-js.5.6.0/TimeSpan.js";
import { StaticTools_executorTool, StaticTools_todowriteTool } from "../Tools/StaticTools.js";
import { fileEditTool, fileWriteTool, fileReadTool } from "../Tools/FileTools.js";

export function buildToolsObject(rt) {
    const makeToolObj = (tool_1) => ({
        description: tool_1.Description,
        execute: (args_1, context_1) => {
            const context = context_1;
            const builder$0040 = task();
            return builder$0040.Run(builder$0040.Delay(() => {
                const sId = SessionIdModule_create((!Operators_IsNull(context) && !Operators_IsNull(context.sessionID)) ? context.sessionID : ((!Operators_IsNull(context) && !Operators_IsNull(context.sessionId)) ? context.sessionId : ""));
                const port = SessionInboxCommandPort_$ctor_Z4592CC48(PluginRuntime__EnsureSessionDriver_2C0A04B3(rt, sId).Inbox);
                const payloadStr = JSON.stringify(args_1);
                const ctx = new ToolContext(sId, PluginRuntime__get_Directory(rt), PluginRuntime__get_CancellationToken(rt), DeadlineModule_ofBudget(utcNow(), fromSeconds(30)), port);
                return builder$0040.Bind(tool_1.Execute(ctx, new ToolInput(payloadStr)), (_arg) => {
                    const out = _arg;
                    return builder$0040.Return({
                        output: out.Result,
                        result: out.Result,
                    });
                });
            }));
        },
        parameters: JSON.parse(tool_1.SchemaJson),
    });
    const todoT = StaticTools_todowriteTool();
    const execT = StaticTools_executorTool();
    const readT = fileReadTool();
    const writeT = fileWriteTool();
    const editT = fileEditTool();
    return {
        todowrite: makeToolObj(todoT),
        executor: makeToolObj(execT),
        read: makeToolObj(readT),
        write: makeToolObj(writeT),
        edit: makeToolObj(editT),
    };
}

