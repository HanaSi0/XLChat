using XLChat.Resources;
using XLChat.Util;
using Dalamud;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace XLChat.Ui.SettingsTabs;

public class Fonts : ISettingsTab
{
    private Configuration Mutable { get; }

    public string Name => Language.Options_Fonts_Tab + "###tabs-fonts";

    public Fonts(Configuration mutable)
    {
        Mutable = mutable;
    }

    public void Draw(bool _)
    {
        using var wrap = ImRaii.TextWrapPos(0.0f);

        ImGui.Checkbox("自动补全简体中文（推荐）",ref Mutable.EnsureChineseFont);
        if(ImGui.Button("修复中文缺字（恢复默认字体）"))
        {
            Mutable.EnsureChineseFont=Mutable.FontsEnabled=true;
            var size=Mutable.GlobalFontV2.SizePt;
            Mutable.GlobalFontV2=new SingleFontSpec{FontId=new DalamudAssetFontAndFamilyId(DalamudAsset.NotoSansCjkRegular),SizePt=size};
            Mutable.JapaneseFontV2=new SingleFontSpec{FontId=new DalamudAssetFontAndFamilyId(DalamudAsset.NotoSansCjkMedium),SizePt=size};
        }
        ImGui.TextWrapped("自动加入中文、标点和全角字形，并使用 Windows 中文字体补缺。关闭自定义字体时也可保留中文补全。");
        ImGui.Checkbox(Language.Options_FontsEnabled, ref Mutable.FontsEnabled);
        ImGui.Spacing();

        if (!Mutable.FontsEnabled)
        {
            ImGuiUtil.FontSizeCombo(Language.Options_FontSize_Name, ref Mutable.FontSizeV2);
        }
        else
        {
            var globalChooser = ImGuiUtil.FontChooser(Language.Options_Font_Name, Mutable.GlobalFontV2, false, ref _);
            globalChooser?.ResultTask.ContinueWith(r =>
            {
                if (r.IsCompletedSuccessfully)
                    Mutable.GlobalFontV2 = r.Result;
            });
            ImGui.SameLine();
            if (ImGui.Button("Reset##global"))
                Mutable.GlobalFontV2 = new SingleFontSpec{ FontId = new DalamudAssetFontAndFamilyId(DalamudAsset.NotoSansCjkRegular), SizePt = 12.75f };

            ImGuiUtil.HelpText(string.Format(Language.Options_Font_Description, Plugin.PluginName));
            ImGuiUtil.WarningText(Language.Options_Font_Warning);
            ImGui.Spacing();

            // LocaleNames being null means it is likely a game font which all support JP symbols
            var japaneseChooser = ImGuiUtil.FontChooser(Language.Options_JapaneseFont_Name, Mutable.JapaneseFontV2, false, ref _, id => !id.LocaleNames?.ContainsKey("ja-jp") ?? false, "いろはにほへと   ちりぬるを");
            japaneseChooser?.ResultTask.ContinueWith(r =>
            {
                if (r.IsCompletedSuccessfully)
                    Mutable.JapaneseFontV2 = r.Result;
            });
            ImGui.SameLine();
            if (ImGui.Button("Reset##japanese"))
                Mutable.JapaneseFontV2 = new SingleFontSpec{ FontId = new DalamudAssetFontAndFamilyId(DalamudAsset.NotoSansCjkMedium), SizePt = 12.75f };

            ImGuiUtil.HelpText(string.Format(Language.Options_JapaneseFont_Description, Plugin.PluginName));
            ImGui.Spacing();

            var italicChooser = ImGuiUtil.FontChooser(Language.Options_ItalicFont_Name, Mutable.ItalicFontV2, true, ref Mutable.ItalicEnabled);
            italicChooser?.ResultTask.ContinueWith(r =>
            {
                if (r.IsCompletedSuccessfully)
                    Mutable.ItalicFontV2 = r.Result;
            });
            ImGui.SameLine();
            if (ImGui.Button("Reset##italic"))
            {
                Mutable.ItalicEnabled = false;
                Mutable.ItalicFontV2 = new SingleFontSpec{ FontId = new DalamudAssetFontAndFamilyId(DalamudAsset.NotoSansCjkRegular), SizePt = 12.75f };
            }

            ImGuiUtil.HelpText(string.Format(Language.Options_Italic_Description, Plugin.PluginName));
            ImGui.Spacing();

            if (ImGui.CollapsingHeader(Language.Options_ExtraGlyphs_Name))
            {
                ImGuiUtil.HelpText(string.Format(Language.Options_ExtraGlyphs_Description, Plugin.PluginName));

                var range = (int) Mutable.ExtraGlyphRanges;
                foreach (var extra in Enum.GetValues<ExtraGlyphRanges>())
                    ImGui.CheckboxFlags(extra.Name(), ref range, (int) extra);

                Mutable.ExtraGlyphRanges = (ExtraGlyphRanges) range;
            }

            ImGui.Spacing();
        }

        ImGuiUtil.FontSizeCombo(Language.Options_SymbolsFontSize_Name, ref Mutable.SymbolsFontSizeV2);
        ImGuiUtil.HelpText(Language.Options_SymbolsFontSize_Description);

        ImGui.Spacing();
    }
}
