using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TEngine.Editor.GameObjectPool
{
    /// <summary>
    /// 配置条目的序列化字段名。
    /// <remarks>
    /// 必须与 <see cref="GameObjectPoolConfig"/> 的公有字段逐字一致。
    /// <c>FindPropertyRelative</c> 找不到字段时返回 <c>null</c> 且不报错，
    /// 字段名写错的表现是"面板上某一行凭空消失"，不是编译错误，也不是异常堆栈。
    /// 因此字面量只允许出现在这里，绘制代码一律引用常量。
    /// </remarks>
    /// </summary>
    internal static class GameObjectPoolFieldName
    {
        /// <summary>
        /// 配置资产上的条目数组字段。
        /// </summary>
        internal const string Pools = "_pools";

        /// <summary>
        /// 池名。
        /// </summary>
        internal const string PoolName = "PoolName";

        /// <summary>
        /// YooAsset 定位地址。
        /// </summary>
        internal const string AssetName = "AssetName";

        /// <summary>
        /// 预热数量。
        /// </summary>
        internal const string PrewarmCount = "PrewarmCount";

        /// <summary>
        /// 每帧预热数量。
        /// </summary>
        internal const string PrewarmPerFrame = "PrewarmPerFrame";

        /// <summary>
        /// 容量目标。
        /// </summary>
        internal const string Capacity = "Capacity";

        /// <summary>
        /// 实例上限。
        /// </summary>
        internal const string MaxInstanceCount = "MaxInstanceCount";

        /// <summary>
        /// 自动释放间隔。
        /// </summary>
        internal const string AutoReleaseInterval = "AutoReleaseInterval";

        /// <summary>
        /// 实例级空闲过期时间。
        /// </summary>
        internal const string ExpireTime = "ExpireTime";

        /// <summary>
        /// 整池过期时间。
        /// </summary>
        internal const string PoolExpireTime = "PoolExpireTime";

        /// <summary>
        /// 整池自动释放间隔。
        /// </summary>
        internal const string PoolAutoReleaseInterval = "PoolAutoReleaseInterval";

        /// <summary>
        /// 回收优先级。
        /// </summary>
        internal const string Priority = "Priority";
    }

    /// <summary>
    /// 对象池配置面板共用的文案、颜色、图标与绘制积木。
    /// <remarks>
    /// 全部做成静态只读：IMGUI 一次重绘至少走 Layout 与 Repaint 两遍，
    /// 在方法体里 <c>new GUIContent</c> / <c>new Color</c> 会按
    /// "条目数 × 字段数 × 事件数" 的量级产生垃圾。这里集中构造一次，绘制代码只管引用。
    /// 就地错误文案直接带上 "× " 前缀：它已经是最终显示内容，
    /// 免掉每帧一次 <c>"× " + message</c> 的拼接。
    /// </remarks>
    /// </summary>
    internal static class GameObjectPoolInspectorContent
    {
        /// <summary>
        /// 底部"配置检查"区每类最多显示的条数，超出只报剩余数量。
        /// </summary>
        internal const int MAX_MESSAGE_COUNT = 8;

        /// <summary>
        /// 每列的最小内容宽度，确保长中文标签和时间输入可读。
        /// </summary>
        internal const float MIN_COLUMN_WIDTH = 200f;

        /// <summary>
        /// 两个字段或回收分区之间的留白。
        /// </summary>
        internal const float SECTION_GAP = 12f;

        /// <summary>
        /// Unity 内置 label 未显式设置字号时的实际字号，用于在它基础上加一号。
        /// </summary>
        private const int DEFAULT_FONT_SIZE = 11;

        /// <summary>
        /// 过期方式下拉框的选项文案。下标与 <see cref="GameObjectPoolExpireMode"/> 一一对应。
        /// </summary>
        internal static readonly string[] ModeOptionNames = { "按时间回收", "永不过期" };

        /// <summary>
        /// 就地错误的文字颜色。
        /// </summary>
        private static readonly Color ERROR_COLOR = new Color(0.94f, 0.38f, 0.34f);

        /// <summary>
        /// Pro 皮肤下的分隔线颜色。
        /// </summary>
        private static readonly Color SEPARATOR_PRO = new Color(1f, 1f, 1f, 0.12f);

        /// <summary>
        /// 亮色皮肤下的分隔线颜色。
        /// </summary>
        private static readonly Color SEPARATOR_PERSONAL = new Color(0f, 0f, 0f, 0.18f);

        /// <summary>
        /// 缓存的自建样式。惰性创建：<c>EditorStyles</c> 与 <c>GUI.skin</c> 只有在绘制阶段才保证可用，
        /// 在静态构造里取会在域重载时拿到 null。同一主题内复用，切换主题时重建。
        /// </summary>
        private static GUIStyle _fieldLabel;
        private static GUIStyle _groupTitle;
        private static GUIStyle _assetTitle;
        private static GUIStyle _card;
        private static GUIStyle _wrappedLabel;
        private static GUIStyle _errorLabel;
        private static GUIStyle _statusLabel;
        private static bool _isProSkin;

        /// <summary>
        /// 卡片边距与内部留白，避免字段贴边。
        /// </summary>
        internal static GUIStyle CardStyle => _card;

        /// <summary>
        /// 可换行的辅助文案样式。
        /// </summary>
        internal static GUIStyle WrappedLabel => _wrappedLabel;

        /// <summary>
        /// 当前主题的结构强调色。
        /// </summary>
        internal static Color AccentColor => _isProSkin
            ? new Color(0.35f, 0.65f, 0.95f) : new Color(0.12f, 0.36f, 0.64f);

        /// <summary>
        /// 在绘制入口初始化样式；切换主题后重建，避免沿用另一主题的文字颜色。
        /// </summary>
        internal static void EnsureStyles()
        {
            if (_card != null && _isProSkin == EditorGUIUtility.isProSkin)
            {
                return;
            }

            _isProSkin = EditorGUIUtility.isProSkin;
            _fieldLabel = null;
            _groupTitle = null;
            _assetTitle = null;
            _card = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(0, 0, 4, 8)
            };
            _wrappedLabel = new GUIStyle(EditorStyles.wordWrappedLabel);
            _errorLabel = new GUIStyle(_wrappedLabel);
            _errorLabel.normal.textColor = StatusColor(true, false);
            _statusLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        /// <summary>
        /// 根据内容宽度选择双列；每列保留完整中文标签与输入区的空间。
        /// </summary>
        internal static bool UseTwoColumns(float width)
        {
            return width >= MIN_COLUMN_WIDTH * 2f + SECTION_GAP;
        }

        /// <summary>
        /// 错误优先于警告，深浅主题分别使用可读的状态色。
        /// </summary>
        internal static Color StatusColor(bool hasError, bool hasWarning)
        {
            if (hasError)
            {
                return _isProSkin ? ERROR_COLOR : new Color(0.7f, 0.15f, 0.12f);
            }

            if (hasWarning)
            {
                return _isProSkin ? new Color(0.96f, 0.72f, 0.3f) : new Color(0.5f, 0.31f, 0.03f);
            }

            return _isProSkin ? new Color(0.4f, 0.8f, 0.61f) : new Color(0.1f, 0.43f, 0.27f);
        }

        /// <summary>
        /// 绘制带文字的状态徽标，颜色不作为唯一的信息来源。
        /// </summary>
        internal static void DrawStatus(Rect rect, bool hasError, bool hasWarning)
        {
            Color color = StatusColor(hasError, hasWarning);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.14f));
            }

            _statusLabel.normal.textColor = color;
            GUI.Label(rect, hasError ? "存在错误" : hasWarning ? "存在警告" : "配置正常", _statusLabel);
        }

        /// <summary>
        /// 标签与输入分行，避免中文标签挤占输入框；保留 SerializedProperty 编辑语义。
        /// </summary>
        internal static void DrawField(SerializedProperty property, GUIContent label)
        {
            GUILayout.Label(label, FieldLabel, GUILayout.MinWidth(0f));
            EditorGUILayout.PropertyField(property, GUIContent.none, GUILayout.MinWidth(0f));
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 字段标签样式，与 <see cref="EditorStyles.label"/> 同字号（供手动绘制的行使用）。
        /// </summary>
        internal static GUIStyle FieldLabel
        {
            get
            {
                if (_fieldLabel == null)
                {
                    _fieldLabel = new GUIStyle(EditorStyles.label);
                }

                return _fieldLabel;
            }
        }

        /// <summary>
        /// 分组标题样式：比正文粗一号，用于"预热与容量"这类小标题。
        /// </summary>
        internal static GUIStyle GroupTitleStyle
        {
            get
            {
                if (_groupTitle == null)
                {
                    _groupTitle = new GUIStyle(EditorStyles.boldLabel);
                    _groupTitle.fontSize = ResolveFontSize() + 1;
                    _groupTitle.normal.textColor = AccentColor;
                }

                return _groupTitle;
            }
        }

        /// <summary>
        /// 池标题样式：比分组标题再大一号，让每个卡片一眼有明确的标题。
        /// </summary>
        internal static GUIStyle AssetTitleStyle
        {
            get
            {
                if (_assetTitle == null)
                {
                    _assetTitle = new GUIStyle(EditorStyles.boldLabel);
                    _assetTitle.fontSize = ResolveFontSize() + 2;
                    _assetTitle.wordWrap = true;
                }

                return _assetTitle;
            }
        }

        /// <summary>
        /// 取 Unity 当前皮肤的 label 字号；未显式设置时退回 <see cref="DEFAULT_FONT_SIZE"/>。
        /// </summary>
        /// <returns>基准字号。</returns>
        private static int ResolveFontSize()
        {
            int size = EditorStyles.label != null ? EditorStyles.label.fontSize : 0;
            return size > 0 ? size : DEFAULT_FONT_SIZE;
        }

        /// <summary>
        /// 分组标题下的分隔线颜色，跟随当前编辑器皮肤深浅。
        /// </summary>
        internal static Color SeparatorColor =>
            EditorGUIUtility.isProSkin ? SEPARATOR_PRO : SEPARATOR_PERSONAL;

        internal static readonly GUIContent PoolNameLabel = new GUIContent(
            "池名称", "稳定逻辑键，大小写敏感，不随资源改名变化；业务按这个名字借出实例");

        internal static readonly GUIContent AssetNameLabel = new GUIContent(
            "资源地址", "YooAsset 定位地址：文件名去扩展名，如 Coin；不带 Assets/ 前缀、不带扩展名、不含反斜杠");

        internal static readonly GUIContent PrewarmCountLabel = new GUIContent(
            "预热数量", "初始化时提前创建的对象数量，0 表示不预热；这不是最低保有量，借出后不会自动补齐");

        internal static readonly GUIContent PrewarmPerFrameLabel = new GUIContent(
            "每帧预热数量", "本池分帧预热时每帧最多发起的创建请求数，只约束本池节奏，与其他池无关");

        internal static readonly GUIContent CapacityLabel = new GUIContent(
            "容量目标", "总数超过该值时底层会尝试释放空闲实例；它是软保留目标，不是实例硬上限");

        internal static readonly GUIContent MaxInstanceCountLabel = new GUIContent(
            "实例上限", "实例总量硬上限，0 表示不限制");

        internal static readonly GUIContent AutoReleaseIntervalLabel = new GUIContent(
            "自动释放间隔（秒）", "底层多久扫描一次可释放的空闲实例；只是扫描频率，不是实例存活时长");

        internal static readonly GUIContent InstanceExpireLabel = new GUIContent(
            "实例过期时间（秒）",
            "单个空闲实例多久后被回收（秒）。它和下面的整池过期是两个概念：这里管单个实例，下面管整个池");

        internal static readonly GUIContent PoolExpireLabel = new GUIContent(
            "整池过期时间（秒）",
            "池内所有对象都不在借出状态时才开始倒计时，有人借出则本轮作废、全部归还后重新计完整时长；"
            + "预热期间不计时，预热结束即刻开始计时");

        internal static readonly GUIContent PoolAutoReleaseIntervalLabel = new GUIContent(
            "整池自动释放间隔（秒）", "本池多久检查一次整池是否该释放；只是扫描频率，空闲多久释放由整池过期时间决定");

        internal static readonly GUIContent PriorityLabel = new GUIContent(
            "优先级", "底层池之间被回收的排序权重，数值越大越晚被回收；它不是禁止销毁的标记");

        /// <summary>
        /// 整池过期关闭时代替扫描间隔输入框显示的文案。
        /// </summary>
        internal static readonly GUIContent PoolIntervalPlaceholder = new GUIContent("整池不过期，无需扫描");

        internal static readonly GUIContent SectionIdentity = new GUIContent("资源标识");

        internal static readonly GUIContent SectionPrewarm = new GUIContent("预热与容量");

        internal static readonly GUIContent SectionInstance = new GUIContent("实例回收");
        internal static readonly GUIContent SectionPool = new GUIContent("整池回收");
        internal static readonly GUIContent ExpireModeLabel = new GUIContent("回收方式", "只控制时间过期规则，容量清理仍按原配置运行");

        internal static readonly GUIContent SectionPriority = new GUIContent("回收优先级");

        internal static readonly GUIContent CopyButton = new GUIContent("复制", "复制这一条，名称自动追加未占用的后缀");

        internal static readonly GUIContent RemoveButton = new GUIContent("删除", "删除这一条");

        internal static readonly GUIContent PingButton = new GUIContent("定位", "在 Project 面板里定位该地址对应的预制体");

        internal static readonly GUIContent CopyNameButton = new GUIContent("复制名称", "把池名复制到系统剪贴板");

        internal static readonly GUIContent RestoreDefaultButton = new GUIContent("恢复默认", "把本条的参数恢复为默认值，保留池名与资源地址");

        internal static readonly GUIContent ErrPoolNameEmpty =
            new GUIContent("× 池名称不能为空：业务按它取池，空名等于这个池无法被访问");

        internal static readonly GUIContent ErrPoolNameWhitespace = new GUIContent("× 池名称首尾不能有空白");

        internal static readonly GUIContent ErrAssetNameEmpty = new GUIContent("× 资源地址不能为空：池没有实例来源");

        internal static readonly GUIContent ErrPrewarmCountNegative = new GUIContent("× 预热数量不能为负数");

        internal static readonly GUIContent ErrPrewarmPerFrame =
            new GUIContent("× 每帧预热数量必须大于 0，否则分帧预热永远推不动");

        internal static readonly GUIContent ErrCapacityNegative = new GUIContent("× 容量目标不能为负数");

        internal static readonly GUIContent ErrMaxInstanceNegative = new GUIContent("× 实例上限不能为负数");

        internal static readonly GUIContent ErrNotFinite =
            new GUIContent("× 必须是有限数值，不能是 NaN 或 Infinity");

        internal static readonly GUIContent ErrInstanceExpireNotPositive =
            new GUIContent("× 必须是大于 0 的秒数：填 0 等于不缓存；不想按时间回收请选择「永不过期」");

        internal static readonly GUIContent ErrPoolExpireNegative =
            new GUIContent("× 不能为负数：不需要整池回收请选择「永不过期」");

        internal static readonly GUIContent ErrAutoReleaseInterval =
            new GUIContent("× 自动释放间隔必须是有限正数");

        internal static readonly GUIContent ErrPoolAutoReleaseInterval =
            new GUIContent("× 整池自动释放间隔必须是有限正数");

        /// <summary>
        /// 在字段下方绘制自动换行的错误提示。
        /// <remarks>
        /// 不用 HelpBox：每个字段下面挂一个 HelpBox 会把面板撑得很长，
        /// 这里要的只是"这一行填错了"的即时反馈，完整原因仍在底部"配置检查"里。
        /// 使用独立样式和主题对应的文字颜色，不污染 EditorStyles 或全局 GUI 颜色。
        /// </remarks>
        /// </summary>
        /// <param name="message">已经是最终显示内容的提示，含 "× " 前缀。</param>
        internal static void DrawInlineError(GUIContent message)
        {
            GUILayout.Label(message, _errorLabel, GUILayout.MinWidth(0f));
        }

        /// <summary>
        /// 条件成立时在字段下方画一行就地错误提示。
        /// <remarks>
        /// 只用来省掉调用处的 <c>if</c>，不做任何判断逻辑：判定条件由调用方给出。
        /// 条件不成立时一个 GUI 控件都不画，不占高度也不产生布局开销。
        /// </remarks>
        /// </summary>
        /// <param name="invalid">字段是否非法。</param>
        /// <param name="message">非法时的提示，含 "× " 前缀。</param>
        internal static void DrawIssue(bool invalid, GUIContent message)
        {
            if (invalid)
            {
                DrawInlineError(message);
            }
        }

        /// <summary>
        /// 绘制一条分组标题，下方带一条跟随皮肤的分隔线。
        /// </summary>
        /// <param name="title">标题文案。</param>
        internal static void DrawSectionHeader(GUIContent title)
        {
            EditorGUILayout.Space(8f);

            Rect rect = EditorGUILayout.GetControlRect(false, 24f);
            EditorGUI.LabelField(rect, title, GroupTitleStyle);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), SeparatorColor);
            }
        }

        /// <summary>
        /// 把标记列表的长度调整为指定值，并全部置为 false。
        /// <remarks>
        /// 容量不会缩回去：条目数量反复变化时复用同一块内存，避免每次刷新都重新分配。
        /// </remarks>
        /// </summary>
        /// <param name="flags">标记列表。</param>
        /// <param name="size">目标长度。</param>
        internal static void ResetFlags(List<bool> flags, int size)
        {
            EnsureSize(flags, size, false);

            for (int i = 0; i < flags.Count; i++)
            {
                flags[i] = false;
            }
        }

        /// <summary>
        /// 把列表长度调整为指定值，新增项填 <paramref name="fill"/>，已有项保持不动。
        /// </summary>
        /// <param name="flags">目标列表。</param>
        /// <param name="size">目标长度。</param>
        /// <param name="fill">新增项的初始值。</param>
        internal static void EnsureSize(List<bool> flags, int size, bool fill)
        {
            while (flags.Count < size)
            {
                flags.Add(fill);
            }

            if (flags.Count > size)
            {
                flags.RemoveRange(size, flags.Count - size);
            }
        }

        /// <summary>
        /// 按 YooAsset 定位地址在采集目录内定位资源。只做定位，不加载、不注册池。
        /// <remarks>
        /// <c>AssetDatabase.FindAssets</c> 会走一次资源数据库查询，成本随工程规模上升，
        /// 所以只在作者点按钮时调用，不做任何缓存或预扫描——预先建全表索引会让打开面板就变慢。
        /// </remarks>
        /// </summary>
        /// <param name="assetName">配置中的定位地址。</param>
        internal static void PingAssetByName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(assetName + " t:Prefab", new[] { "Assets/AssetRaw" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName)
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        return;
                    }
                }
            }

            Debug.LogWarning($"[GameObjectPool] 未在 Assets/AssetRaw 下按文件名找到地址 {assetName} 对应的预制体");
        }

    }
}
