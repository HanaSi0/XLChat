namespace XLinUI;
internal static class ChannelEditPolicy
{
    internal static string CommitName(string draft,string previous)=>string.IsNullOrWhiteSpace(draft)?previous:draft.Trim();
    internal static string Header(string name)=>name+"###category";
}
