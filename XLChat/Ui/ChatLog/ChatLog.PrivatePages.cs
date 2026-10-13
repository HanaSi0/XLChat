using XLChat.Code;
using XLChat.GameFunctions.Types;
using XLChat.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace XLChat.Ui.ChatLog;

public partial class ChatLog
{
    private string privateName = "";
    private uint privateWorld;

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
        ImGui.TextUnformatted("XLChat");
        if (ImGui.Button("＋ 新建私聊")) ImGui.OpenPopup("private-page-create");
        using var popup = ImRaii.Popup("private-page-create");
        if (!popup.Success) return;
        ImGui.TextUnformatted("独立私聊页面");
        if (Plugin.TargetManager.Target is IPlayerCharacter player && ImGui.Button("与当前选中玩家私聊"))
        {
            var target = TellTarget.Empty(); target.FromTarget(player);
            OpenPrivatePage(target); ImGui.CloseCurrentPopup();
        }
        using (var friends = ImRaii.Combo("好友", "从游戏好友列表选择"))
        {
            if (friends.Success)
                foreach (var friend in GameFunctions.GameFunctions.GetFriends())
                    if (ImGui.Selectable($"{friend.NameString}##{friend.ContentId}"))
                    {
                        OpenPrivatePage(new TellTarget(friend.NameString, friend.HomeWorld, friend.ContentId, TellReason.Friend));
                        ImGui.CloseCurrentPopup();
                    }
        }
        ImGui.Separator();
        ImGui.InputText("角色姓名", ref privateName, 64);
        var preview = Sheets.WorldSheet.TryGetRow(privateWorld, out var world) ? world.Name.ToString() : "选择服务器";
        using (var worlds = ImRaii.Combo("服务器", preview))
            if (worlds.Success)
                foreach (var entry in Sheets.WorldSheet)
                    if (entry.IsPublic && ImGui.Selectable($"{entry.Name}##{entry.RowId}", privateWorld == entry.RowId)) privateWorld = entry.RowId;
        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(privateName) || privateWorld == 0 || privateName.Contains('@') || privateName.Contains('/')))
            if (ImGui.Button("创建页面"))
            {
                OpenPrivatePage(new TellTarget(privateName.Trim(), privateWorld, 0, TellReason.Direct));
                ImGui.CloseCurrentPopup();
            }
        ImGui.TextDisabled("固定收发对象；右键页面可重命名或删除。");
    }
}
