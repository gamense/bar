
if not (Spring.GetConfigInt("LuaSocketEnabled", 0) == 1) then
	Spring.Echo("LuaSocketEnabled is disabled")
	return false
end

Spring.Echo("started bar-shock")

function widget:GetInfo()
return {
	name    = "Tachyon Enforcer Bridge",
	desc    = "TCP connection to Tachyon-Enforcer",
	author  = "varunda",
	date    = "2025",
	license = "MIT",
	layer   = 0,
	enabled = true,
}
end

local glBlending          		= gl.Blending
local glScale          			= gl.Scale
local glRotate					= gl.Rotate
local glTranslate				= gl.Translate
local glPushMatrix          	= gl.PushMatrix
local glPopMatrix				= gl.PopMatrix

local glCreateList				= gl.CreateList
local glDeleteList				= gl.DeleteList
local glCallList				= gl.CallList

local client = nil
local myTeam = nil

local DEBUG_TCP_WRITES = false

local UNIT_DEF_METAL = {}
local UNIT_DEF_ENERGY = {}
local UNIT_DEF_NAMES = {}

function dump(o)
   if type(o) == 'table' then
      local s = '{ '
      for k,v in pairs(o) do
         if type(k) ~= 'number' then k = '"'..k..'"' end
         s = s .. '['..k..'] = ' .. dump(v) .. ','
      end
      return s .. '} '
   else
      return tostring(o)
   end
end

local unitNamesSent = {}

-- if a unit is being created, and hasn't been produced from the factory,
-- we don't want to report those units being destroyed
local unitsBeingCreated = {}

function widget:UnitCreated(unitID)
    unitsBeingCreated[unitID] = unitID
end

function widget:UnitFinished(unitID, unitDefID, unitTeam)
    unitsBeingCreated[unitID] = nil
    if (unitTeam == myTeam) then
        SendUnitName(unitDefID)
        SocketSend("mk", "i" .. unitID .. "u" .. unitDefID .. "t" .. unitTeam .. "m" .. UNIT_DEF_METAL[unitDefID] .. "e" .. UNIT_DEF_ENERGY[unitDefID])
    end
end

last_kill_count = nil

function widget:UnitDestroyed(unitID, unitDefID, unitTeam, attackerID, attackerDefID, attackerTeam, weaponDefID)

    -- janky work around
    -- because attackerTeam is not given, we instead check the kill stats every time a unit
    -- is killed to see if our team was the one that killed the unit
    local curr_kills = Spring.GetTeamUnitStats(myTeam)

    if (last_kill_count ~= curr_kills) then
        attackerTeam = myTeam
    end

    -- if the unit was still being constructed, don't count it
    if (unitsBeingCreated[unitID] == nil) then
        SendUnitName(unitDefID)
        if (attackerDefID ~= nil) then
            SendUnitName(attackerDefID)
        end
        SocketSend("rm", "i" .. unitID .. "u" .. unitDefID .. "t" .. unitTeam .. "a" .. (attackerTeam or "-") .. "m" .. UNIT_DEF_METAL[unitDefID] .. "e" .. UNIT_DEF_ENERGY[unitDefID])

    end

    last_kill_count = Spring.GetTeamUnitStats(myTeam)
end

local function SocketListen(host, port)
    if (client ~= nil) then
        client:close()
        client = nil
    end

	client=socket.tcp()
	client:settimeout(0)
	res, err = client:connect(host, port)
    Spring.Echo("connection attempt", res, err)
	if not res and not res=="timeout" then
		Spring.Echo("Error in connect: "..err)
        client = nil
		return false
	end

    Spring.Echo("client connected", res, err)
    SocketSend("hi", "t" .. myTeam)
    EcoSync()

	return true
end

local host = "127.0.0.1"
local port = 41666

function widget:Initialize()
    myTeam = Spring.GetMyTeamID()
    Spring.Echo("player is " .. Spring.GetMyPlayerID() .. " on team " .. myTeam)

    for k,v in pairs(UnitDefs) do
        UNIT_DEF_METAL[k] = v.metalCost
        UNIT_DEF_ENERGY[k] = v.energyCost
        UNIT_DEF_NAMES[k] = v.translatedHumanName
    end

    last_kill_count = Spring.GetTeamUnitStats(myTeam)

	SocketListen(host, port)
end

function widget:Shutdown()
    if (client ~= nil) then
        client:close()
    end
end

function EcoSync()
    local units = Spring.GetTeamUnits(myTeam)
    local metal = 0
    local eng = 0

    for k,v in pairs(units) do
        local udid = Spring.GetUnitDefID(v)
        metal = metal + UNIT_DEF_METAL[udid]
        eng = eng + UNIT_DEF_ENERGY[udid]
    end

    SocketSend("sm", "" .. metal)
    SocketSend("se", "" .. eng)
end


function SocketSend(op, msg)
    if (DEBUG_TCP_WRITES == true) then
        Spring.Echo("(" .. op .. ") " .. msg .. " " .. type(msg))
    end

    if (client ~= nil) then
        client:send(op .. msg:len() .. ";" .. msg)
    end
end

function GetType(unitDef)
    if (unitDef.isTransport) then
        return "transport"
    elseif (unitDef.isGroundUnit) then
        return "ground"
    elseif (unitDef.isBuilder) then
        return "builder"
    elseif (unitDef.isAirUnit) then
        return "air"
    elseif (unitDef.isExtractor) then
        return "mex"
    elseif (unitDef.windGenerator) then
        return "pgen"
    elseif (unitDef.energyUpkeep < 0 or unitDef.energyMake > 0) then
        return "pgen"
    end

    return "unknown"
end

ecoUpdateTimer = 1

function widget:Update(dt)
    if (client ~= nil) then
        local s, status, partial = client:receive("*a")
        if (status == "closed") then
            Spring.Echo("tcp socket closed")
            client = nil
        end
    end

    -- send eco updates every second
    ecoUpdateTimer = ecoUpdateTimer - dt
    if (ecoUpdateTimer < 0) then
        ecoUpdateTimer = 1

        SendEco("m", "em")
        SendEco("e", "ee")
    end
end

function SendEco(which, op)
    local curr, stor, pull, income, expense = Spring.GetTeamResources(myTeam, which)
    SocketSend(op, "c" .. curr .. "s" .. stor .. "p" .. pull .. "i" .. income .. "e" .. expense)
end

function updatePos()
    if WG['mascot'] ~= nil then
        parentPos = WG['mascot'].GetPosition()
	elseif WG['displayinfo'] ~= nil then
		parentPos = WG['displayinfo'].GetPosition()
	elseif WG['unittotals'] ~= nil then
		parentPos = WG['unittotals'].GetPosition()
	elseif WG['music'] ~= nil then
		parentPos = WG['music'].GetPosition()
	elseif WG['advplayerlist_api'] ~= nil then
		parentPos = WG['advplayerlist_api'].GetPosition()
	else
		local scale = (vsy / 880) * (1 + (Spring.GetConfigFloat("ui_scale", 1) - 1) / 1.25)
		parentPos = {0,vsx-(220*scale),0,vsx,scale}
	end

    return parentPos
end

function getGuiPos()
    local pos = updatePos()

    xPos = pos[2] + 4 --+(20) + (40 * parentPos[5])
    yPos = pos[1] + 2 --+(20) + (40 * parentPos[5])

    local mx = xPos + 60
    local my = yPos
    local sx = 60
    local sy = 60

    return mx, my, sx, sy
end

function widget:DrawScreen()
    local mx, my, sx, sy = getGuiPos()

    glPushMatrix()
        --glTranslate(xPos, yPos, 0)
        if (client == nil) then
            gl.Color(1, 0, 0, 1)
        else
            gl.Color(0, 1, 0, 1)
        end
        gl.BeginEnd(GL.QUADS, function()
            gl.Vertex(mx, my, 0)
            gl.Vertex(mx + sx, my, 0)
            gl.Vertex(mx + sx, my + sy, 0)
            gl.Vertex(mx, my + sy, 0)
        end)
    glPopMatrix()

end

function widget:MousePress(mx, my, mb)
    if (mb ~= 1) then
        return
    end

    local gx, gy, sx, sy = getGuiPos()

    if (math.isInRect(mx, my, gx, gy, gx + sx, gy + sy)) then
        SocketListen(host, port)
    end

end

function SendUnitName(unitDefID)
    if (unitNamesSent[unitDefID] == nil) then
        SocketSend("is", "i" .. unitDefID .. "n\"" .. UNIT_DEF_NAMES[unitDefID] .. "\"")
        unitNamesSent[unitDefID] = unitDefID
    end
end
