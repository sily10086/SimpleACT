using System;
using System.Collections.Generic;
using TEngine.Editor.Inspector;
using UnityEditor;
using UnityEngine;

namespace TEngine.Editor.GameObjectPool
{
    /// <summary>
    /// 条目上的结构操作。绘制阶段只产出意图，写入放到绘制之后执行。
    /// <remarks>
    /// 增删会改变数组长度并使已取得的 <see cref="SerializedProperty"/> 失效，
    /// 所以不能在绘制到一半时动手：先正常收尾 GUI 栈，再按这个结果统一处理。
    /// </remarks>
    /// </summary>
    internal enum GameObjectPoolEntryAction
    {
        /// <summary>
        /// 没有操作。
        /// </summary>
        None = 0,

        /// <summary>
        /// 复制这一条。
        /// </summary>
        Duplicate = 1,

        /// <summary>
        /// 删除这一条。
        /// </summary>
        Remove = 2,
    }

    /// <summary>
    /// 游戏对象池配置资产的 Inspector。
    /// <remarks>
    /// 只做三件事：把 11 个逐池字段按语义分组画清楚、把"永不过期"从时间字段里拆成开关、
    /// 把非法值就地标出来。不做运行期初始化，不加载资源，不注册池。
    /// 所有写操作都走 SerializedProperty 改资产，因此天然带 Undo；
    /// 播放模式下写操作整体禁用，避免运行数据与资产配置互相覆盖。
    /// <para>
    /// 稳态成本是三件事都收敛过的：字段引用缓存在 <see cref="GameObjectPoolEntryDrawer"/>，
    /// 每帧不再查找；文案与颜色是静态常量，不再逐帧构造；诊断文案在刷新时格式化一次，
    /// 绘制阶段只读取。IMGUI 一次重绘至少两遍（Layout、Repaint），这些乘以条目数就是实际开销。
    /// </para>
    /// </remarks>
    /// </summary>
    [CustomEditor(typeof(GameObjectPoolSetting))]
    internal sealed class GameObjectPoolSettingInspector : GameFrameworkInspector
    {
        private readonly List<GameObjectPoolValidationMessage> _errors =
            new List<GameObjectPoolValidationMessage>(8);

        private readonly List<GameObjectPoolValidationMessage> _warnings =
            new List<GameObjectPoolValidationMessage>(8);

        /// <summary>
        /// 已经拼好"第 N 条："前缀的诊断文案，绘制阶段直接用。
        /// </summary>
        private readonly List<string> _errorLines = new List<string>(8);
        private readonly List<string> _warningLines = new List<string>(8);

        /// <summary>
        /// 条目标题文案，随池名变化。在刷新时生成，避免每帧拼接。
        /// </summary>
        private readonly List<GUIContent> _titles = new List<GUIContent>(8);

        /// <summary>
        /// 逐条目的折叠状态，下标与条目数组一一对应。
        /// </summary>
        private readonly List<bool> _expanded = new List<bool>(8);

        /// <summary>
        /// 逐条目"是否有错误/警告"，用于标题行图标与"只看有问题"过滤。
        /// 在刷新时与诊断列表一起重建，不在绘制里遍历诊断列表。
        /// </summary>
        private readonly List<bool> _entryHasError = new List<bool>(8);
        private readonly List<bool> _entryHasWarning = new List<bool>(8);

        /// <summary>
        /// 逐条目的字段绘制器，同时持有该条目的字段引用缓存。
        /// </summary>
        private readonly List<GameObjectPoolEntryDrawer> _drawers = new List<GameObjectPoolEntryDrawer>(8);

        /// <summary>
        /// 生成复制名称时用来判重的缓冲，复用避免每次复制都新建集合。
        /// </summary>
        private readonly HashSet<string> _nameBuffer = new HashSet<string>(StringComparer.Ordinal);

        private SerializedProperty _pools;
        private string _searchText = string.Empty;
        private string _entryCountLabel = string.Empty;
        private string _passSummary = string.Empty;
        private string _errorOverflow = string.Empty;
        private string _warningOverflow = string.Empty;
        private bool _onlyShowIssues;
        private bool _showHelp;
        private string _overviewSummary = string.Empty;
        private float _measuredWidth;
        private float _layoutWidth;

        /// <summary>
        /// 本帧的播放状态。<see cref="EditorApplication.isPlayingOrWillChangePlaymode"/> 是 native 查询，
        /// 而每个条目都要读它判断只读，所以在每次绘制开头取一次。
        /// </summary>
        private bool _readOnly;

        /// <summary>
        /// 资源变化导致的待刷新标记。
        /// </summary>
        private bool _validationDirty;

        /// <summary>
        /// 是否已经挂了一个延迟重绘，避免批量导入时排出一长串回调。
        /// </summary>
        private bool _repaintScheduled;

        /// <summary>
        /// 绘制响应式配置面板，序列化写入和诊断刷新仍集中在原有入口。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GameObjectPoolInspectorContent.EnsureStyles();
            UpdateLayoutWidth();

            _readOnly = EditorApplication.isPlayingOrWillChangePlaymode;

            serializedObject.Update();

            // 序列化字段缺失时不再往下画：继续画全是空引用，报错信息也说不清原因
            if (_pools == null)
            {
                EditorGUILayout.HelpBox(
                    "序列化字段 _pools 缺失：脚本与资产不匹配，请重新创建或重新绑定配置资产。",
                    MessageType.Error);
                return;
            }

            // 资源变化只置脏，真正的重算合并到这里做一次
            if (_validationDirty)
            {
                _validationDirty = false;
                RebuildValidation();
            }

            int size = _pools.arraySize;
            EnsureDrawers(size);
            GameObjectPoolInspectorContent.EnsureSize(_expanded, size, true);

            DrawHeader();
            DrawFilterBar();

            EditorGUILayout.Space(4f);
            DrawPoolEntries(size);

            EditorGUILayout.Space(6f);
            DrawBottomBar();

            if (_readOnly)
            {
                EditorGUILayout.HelpBox("播放模式下禁止修改对象池配置，退出播放后可编辑。", MessageType.Info);
            }

            // 只有配置确实发生变化时才重算提示，避免每次重绘都全量校验
            if (serializedObject.ApplyModifiedProperties())
            {
                RebuildValidation();
            }

            DrawValidation();
        }

        private void OnEnable()
        {
            _pools = serializedObject.FindProperty(GameObjectPoolFieldName.Pools);

            RebuildValidation();
            EnsureDrawers(_pools != null ? _pools.arraySize : 0);

            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        /// <summary>
        /// 取消编辑器通知订阅，避免关闭面板后保留编辑器实例。
        /// </summary>
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;

            // 已经排上的延迟重绘也要撤掉：回调里虽然有 this == null 的兜底，
            // 但静态事件上留着一个指向已销毁 Inspector 的委托会让整个面板无法被回收
            EditorApplication.delayCall -= FlushDeferredRepaint;
            _repaintScheduled = false;
        }

        /// <summary>
        /// Undo 或 Redo 后重新读取配置、重建缓存并刷新诊断。
        /// </summary>
        private void OnUndoRedo()
        {
            serializedObject.Update();
            RebuildValidation();

            int size = _pools != null ? _pools.arraySize : 0;
            EnsureDrawers(size);
            GameObjectPoolInspectorContent.EnsureSize(_expanded, size, true);

            Repaint();
        }

        /// <summary>
        /// 工程资源变化。批量导入/删除时这个事件会连发几十次，
        /// 每次都全量校验并重绘会明显拖慢导入流程，因此只置脏，把重绘合并成一次。
        /// </summary>
        private void OnProjectChanged()
        {
            _validationDirty = true;

            if (_repaintScheduled)
            {
                return;
            }

            _repaintScheduled = true;
            EditorApplication.delayCall += FlushDeferredRepaint;
        }

        /// <summary>
        /// 延迟重绘回调。先撤掉自己再重绘，保证一次合并只排一个回调。
        /// </summary>
        private void FlushDeferredRepaint()
        {
            EditorApplication.delayCall -= FlushDeferredRepaint;
            _repaintScheduled = false;

            // 面板可能在回调执行前就被关掉了
            if (this != null)
            {
                Repaint();
            }
        }

        /// <summary>
        /// 让字段缓存与条目数量对齐。长度一致时直接返回，稳态零查找。
        /// </summary>
        /// <param name="size">当前条目数量。</param>
        private void EnsureDrawers(int size)
        {
            if (size == _drawers.Count)
            {
                return;
            }

            while (_drawers.Count < size)
            {
                _drawers.Add(new GameObjectPoolEntryDrawer());
            }

            if (_drawers.Count > size)
            {
                _drawers.RemoveRange(size, _drawers.Count - size);
            }

            for (int i = 0; i < size; i++)
            {
                SerializedProperty entry = _pools.GetArrayElementAtIndex(i);
                if (!_drawers[i].IsBoundTo(entry))
                {
                    _drawers[i].Bind(entry);
                }
            }
        }

        /// <summary>
        /// 丢弃全部字段缓存。数组结构变化后调用，下一次 <see cref="EnsureDrawers"/> 会重建。
        /// </summary>
        private void InvalidateDrawers()
        {
            _drawers.Clear();
        }

        /// <summary>
        /// 绘制标题行与可折叠的说明。
        /// </summary>
        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(GameObjectPoolInspectorContent.CardStyle))
            {
                Rect accent = EditorGUILayout.GetControlRect(false, 3f);
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(accent, GameObjectPoolInspectorContent.AccentColor);
                }

                GUILayout.Label("TEngine 游戏对象池", GameObjectPoolInspectorContent.AssetTitleStyle,
                    GUILayout.MinWidth(0f));
                GUILayout.Label(_overviewSummary, GameObjectPoolInspectorContent.WrappedLabel,
                    GUILayout.MinWidth(0f));
                Rect status = EditorGUILayout.GetControlRect(false, 22f);
                GameObjectPoolInspectorContent.DrawStatus(status, _errors.Count > 0, _warnings.Count > 0);
                _showHelp = EditorGUILayout.Foldout(_showHelp, "配置说明", true);
                if (_showHelp)
                {
                    EditorGUILayout.HelpBox(
                        "由 GameEntry 的 Settings 引用，每个条目独立配置一个对象池。\n"
                        + "资源地址使用 YooAsset 文件名地址，例如 Coin，不带路径和扩展名。\n"
                        + "实例永不过期只关闭时间过期，容量清理仍可回收空闲实例。"
                        + "整池永不过期关闭整池空闲释放。时间字段单位均为秒。",
                        MessageType.Info);
                }
            }
        }

        /// <summary>
        /// 测量实际内容宽度，并只在 Layout 时切换布局，保持同一绘制周期的 GUI 栈一致。
        /// </summary>
        private void UpdateLayoutWidth()
        {
            if (Event.current.type == EventType.Layout)
            {
                _layoutWidth = _measuredWidth > 0f ? _measuredWidth : EditorGUIUtility.currentViewWidth - 40f;
            }

            Rect probe = EditorGUILayout.GetControlRect(false, 0f);
            if (Event.current.type == EventType.Repaint && Mathf.Abs(probe.width - _measuredWidth) > 1f)
            {
                _measuredWidth = probe.width;
                Repaint();
            }
        }

        /// <summary>
        /// 绘制搜索框与"只看有问题"开关。
        /// </summary>
        private void DrawFilterBar()
        {
            EditorGUILayout.Space(2f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索", GUILayout.Width(32f));
            _searchText = EditorGUILayout.TextField(_searchText);
            if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(40f)))
            {
                _searchText = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_entryCountLabel, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("展开全部", EditorStyles.miniButtonLeft))
            {
                SetAllExpanded(true);
            }

            if (GUILayout.Button("折叠全部", EditorStyles.miniButtonRight))
            {
                SetAllExpanded(false);
            }

            _onlyShowIssues = GUILayout.Toggle(
                _onlyShowIssues, "只看有问题", EditorStyles.miniButton);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制全部对象池条目。
        /// </summary>
        /// <param name="size">当前条目数量。</param>
        private void DrawPoolEntries(int size)
        {
            if (size == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前没有配置任何对象池。空配置是合法的：模块可以在没有任何池的情况下初始化。",
                    MessageType.Info);
                return;
            }

            int visible = 0;
            for (int i = 0; i < size; i++)
            {
                GameObjectPoolEntryDrawer drawer = _drawers[i];

                if (!MatchesSearch(drawer.PoolNameValue, drawer.AssetNameValue))
                {
                    continue;
                }

                if (_onlyShowIssues && !HasIssue(i))
                {
                    continue;
                }

                visible++;

                GameObjectPoolEntryAction action = DrawEntry(i, drawer);
                if (action != GameObjectPoolEntryAction.None)
                {
                    // 结构变化后已取得的 SerializedProperty 全部失效：
                    // 处理完立刻结束本轮绘制，下一帧从完整状态重来
                    ApplyEntryAction(i, action);
                    return;
                }
            }

            if (visible == 0)
            {
                EditorGUILayout.HelpBox(
                    _onlyShowIssues ? "没有存在问题的条目。" : "没有匹配搜索条件的条目。",
                    MessageType.Info);
            }
        }

        /// <summary>
        /// 绘制单个条目，返回作者在这一条上点了什么。
        /// </summary>
        /// <param name="index">条目下标。</param>
        /// <param name="drawer">该条目的字段绘制器。</param>
        /// <returns>绘制阶段产出的操作意图，由调用方在收尾后执行。</returns>
        private GameObjectPoolEntryAction DrawEntry(int index, GameObjectPoolEntryDrawer drawer)
        {
            bool hasError = index < _entryHasError.Count && _entryHasError[index];
            bool hasWarning = index < _entryHasWarning.Count && _entryHasWarning[index];

            EditorGUILayout.BeginVertical(GameObjectPoolInspectorContent.CardStyle);
            float contentWidth = _layoutWidth - GameObjectPoolInspectorContent.CardStyle.padding.horizontal;
            bool inlineActions = GameObjectPoolInspectorContent.UseTwoColumns(contentWidth);
            Rect header = EditorGUILayout.GetControlRect(false, 28f);
            DrawEntryTitle(header, index, hasError, hasWarning, inlineActions);
            Rect actions = inlineActions
                ? new Rect(header.xMax - 96f, header.y + 3f, 96f, 22f)
                : EditorGUILayout.GetControlRect(false, 22f);
            if (!inlineActions)
            {
                actions.xMin = actions.xMax - 96f;
            }

            GameObjectPoolEntryAction action = DrawEntryActions(actions);

            if (_expanded[index])
            {
                drawer.DrawFields(_readOnly, contentWidth);
            }

            EditorGUILayout.EndVertical();

            return action;
        }

        /// <summary>
        /// 标题使用受限矩形与完整 Tooltip，状态和按钮拥有独立空间。
        /// </summary>
        private void DrawEntryTitle(Rect rect, int index, bool hasError, bool hasWarning, bool inlineActions)
        {
            Color color = GameObjectPoolInspectorContent.StatusColor(hasError, hasWarning);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.07f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), color);
            }

            Rect status = new Rect(rect.xMax - (inlineActions ? 172f : 72f), rect.y + 3f, 68f, 22f);
            GameObjectPoolInspectorContent.DrawStatus(status, hasError, hasWarning);
            Rect foldout = new Rect(rect.x + 18f, rect.y + 4f,
                Mathf.Max(0f, status.x - rect.x - 24f), 20f);
            _expanded[index] = EditorGUI.Foldout(foldout, _expanded[index], TitleOf(index), true);
        }

        /// <summary>
        /// 绘制增删操作，仅返回意图；调用方收尾布局后才更改数组。
        /// </summary>
        private GameObjectPoolEntryAction DrawEntryActions(Rect rect)
        {
            using (new EditorGUI.DisabledScope(_readOnly))
            {
                Rect copy = new Rect(rect.x, rect.y, 46f, rect.height);
                Rect remove = new Rect(rect.x + 50f, rect.y, 46f, rect.height);
                if (GUI.Button(copy, GameObjectPoolInspectorContent.CopyButton, EditorStyles.miniButton))
                {
                    return GameObjectPoolEntryAction.Duplicate;
                }

                if (GUI.Button(remove, GameObjectPoolInspectorContent.RemoveButton, EditorStyles.miniButton))
                {
                    return GameObjectPoolEntryAction.Remove;
                }
            }

            return GameObjectPoolEntryAction.None;
        }

        /// <summary>
        /// 执行绘制阶段产出的条目操作。
        /// </summary>
        /// <param name="index">条目下标。</param>
        /// <param name="action">操作。</param>
        private void ApplyEntryAction(int index, GameObjectPoolEntryAction action)
        {
            switch (action)
            {
                case GameObjectPoolEntryAction.Duplicate:
                    DuplicateEntry(index);
                    break;
                case GameObjectPoolEntryAction.Remove:
                    RemoveEntry(index);
                    break;
            }
        }

        /// <summary>
        /// 绘制底部"添加条目"与"全部恢复默认"。
        /// </summary>
        private void DrawBottomBar()
        {
            bool addEntry;
            bool resetAll;

            EditorGUI.BeginDisabledGroup(_readOnly);
            bool inlineActions = GameObjectPoolInspectorContent.UseTwoColumns(_layoutWidth);
            if (inlineActions)
            {
                EditorGUILayout.BeginHorizontal();
            }

            addEntry = GUILayout.Button("＋ 添加对象池条目", GUILayout.Height(26f));
            resetAll = GUILayout.Button("全部恢复默认值", EditorStyles.miniButton, GUILayout.Height(22f));

            if (inlineActions)
            {
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.EndDisabledGroup();

            // 结构变化与模态框都放在布局收尾之后：AddEntry 会写回资产，
            // 模态框会打断布局，两者都不适合出现在 BeginHorizontal 内部
            if (addEntry)
            {
                AddEntry();
                return;
            }

            if (resetAll && EditorUtility.DisplayDialog(
                    "恢复默认值",
                    "将把所有条目的参数恢复为默认值，池名称与资源地址保留。\n此操作可以用 Ctrl+Z 撤销。",
                    "恢复默认值", "取消"))
            {
                ResetAllEntries();
            }
        }

        /// <summary>
        /// 绘制底部诊断区。
        /// </summary>
        private void DrawValidation()
        {
            EditorGUILayout.Space(6f);

            int total = _errorLines.Count + _warningLines.Count;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("配置检查", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (total > 0 && GUILayout.Button("展开问题条目", EditorStyles.miniButton, GUILayout.Width(86f)))
            {
                ExpandIssueEntries();
            }

            EditorGUILayout.EndHorizontal();

            if (total == 0)
            {
                EditorGUILayout.HelpBox(_passSummary, MessageType.Info);
                return;
            }

            int errorCount = Mathf.Min(_errorLines.Count, GameObjectPoolInspectorContent.MAX_MESSAGE_COUNT);
            for (int i = 0; i < errorCount; i++)
            {
                EditorGUILayout.HelpBox(_errorLines[i], MessageType.Error);
            }

            if (!string.IsNullOrEmpty(_errorOverflow))
            {
                EditorGUILayout.HelpBox(_errorOverflow, MessageType.Error);
            }

            int warningCount = Mathf.Min(_warningLines.Count, GameObjectPoolInspectorContent.MAX_MESSAGE_COUNT);
            for (int i = 0; i < warningCount; i++)
            {
                EditorGUILayout.HelpBox(_warningLines[i], MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_warningOverflow))
            {
                EditorGUILayout.HelpBox(_warningOverflow, MessageType.Warning);
            }
        }

        /// <summary>
        /// 给诊断文案补上条目序号，让"第几条"能直接对上眼睛看到的条目。
        /// </summary>
        /// <param name="message">诊断。</param>
        private static string FormatMessage(GameObjectPoolValidationMessage message)
        {
            return message.Index < 0
                ? message.Message
                : "第 " + (message.Index + 1) + " 条：" + message.Message;
        }

        /// <summary>
        /// 取指定条目的标题文案。刷新时已生成，这里只做越界兜底。
        /// </summary>
        /// <param name="index">条目下标。</param>
        private GUIContent TitleOf(int index)
        {
            return index < _titles.Count ? _titles[index] : GUIContent.none;
        }

        /// <summary>
        /// 判断条目是否命中搜索关键字。池名与地址都参与匹配，大小写不敏感。
        /// </summary>
        /// <param name="poolName">池名。</param>
        /// <param name="assetName">资源地址。</param>
        private bool MatchesSearch(string poolName, string assetName)
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                return true;
            }

            return ContainsIgnoreCase(poolName) || ContainsIgnoreCase(assetName);
        }

        /// <summary>
        /// 字符串是否包含搜索关键字（Ordinal 忽略大小写）。
        /// </summary>
        /// <param name="source">待搜索字符串。</param>
        private bool ContainsIgnoreCase(string source)
        {
            return !string.IsNullOrEmpty(source)
                   && source.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 条目是否存在错误或提示。
        /// </summary>
        /// <param name="index">条目下标。</param>
        private bool HasIssue(int index)
        {
            return (index < _entryHasError.Count && _entryHasError[index])
                   || (index < _entryHasWarning.Count && _entryHasWarning[index]);
        }

        /// <summary>
        /// 展开或折叠全部条目。
        /// </summary>
        /// <param name="expanded">目标状态。</param>
        private void SetAllExpanded(bool expanded)
        {
            if (_pools == null)
            {
                return;
            }

            GameObjectPoolInspectorContent.EnsureSize(_expanded, _pools.arraySize, expanded);

            for (int i = 0; i < _expanded.Count; i++)
            {
                _expanded[i] = expanded;
            }
        }

        /// <summary>
        /// 展开所有存在错误或提示的条目，便于从底部诊断直接跳到问题处。
        /// </summary>
        private void ExpandIssueEntries()
        {
            GameObjectPoolInspectorContent.EnsureSize(_expanded, _pools.arraySize, true);

            for (int i = 0; i < _expanded.Count; i++)
            {
                if (HasIssue(i))
                {
                    _expanded[i] = true;
                }
            }
        }

        /// <summary>
        /// 添加一条空白配置，字段全部用默认值。
        /// </summary>
        private void AddEntry()
        {
            int index = _pools.arraySize;
            _pools.InsertArrayElementAtIndex(index);

            // 结构变了，逐个复用旧缓存已不可靠：整体重建，下一帧按新长度绑定
            InvalidateDrawers();
            EnsureDrawers(_pools.arraySize);

            // 不依赖"Unity 扩展数组时复制上一项"的行为，身份与参数都显式写一遍
            _drawers[index].ClearIdentity();
            _drawers[index].ApplyDefaults();

            _expanded.Add(true);

            serializedObject.ApplyModifiedProperties();
            RebuildValidation();
        }

        /// <summary>
        /// 把所有条目的参数恢复为默认值，保留各自的池名与资源地址。
        /// <remarks>
        /// 不在这里写回资产：值改动会在 OnInspectorGUI 末尾统一写回并记 Undo，
        /// 中途写回会让已取得的 SerializedProperty 状态与磁盘不一致。
        /// </remarks>
        /// </summary>
        private void ResetAllEntries()
        {
            for (int i = 0; i < _drawers.Count; i++)
            {
                _drawers[i].ApplyDefaults();
            }
        }

        /// <summary>
        /// 复制一条配置，名称自动追加未占用的后缀，避免静默重名。
        /// </summary>
        /// <param name="index">源条目下标。</param>
        private void DuplicateEntry(int index)
        {
            string copyName = MakeUniqueCopyName(_drawers[index].PoolNameValue);

            _pools.InsertArrayElementAtIndex(index + 1);
            _pools.GetArrayElementAtIndex(index + 1)
                .FindPropertyRelative(GameObjectPoolFieldName.PoolName).stringValue = copyName;

            if (index < _expanded.Count)
            {
                _expanded.Insert(index + 1, true);
            }

            InvalidateDrawers();
            serializedObject.ApplyModifiedProperties();
            RebuildValidation();
            EnsureDrawers(_pools.arraySize);

            Debug.Log($"[GameObjectPool] 已复制条目，新名称为 {copyName}，请确认参数是否需要调整");
        }

        /// <summary>
        /// 生成不重复的复制名称：Name_Copy、Name_Copy2、Name_Copy3……
        /// <remarks>
        /// 先把现有名字一次性收进集合再查重，而不是每试一个候选名就全表扫一遍：
        /// 后者在重名较多时是平方级的，且每次比较都要走一次 SerializedProperty 取值。
        /// </remarks>
        /// </summary>
        /// <param name="sourceName">源池名。</param>
        private string MakeUniqueCopyName(string sourceName)
        {
            _nameBuffer.Clear();

            int size = _pools.arraySize;
            for (int i = 0; i < size; i++)
            {
                string name = _pools.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(GameObjectPoolFieldName.PoolName).stringValue;
                if (!string.IsNullOrEmpty(name))
                {
                    _nameBuffer.Add(name);
                }
            }

            string baseName = string.IsNullOrEmpty(sourceName) ? "Pool" : sourceName;
            string candidate = baseName + "_Copy";

            int suffix = 2;
            while (_nameBuffer.Contains(candidate))
            {
                candidate = baseName + "_Copy" + suffix;
                suffix++;
            }

            return candidate;
        }

        /// <summary>
        /// 删除指定条目。
        /// </summary>
        /// <param name="index">条目下标。</param>
        private void RemoveEntry(int index)
        {
            int sizeBefore = _pools.arraySize;
            _pools.DeleteArrayElementAtIndex(index);

            // 引用类型元素在首次删除时只会清空内容，这里按实际结果再删一次；
            // 值类型（本配置条目）第一次就会真正缩短数组，条件不成立直接跳过
            if (_pools.arraySize == sizeBefore)
            {
                _pools.DeleteArrayElementAtIndex(index);
            }

            if (index < _expanded.Count)
            {
                _expanded.RemoveAt(index);
            }

            InvalidateDrawers();
            serializedObject.ApplyModifiedProperties();
            RebuildValidation();
            EnsureDrawers(_pools.arraySize);
        }

        /// <summary>
        /// 重新计算配置的提示信息，并同步逐条目的状态标记与全部预格式化文案。
        /// <remarks>
        /// 完整诊断在 TEngine.Editor 程序集，发布版不含该代码。
        /// 这里只做一次收集，然后把结果摊平：条目状态标记直接查，
        /// 诊断文案连"第 N 条："前缀一起拼好，标题也一并生成。
        /// 绘制阶段因此没有任何列表遍历和字符串拼接。
        /// </remarks>
        /// </summary>
        private void RebuildValidation()
        {
            GameObjectPoolSetting setting = (GameObjectPoolSetting)target;
            GameObjectPoolEditorValidator.Collect(setting, _errors, _warnings);

            int size = _pools != null ? _pools.arraySize : 0;

            GameObjectPoolInspectorContent.ResetFlags(_entryHasError, size);
            GameObjectPoolInspectorContent.ResetFlags(_entryHasWarning, size);

            _errorLines.Clear();
            _warningLines.Clear();

            for (int i = 0; i < _errors.Count; i++)
            {
                GameObjectPoolValidationMessage message = _errors[i];
                if (message.Index >= 0 && message.Index < size)
                {
                    _entryHasError[message.Index] = true;
                }

                _errorLines.Add(FormatMessage(message));
            }

            for (int i = 0; i < _warnings.Count; i++)
            {
                GameObjectPoolValidationMessage message = _warnings[i];
                if (message.Index >= 0 && message.Index < size)
                {
                    _entryHasWarning[message.Index] = true;
                }

                _warningLines.Add(FormatMessage(message));
            }

            RebuildTitles(setting, size);

            _entryCountLabel = "共 " + size + " 个条目";
            _overviewSummary = size + " 个对象池  ·  " + _errors.Count + " 条错误  ·  " + _warnings.Count + " 条警告";
            _passSummary = size == 0
                ? "当前没有任何条目，空配置是合法的。"
                : "全部 " + size + " 个条目通过检查。";

            _errorOverflow = BuildOverflow(_errorLines.Count, "条错误未显示。");
            _warningOverflow = BuildOverflow(_warningLines.Count, "条提示未显示。");
        }

        /// <summary>
        /// 生成条目标题。"未命名"的条目用序号占位，避免标题栏空一片。
        /// </summary>
        /// <param name="setting">配置资产。</param>
        /// <param name="size">条目数量。</param>
        private void RebuildTitles(GameObjectPoolSetting setting, int size)
        {
            _titles.Clear();

            GameObjectPoolConfig[] pools = setting != null ? setting.Pools : null;

            for (int i = 0; i < size; i++)
            {
                string poolName = pools != null && i < pools.Length ? pools[i].PoolName : null;

                string title = string.IsNullOrEmpty(poolName)
                    ? (i + 1) + ". （未命名）"
                    : (i + 1) + ". " + poolName;
                _titles.Add(new GUIContent(title, title));
            }
        }

        /// <summary>
        /// 生成"还有 N 条未显示"的提示，未超出上限时返回空字符串。
        /// </summary>
        /// <param name="count">实际条数。</param>
        /// <param name="suffix">数量之后的说明。</param>
        private static string BuildOverflow(int count, string suffix)
        {
            int hidden = count - GameObjectPoolInspectorContent.MAX_MESSAGE_COUNT;
            return hidden > 0 ? "还有 " + hidden + " " + suffix : string.Empty;
        }
    }
}
