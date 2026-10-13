# v0.4.0 设计和接口参考

本次按用户给出的顶部状态栏及六张弹出面板截图重做。BIS 入口明确去除。

## 查阅的 FF14 UI 项目

- [Umbra](https://github.com/una-xiv/umbra)：直接相关的工具栏/弹出面板设计。查阅 BattleEffects 和 OnlineStatus 等接口用法，核对战斗特效设置为 0=完全、1=简单、2=关闭，召唤兽尺寸为 0=小、1=中、2=大。
- [DelvUI](https://github.com/DelvUI/DelvUI)：可定制替代 HUD 和资源条设计。
- [FFXIV Material UI](https://github.com/skotlex/ffxiv-material-ui)：原版 UI 视觉皮肤参考；本项目不安装其贴图包，也不修改原版资源。
- [ChatTwo](https://github.com/Infiziert90/ChatTwo)：聊天侧栏、可变字号与过滤等交互参考。本项目未完整实现其富文本和无限标签能力。
- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs)：核对游戏已有设置、装备、声望、传送等类型和调用接口。
- [FFXIV datamining](https://github.com/xivapi/ffxiv-datamining)：核对普通动作 ID、在线状态与货币表；实际名称/图标使用玩家客户端数据。

参考界面和 API 事实；弹出面板采用本项目的 ImGui 实现，没有把上述插件安装或嵌入为运行依赖。

## 原版组件与读取范围

原版热键栏和战斗状态采用保留游戏渲染的方式，不重画一组看起来相似的图标，因此游戏现有键位、缩放、隐藏设置、冷却、目标和工具提示继续由游戏管理。自定义 HP/MP（或 GP/CP）和顶部信息分别替代 Parameter、DTR 根节点，停用时恢复原先可见状态。

读取当前已加载窗口的位置、比例和尺寸，尚未加载的窗口稍后补充。位置/尺寸变化继续记录；插件位置导入时限制在屏幕内。没有读取全部游戏配置文件或所有 HUD 档案。角色退出后清空会话窗口记录。

## 截图功能映射

特效 → 游戏 UiConfig；部族 → PlayerState/QuestManager 及 BeastTribe 表；在线 → 正常状态选择与个人搜索信息；职业 → 已保存的 RaptureGearsetModule 套装；装备 → EquippedItems 的耐久/精炼度；货币 → 原生计数、物品表上限及军票/周限专用接口；传送 → 当前 Telepo 列表；插件 → 已加载插件元数据；指令 → ICommandManager 当前注册表；外部 AI 等状态 → IDtrBar 已公开条目。

原版修理/精制/镶嵌动作 ID 分别为 6/14/12，来自 GeneralAction 表；用户点击才调用。不会进行自动修理、自动传送或自动执行插件指令。

## 限制

面板视觉不是对参考截图的逐像素复制；需要用户本机反馈才能核对实际字号、语言和布局。其他插件的私有状态没有通用读取方式；仅自动发现公开 DTR 项。DTR 人工事件的 AtkEventSource 为空，要求原生事件数据的插件可能需要专门适配。货币列表基于当前容器、持有数量及在役神典石，不保证覆盖所有专用钱包。个人搜索信息在游戏社交缓存未就绪时可能无法打开。没有 BIS 或完整原版窗口重写。v0.9.0 对 XLinUI 主窗口加入公开 API 的采样模糊玻璃。

测试验证管理状态和配置，不能替代 soil 游戏内对新增原生接口与各个 DTR 提供者的验证。


## v0.5.0：多栏与通用窗口审查

参考资料再次核查：[Umbra](https://github.com/una-xiv/umbra) 的模块化入口，[DelvUI](https://github.com/DelvUI/DelvUI) 的模块化 HUD 替代，以及 [FFXIVClientStructs 的热键栏结构](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonActionBarBase.cs)。读取字段以构建所用 API 15 程序集为准，不把网页的最新偏移写入插件。

| 功能 | 本版处理 | 未覆盖内容 |
| --- | --- | --- |
| 标准热键栏 1–10 | 识别已显示栏，读取每槽原生屏幕坐标和父级缩放，装饰叶节点可恢复地隐藏并画直角轮廓 | 图标贴图自身圆角不重采样；soil 实机坐标仍需验证 |
| 主栏翻页／共享栏／职业切换 | 保留原生窗口和操作，按当前槽位绘制 | 不制作另一套绑定与拖放状态 |
| 手柄跨界／双十字栏 | 保留原生功能 | 未改画其特殊布局外观 |
| 小地图 | 跟随原生根节点绘制方形外框 | 内容仍圆形裁切，不擅改地图遮罩和标记 |
| 背包、角色、市场等标准窗口 | 可选背景色调和透明度；逐窗排除 | 内容结构、标题和原生控制按钮保留 |
| 非标准专用窗口、任务对话、副本机制 | 识别已加载窗口并展示审查状态，原生内容保留 | 没有通用安全内容替代，不声称已经全量重做 |
| 主菜单功能 | 自动读取 MainCommand，提供搜索入口 | 服务 NPC 和上下文限定界面不是主菜单命令，不伪造入口 |
| 传送、货币、在线状态、职业、特效、聊天 | 继续使用已有简洁面板 | 原生回退入口仍保留 |

所有新外框使用 ImGui 非交互绘图列表，避免遮挡原生点击。坐标持续读取，不保存第二份热键栏位置；编辑布局仍使用游戏 HUD 布局界面。背景颜色按节点身份保存原值，退出、停止绘制、停用或关闭功能时只向实时枚举的同一节点恢复，PreFinalize 时丢弃失效身份。样式不保存到游戏 HUD 文件。

实际效果审查必须在国服 soil 中完成：测试至少三个可见热键栏（12×1、6×2、1×12），切换 80%/120% 缩放、拖动 HUD、职业切换和主栏翻页；确认拖放、鼠标点击、按键、冷却、提示继续正确。小地图须确认地图标记与缩放操作。原生窗口换肤须分别检查角色、背包、市场、任务、商店、交易和弹出确认；发现背景或文字异常时逐窗排除。云端构建和状态机测试不能证明这些实机检查已通过。

## v0.6.0：主动接口适配与覆盖审查

本轮重新核查 Dalamud `IBattleChara`、`ICharacter`、`ITargetManager`、`IPartyList`、`IStatus` 的当前构建文档，以及以下公开绑定；字段／方法均以 API 15 构建验证，不写死网页偏移：

- [BattleChara](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/ClientState/Objects/Types/BattleChara.cs)：状态列表、读条类型与 ID、当前／总读条时长、可打断标记。
- [StatusManager](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/StatusManager.cs)：仅在用户右键时调用 `ExecuteStatusOff`，游戏决定能否取消。不会使用本地 RemoveStatus 伪造效果消失。
- [AgentHUD](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentHUD.cs)：目标原生上下文菜单、主菜单功能可用状态。
- [InventoryContainer](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs)：只读取已加载的 Inventory1–4 容器，复制 ID、数量和 HQ，不缓存原生物品指针。
- [QuestWork](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Application/Network/WorkDefinitions/QuestWork.cs)：已接受普通任务 ID、优先与隐藏标记。不把 Sequence 伪装成完成百分比。
- [AddonNaviMap](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonNaviMap.cs)：使用 MapBase 边界，保留原生 Mask 与地图渲染。
- [AddonPartyList](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/AddonPartyList.cs)：仅背景装饰换色，不重写其控制、伙伴／NPC、仇恨、准备确认和排序信息。

目标、焦点、自身读条和状态新增真正的 ImGui 面板替代。所有替代使用每帧 readiness 位，完整内容与必要图标成功绘制才隐藏对应的确切原生 addon 名；未认识的 addon 不扩展匹配。自身资源和 DTR 也改用此策略，修复配置已启用但窗口没绘制时仍隐藏原版的问题。异步图标尚未就绪或状态表找不到对应行时保留原版，避免仅显示 ID 时就遮掉有效信息。特殊状态组保持原样。

读条动作按类型解析 Action／Item，不把任何 ID 都当作技能。无效计时或其他类型保留原版读条。状态叠层只在表定义 MaxStacks > 1 时解释 Param，避免把食物／药品参数当叠层。自身状态右键取消需已启用游戏交互；目标和焦点状态没有取消操作。

队伍概览目前可选，保留原生名单。背包与任务是简洁只读概览，保留完整原生操作入口，不以缺少操作的概览隐藏原生窗口。任务目标、商店交易与副本专用机制依旧需要逐功能实现，不能靠隐藏所有原版 UI 宣称已完成现代化。

默认启用现代目标、焦点、读条、自身状态、窗口背景及聊天隐藏。已有保存配置的明确选择保留。现代预设不会打开游戏交互或删除个人分类、顶部选择和排除名单。暂停游戏 HUD 显示时插件 HUD 也暂停；退出角色清除所有新增列表和初次布局导入标记。

实机验收新增：目标／焦点切换与清除，长短读条和可打断提示，永久状态／叠层／图标异步加载／状态取消权限；队伍跨区成员与原生准备确认，背包 HQ／堆叠／四页容量／登录数据未加载，普通任务优先／隐藏标记。云端 48 项测试验证策略与配置，不替代 soil 实机验证。


## v0.7.0：视野与透明界面

再次参考 Umbra 的轻量顶部入口、DelvUI 的独立 HUD 配置与 Material UI 的信息减负；不安装贴图模组，也没有拷贝其实现代码。上一版预览的高不透明度大面板遮挡场景过多，本版默认把窗口底板与信息内容分离。

`ForceTransparentPanels` 强制插件 WindowBg 为零；`ForceTransparentNative` 将已识别标准 Window NineGrid 叶节点及队伍背景的 alpha 置零。图标、按钮、文字、热键绑定和进度条仍保留。两者独立于 TextOnly／Glass／Opacity，可分别取消；已保存的关闭选择尊重，逐窗排除名单继续有效。背景 NineGrid 若带子内容则不进行透明处理，避免父透明度连带隐藏内容。

插件文字在自身 ImGui 绘图列表中先绘制 1px 黑色四向描边，再绘制文字；遵循父窗口／子窗口裁切。常规、换行、次要、彩色、按钮、选择项、勾选标签、状态时长与数量均覆盖。控件悬停仍使用轻量反馈。次要文本使用更明亮颜色；彩色标签按亮度下限向白色混合，保留色调排序，不通过整窗 alpha 去降低内容可见度。细进度条使用暗色外沿，提高浅色背景下的识别。

原生文字仅在已识别 Window 组件内及原生队伍范围递归适配（深度和节点数有界）；保存描边 RGBA 与 Edge 位，保留原生正文颜色、字体、链接和布局。恢复时只恢复拥有的 Edge 位，避免把游戏运行时改变的 Bold、WordWrap 等位还原成过时值；不会向失效节点地址写入。该方案需在 soil 实机确认字体缓存与渲染效果，构建通过不能证明已呈现描边。

无状态时隐藏插件占位窗口，新增选项允许为排版临时显示。默认目标与读条高度、窗口 padding 和间距降低。设置拆为五个分页；接口结构细节集中在原版适配页面。增加亮／暗背景的独立演示预览，并保留 v0.6 画面对照；场景和数据均为示意，不声称它们是实际游戏渲染。

验证新增 12 项透明度／对比度／描边恢复测试和 3 项配置测试，累计 63 项通过。实际验证清单：亮背景、暗背景、繁忙战斗场景中的文字；市场／背包／交易／任务窗口的正文、按钮与焦点；开启、关闭与逐窗排除后背景和描边恢复；字号缩放、聊天换行和长列表滚动；字体缓存和状态颜色保持不变。真实模糊、特殊窗口背景和全量贴图换肤依旧不在已完成范围。


## v0.8.0：聊天布局与游戏接口

继续参考 ChatTwo 的分类视图与游戏链接处理方向、Umbra 的轻量配置入口；未复制这些插件的代码。新增聊天实现为独立 partial，配置分离出导航、内容呈现、尺寸、分组和布局档案。

实际数据来源：`IChatGui.ChatMessageUnhandled`（可选 `ChatMessageHandled`）的 `LogKind`、sender、message 及 Player／Map／Item payload；客户端 `LogFilter.Name/LogKind` 提供本地化频道名称。时间使用事件的原生 Unix 时间戳转本地时间，缺失时才回退到接收时间。发送使用 `UIModule.ProcessChatBoxEntry`，前置白名单只构造固定聊天命令，私信目标与全文 UTF-8 长度验证后才进入游戏。发送入口已提交不代表服务端送达；附近游戏错误提示在输入区展示，不伪造成功确认。通讯贝、新人等频道权限由游戏决定。

地图链接复制 Territory、Map 与原始整数坐标，用户右键选择后重建 `MapLinkPayload`，交给 `IGameGui.OpenMapWithMapLink`。玩家链接只复制名称／世界 ID，回复时读取 World 名并填入私信收件人，不自动发送；物品链接只提供表中基本说明，没有伪装成完整原生物品菜单。未识别或未实现的链接有直接恢复原版聊天入口。

采用处理链后的最终消息，默认不重新显示其他插件已隐藏的消息；“也显示已处理消息”选项可明确开启。事件对象只在回调期间使用。拥有的字符串和数值快照进入有界 ConcurrentQueue，由框架更新统一写入历史与未读状态；退出角色清空队列、历史、草稿、链接和错误提示。视图过滤缓存按历史修订、分类和搜索失效；默认只展开最近 400 条，搜索仍针对完整保留历史，加载更早可展开剩余内容。

尺寸按当前窗口可用空间计算，不让侧栏或输入区完全占据消息区域。分隔线拖动使用实时计算的区域大小，而非过大的未生效设定值；主窗保存相对主视口的位置，应用档案时重新夹取到当前屏幕。消息正文显式设置换行宽度，长发言人会换行，时间戳在窄区域另起一行；过小尺寸导致输入框被裁切时不接管原版聊天隐藏。

布局档案仅允许 Configuration 中 Chat 前缀的数值、布尔、枚举和矢量尺寸字段，不允许修改 NativeInteractions 等授权项，不保存聊天文本或分类过滤。应用后统一归一化，未知键和损坏值忽略。旧配置保留尺寸和分类，补齐默认发送路由、合法 ID 和新的显示选项。接收过滤与发送目的地分离，自动采用分类默认发送路由为显式开关。

测试新增 11 项发送白名单、私信参数、UTF-8 上限、过滤与区域布局检查，以及 5 项档案、迁移、无效值和查看／发送状态保存检查，累计 79 项通过。网页预览验证导航左右切换／顶部标签、拖动分隔、宽窄适配、分类草稿、档案保存应用、右键准备私信和游戏不连接声明。soil 实机的实际布局、IME 输入、地图打开、频道权限与送达仍未验证。


## v0.9.0 素材和字体

AppearanceAssets 拥有图片、玻璃纹理、独立托管字体图集和文件选择器。字体/图片参数变化防抖 350ms；字体按指定 TTC face 导入，合并 zh-CN Windows 字体、Dalamud 补充字形和游戏符号。素材大小限制 32 MB，导入副本以 SHA256 命名，只存用户插件配置目录。无素材随源代码或 Release 分发。

玻璃用 ITextureProvider.CreateFromImGuiViewportAsync(TakeBeforeImGuiRender=true, AutoUpdate=false) 采样主视口，避免插件界面反馈；使用 ITextureReadbackProvider 公开接口转换 RGBA8、缩小到宽 480 后进行三次可分离 box blur。读回 Pitch 可以有行填充，先打包连续行；透明图片采用预乘 alpha 模糊避免透明 RGB 污染。每次仅一个 capture task，频率最高 4 Hz；退出/关闭时取消并释放未完成任务产物。采样不写磁盘、不传网络，不采用 DirectX 私有挂钩。辅助视口回退颜色背景。

背景绘制在各 XLinUI 根窗口内容前，圆角图像与填充在窗口剪裁矩形内；聊天独立背景或 text-only 可覆盖全局素材。透明背景与原版文字样式继续独立。云中完成 API 15 编译和 87 项策略/配置/模糊测试，无法实机证明 soil 画面捕获兼容性或字体渲染效果。
