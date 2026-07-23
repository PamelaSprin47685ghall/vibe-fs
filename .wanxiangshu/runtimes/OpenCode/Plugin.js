
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { OpencodeToolExecuteOutput, OpencodeToolExecuteInput, OpencodeHookInput, OpencodeModel } from "./OpencodeTypes.js";
import { toArray, empty, choose, ofArray, map } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { HostMessage, SessionSnapshot, MessageTransform_transform } from "../Tools/MessageTransform.js";
import { map as map_1 } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { toFail, join, isNullOrEmpty, printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { PluginRuntime__GetInboxMap, PluginRuntime__get_SessionDrivers, PluginRuntime__EnsureSessionDriver_2C0A04B3, PluginRuntime_start, PluginRuntime__GetOrCreateSessionRuntime_2C0A04B3, PluginRuntime__get_Gateway } from "./PluginRuntime.js";
import { SessionIdModule_create } from "../Kernel/Identity.js";
import { SessionInboxEvent } from "../Session/Inbox.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { OpenCodePort_create } from "./OpenCodePort.js";
import { handleToolExecuteBefore, handleEvent, handleChatMessage } from "./OpencodeHooks.js";
import { buildToolsObject } from "./PluginTools.js";

export class PluginConfig extends Record {
    constructor(Directory) {
        super();
        this.Directory = Directory;
    }
}

export function PluginConfig_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.PluginConfig", [], PluginConfig, () => [["Directory", string_type]]);
}

function Plugin_buildHookInput(inObj) {
    let m;
    if (Operators_IsNull(inObj) ? true : Operators_IsNull(inObj.model)) {
        m = undefined;
    }
    else {
        const mObj = inObj.model;
        m = (new OpencodeModel(mObj.providerID, mObj.modelID, Operators_IsNull(mObj.variant) ? undefined : mObj.variant));
    }
    return new OpencodeHookInput((Operators_IsNull(inObj) ? true : Operators_IsNull(inObj.sessionID)) ? "" : inObj.sessionID, (Operators_IsNull(inObj) ? true : Operators_IsNull(inObj.messageID)) ? undefined : inObj.messageID, (Operators_IsNull(inObj) ? true : Operators_IsNull(inObj.agent)) ? undefined : inObj.agent, m);
}

function Plugin_handleChatTransform(rt, outObj) {
    if (!Operators_IsNull(outObj)) {
        const jsMsgs = map((tm) => ({
            role: tm.Role,
            text: tm.Text,
        }), MessageTransform_transform(new SessionSnapshot(ofArray(["coder", "inspector", "browser", "meditator"]), map_1((v) => toText(printf("%A"))(v), PluginRuntime__get_Gateway(rt).ProjectionSet.LastReview), "Issue independent tool calls in parallel when possible."), choose((m) => {
            if (Operators_IsNull(m)) {
                return undefined;
            }
            else {
                return new HostMessage(Operators_IsNull(m.role) ? "" : m.role, Operators_IsNull(m.text) ? "" : m.text, undefined, undefined);
            }
        }, Operators_IsNull(outObj.messages) ? empty() : outObj.messages)));
        outObj.messages = jsMsgs;
    }
}

function Plugin_handleToolDefinition(outObj) {
    if (!Operators_IsNull(outObj)) {
        const staticToolsList = toArray(map((value) => value, ofArray([{
            description: "Update task todo snapshot, report progress, and methodology.",
            name: "todowrite",
            parameters: JSON.parse("{\"type\":\"object\",\"properties\":{\"todos\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"todos\"]}"),
        }, {
            description: "Read file content from filesystem.",
            name: "read",
            parameters: JSON.parse("{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"}},\"required\":[\"filePath\"]}"),
        }, {
            description: "Write file content to filesystem.",
            name: "write",
            parameters: JSON.parse("{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"filePath\",\"content\"]}"),
        }, {
            description: "Edit file content in filesystem using exact string replacement.",
            name: "edit",
            parameters: JSON.parse("{\"type\":\"object\",\"properties\":{\"filePath\":{\"type\":\"string\"},\"oldString\":{\"type\":\"string\"},\"newString\":{\"type\":\"string\"}},\"required\":[\"filePath\",\"oldString\",\"newString\"]}"),
        }, {
            description: "Execute shell command within timeout budget.",
            name: "executor",
            parameters: JSON.parse("{\"type\":\"object\",\"properties\":{\"command\":{\"type\":\"string\"}},\"required\":[\"command\"]}"),
        }])));
        outObj.tools = staticToolsList;
    }
}

function Plugin_handleToolExecuteAfter(rt, inObj, outObj) {
    if (!Operators_IsNull(inObj)) {
        const t = Operators_IsNull(inObj.tool) ? "unknown" : inObj.tool;
        const s = Operators_IsNull(inObj.sessionID) ? "" : inObj.sessionID;
        const c = Operators_IsNull(inObj.callID) ? "" : inObj.callID;
        const argsStr = (Operators_IsNull(outObj) ? true : Operators_IsNull(outObj.args)) ? "{}" : JSON.stringify(outObj.args);
        const outStr = (Operators_IsNull(outObj) ? true : Operators_IsNull(outObj.output)) ? "{}" : JSON.stringify(outObj.output);
        if (!isNullOrEmpty(s)) {
            const sr = PluginRuntime__GetOrCreateSessionRuntime_2C0A04B3(rt, SessionIdModule_create(s));
            sr.Inbox.TryPost(new SessionInboxEvent(3, [t, c, argsStr, outStr]));
        }
    }
}

function Plugin_handleCommand(rt, inObj) {
    if (!Operators_IsNull(inObj)) {
        const cmdName = Operators_IsNull(inObj.name) ? "" : inObj.name;
        const s = Operators_IsNull(inObj.sessionID) ? "" : inObj.sessionID;
        const argsText = Operators_IsNull(inObj.arguments) ? "" : inObj.arguments;
        if (!isNullOrEmpty(s)) {
            const sr = PluginRuntime__GetOrCreateSessionRuntime_2C0A04B3(rt, SessionIdModule_create(s));
            if ((cmdName === "loop") ? true : (cmdName === "/loop")) {
                sr.Inbox.TryPost(new SessionInboxEvent(7, [SessionIdModule_create(s), argsText]));
            }
            else if ((cmdName === "squad") ? true : (cmdName === "/squad")) {
                sr.Inbox.TryPost(new SessionInboxEvent(8, [s, argsText]));
            }
        }
    }
}

function Plugin_handleConfig(config) {
    if (!Operators_IsNull(config)) {
        const commands = Operators_IsNull(config.command) ? {} : config.command;
        if (Operators_IsNull(commands.loop)) {
            commands.loop = {
                template: "$ARGUMENTS",
                description: "Continue work until the task is complete.",
            };
        }
        if (Operators_IsNull(commands.squad)) {
            commands.squad = {
                template: "$ARGUMENTS",
                description: "Delegate work to a coordinated agent squad.",
            };
        }
        config.command = commands;
    }
}

function Plugin_handleCompacting(rt, outObj) {
    if (!Operators_IsNull(outObj)) {
        const proj = PluginRuntime__get_Gateway(rt).ProjectionSet;
        let revInfo;
        const matchValue = proj.LastReview;
        if (matchValue == null) {
            revInfo = "";
        }
        else {
            const v = matchValue;
            revInfo = toText(printf("Review: %A\n"))(v);
        }
        let todoInfo;
        const matchValue_1 = proj.Todos;
        if (matchValue_1 == null) {
            todoInfo = "";
        }
        else {
            const arg_1 = join("; ", matchValue_1.Items);
            todoInfo = toText(printf("Todos: %s"))(arg_1);
        }
        const ctxStr = (revInfo + todoInfo).trim();
        if (!isNullOrEmpty(ctxStr)) {
            outObj.context = ctxStr;
        }
    }
}

export function Plugin_initPlugin(input) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        const dir = (Operators_IsNull(input) ? true : Operators_IsNull(input.directory)) ? "." : input.directory;
        const portOpt = OpenCodePort_create(input);
        return builder$0040.Bind(PluginRuntime_start(dir, portOpt), (_arg) => {
            const rtRes = _arg;
            const rt = (rtRes.tag === 1) ? toFail(printf("Failed to initialize PluginRuntime: %A"))(rtRes.fields[0]) : rtRes.fields[0];
            const hooks = {
                "chat.message": (inObj, outObj) => {
                    const hookInput = Plugin_buildHookInput(inObj);
                    if (!isNullOrEmpty(hookInput.sessionID)) {
                        PluginRuntime__EnsureSessionDriver_2C0A04B3(rt, SessionIdModule_create(hookInput.sessionID));
                    }
                    handleChatMessage(PluginRuntime__get_Gateway(rt), PluginRuntime__get_SessionDrivers(rt), PluginRuntime__GetInboxMap(rt), hookInput, outObj);
                },
                "chat.transform": (inObj_1, outObj_1) => {
                    Plugin_handleChatTransform(rt, outObj_1);
                },
                command: (inObj_8, outObj_8) => {
                    Plugin_handleCommand(rt, inObj_8);
                },
                "command.execute.before": (inObj_5, outObj_5) => {
                    Plugin_handleCommand(rt, inObj_5);
                },
                config: (config) => {
                    Plugin_handleConfig(config);
                },
                dispose: () => {
                    const builder$0040_1 = task();
                    return builder$0040_1.Run(builder$0040_1.Delay(() => builder$0040_1.Bind(rt["System.IAsyncDisposable.DisposeAsync"](), () => builder$0040_1.Zero())));
                },
                event: (eventObj) => {
                    handleEvent(PluginRuntime__get_Gateway(rt), PluginRuntime__GetInboxMap(rt), eventObj);
                },
                "experimental.compaction.autocontinue": (inObj_7, outObj_7) => {
                    if (!Operators_IsNull(outObj_7)) {
                        outObj_7.enabled = true;
                    }
                },
                "experimental.session.compacting": (inObj_6, outObj_6) => {
                    Plugin_handleCompacting(rt, outObj_6);
                },
                getOrCreateInbox: (sessionId) => PluginRuntime__GetOrCreateSessionRuntime_2C0A04B3(rt, sessionId).Inbox,
                tool: buildToolsObject(rt),
                "tool.definition": (inObj_2, outObj_2) => {
                    Plugin_handleToolDefinition(outObj_2);
                },
                "tool.execute.after": (inObj_4, outObj_4) => {
                    Plugin_handleToolExecuteAfter(rt, inObj_4, outObj_4);
                },
                "tool.execute.before": (inObj_3, outObj_3) => {
                    handleToolExecuteBefore(new OpencodeToolExecuteInput(inObj_3.tool, inObj_3.sessionID, inObj_3.callID), new OpencodeToolExecuteOutput(outObj_3.args));
                },
            };
            return builder$0040.Return(hooks);
        });
    }));
}

export const Plugin_defaultExport = {
    id: "wanxiangshu-next",
    server: Plugin_initPlugin,
};

export default Plugin_defaultExport;

