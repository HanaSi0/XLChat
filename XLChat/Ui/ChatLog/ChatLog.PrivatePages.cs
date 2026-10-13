using XLChat.Code;
using XLChat.GameFunctions.Types;
using XLChat.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace XLChat.Ui.ChatLog;

public partial class ChatLog
{
    private (string Name, uint World, ulong ContentId)[] privateFriends = [];

    private void OpenPrivatePage(TellTarget target)
    {
        if (!target.IsSet()) return;
        var index = Plugin.Config.Tabs.FindIndex(tab => tab.Channel == InputChannel.Tell &&
            tab.TellTarget.World == target.World && string.Equals(tab.TellTarget.Name, target.Name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            var tab = TabsUtil.VanillaTellExclusive;
            tab.Name = target.Name;
            tab.AllSenderMessages = false;
            tab.TellTarget = target.Clone();
            Plugin.Config.Tabs.Add(tab);
            index = Plugin.Config.Tabs.Count - 1;
            Plugin.SaveConfig();
            Plugin.MessageManager.FilterAllTabsAsync();
        }
        Plugin.WantedTab = index;
    }

    private void DrawPrivatePageCreator()
    {
        if (ImGuiUtil.IconButton(Dalamud.Interface.FontAwesomeIcon.Plus, tooltip: "新建私聊")) ImGui.OpenPopup("private-page-create");
        using var popup = ImRaii.Popup("private-page-create");
        if (!popup.Success) return;
        ImGui.TextUnformatted("选择好友开始私聊");
        if (ImGui.IsWindowAppearing() || ImGui.Button("刷新好友列表"))
            privateFriends = GameFunctions.GameFunctions.GetFriends()
                .Select(friend => (friend.NameString, (uint)friend.HomeWorld, friend.ContentId)).ToArray();
        if (privateFriends.Length == 0)
            ImGui.TextWrapped("当前没有可读取的好友。请先打开游戏好友列表，再重新打开此菜单。");
        using (var list = ImRaii.Child("private-friends", new System.Numerics.Vector2(280, 240) * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale))
        {
            if (list.Success)
                foreach (var friend in privateFriends)
                {
                    var server = Sheets.WorldSheet.TryGetRow(friend.World, out var world) ? world.Name.ToString() : friend.World.ToString();
                    if (ImGui.Selectable($"{friend.Name}@{server}##{friend.ContentId}"))
                    {
                        OpenPrivatePage(new TellTarget(friend.Name, friend.World, friend.ContentId, TellReason.Friend));
                        ImGui.CloseCurrentPopup();
                    }
                }
        }
    }
}
