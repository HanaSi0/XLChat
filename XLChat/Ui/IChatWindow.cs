using System.Numerics;
using XLChat.GameFunctions.Types;

namespace XLChat.Ui;

public interface IChatWindow
{
    Vector2 LastWindowPos { get; set; }
    Vector2 LastWindowSize { get; set; }
    HideState CurrentHideState { get; set; }
}