using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private void ChatSlider(string label,float value,float minimum,float maximum,Action<float> set,ref bool changed,string format="%.0f")
    {if(ImGui.SliderFloat(label,ref value,minimum,maximum,format)){set(value);changed=true;}}
    private void DrawChatCustomization(ref bool changed)
    {
        changed|=Toggle("显示插件聊天",C.ShowChat,v=>C.ShowChat=v);
        changed|=Toggle("显示插件聊天时隐藏原版聊天",C.HideOriginalChat,v=>C.HideOriginalChat=v);
        if(ImGui.CollapsingHeader("窗口与各区域尺寸",ImGuiTreeNodeFlags.DefaultOpen))
        {
            ChatSlider("主窗宽度",C.ChatWidth,320,3840,v=>{C.ChatWidth=v;applyChatSize=true;},ref changed);
            ChatSlider("主窗高度",C.ChatHeight,200,2160,v=>{C.ChatHeight=v;applyChatSize=true;},ref changed);
            var nav=(int)C.ChatNavigation;if(ImGui.Combo("频道导航位置",ref nav,new[]{"左侧","右侧","顶部标签","隐藏侧栏（保留选择器）"},4)){C.ChatNavigation=(ChatNavigation)nav;changed=true;}
            ChatSlider("频道栏宽度",C.ChatSidebarWidth,40,600,v=>C.ChatSidebarWidth=v,ref changed);
            ChatSlider("频道行高度",C.ChatSidebarItemHeight,20,80,v=>C.ChatSidebarItemHeight=v,ref changed);
            ChatSlider("输入区高度",C.ChatComposerHeight,60,300,v=>C.ChatComposerHeight=v,ref changed);
            ChatSlider("输入框宽度（0为填满）",C.ChatInputWidth,0,3000,v=>C.ChatInputWidth=v,ref changed);
            ChatSlider("输入框高度",C.ChatInputHeight,24,100,v=>C.ChatInputHeight=v,ref changed);
            ChatSlider("发送频道选择器宽度",C.ChatSelectorWidth,60,300,v=>C.ChatSelectorWidth=v,ref changed);
            ChatSlider("发送按钮高度",C.ChatSendButtonHeight,24,100,v=>C.ChatSendButtonHeight=v,ref changed);
            ChatSlider("搜索框宽度（0为填满）",C.ChatSearchWidth,0,3000,v=>C.ChatSearchWidth=v,ref changed);
            ChatSlider("搜索框高度",C.ChatSearchHeight,24,80,v=>C.ChatSearchHeight=v,ref changed);
            ChatSlider("滚动条宽度",C.ChatScrollbarWidth,8,30,v=>C.ChatScrollbarWidth=v,ref changed);
            ChatSlider("发送按钮宽度",C.ChatSendButtonWidth,40,180,v=>C.ChatSendButtonWidth=v,ref changed);
            ChatSlider("消息最大宽度（0为填满）",C.ChatMessageWidth,0,3000,v=>C.ChatMessageWidth=v,ref changed);
            ChatSlider("面板内边距",C.ChatPadding,0,30,v=>C.ChatPadding=v,ref changed);
            ReadableText.TextDisabled("也可拖动频道栏／消息区、消息区／输入区的分隔线。窄窗口会自动限制区域大小。");
        }
        if(ImGui.CollapsingHeader("文字、显示与独立背景"))
        {
            ChatSlider("消息字号比例",C.ChatFontScale,.6f,2.5f,v=>C.ChatFontScale=v,ref changed,"%.2f");
            ChatSlider("分组发言人字号比例",C.ChatHeaderFontScale,.6f,2,v=>C.ChatHeaderFontScale=v,ref changed,"%.2f");
            ChatSlider("分组时间戳字号比例",C.ChatTimestampFontScale,.6f,1.5f,v=>C.ChatTimestampFontScale=v,ref changed,"%.2f");
            ChatSlider("频道栏字号比例",C.ChatSidebarFontScale,.6f,2,v=>C.ChatSidebarFontScale=v,ref changed,"%.2f");
            ChatSlider("消息间距",C.ChatLineSpacing,0,30,v=>C.ChatLineSpacing=v,ref changed);
            ChatSlider("分组间距",C.ChatGroupSpacing,0,50,v=>C.ChatGroupSpacing=v,ref changed);
            var format=(int)C.ChatPresentation;if(ImGui.Combo("消息格式",ref format,new[]{"社交分组","紧凑日志"},2)){C.ChatPresentation=(ChatPresentation)format;changed=true;}
            changed|=Toggle("合并同一发言人的连续消息",C.ChatGroupMessages,v=>C.ChatGroupMessages=v);
            changed|=Toggle("时间戳显示秒",C.ChatTimestampSeconds,v=>C.ChatTimestampSeconds=v);
            changed|=Toggle("显示时间戳",C.ChatShowTimestamp,v=>C.ChatShowTimestamp=v);
            changed|=Toggle("显示游戏频道名称",C.ChatShowChannelName,v=>C.ChatShowChannelName=v);
            changed|=Toggle("显示发送按钮（关闭后仅 Enter 发送）",C.ChatShowSendButton,v=>C.ChatShowSendButton=v);
            changed|=Toggle("也显示被其他插件处理／隐藏的消息",C.ChatIncludeHandled,v=>C.ChatIncludeHandled=v);
            changed|=Toggle("显示搜索框",C.ChatShowSearch,v=>C.ChatShowSearch=v);
            changed|=Toggle("自动跟随新消息",C.ChatFollow,v=>C.ChatFollow=v);
            var bg=(int)C.ChatBackground;if(ImGui.Combo("聊天独立背景",ref bg,new[]{"跟随全局","始终透明","自定义背景（覆盖全局）"},3)){C.ChatBackground=(ChatBackground)bg;changed=true;}
            if(C.ChatBackground==ChatBackground.Custom)ChatSlider("聊天背景不透明度",C.ChatBackgroundOpacity,0,1,v=>C.ChatBackgroundOpacity=v,ref changed,"%.2f");
            var page=C.ChatPageSize;if(ImGui.SliderInt("每次展开消息条数",ref page,100,2000)){C.ChatPageSize=page;changed=true;}
            var limit=C.ChatHistoryLimit;if(ImGui.SliderInt("历史消息容量",ref limit,200,10000)){C.ChatHistoryLimit=limit;changed=true;}
        }
        if(ImGui.CollapsingHeader("布局档案"))
        {
            ImGui.InputText("档案名称",ref profileName,64);
            if(ReadableText.Button("保存／覆盖当前聊天布局")&&profileName.Trim().Length>0)
            {C.ChatProfiles[profileName.Trim()]=ChatLayoutProfile.Capture(C);profileSelection=profileName.Trim();changed=true;}
            var names=C.ChatProfiles.Keys.ToArray();
            if(names.Length>0)
            {
                var selected=Math.Max(0,Array.IndexOf(names,profileSelection));
                if(ImGui.Combo("已保存档案",ref selected,names,names.Length))profileSelection=names[selected];
                var key=names[selected];
                if(ReadableText.Button("应用档案")){C.ChatProfiles[key].Apply(C);applyChatSize=true;applyChatPosition=true;importedLayouts.Remove("chat");changed=true;}
                ImGui.SameLine();if(ReadableText.Button("删除档案")){C.ChatProfiles.Remove(key);changed=true;}
            }
            if(ReadableText.Button("重新从原版聊天导入位置")){C.ChatHasPosition=false;importedLayouts.Remove("chat");applyChatPosition=true;changed=true;}
            ReadableText.TextDisabled("档案只保存聊天布局与显示选项，不保存消息或改变游戏交互授权。");
        }
        if(ImGui.CollapsingHeader("分类、频道过滤与颜色"))
        {
            changed|=Toggle("切换分类时采用该分类的默认发送频道",C.ChatAutoSendRoute,v=>C.ChatAutoSendRoute=v);
            ReadableText.TextDisabled("接收过滤与发送频道独立；发送前请看输入区的目的地。空过滤表示全部消息。");
            Channel? remove=null;int move=-1,moveBy=0;
            for(var i=0;i<C.Channels.Count;i++)
            {
                var channel=C.Channels[i];ImGui.PushID(channel.Id);
                try
                {
                    if(ImGui.TreeNode(channel.Name+"##category"))
                    {
                        var name=channel.Name;if(ImGui.InputText("分类名称",ref name,32)){channel.Name=name;changed=true;}
                        if(i>0&&ReadableText.SmallButton("上移")){move=i;moveBy=-1;}ImGui.SameLine();
                        if(i<C.Channels.Count-1&&ReadableText.SmallButton("下移")){move=i;moveBy=1;}ImGui.SameLine();
                        if(C.Channels.Count>1&&ReadableText.SmallButton("删除"))remove=channel;
                        var route=Math.Max(0,ChatPolicy.Routes.FindIndex(r=>r.Key==channel.SendRoute));
                        if(ImGui.Combo("默认发送频道",ref route,ChatPolicy.Routes.Select(r=>r.Label).ToArray(),ChatPolicy.Routes.Count)){channel.SendRoute=ChatPolicy.Routes[route].Key;changed=true;}
                        var custom=channel.CustomColor;if(ReadableText.Checkbox("分类独立颜色",ref custom)){channel.CustomColor=custom;changed=true;}
                        if(custom){var color=channel.Color;if(ImGui.ColorEdit3("发言人颜色",ref color)){channel.Color=color;changed=true;}}
                        if(ImGui.TreeNode("关联游戏频道"))
                        {
                            foreach(var type in Enum.GetValues<XivChatType>().Distinct())
                            {var id=(ushort)type;var selected=channel.Types.Contains(id);if(ReadableText.Checkbox($"{ChannelName(id)} ({id})",ref selected)){if(selected)channel.Types.Add(id);else channel.Types.Remove(id);changed=true;}}
                            ImGui.TreePop();
                        }
                        ImGui.TreePop();
                    }
                }
                finally{ImGui.PopID();}
            }
            if(move>=0){(C.Channels[move],C.Channels[move+moveBy])=(C.Channels[move+moveBy],C.Channels[move]);changed=true;}
            if(remove is not null){C.Channels.Remove(remove);plugin.Unread.Remove(remove.Id);channelDrafts.Remove(remove.Id);changed=true;}
            if(ReadableText.Button("添加分类")){C.Channels.Add(new Channel("新分类",[]));changed=true;}
        }
        ReadableText.TextDisabled("右键消息：复制、引用、私信发言人、打开已识别地图链接。物品只提供基本说明，完整交互保留原版聊天。");
    }
}
