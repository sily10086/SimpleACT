using UnityEditor;
using UnityEngine;

namespace TEngine.Editor.GameObjectPool
{
    /// <summary>
    /// 单条配置的字段绘制器，同时缓存这个条目的 11 个 <see cref="SerializedProperty"/>。
    /// <remarks>
    /// 缓存的意义：Inspector 每次重绘都会把所有可见条目重画一遍，
    /// 而 <c>FindPropertyRelative</c> 是在子属性链表上做名字比较的线性查找。
    /// 不缓存时稳态成本是"条目数 × 11 次查找 × 事件数"，条目上百就很可观。
    /// 缓存后稳态是零次查找，只在数组长度变化时重新绑定。
    /// <para>
    /// <b>缓存的语义是绑定路径，不是绑定值</b>：<c>_pools.Array.data[3]</c> 这个路径始终指向
    /// 数组的第四个元素，元素被拖拽换序后它自动指向新内容，因此重排不需要重建。
    /// 只有数组长度变化（增、删、复制）才会让路径越界或残留，那时由主面板调用 <see cref="Bind"/> 重建。
    /// </para>
    /// <para>
    /// 只画字段，不画标题行、折叠箭头、增删按钮：那些属于列表控制，由主面板负责。
    /// 这样"一条配置长什么样"这个知识只存在于一个文件里。
    /// </para>
    /// <para>
    /// 字段标签与输入分行；容量与回收分区根据可用宽度切换单双列。
    /// </para>
    /// </remarks>
    /// </summary>
    internal sealed class GameObjectPoolEntryDrawer
    {
        private SerializedProperty _entry;
        private SerializedProperty _poolName;
        private SerializedProperty _assetName;
        private SerializedProperty _prewarmCount;
        private SerializedProperty _prewarmPerFrame;
        private SerializedProperty _capacity;
        private SerializedProperty _maxInstanceCount;
        private SerializedProperty _autoReleaseInterval;
        private SerializedProperty _expireTime;
        private SerializedProperty _poolExpireTime;
        private SerializedProperty _poolAutoReleaseInterval;
        private SerializedProperty _priority;
        private float _contentWidth;

        /// <summary>
        /// 绑定的池名原始值，供主面板做搜索匹配，免去再次查找。
        /// </summary>
        internal string PoolNameValue => _poolName != null ? _poolName.stringValue : string.Empty;

        /// <summary>
        /// 绑定的资源地址原始值，供主面板做搜索匹配与按钮可用性判断。
        /// </summary>
        internal string AssetNameValue => _assetName != null ? _assetName.stringValue : string.Empty;

        /// <summary>
        /// 缓存是否仍然指向同一个路径。数组长度变化后下标会错位，必须靠它判定重建。
        /// </summary>
        /// <param name="entry">数组元素属性。</param>
        /// <returns>路径一致且字段齐全时为 true。</returns>
        internal bool IsBoundTo(SerializedProperty entry)
        {
            return _entry != null
                   && _poolName != null
                   && ReferenceEquals(_entry.serializedObject, entry.serializedObject)
                   && string.Equals(_entry.propertyPath, entry.propertyPath, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 一次性取出这条配置的全部字段。
        /// </summary>
        /// <param name="entry">数组元素属性。</param>
        internal void Bind(SerializedProperty entry)
        {
            _entry = entry;
            _poolName = entry.FindPropertyRelative(GameObjectPoolFieldName.PoolName);
            _assetName = entry.FindPropertyRelative(GameObjectPoolFieldName.AssetName);
            _prewarmCount = entry.FindPropertyRelative(GameObjectPoolFieldName.PrewarmCount);
            _prewarmPerFrame = entry.FindPropertyRelative(GameObjectPoolFieldName.PrewarmPerFrame);
            _capacity = entry.FindPropertyRelative(GameObjectPoolFieldName.Capacity);
            _maxInstanceCount = entry.FindPropertyRelative(GameObjectPoolFieldName.MaxInstanceCount);
            _autoReleaseInterval = entry.FindPropertyRelative(GameObjectPoolFieldName.AutoReleaseInterval);
            _expireTime = entry.FindPropertyRelative(GameObjectPoolFieldName.ExpireTime);
            _poolExpireTime = entry.FindPropertyRelative(GameObjectPoolFieldName.PoolExpireTime);
            _poolAutoReleaseInterval =
                entry.FindPropertyRelative(GameObjectPoolFieldName.PoolAutoReleaseInterval);
            _priority = entry.FindPropertyRelative(GameObjectPoolFieldName.Priority);
        }

        /// <summary>
        /// 写入这条配置的默认参数，池名与地址不动。
        /// <remarks>
        /// 这是默认值的<b>唯一</b>写入点："添加条目"、"单条恢复默认"、"全部恢复默认值"都走这里。
        /// 九个值全部来自 <see cref="GameObjectPoolEntryDefaults"/>，编辑器代码里没有魔数——
        /// 同一组默认值写两处必然漂移。池名与地址是身份信息，不属于参数，故不覆盖。
        /// </remarks>
        /// </summary>
        internal void ApplyDefaults()
        {
            if (_entry == null)
            {
                return;
            }

            _prewarmCount.intValue = GameObjectPoolEntryDefaults.PrewarmCount;
            _prewarmPerFrame.intValue = GameObjectPoolEntryDefaults.PrewarmPerFrame;
            _capacity.intValue = GameObjectPoolEntryDefaults.Capacity;
            _maxInstanceCount.intValue = GameObjectPoolEntryDefaults.MaxInstanceCount;
            _autoReleaseInterval.floatValue = GameObjectPoolEntryDefaults.AutoReleaseInterval;
            _expireTime.floatValue = GameObjectPoolEntryDefaults.InstanceExpireTime;
            _poolExpireTime.floatValue = GameObjectPoolEntryDefaults.PoolExpireTime;
            _poolAutoReleaseInterval.floatValue = GameObjectPoolEntryDefaults.PoolAutoReleaseInterval;
            _priority.intValue = GameObjectPoolEntryDefaults.Priority;
        }

        /// <summary>
        /// 把池名与地址写成空字符串。新增条目时用，避免继承数组扩容带过来的上一项内容。
        /// </summary>
        internal void ClearIdentity()
        {
            if (_entry == null)
            {
                return;
            }

            _poolName.stringValue = string.Empty;
            _assetName.stringValue = string.Empty;
        }

        /// <summary>
        /// 绘制这条配置的四个字段分组。
        /// </summary>
        /// <param name="readOnly">播放模式下一律只读。</param>
        /// <param name="contentWidth">卡片扣除内边距后的可用宽度。</param>
        internal void DrawFields(bool readOnly, float contentWidth)
        {
            // 字段缓存失效（数组被外部脚本改动、绑定没跟上）时宁可不画，也不要抛空引用把整个 Inspector 打空
            if (_poolName == null)
            {
                EditorGUILayout.HelpBox(
                    "条目字段缓存已失效：折叠再展开该条目可重新绑定。", MessageType.Warning);
                return;
            }

            _contentWidth = contentWidth;
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.BeginDisabledGroup(readOnly);

            // 独立标签不依赖全局 labelWidth，缩进归零以保持两列对齐。
            try
            {
                DrawIdentitySection();
                DrawPrewarmSection();
                DrawExpireSection();
                DrawPrioritySection();
            }
            finally
            {
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel = previousIndent;
            }
        }

        /// <summary>
        /// 资源标识各占一行，定位按钮紧邻地址输入。
        /// </summary>
        private void DrawIdentitySection()
        {
            GameObjectPoolInspectorContent.DrawSectionHeader(GameObjectPoolInspectorContent.SectionIdentity);

            GameObjectPoolInspectorContent.DrawField(_poolName, GameObjectPoolInspectorContent.PoolNameLabel);
            GUILayout.Label(GameObjectPoolInspectorContent.AssetNameLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(
                _assetName, GUIContent.none, GUILayout.MinWidth(0f));

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_assetName.stringValue));
            if (GUILayout.Button(
                    GameObjectPoolInspectorContent.PingButton, EditorStyles.miniButton, GUILayout.Width(42f)))
            {
                GameObjectPoolInspectorContent.PingAssetByName(_assetName.stringValue);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            string name = _poolName.stringValue;
            GameObjectPoolInspectorContent.DrawIssue(
                string.IsNullOrEmpty(name), GameObjectPoolInspectorContent.ErrPoolNameEmpty);
            GameObjectPoolInspectorContent.DrawIssue(
                !string.IsNullOrEmpty(name) && name != name.Trim(),
                GameObjectPoolInspectorContent.ErrPoolNameWhitespace);
            GameObjectPoolInspectorContent.DrawIssue(
                string.IsNullOrEmpty(_assetName.stringValue),
                GameObjectPoolInspectorContent.ErrAssetNameEmpty);
        }

        /// <summary>
        /// 预热与容量：宽面板等宽双列，窄面板单列。
        /// </summary>
        private void DrawPrewarmSection()
        {
            GameObjectPoolInspectorContent.DrawSectionHeader(GameObjectPoolInspectorContent.SectionPrewarm);

            DrawFieldPair(_prewarmCount, GameObjectPoolInspectorContent.PrewarmCountLabel,
                _prewarmPerFrame, GameObjectPoolInspectorContent.PrewarmPerFrameLabel);

            GameObjectPoolInspectorContent.DrawIssue(
                _prewarmCount.intValue < 0, GameObjectPoolInspectorContent.ErrPrewarmCountNegative);
            GameObjectPoolInspectorContent.DrawIssue(
                _prewarmPerFrame.intValue <= 0, GameObjectPoolInspectorContent.ErrPrewarmPerFrame);

            DrawFieldPair(_capacity, GameObjectPoolInspectorContent.CapacityLabel,
                _maxInstanceCount, GameObjectPoolInspectorContent.MaxInstanceCountLabel);

            GameObjectPoolInspectorContent.DrawIssue(
                _capacity.intValue < 0, GameObjectPoolInspectorContent.ErrCapacityNegative);
            GameObjectPoolInspectorContent.DrawIssue(
                _maxInstanceCount.intValue < 0, GameObjectPoolInspectorContent.ErrMaxInstanceNegative);
        }

        /// <summary>
        /// 双列固定左列宽度，右列填充余量，避免根据标签长度挤压输入区。
        /// </summary>
        private void DrawFieldPair(SerializedProperty first, GUIContent firstLabel,
            SerializedProperty second, GUIContent secondLabel)
        {
            if (!GameObjectPoolInspectorContent.UseTwoColumns(_contentWidth))
            {
                GameObjectPoolInspectorContent.DrawField(first, firstLabel);
                GameObjectPoolInspectorContent.DrawField(second, secondLabel);
                return;
            }

            float columnWidth = (_contentWidth - GameObjectPoolInspectorContent.SECTION_GAP) * 0.5f;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(columnWidth)))
                {
                    GameObjectPoolInspectorContent.DrawField(first, firstLabel);
                }

                GUILayout.Space(GameObjectPoolInspectorContent.SECTION_GAP);
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0f)))
                {
                    GameObjectPoolInspectorContent.DrawField(second, secondLabel);
                }
            }
        }

        /// <summary>
        /// 清理与过期按可用宽度切换并排或上下两个独立区域。
        /// </summary>
        private void DrawExpireSection()
        {
            if (!GameObjectPoolInspectorContent.UseTwoColumns(_contentWidth))
            {
                DrawInstanceRelease();
                DrawPoolRelease();
                return;
            }

            float columnWidth = (_contentWidth - GameObjectPoolInspectorContent.SECTION_GAP) * 0.5f;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(columnWidth)))
                {
                    DrawInstanceRelease();
                }

                GUILayout.Space(GameObjectPoolInspectorContent.SECTION_GAP);
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0f)))
                {
                    DrawPoolRelease();
                }
            }
        }

        /// <summary>
        /// 实例过期关闭后仍保留扫描间隔，容量释放与时间过期不是同一条件。
        /// </summary>
        private void DrawInstanceRelease()
        {
            GameObjectPoolInspectorContent.DrawSectionHeader(GameObjectPoolInspectorContent.SectionInstance);

            bool instanceForever = GameObjectPoolExpireRule.IsInstanceExpireDisabled(_expireTime.floatValue);

            instanceForever = DrawExpireGroup(
                _expireTime, GameObjectPoolInspectorContent.InstanceExpireLabel, instanceForever,
                GameObjectPoolExpireRule.NeverExpireInstance,
                GameObjectPoolEntryDefaults.InstanceExpireTime);
            GameObjectPoolInspectorContent.DrawField(
                _autoReleaseInterval, GameObjectPoolInspectorContent.AutoReleaseIntervalLabel);

            float autoRelease = _autoReleaseInterval.floatValue;
            GameObjectPoolInspectorContent.DrawIssue(
                float.IsNaN(autoRelease) || float.IsInfinity(autoRelease) || autoRelease <= 0f,
                GameObjectPoolInspectorContent.ErrAutoReleaseInterval);

            if (!instanceForever)
            {
                // 实例级的 0 不是"关闭"：底层的过期基准会算成"现在"，
                // 下一次清理就把空闲实例全释放，等于不缓存。要关闭请选「永不过期」。
                float expire = _expireTime.floatValue;
                bool notFinite = float.IsNaN(expire) || float.IsInfinity(expire);
                GameObjectPoolInspectorContent.DrawIssue(
                    notFinite, GameObjectPoolInspectorContent.ErrNotFinite);
                GameObjectPoolInspectorContent.DrawIssue(
                    !notFinite && expire <= 0f, GameObjectPoolInspectorContent.ErrInstanceExpireNotPositive);
            }
        }

        /// <summary>
        /// 整池过期关闭时显示扫描不适用提示，保留已有扫描间隔值。
        /// </summary>
        private void DrawPoolRelease()
        {
            GameObjectPoolInspectorContent.DrawSectionHeader(GameObjectPoolInspectorContent.SectionPool);
            bool poolForever = GameObjectPoolExpireRule.IsPoolExpireDisabled(_poolExpireTime.floatValue);

            poolForever = DrawExpireGroup(
                _poolExpireTime, GameObjectPoolInspectorContent.PoolExpireLabel, poolForever,
                GameObjectPoolExpireRule.NeverExpirePool,
                GameObjectPoolEntryDefaults.PoolExpireTimeOnEnable);

            // 整池过期关闭时这个值不参与任何判断，留着输入框只会让人以为还需要填
            if (poolForever)
            {
                GUILayout.Label(
                    GameObjectPoolInspectorContent.PoolIntervalPlaceholder,
                    GameObjectPoolInspectorContent.WrappedLabel, GUILayout.MinWidth(0f));
            }
            else
            {
                GameObjectPoolInspectorContent.DrawField(
                    _poolAutoReleaseInterval,
                    GameObjectPoolInspectorContent.PoolAutoReleaseIntervalLabel);
            }

            if (poolForever)
            {
                return;
            }

            // 池级的 0 恰好就是关闭，所以选「永不过期」与填 0 等价；负数才是填错
            float poolExpire = _poolExpireTime.floatValue;
            bool poolNotFinite = float.IsNaN(poolExpire) || float.IsInfinity(poolExpire);
            GameObjectPoolInspectorContent.DrawIssue(
                poolNotFinite, GameObjectPoolInspectorContent.ErrNotFinite);
            GameObjectPoolInspectorContent.DrawIssue(
                !poolNotFinite && poolExpire < 0f, GameObjectPoolInspectorContent.ErrPoolExpireNegative);

            float poolInterval = _poolAutoReleaseInterval.floatValue;
            GameObjectPoolInspectorContent.DrawIssue(
                float.IsNaN(poolInterval) || float.IsInfinity(poolInterval) || poolInterval <= 0f,
                GameObjectPoolInspectorContent.ErrPoolAutoReleaseInterval);
        }

        /// <summary>
        /// 回收模式与过期秒数分行，沿用原有哨兵值与默认秒数切换语义。
        /// <remarks>
        /// 选项是「按时间回收 / 永不过期」而不是勾选框：同一个字段有两种语义时，
        /// 下拉框能同时把两个选项写在脸上，勾选框只表达"关掉了什么"，
        /// 而且四个字加一个勾选框在窄面板里会被截断成"永不过…"。
        /// <para>
        /// 切到「永不过期」写该字段的哨兵值，切回「按时间回收」写推荐秒数：
        /// 原来的秒数不保留（保留需要在面板里存草稿，条目增删会打乱下标）。
        /// </para>
        /// 判定一律走 <see cref="GameObjectPoolExpireRule"/>，不在这里另写一套比较。
        /// </remarks>
        /// </summary>
        /// <param name="property">时间字段。</param>
        /// <param name="label">标签。</param>
        /// <param name="forever">当前是否处于永不过期状态。</param>
        /// <param name="neverValue">选择永不过期时写入的哨兵值。</param>
        /// <param name="enableValue">切回按时间回收时填入的秒数。</param>
        /// <returns>绘制结束后的永不过期状态。</returns>
        private static bool DrawExpireGroup(
            SerializedProperty property, GUIContent label, bool forever,
            float neverValue, float enableValue)
        {
            GUILayout.Label(GameObjectPoolInspectorContent.ExpireModeLabel);

            int mode = forever ? (int)GameObjectPoolExpireMode.Never : (int)GameObjectPoolExpireMode.Timeout;

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(
                mode, GameObjectPoolInspectorContent.ModeOptionNames,
                GUILayout.MinWidth(0f));
            if (EditorGUI.EndChangeCheck())
            {
                forever = selected == (int)GameObjectPoolExpireMode.Never;
                property.floatValue = forever ? neverValue : enableValue;
            }

            EditorGUILayout.Space(4f);
            if (!forever)
            {
                GameObjectPoolInspectorContent.DrawField(property, label);
            }

            return forever;
        }

        /// <summary>
        /// 优先级独立编辑，复制名称与恢复默认放在卡片底部。
        /// </summary>
        private void DrawPrioritySection()
        {
            GameObjectPoolInspectorContent.DrawSectionHeader(GameObjectPoolInspectorContent.SectionPriority);

            // 底层是排序权重，可以为负，所以这里不做任何下限检查
            GameObjectPoolInspectorContent.DrawField(_priority, GameObjectPoolInspectorContent.PriorityLabel);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_poolName.stringValue));
            if (GUILayout.Button(
                    GameObjectPoolInspectorContent.CopyNameButton, EditorStyles.miniButtonLeft,
                    GUILayout.MinWidth(0f)))
            {
                EditorGUIUtility.systemCopyBuffer = _poolName.stringValue;
                Debug.Log("[GameObjectPool] 已复制池名称：" + _poolName.stringValue);
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(
                    GameObjectPoolInspectorContent.RestoreDefaultButton, EditorStyles.miniButtonRight,
                    GUILayout.MinWidth(0f)))
            {
                // 不在这里写回资产：值改动统一由 OnInspectorGUI 末尾的
                // ApplyModifiedProperties 写回并记 Undo，中途写回会让已取得的属性与磁盘不一致
                ApplyDefaults();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
