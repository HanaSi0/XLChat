using System;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace XLinUI;

// Uses generated bindings shipped by the active Dalamud installation, never hardcoded addresses.
internal static unsafe class GameBridge
{
    internal static bool UseGeneralAction(uint id)
    {
        var manager = ActionManager.Instance();
        return manager != null && manager->UseAction(ActionType.GeneralAction, id);
    }
    internal static List<GearsetSummary> ReadGearsets()
    {
        var rows = new List<GearsetSummary>();
        var module = RaptureGearsetModule.Instance();
        if (module == null) return rows;
        for (var i = 0; i < 100; i++)
        {
            var row = module->GetGearset(i);
            if (row == null || (row->Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0) continue;
            rows.Add(new GearsetSummary(i, row->NameString, row->ClassJob, row->ItemLevel));
        }
        return rows;
    }

    internal static void EquipGearset(int index)
    {
        var module = RaptureGearsetModule.Instance();
        if (module != null) module->EquipGearset(index);
    }

    internal static void SetOnlineStatus(uint id)
    {
        var proxy = InfoProxyDetail.Instance();
        if (proxy != null) proxy->SendOnlineStatusUpdate(id);
    }

    internal static bool OpenSearchInfo()
    {
        var info = InfoModule.Instance();
        var party = InfoProxyPartyMember.Instance();
        var detail = InfoProxyDetail.Instance();
        var agent = AgentDetail.Instance();
        if (info == null || party == null || detail == null || agent == null || info->IsInCrossWorldDuty()) return false;
        var local = party->InfoProxyCommonList.GetEntryByContentId(info->GetLocalContentId());
        if (local == null) return false;
        agent->OpenForCharacterData(local, &detail->UpdateData);
        return true;
    }

    internal static (byte Rank, ushort Current, ushort Required) ReadReputation(byte id)
    {
        var state = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
        return state == null ? default : (state->GetBeastTribeRank(id), state->GetBeastTribeCurrentReputation(id), state->GetBeastTribeNeededReputation(id));
    }

    internal static uint TribeAllowance()
    {
        var manager = QuestManager.Instance();
        return manager == null ? 0 : manager->GetBeastTribeAllowance();
    }

    internal static int ItemCount(uint id)
    {
        var manager = InventoryManager.Instance();
        return manager == null ? 0 : manager->GetInventoryItemCount(id);
    }

    internal static uint CompanySealCap(byte company)
    {
        var manager = InventoryManager.Instance();
        return manager == null ? 0 : manager->GetMaxCompanySeals(company);
    }

    internal static (int Acquired, int Limit) WeeklyTomestones()
    {
        var manager = InventoryManager.Instance();
        return manager == null ? default : (manager->GetWeeklyAcquiredTomestoneCount(), InventoryManager.GetLimitedTomestoneWeeklyLimit());
    }

    internal static List<InventorySummary> ReadInventory(InventoryType type)
    {
        var rows = new List<InventorySummary>();
        var manager = InventoryManager.Instance();
        if (manager == null) return rows;
        var container = manager->GetInventoryContainer(type);
        if (container == null) return rows;
        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item == null || item->ItemId == 0) continue;
            rows.Add(new InventorySummary(item->GetBaseItemId(), item->Quantity, item->Condition / 300f, item->SpiritbondOrCollectability / 100f));
        }
        return rows;
    }
    internal static (uint Gil, byte Weather, long ServerTime) ReadTopData()
    {
        var inventory = InventoryManager.Instance();
        var weather = WeatherManager.Instance();
        return (inventory == null ? 0 : inventory->GetGil(), weather == null ? (byte)0 : weather->GetCurrentWeather(), Framework.GetServerTime());
    }

    internal static List<TeleportDestination> ReadTeleports()
    {
        var rows = new List<TeleportDestination>();
        var module = Telepo.Instance();
        if (module == null) return rows;
        module->UpdateAetheryteList();
        foreach (var row in module->TeleportList)
            rows.Add(new TeleportDestination(row.AetheryteId, row.SubIndex, row.GilCost));
        return rows;
    }

    internal static bool Teleport(uint id, byte subIndex)
    {
        var module = Telepo.Instance();
        return module != null && module->Teleport(id, subIndex);
    }
    internal static bool Open(uint command)
    {
        var module = UIModule.Instance();
        if (module == null) return false;
        module->ExecuteMainCommand(command);
        return true;
    }

    internal static bool Send(string prefix, string text)
    {
        // No arbitrary slash-command execution in the text field.
        if (text.Length == 0 || text.Contains('\n') || text.Contains('\r') || text.Contains('\0')) return false;
        if (Encoding.UTF8.GetByteCount(text) > 450) return false;
        var module = UIModule.Instance();
        if (module == null) return false;
        using var value = new Utf8String();
        value.SetString(prefix + " " + text);
        module->ProcessChatBoxEntry(&value, saveToHistory: true);
        return true;
    }

    internal static SlotInfo ReadSlot(int bar, int index)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null) return default;
        var slot = module->GetSlotById((uint)bar, (uint)index);
        if (slot == null || slot->IsEmpty) return default;
        var cooldown = 0;
        slot->GetSlotActionCooldownPercentage(&cooldown);
        return new SlotInfo(slot->IconId, slot->CommandId, slot->CommandType.ToString(),
            Math.Max(0, cooldown));
    }

    internal static void ExecuteSlot(int bar, int index)
    {
        var module = RaptureHotbarModule.Instance();
        if (module == null) return;
        module->ExecuteSlotById((uint)bar, (uint)index);
    }
}

internal readonly record struct SlotInfo(uint Icon, uint Command, string? Type, int Cooldown);
internal sealed record TeleportDestination(uint Id, byte SubIndex, uint Cost);
internal sealed record GearsetSummary(int Index, string Name, byte Job, short ItemLevel);
internal sealed record InventorySummary(uint Id, int Quantity, float Condition, float Spiritbond);
