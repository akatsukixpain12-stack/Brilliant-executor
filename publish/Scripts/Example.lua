-- ============================================================================
-- Brilliant Executor Example Script
-- ============================================================================
-- This is a demo script that showcases the full UNC API available in
-- Brilliant Executor.
-- ============================================================================

-- Get basic player info
local player = game.Players.LocalPlayer
print("Hello, " .. player.Name .. "!")

-- Test executor identification
local executorName, version = identifyexecutor()
print("Running on " .. executorName .. " v" .. version)

-- Test identity (should be 8)
print("Current identity: " .. getidentity())

-- Test getgenv
local g = getgenv()
print("Genv is accessible: " .. tostring(g ~= nil))

-- Test HTTP request
local success, result = pcall(function()
    return request({
        Url = "https://httpbin.org/get",
        Method = "GET",
        Headers = {["User-Agent"] = "Brilliant Executor"}
    })
end)
if success and result and result.Success then
    print("HTTP request successful! (Status: " .. result.StatusCode .. ")")
else
    warn("HTTP request failed")
end

-- Test clipboards
setclipboard("Hello from " .. identifyexecutor())
print("Clipboard set!")

-- Test getting the player's position
local char = player.Character
if char then
    local hrp = char:FindFirstChild("HumanoidRootPart")
    if hrp then
        print("Position: " .. tostring(hrp.Position))
    end
end

-- Show notification
game:GetService("StarterGui"):SetCore("SendNotification", {
    Title = executorName,
    Text = "Script executed successfully!",
    Duration = 5
})

print("Done!")