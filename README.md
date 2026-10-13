# XLChat · 中国服特供

XLChat 基于 **Chat Two 源码重建**，由 **HanaSi0** 继续开发与维护，定位为 FF14 **中国服特供**聊天插件。

Chat Two 原作者：**Infi、Anna**。原项目：[Infiziert90/ChatTwo](https://github.com/Infiziert90/ChatTwo)。感谢原作者提供的聊天界面、频道筛选与消息处理实现。

当前版本 **1.2.0**，构建目标为国服 **soil XIVLauncher / Dalamud API 15**。实际国服兼容性以游戏内运行结果为准。

## Thanks to

The original dev Anna~

## 安装

[下载 XLChat.zip](https://github.com/HanaSi0/XLChat/releases/download/v1.2.0/XLChat.zip)，完整解压安装包，保留主 DLL 和全部依赖文件。

Dalamud 自定义插件仓库：

```text
https://raw.githubusercontent.com/HanaSi0/XLChat/main/pluginmaster.json
```

## 字体与背景

设置 → 字体：默认开启“自动补全简体中文”，加入中文、标点和全角字形，并使用 Windows 中文字体补缺。游戏符号使用单独的符号范围；关闭自定义字体时仍可保留中文补全。

设置 → 外观与背景：选择纯色、毛玻璃（静态缓存）或上传图片，提供色盘、不透明度和圆角选项。图片支持 PNG/JPG/BMP，不超过 32 MB、1600 万像素；导入后保存在插件本机配置目录。修改后点击保存应用。

毛玻璃使用静态纹理或导入图片的 CPU 模糊缓存，不采样游戏画面、不读回 GPU。只有素材或模糊强度改变时重新生成；改变色调、透明度和窗口大小不重复处理图片。无图片或载入失败时使用颜色背景。弹出分类继承背景，也可使用原有独立透明度。

中文效果及游戏内透明度需要客户端验证；若字形仍缺失，可在字体设置中恢复默认字体并保持中文补全开启。

## 授权

沿用 Chat Two 的源码遵循 **EUPL 1.2**；原作者、源码来源和修改记录见 [COPYRIGHT](COPYRIGHT)，许可证全文见 [LICENCE](LICENCE)。

聊天页面默认使用可调整宽度的侧栏，点击“新建私聊”可选择游戏好友、当前目标，或填写角色姓名和服务器。好友页面固定私聊对象，支持独立未读数。右键页面可重命名、删除或弹出。

设置 → 外观与背景 → 消息样式：可选择纯文字或聊天气泡，分别设置接收、发出气泡的颜色、透明度及圆角。
