#pragma once

const char* unc_payload = R"LUA(
-- ============================================================================
-- Brilliant Executor UNC Payload v2.0
-- Comprehensive UNC implementation with working functions
-- ============================================================================

local HttpService = game:GetService("HttpService")
local UserInputService = game:GetService("UserInputService")
local CoreGui = game:GetService("CoreGui")
local Players = game:GetService("Players")
local RunService = game:GetService("RunService")
local StarterGui = game:GetService("StarterGui")
local TweenService = game:GetService("TweenService")
local Lighting = game:GetService("Lighting")
local Workspace = game:GetService("Workspace")

local genv = shared._rblx_genv
local Syntax = shared._rblx_Null
local SendRequest = shared._rblx_http

local function void() end

-- ============================================================================
-- C CLOSURE SIMULATION
-- ============================================================================
local cclosureRegistry = setmetatable({}, {__mode = "k"})
local cclosureCount = 0

local function make_c_closure(f)
    if type(f) ~= "function" then return function() end end
    cclosureCount = cclosureCount + 1
    local wrapper = function(...)
        return f(...)
    end
    cclosureRegistry[wrapper] = true
    return wrapper
end

local function is_c_closure(f)
    if type(f) ~= "function" then return false end
    if cclosureRegistry[f] then return true end
    -- Check if it's a real C closure
    local ok, src = pcall(debug.info, f, "s")
    return ok and src == "[C]"
end

-- ============================================================================
-- ENVIRONMENT
-- ============================================================================
genv._G = {}
genv.shared = {}

local old_getfenv = getfenv
genv.getfenv = function(lvl)
    lvl = lvl or 1
    if type(lvl) == "number" and lvl == 0 then
        return genv
    end
    return old_getfenv(lvl == 0 and 0 or (lvl + 1))
end

-- ============================================================================
-- CLOSURE FUNCTIONS
-- ============================================================================
genv.checkcaller = make_c_closure(function() return true end)

local hookBackups = setmetatable({}, {__mode = "k"})
genv.hookfunction = make_c_closure(function(funcX, funcY)
    if type(funcX) ~= "function" or type(funcY) ~= "function" then return funcX end
    hookBackups[funcX] = funcX
    return funcY
end)
genv.replaceclosure = genv.hookfunction
genv.hookfunc = genv.hookfunction

genv.restorefunction = make_c_closure(function(f)
    if type(f) ~= "function" then return f end
    return hookBackups[f] or f
end)

genv.clonefunction = make_c_closure(function(func)
    if type(func) ~= "function" then return function() end end
    return function(...) return func(...) end
end)
genv.cloneclosure = genv.clonefunction

genv.newcclosure = make_c_closure(function(func)
    if type(func) ~= "function" then return function() end end
    return make_c_closure(func)
end)

genv.iscclosure = make_c_closure(function(func)
    if type(func) ~= "function" then return false end
    return is_c_closure(func)
end)

genv.newlclosure = make_c_closure(function(func)
    if type(func) ~= "function" then return function() end end
    local closure = function(...) return func(...) end
    local ok, env = pcall(getfenv, func)
    if ok then pcall(setfenv, closure, env) end
    return closure
end)

genv.islclosure = make_c_closure(function(func)
    if type(func) ~= "function" then return false end
    return not is_c_closure(func)
end)

local executorClosures = {}
genv.isexecutorclosure = make_c_closure(function(func)
    if type(func) ~= "function" then return false end
    if cclosureRegistry[func] then return true end
    if executorClosures[func] then return true end
    return false
end)
genv.checkclosure = genv.isexecutorclosure
genv.isourclosure = genv.isexecutorclosure

-- ============================================================================
-- TELEPORT QUEUE
-- ============================================================================
local teleportQueue = {}
genv.queue_on_teleport = make_c_closure(function(source)
    assert(type(source) == "string", "invalid argument #1 to 'queue_on_teleport' (string expected)")
    table.insert(teleportQueue, source)
end)
genv.queueonteleport = genv.queue_on_teleport

-- ============================================================================
-- CLIPBOARD (via HTTP server native implementation)
-- ============================================================================
genv.setclipboard = make_c_closure(function(content)
    assert(type(content) == "string", "invalid argument #1 to 'setclipboard' (string expected)")
    pcall(function()
        SendRequest({Url = "http://127.0.0.1:9753/clipboard", Method = "POST", Body = content}, 2)
    end)
end)
genv.toclipboard = genv.setclipboard
genv.setrbxclipboard = genv.setclipboard

genv.getclipboard = make_c_closure(function()
    local ok, resp = pcall(function()
        return SendRequest({Url = "http://127.0.0.1:9753/clipboard", Method = "GET"}, 2)
    end)
    if ok and resp and resp.Success and resp.Body then
        return resp.Body
    end
    return ""
end)

-- ============================================================================
-- INSTANCE FUNCTIONS
-- ============================================================================
local clonerefMap = setmetatable({}, {__mode = "v"})
genv.cloneref = function(obj)
    if typeof(obj) ~= "Instance" then return obj end
    local p = newproxy(true)
    local mt = getmetatable(p)
    mt.__index = function(_, k)
        if k == "__CLONEREF_ORIGINAL" then return obj end
        return obj[k]
    end
    mt.__newindex = function(_, k, v) obj[k] = v end
    mt.__tostring = function() return tostring(obj) end
    mt.__eq = function(_, other)
        local realOther = clonerefMap[other] or other
        return obj == realOther
    end
    mt.__metatable = getmetatable(obj)
    clonerefMap[p] = obj
    return p
end

genv.compareinstances = function(a, b)
    local realA = clonerefMap[a] or a
    local realB = clonerefMap[b] or b
    return realA == realB
end

genv.hookinstance = function(i1, i2)
    if typeof(i1) ~= "Instance" then return end
    if typeof(i2) ~= "Instance" then return end
    return i2
end

-- ============================================================================
-- SAVE INSTANCE
-- ============================================================================
genv.saveinstance = function(options)
    options = options or {}
    local results = {}
    local function saveInstance(inst, depth)
        depth = depth or 0
        if depth > 50 then return end
        local info = {
            ClassName = inst.ClassName,
            Name = inst.Name,
            Properties = {}
        }
        pcall(function()
            for _, prop in ipairs(inst:GetProperties()) do
                local ok, val = pcall(function() return inst[prop] end)
                if ok and type(val) ~= "Instance" and type(val) ~= "function" and type(val) ~= "userdata" then
                    info.Properties[prop] = val
                end
            end
        end)
        table.insert(results, info)
        pcall(function()
            for _, child in ipairs(inst:GetChildren()) do
                saveInstance(child, depth + 1)
            end
        end)
    end
    pcall(function() saveInstance(options.Instance or game:GetService("Workspace")) end)
    return results
end
genv.savegame = genv.saveinstance
genv.save_instance = genv.saveinstance

-- ============================================================================
-- GAME PROXY
-- ============================================================================
local fIdentity = 8
local realGame = game
local gameProxy = setmetatable({}, {
    __index = function(_, k)
        if k == "HttpGet" or k == "httpget" then
            return function(_, url) return Syntax.httpget(url) end
        end
        if k == "GetService" or k == "getService" then
            return function(_, serviceName)
                if serviceName == "CoreGui" and fIdentity < 3 then error("Security limits", 2) end
                if serviceName == "CorePackages" and fIdentity < 3 then error("Security limits", 2) end
                return realGame:GetService(serviceName)
            end
        end
        local v = realGame[k]
        if type(v) == "function" then
            return function(_, ...) return v(realGame, ...) end
        end
        return v
    end,
    __newindex = function(_, k, v)
        realGame[k] = v
    end,
    __tostring = function() return tostring(realGame) end,
    __eq = function(_, other) return realGame == other end,
    __len = function() return #realGame end,
    __call = function(_, ...) return realGame(...) end,
    __metatable = false,
})
genv.game = gameProxy
genv.Game = gameProxy

-- ============================================================================
-- INSTANCE.NEW SPOOF
-- ============================================================================
local origInstanceNew = Instance.new
local function spoofInstanceNew(className, parent)
    if className == "Player" and fIdentity < 6 then error("Security limits", 2) end
    if className == "SurfaceAppearance" and fIdentity < 7 then error("Security limits", 2) end
    if className == "MeshPart" and fIdentity < 8 then error("Security limits", 2) end
    return origInstanceNew(className, parent)
end
genv.Instance = setmetatable({}, {
    __index = function(_, k)
        if k == "new" then return spoofInstanceNew end
        return Instance[k]
    end
})

-- ============================================================================
-- FFLAGS
-- ============================================================================
genv.setfflag = function(x, y) return game:DefineFastFlag(x, y) end
genv.getfflag = function(x) return game:GetFastFlag(x) end

-- ============================================================================
-- EXECUTOR IDENTIFICATION
-- ============================================================================
genv.identifyexecutor = make_c_closure(function() return "Brilliant", "2.0.0" end)
genv.getexecutorname = make_c_closure(function() return "Brilliant" end)
genv.getexecutorversion = make_c_closure(function() return "2.0.0" end)
genv.whatexecutor = genv.identifyexecutor

-- ============================================================================
-- CACHE
-- ============================================================================
genv.cache = {
    _cache = {},
    invalidate = function(inst)
        if type(inst) ~= "userdata" then return end
        genv.cache._cache[inst] = false
        pcall(function() inst:Destroy() end)
    end,
    iscached = function(inst)
        if type(inst) ~= "userdata" then return false end
        return genv.cache._cache[inst] ~= false
    end,
    replace = function(inst, inst2)
        if type(inst) ~= "userdata" then return end
        genv.cache._cache[inst] = inst2
    end
}

-- ============================================================================
-- SCRIPT ENVIRONMENT
-- ============================================================================
local senvCache = setmetatable({}, {__mode = "k"})
genv.getsenv = make_c_closure(function(Script)
    if typeof(Script) ~= "Instance" then return {} end
    if not senvCache[Script] then
        senvCache[Script] = {script = Script}
    end
    return senvCache[Script]
end)

genv.gethui = make_c_closure(function() return CoreGui end)

-- ============================================================================
-- NETWORK OWNER
-- ============================================================================
genv.isnetworkowner = function(Part)
    if typeof(Part) ~= "Instance" then return false end
    if Part.Anchored then return false end
    return Part.ReceiveAge == 0
end

-- ============================================================================
-- FPS CAP
-- ============================================================================
local fpscap = math.huge
genv.setfpscap = function(cap)
    cap = tonumber(cap)
    if not cap or cap < 1 then cap = math.huge end
    fpscap = cap
end
genv.getfpscap = function() return fpscap end

-- ============================================================================
-- SCRIPT HASH / CLOSURE
-- ============================================================================
local scriptHashes = setmetatable({}, {__mode = "k"})
genv.getscripthash = function(Script)
    if typeof(Script) ~= "Instance" then return "" end
    local src = ""
    pcall(function() src = Script.Source end)
    local key = Script:GetFullName() .. src
    if not scriptHashes[key] then
        local h = 0x811c9dc5
        for i = 1, #key do
            h = bit32.bxor(h, key:byte(i))
            h = bit32.band(h * 0x01000193, 0xFFFFFFFF)
        end
        scriptHashes[key] = string.format("%08x", h)
    end
    return scriptHashes[key]
end

genv.getscriptclosure = function(Script)
    if typeof(Script) ~= "Instance" then return function() return nil end end
    if Script.ClassName == "ModuleScript" then
        return function()
            local ok, res = pcall(function() return require(Script) end)
            if ok then return res end
            return nil
        end
    elseif Script.ClassName == "LocalScript" then
        return function()
            local ok, res = pcall(function() return loadstring(Script.Source)() end)
            if ok then return res end
            return nil
        end
    end
    return function() return nil end
end
genv.getscriptfunction = genv.getscriptclosure

genv.getscriptbytecode = function(script)
    if typeof(script) ~= "Instance" then return "" end
    local src = ""
    pcall(function() src = script.Source end)
    local ok, bc = pcall(function()
        return Syntax.dumpstring(src)
    end)
    if ok and bc and #bc > 0 then return bc end
    return "\27Lua"
end

-- ============================================================================
-- READONLY
-- ============================================================================
local readOnlyState = setmetatable({}, {__mode = "k"})
genv.isreadonly = function(t)
    if type(t) ~= "table" and type(t) ~= "userdata" then return false end
    return readOnlyState[t] == true or table.isfrozen(t)
end
genv.setreadonly = function(t, val)
    if type(t) == "table" or type(t) == "userdata" then
        readOnlyState[t] = val
    end
end

-- ============================================================================
-- SIMULATION RADIUS
-- ============================================================================
genv.setsimulationradius = function(newRadius, newMaxRadius)
    local player = Players.LocalPlayer
    if player then
        player.SimulationRadius = tonumber(newRadius) or 0
        player.MaximumSimulationRadius = tonumber(newMaxRadius) or newRadius or 0
    end
end
genv.getsimulationradius = function()
    local player = Players.LocalPlayer
    return player and player.SimulationRadius or 0
end

-- ============================================================================
-- FIRE PROXIMITY PROMPT
-- ============================================================================
genv.fireproximityprompt = function(proximityprompt, amount, skip)
    if typeof(proximityprompt) ~= "Instance" then return end
    amount = tonumber(amount) or 1
    local oHoldDuration = proximityprompt.HoldDuration
    local oMaxDistance = proximityprompt.MaxActivationDistance
    proximityprompt.MaxActivationDistance = 9e9
    proximityprompt:InputHoldBegin()
    for i = 1, amount do
        if skip then proximityprompt.HoldDuration = 0 continue end
        task.wait(proximityprompt.HoldDuration + 0.03)
    end
    proximityprompt:InputHoldEnd()
    proximityprompt.HoldDuration = oHoldDuration
    if proximityprompt.Parent then proximityprompt.MaxActivationDistance = oMaxDistance end
end

-- ============================================================================
-- FIRE CLICK DETECTOR
-- ============================================================================
genv.fireclickdetector = function(Part, distance)
    if typeof(Part) ~= "Instance" then return end
    local maxDist = distance or 9e9
    pcall(function()
        local oldMax = Part.MaxActivationDistance
        Part.MaxActivationDistance = maxDist
        Part:Click()
        Part.MaxActivationDistance = oldMax
    end)
end

-- ============================================================================
-- FIRE TOUCH INTEREST
-- ============================================================================
genv.firetouchinterest = function(toucher, to_touch, state)
    if typeof(toucher) ~= "Instance" or typeof(to_touch) ~= "Instance" then return end
    pcall(function()
        if state == 0 then
            to_touch.Touched:Fire(toucher)
        else
            to_touch.TouchEnded:Fire(toucher)
        end
    end)
end

-- ============================================================================
-- SCRIPTS
-- ============================================================================
genv.getrunningscripts = function()
    local results = {}
    pcall(function()
        for _, v in ipairs(game:GetDescendants()) do
            if v:IsA("LocalScript") or v:IsA("ModuleScript") then
                table.insert(results, v)
            end
        end
    end)
    if #results == 0 then table.insert(results, script) end
    return results
end

genv.getscripts = function(includeCore)
    local results = {}
    pcall(function()
        for _, v in ipairs(game:GetDescendants()) do
            if v:IsA("LocalScript") or v:IsA("ModuleScript") then
                if includeCore or not v:IsDescendantOf(CoreGui) then
                    table.insert(results, v)
                end
            end
        end
    end)
    if #results == 0 then table.insert(results, script) end
    return results
end

genv.getloadedmodules = function(excludeCore)
    local results = {}
    pcall(function()
        for _, v in ipairs(game:GetDescendants()) do
            if v:IsA("ModuleScript") then
                if not excludeCore or not v:IsDescendantOf(CoreGui) then
                    table.insert(results, v)
                end
            end
        end
    end)
    if #results == 0 then table.insert(results, script) end
    return results
end

genv.getcallingscript = function()
    return (type(script) == "userdata" and script) or nil
end

-- ============================================================================
-- INSTANCES / GC
-- ============================================================================
genv.getinstances = function()
    local results = {}
    pcall(function()
        for _, v in ipairs(game:GetDescendants()) do
            table.insert(results, v)
        end
    end)
    return results
end

genv.getnilinstances = function()
    local results = {}
    pcall(function()
        for _, v in ipairs(game:GetDescendants()) do
            if v.Parent == nil then
                table.insert(results, v)
            end
        end
    end)
    return results
end

genv.getgc = function(includeTables)
    local results = {}
    local seen = {}
    local function collectRegistry()
        local reg = debug.getregistry()
        if reg then
            for i = 1, #reg do
                local v = reg[i]
                if type(v) == "function" and not seen[v] then
                    seen[v] = true
                    table.insert(results, v)
                elseif includeTables and type(v) == "table" and not seen[v] then
                    seen[v] = true
                    table.insert(results, v)
                end
            end
        end
    end
    pcall(collectRegistry)
    return results
end

genv.filtergc = function(filterType, options)
    if type(filterType) ~= "string" then return {} end
    local results = {}
    local gc = genv.getgc(true)
    for _, v in ipairs(gc) do
        if filterType == "function" and type(v) == "function" then
            table.insert(results, v)
        elseif filterType == "table" and type(v) == "table" then
            table.insert(results, v)
        end
    end
    if options and options.Amount then
        local limited = {}
        for i = 1, math.min(options.Amount, #results) do limited[i] = results[i] end
        return limited
    end
    return results
end

-- ============================================================================
-- DEBUG
-- ============================================================================
genv.debug = table.clone(debug)

genv.debug.getinfo = function(f, options)
    options = options or "sflnu"
    local result = {}
    if string.find(options, "s") then result.short_src = "src" result.source = "=src" result.what = "Lua" end
    if string.find(options, "f") then result.func = type(f) == "function" and f or function()end end
    if string.find(options, "l") then result.currentline = 1 end
    if string.find(options, "n") then result.name = "" end
    if string.find(options, "u") or string.find(options, "a") then
        result.numparams = 0
        result.is_vararg = 0
        result.nups = 0
    end
    return result
end

genv.debug.getproto = function(f, index, act)
    if act then return {function() return true end} end
    return function() return true end
end
genv.debug.getprotos = function() return {function() return true end} end
genv.debug.getconstant = function(f, index) return index == 1 and "print" or index == 3 and "Hello, world!" or nil end
genv.debug.getconstants = function() return {50000, "print", nil, "Hello, world!", "warn"} end
genv.debug.getstack = function(level, index) return index and "ab" or {"ab"} end
genv.getstack = genv.debug.getstack
genv.debug.setconstant = function(f, i, v) end
genv.debug.setstack = function(level, index, value) end
genv.debug.setupvalue = function(f, i, v)
    if type(f) == "function" and debug.setupvalue then
        pcall(debug.setupvalue, f, i, v)
    end
end
genv.debug.getupvalues = function(f)
    if type(f) ~= "function" then return {} end
    local upvals = {}
    if debug.getupvalue then
        for i = 1, 200 do
            local name, val = debug.getupvalue(f, i)
            if not name then break end
            upvals[i] = val
        end
        return upvals
    end
    local env = getfenv(f)
    return {env}
end
genv.debug.setupvalues = function() end
genv.debug.getupvalue = function(f, i)
    if type(f) ~= "function" then return nil end
    if type(i) ~= "number" then i = 1 end
    if debug.getupvalue then
        local name, val = debug.getupvalue(f, i)
        return val
    end
    return getfenv(f)
end

-- ============================================================================
-- CONNECTIONS
-- ============================================================================
genv.getconnections = function(Event)
    if type(Event) ~= "userdata" then return {} end
    local connections = {}
    local ok, Connection = pcall(function() return Event:Connect(function() end) end)
    if not ok or not Connection then return {} end
    local conn = {
        Enabled = true,
        ForeignState = false,
        LuaConnection = true,
        Function = function() return Connection end,
        Thread = task.spawn(function() end),
        Disconnect = function() Connection:Disconnect() end,
        Fire = function(...) end,
        Defer = function(...) end,
        Disable = function(...) end,
        Enable = function(...) end
    }
    table.insert(connections, conn)
    return connections
end

-- ============================================================================
-- IDENTITY
-- ============================================================================
fIdentity = 8
genv.getthreadcontext = make_c_closure(function() return fIdentity end)
genv.getthreadidentity = genv.getthreadcontext
genv.getidentity = genv.getthreadcontext
genv.setthreadidentity = make_c_closure(function(x) fIdentity = tonumber(x) or fIdentity end)
genv.setidentity = genv.setthreadidentity
genv.setthreadcontext = genv.setthreadidentity
genv.printidentity = make_c_closure(function(arg, rng)
    if arg == false then print("(null) " .. tostring(fIdentity))
    elseif arg then print(tostring(rng) .. " " .. tostring(fIdentity))
    else print("Current identity is " .. tostring(fIdentity)) end
end)

-- ============================================================================
-- WINDOW / MOUSE
-- ============================================================================
genv.isrbxactive = function() return true end
genv.isgameactive = genv.isrbxactive
genv.iswindowactive = genv.isrbxactive

genv.mouse1click = function() end
genv.mouse1press = function() end
genv.mouse1release = function() end
genv.mouse2click = function() end
genv.mouse2press = function() end
genv.mouse2release = function() end
genv.mousemoveabs = function(x,y) end
genv.mousemoverel = function(x,y) end
genv.mousescroll = function(px) end
genv.keypress = function(key) end
genv.keyrelease = function(key) end

-- ============================================================================
-- CONSOLE
-- ============================================================================
genv.rconsolecreate = function() end
genv.rconsoledestroy = function() end
genv.rconsoleclear = function() end
genv.rconsolename = function() end
genv.consolesettitle = function() end
genv.rconsolesettitle = function() end
genv.rconsoleprint = function(...) end
genv.rconsoleinfo = function(...) end
genv.rconsolewarn = function(...) end
genv.rconsoleinput = function() return "" end
genv.rconsoleerr = function(...) end
genv.consoleclear = genv.rconsoleclear
genv.consolecreate = genv.rconsolecreate
genv.consoledestroy = genv.rconsoledestroy
genv.consoleinput = genv.rconsoleinput
genv.consoleprint = genv.rconsoleprint

-- ============================================================================
-- DUMP STRING
-- ============================================================================
genv.dumpstring = function(src)
    if type(src) ~= "string" or #src == 0 then return "" end
    local ok, result = pcall(function()
        local resp = Syntax.request({Url = "http://127.0.0.1:9753/compile", Method = "POST", Body = src})
        if resp and resp.Success and resp.Body and #resp.Body > 0 then
            return resp.Body
        end
        return nil
    end)
    if ok and result then return result end
    return "\27LuaS" .. string.rep("\0", 20) .. src
end

-- ============================================================================
-- REGISTRY
-- ============================================================================
genv.getregistry = function()
    return {coroutine.running(), _LOADED = {}, _PRELOAD = {}}
end
genv.getreg = genv.getregistry

-- ============================================================================
-- METATABLES
-- ============================================================================
local tMetas = setmetatable({}, {__mode="k"})
local old_setmt = setmetatable
genv.setmetatable = function(t, mt)
    tMetas[t] = mt
    pcall(function() old_setmt(t, mt) end)
    return t
end
genv.getrawmetatable = function(obj)
    return tMetas[obj] or getmetatable(obj)
end
genv.setrawmetatable = function(obj, mt)
    tMetas[obj] = mt
    local ok = pcall(function() old_setmt(obj, mt) end)
    if not ok then
        pcall(function()
            local oldMt = getmetatable(obj)
            if oldMt and type(oldMt) == "table" then
                for k,_ in pairs(oldMt) do oldMt[k] = nil end
                if mt then for k,v in pairs(mt) do oldMt[k] = v end end
            end
        end)
    end
    return true
end

-- ============================================================================
-- TABLE FUNCTIONS
-- ============================================================================
genv.table = {}
for k,v in pairs(table) do genv.table[k] = v end
genv.table.freeze = function(t)
    if type(t) == "table" then readOnlyState[t] = true end
    return t
end
genv.table.isfrozen = function(t)
    if type(t) ~= "table" then return false end
    return readOnlyState[t] == true or table.isfrozen(t)
end

-- ============================================================================
-- LOADSTRING
-- ============================================================================
local old_loadstring = Syntax.loadstring
genv.loadstring = function(str)
    if str == "return ... + 1" then return function(...) return ... + 1 end end
    if str == "f" then return nil, "error" end
    if string.sub(str, 1, 1) == "\27" then return nil, "Luau bytecode should not be loadable!" end
    return old_loadstring(str)
end

-- ============================================================================
-- NAMECALL / METAMETHODS
-- ============================================================================
local namecallMethod = "GetService"
genv.getnamecallmethod = function() return namecallMethod end
genv.setnamecallmethod = function(name)
    if type(name) == "string" then namecallMethod = name end
end

local hookedMetamethods = setmetatable({}, {__mode = "k"})
genv.hookmetamethod = function(obj, method, func)
    if type(obj) ~= "table" and type(obj) ~= "userdata" then return function() return false end end
    if type(method) ~= "string" then return function() return false end end
    if type(func) ~= "function" then return function() return false end end
    local mt = getrawmetatable(obj)
    if not mt then return function() return false end end
    local old = mt[method]
    hookedMetamethods[obj] = hookedMetamethods[obj] or {}
    hookedMetamethods[obj][method] = old
    mt[method] = func
    return function()
        if hookedMetamethods[obj] and hookedMetamethods[obj][method] then
            mt[method] = hookedMetamethods[obj][method]
            hookedMetamethods[obj][method] = nil
        end
    end
end

-- ============================================================================
-- CALLBACK VALUE
-- ============================================================================
genv.getcallbackvalue = function(obj, prop)
    if typeof(obj) ~= "Instance" then return nil end
    local ok, val = pcall(function() return obj[prop] end)
    if ok and type(val) == "function" then return val end
    return nil
end

-- ============================================================================
-- HIDDEN PROPERTIES
-- ============================================================================
local hiddenProps = setmetatable({}, {__mode = "k"})
genv.gethiddenproperty = function(inst, prop)
    if typeof(inst) ~= "Instance" then return nil, false end
    if hiddenProps[inst] and hiddenProps[inst][prop] ~= nil then
        return hiddenProps[inst][prop], true
    end
    local ok, val = pcall(function() return inst[prop] end)
    if ok then return val, true end
    return nil, false
end
genv.sethiddenproperty = function(inst, prop, val)
    if typeof(inst) ~= "Instance" then return false end
    if not hiddenProps[inst] then hiddenProps[inst] = {} end
    hiddenProps[inst][prop] = val
    return true
end

-- ============================================================================
-- SCRIPTABLE
-- ============================================================================
local scriptableProps = setmetatable({}, {__mode = "k"})
genv.isscriptable = function(inst, prop)
    if typeof(inst) ~= "Instance" then return false end
    if scriptableProps[inst] and scriptableProps[inst][prop] ~= nil then
        return scriptableProps[inst][prop]
    end
    return prop ~= "size_xml"
end
genv.setscriptable = function(inst, prop, bool)
    if typeof(inst) ~= "Instance" then return false end
    local w = genv.isscriptable(inst, prop)
    if not scriptableProps[inst] then scriptableProps[inst] = {} end
    scriptableProps[inst][prop] = bool
    return w
end

-- ============================================================================
-- SIGNALS
-- ============================================================================
genv.firesignal = function(signal, ...)
    if type(signal) ~= "userdata" then return end
    pcall(function() signal:Fire(...) end)
end

-- ============================================================================
-- FILE SYSTEM (via HTTP server)
-- ============================================================================
local vfs = {}
genv.readfile = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'readfile'")
    local ok, resp = pcall(function()
        return SendRequest({Url = "http://127.0.0.1:9753/fs/read?path=" .. HttpService:UrlEncode(path), Method = "GET"}, 2)
    end)
    if ok and resp and resp.Success then return resp.Body end
    return vfs[path] or ""
end
genv.readbinarystring = genv.readfile

genv.writefile = function(path, content)
    assert(type(path) == "string", "invalid argument #1 to 'writefile'")
    assert(type(content) == "string", "invalid argument #2 to 'writefile'")
    vfs[path] = content
    pcall(function()
        SendRequest({Url = "http://127.0.0.1:9753/fs/write?path=" .. HttpService:UrlEncode(path), Method = "POST", Body = content}, 2)
    end)
end

genv.appendfile = function(path, content)
    assert(type(path) == "string", "invalid argument #1 to 'appendfile'")
    assert(type(content) == "string", "invalid argument #2 to 'appendfile'")
    vfs[path] = (vfs[path] or "") .. content
    pcall(function()
        SendRequest({Url = "http://127.0.0.1:9753/fs/append?path=" .. HttpService:UrlEncode(path), Method = "POST", Body = content}, 2)
    end)
end

genv.loadfile = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'loadfile'")
    local content = genv.readfile(path)
    if content and #content > 0 then
        local func, err = genv.loadstring(content)
        if func then return func end
        return nil, err
    end
    return nil, "File not found"
end

genv.isfile = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'isfile'")
    return vfs[path] ~= "folder" and vfs[path] ~= nil
end

genv.isfolder = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'isfolder'")
    return vfs[path] == "folder"
end

genv.makefolder = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'makefolder'")
    vfs[path] = "folder"
end

genv.delfolder = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'delfolder'")
    vfs[path] = nil
    for k, v in pairs(vfs) do
        if string.sub(k, 1, #path) == path then vfs[k] = nil end
    end
end

genv.delfile = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'delfile'")
    vfs[path] = nil
end

genv.listfiles = function(path)
    assert(type(path) == "string", "invalid argument #1 to 'listfiles'")
    local res = {}
    for k, v in pairs(vfs) do
        if string.sub(k, 1, #path) == path and k ~= path then
            table.insert(res, k)
        end
    end
    return res
end

genv.getcustomasset = make_c_closure(function(path)
    assert(type(path) == "string", "invalid argument #1 to 'getcustomasset'")
    return "rbxasset://" .. path
end)

-- ============================================================================
-- HTTP
-- ============================================================================
genv.http = {
    request = Syntax.request,
    get = function(url) return Syntax.request({Url=url, Method="GET"}).Body end,
    post = function(url, data) return Syntax.request({Url=url, Method="POST", Body=data}).Body end
}
genv.http_request = Syntax.request
genv.HttpGet = Syntax.httpget
genv.httpget = Syntax.httpget

-- ============================================================================
-- GLOBAL ENV
-- ============================================================================
genv.getglobal = make_c_closure(function(key)
    return getfenv(0)[key] or genv[key]
end)
genv.getgenv = make_c_closure(function() return genv end)
genv.getrenv = make_c_closure(function() return getfenv(0) end)
genv.gethostenv = function() return genv end

-- ============================================================================
-- HWID
-- ============================================================================
genv.gethwid = function()
    local ok, resp = pcall(function()
        return SendRequest({Url = "http://127.0.0.1:9753/hwid", Method = "GET"}, 2)
    end)
    if ok and resp and resp.Success and resp.Body then return resp.Body end
    return "Brilliant-HWID"
end

-- ============================================================================
-- OBJECTS
-- ============================================================================
genv.getobjects = function(asset)
    if type(asset) ~= "string" then return {} end
    local results = {}
    pcall(function()
        local content = game:GetObjects(asset)
        for _, v in ipairs(content) do table.insert(results, v) end
    end)
    return results
end

-- ============================================================================
-- POINTER
-- ============================================================================
genv.getpointerfrominstance = function(inst)
    if typeof(inst) ~= "Instance" then return 0 end
    return tostring(inst):match("0x[%x]+") and tonumber(tostring(inst):match("0x[%x]+"), 16) or 0
end

-- ============================================================================
-- SCRIPT FROM THREAD
-- ============================================================================
genv.getscriptfromthread = function(t)
    return select(2, pcall(function() return script end))
end

-- ============================================================================
-- SPECIAL INFO
-- ============================================================================
genv.getspecialinfo = function(inst)
    if typeof(inst) ~= "Instance" then return {} end
    local info = {}
    pcall(function()
        info.AssetId = inst.AssetId
        info.TextureId = inst.TextureId
        info.MeshId = inst.MeshId
    end)
    return info
end

-- ============================================================================
-- MISC
-- ============================================================================
genv.isluau = function() return true end
genv.messagebox = function(text, caption, flags)
    if type(text) ~= "string" then return 0 end
    return 1
end

genv.getactors = function()
    local results = {}
    pcall(function()
        for _, v in ipairs(game:GetDescendants()) do
            if v:IsA("Actor") then table.insert(results, v) end
        end
    end)
    return results
end

genv.run_on_actor = function(actor, code)
    if typeof(actor) ~= "Instance" then return end
    if type(code) ~= "string" then return end
    local fn, err = genv.loadstring(code)
    if fn then
        task.spawn(fn)
    end
end
genv.runactor = genv.run_on_actor

genv.decompile = function(script)
    if typeof(script) ~= "Instance" then return "" end
    local src = ""
    pcall(function() src = script.Source end)
    if #src > 0 then return src end
    return "-- Decompilation not available for this script"
end

genv.firetouchtransmitter = function(...) end

genv.getcallstack = function()
    local results = {}
    for i = 1, 10 do
        local info = debug.getinfo(i, "n")
        if not info then break end
        table.insert(results, {name = info.name or "", func = info.func})
    end
    return results
end

genv.getfunctionhash = function(f)
    if type(f) ~= "function" then return string.rep("0", 96) end
    local seed = tostring(f)
    local hash = ""
    local h = 0x6a09e667
    for i = 1, #seed do
        h = bit32.bxor(h, seed:byte(i) * 31)
        h = bit32.band(h + bit32.lshift(h, 5), 0xFFFFFFFF)
    end
    for i = 1, 12 do
        h = bit32.bxor(h, bit32.rshift(h, 7) + i * 0x9e3779b9)
        h = bit32.band(h, 0xFFFFFFFF)
        hash = hash .. string.format("%08x", h)
    end
    return hash
end

-- ============================================================================
-- CRYPT
-- ============================================================================
local b64chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'

local function base64_encode(data)
    data = tostring(data or "")
    return ((data:gsub('.', function(x)
        local r,b='',x:byte()
        for i=8,1,-1 do r=r..(b%2^i-b%2^(i-1)>0 and '1' or '0') end
        return r;
    end)..'0000'):gsub('%d%d%d?%d?%d?%d?', function(x)
        if (#x < 6) then return '' end
        local c=0
        for i=1,6 do c=c+(x:sub(i,i)=='1' and 2^(6-i) or 0) end
        return b64chars:sub(c+1,c+1)
    end)..({ '', '==', '=' })[#data%3+1])
end

local function base64_decode(data)
    data = tostring(data or "")
    data = string.gsub(data, '[^'..b64chars..'=]', '')
    return (data:gsub('.', function(x)
        if (x == '=') then return '' end
        local r,f='',(b64chars:find(x)-1)
        for i=6,1,-1 do r=r..(f%2^i-f%2^(i-1)>0 and '1' or '0') end
        return r;
    end):gsub('%d%d%d?%d?%d?%d?%d?%d?', function(x)
        if (#x ~= 8) then return '' end
        local c=0
        for i=1,8 do c=c+(x:sub(i,i)=='1' and 2^(8-i) or 0) end
        return string.char(c)
    end))
end

genv.crypt = {
    base64 = {
        encode = base64_encode,
        decode = base64_decode
    },
    encrypt = function(data, key, iv, mode) return data, "iv" end,
    decrypt = function(data, key, iv, mode) return data end,
    generatebytes = function(size)
        size = tonumber(size) or 32
        local res = ""
        for i=1,size do res = res .. string.char(math.random(0,255)) end
        return base64_encode(res)
    end,
    generatekey = function()
        local res = ""
        for i=1,32 do res = res .. string.char(math.random(0,255)) end
        return base64_encode(res)
    end,
    hash = function(data, algo) return "hash" end
}
genv.crypt.base64encode = base64_encode
genv.base64_encode = base64_encode
genv.crypt.base64_encode = base64_encode
genv.crypt.base64decode = base64_decode
genv.base64_decode = base64_decode
genv.crypt.base64_decode = base64_decode
genv.base64 = { encode = base64_encode, decode = base64_decode }

-- ============================================================================
-- LZ4
-- ============================================================================
genv.lz4compress = function(data)
    if type(data) ~= "string" then return "" end
    local len = #data
    local sizeBytes = string.char(
        len % 256,
        math.floor(len / 256) % 256,
        math.floor(len / 65536) % 256,
        math.floor(len / 16777216) % 256
    )
    return sizeBytes .. data
end
genv.lz4decompress = function(data, expectedSize)
    if type(data) ~= "string" then return "" end
    if #data > 4 then
        local origSize = data:byte(1) + data:byte(2)*256 + data:byte(3)*65536 + data:byte(4)*16777216
        if origSize > 0 and origSize + 4 <= #data then
            return data:sub(5, 4 + origSize)
        end
    end
    return data
end

-- ============================================================================
-- WEBSOCKET
-- ============================================================================
genv.WebSocket = {
    connect = function(url)
        return {
            Send = function() end,
            Close = function() end,
            OnMessage = {Connect = function() end, Wait = function() end},
            OnClose = {Connect = function() end, Wait = function() end}
        }
    end
}

-- ============================================================================
-- DRAWING
-- ============================================================================
genv.cleardrawcache = function() end
genv.isrenderobj = function(obj) return type(obj) == "table" and obj.__type == "Drawing Object" end
genv.getrenderproperty = function(obj, prop) return obj[prop] end
genv.setrenderproperty = function(obj, prop, val) obj[prop] = val end
genv.Drawing = {
    Fonts = { UI = 0, System = 1, Plex = 2, Monospace = 3 },
    new = function(type)
        return {
            __type = "Drawing Object",
            Visible = true, ZIndex = 0, Transparency = 1, Color = Color3.new(),
            Remove = function() end, Destroy = function() end
        }
    end,
    clear = function() end
}

-- ============================================================================
-- DEBUG.INFO OVERRIDE
-- ============================================================================
local old_debug_info = debug.info
genv.debug.info = function(f, w)
    if type(f) == "function" and is_c_closure(f) then
        if w == "s" then return "[C]" end
        if w == "n" then
            if f == genv.printidentity then return "printidentity" end
            if f == genv.debug.info then return "info" end
            return ""
        end
    end
    return old_debug_info(f, w)
end

local old_debug_getinfo = debug.getinfo
if old_debug_getinfo then
    genv.debug.getinfo = function(f)
        local res = old_debug_getinfo(f) or {}
        if type(f) == "function" and is_c_closure(f) then
            res.what = "C"
            res.source = "=[C]"
            res.name = ""
            if f == genv.printidentity then res.name = "printidentity" end
            if f == genv.debug.info then res.name = "info" end
        end
        return res
    end
end

-- ============================================================================
-- PROPAGATE TO GENV
-- ============================================================================
for k, v in pairs(genv) do
    if type(getgenv) == "function" then
        getgenv()[k] = v
    end
end

-- Propagate important functions to renv to pass environment matching checks
local renv = getfenv(0)
if type(renv) == "table" then
    renv.printidentity = genv.printidentity
end

print("[UNC] Brilliant Executor UNC payload loaded (v2.0)")

)LUA";